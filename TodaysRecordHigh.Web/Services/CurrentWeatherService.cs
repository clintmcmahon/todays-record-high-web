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

        try
        {
            var url = $"v1/forecast?latitude={lat:F4}&longitude={lon:F4}" +
                      "&current=temperature_2m,apparent_temperature,weathercode,is_day" +
                      "&temperature_unit=fahrenheit&timezone=auto&forecast_days=1";

            var json = await _http.GetStringAsync(url);
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

            DateTime.TryParse(timeStr, out var asOf);

            var result = new CurrentConditions(temp, feels, asOf, tzAbbr, weatherCode, isDay);
            _cache.Set(key, result, CacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open-Meteo request failed for {Lat},{Lon}", lat, lon);
            return null;
        }
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
