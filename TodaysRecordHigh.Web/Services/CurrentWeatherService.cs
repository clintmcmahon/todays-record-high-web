using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TodaysRecordHigh.Web.Services;

public class CurrentWeatherService : ICurrentWeatherService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CurrentWeatherService> _logger;

    // Open-Meteo updates hourly; cache 20 min so cards feel live without hammering the API
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    // Open-Meteo's free tier caps at 10,000 calls/day and answers 429 once it's spent.
    // Static because typed HttpClient services are transient: every instance must see
    // the same block. Retrying while blocked only burns time and keeps us on their radar.
    private static long _blockedUntilUtcTicks;

    public CurrentWeatherService(HttpClient http, IMemoryCache cache, ILogger<CurrentWeatherService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CurrentConditions?> GetCurrentAsync(double lat, double lon)
    {
        // Round to 2 decimal places so nearby stations share a cache entry
        var key = $"wx:current:{lat:F2}:{lon:F2}";
        if (_cache.TryGetValue(key, out CurrentConditions? cached))
            return cached;

        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _blockedUntilUtcTicks))
            return null;

        try
        {
            var url = $"v1/forecast?latitude={lat:F4}&longitude={lon:F4}" +
                      "&current=temperature_2m,apparent_temperature,weathercode,is_day,dew_point_2m,relative_humidity_2m" +
                      "&daily=temperature_2m_max" +
                      "&temperature_unit=fahrenheit&timezone=auto&forecast_days=1";

            using var response = await _http.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync();
                var until = BackoffUntil(body, DateTime.UtcNow);
                if (until is { } u)
                {
                    Interlocked.Exchange(ref _blockedUntilUtcTicks, u.Ticks);
                    _logger.LogWarning("Open-Meteo rate limit hit; pausing requests until {Until:u}. Response: {Body}", u, body);
                }
                else
                {
                    _logger.LogWarning("Open-Meteo rejected request for {Lat},{Lon}: {Body}", lat, lon, body);
                }
                return null;
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var ianaZone = doc.RootElement.TryGetProperty("timezone", out var zoneEl)
                           ? (zoneEl.GetString() ?? "") : "";
            var utcOffset = doc.RootElement.TryGetProperty("utc_offset_seconds", out var offEl)
                            ? offEl.GetInt32() : 0;
            var tzAbbr = ResolveAbbr(ianaZone, utcOffset);

            var current     = doc.RootElement.GetProperty("current");
            var temp        = current.GetProperty("temperature_2m").GetDouble();
            var feels       = current.GetProperty("apparent_temperature").GetDouble();
            var timeStr     = current.GetProperty("time").GetString() ?? "";
            var weatherCode = current.TryGetProperty("weathercode", out var wcEl) ? wcEl.GetInt32() : 0;
            var isDay       = current.TryGetProperty("is_day", out var idEl) && idEl.GetInt32() == 1;
            double? dewPoint = current.TryGetProperty("dew_point_2m", out var dpEl) && dpEl.ValueKind == JsonValueKind.Number
                ? dpEl.GetDouble() : null;
            int? humidity   = current.TryGetProperty("relative_humidity_2m", out var rhEl) && rhEl.ValueKind == JsonValueKind.Number
                ? rhEl.GetInt32() : null;

            DateTime.TryParse(timeStr, out var asOf);

            double? dailyMaxF = null;
            if (doc.RootElement.TryGetProperty("daily", out var daily) &&
                daily.TryGetProperty("temperature_2m_max", out var maxArr) &&
                maxArr.ValueKind == JsonValueKind.Array &&
                maxArr.GetArrayLength() > 0)
            {
                dailyMaxF = maxArr[0].ValueKind == JsonValueKind.Number ? maxArr[0].GetDouble() : null;
            }

            var result = new CurrentConditions(temp, feels, asOf, tzAbbr, weatherCode, isDay, dailyMaxF, dewPoint, humidity);
            _cache.Set(key, result, CacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open-Meteo request failed for {Lat},{Lon}", lat, lon);
            return null;
        }
    }

    /// <summary>
    /// How long to stop calling Open-Meteo after a 429, keyed off the limit its body names.
    /// "Too many concurrent requests" clears as soon as in-flight calls finish, so it
    /// blocks nothing: that request fails, the next one goes through.
    /// </summary>
    public static DateTime? BackoffUntil(string body, DateTime utcNow)
    {
        if (body.Contains("Daily", StringComparison.OrdinalIgnoreCase))
            return utcNow.Date.AddDays(1);
        if (body.Contains("Hourly", StringComparison.OrdinalIgnoreCase))
            return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
        if (body.Contains("Minutely", StringComparison.OrdinalIgnoreCase))
            return utcNow.AddMinutes(1);
        return null;
    }

    // Maps IANA zone + current UTC offset to the familiar US abbreviation (DST-aware).
    // utc_offset_seconds from Open-Meteo already reflects whether DST is active.
    private static string ResolveAbbr(string ianaZone, int utcOffsetSeconds) => ianaZone switch
    {
        "America/New_York"             => utcOffsetSeconds == -14400 ? "EDT" : "EST",
        "America/Indiana/Indianapolis" => utcOffsetSeconds == -14400 ? "EDT" : "EST",
        "America/Indiana/Knox"         => utcOffsetSeconds == -18000 ? "CDT" : "CST",
        "America/Chicago"              => utcOffsetSeconds == -18000 ? "CDT" : "CST",
        "America/Menominee"            => utcOffsetSeconds == -18000 ? "CDT" : "CST",
        "America/Denver"               => utcOffsetSeconds == -21600 ? "MDT" : "MST",
        "America/Phoenix"              => "MST",
        "America/Los_Angeles"          => utcOffsetSeconds == -25200 ? "PDT" : "PST",
        "America/Anchorage"            => utcOffsetSeconds == -28800 ? "AKDT" : "AKST",
        "Pacific/Honolulu"             => "HST",
        "America/Puerto_Rico"          => "AST",
        "America/Boise"                => utcOffsetSeconds == -21600 ? "MDT" : "MST",
        _ => ""
    };
}
