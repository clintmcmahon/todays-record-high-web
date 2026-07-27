using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TodaysRecordHigh.Web.Services;

public class WeatherAlertsService : IWeatherAlertsService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeatherAlertsService> _logger;

    // NWS alerts don't change second-to-second; cache 10 min so a viral heat
    // event doesn't turn into a self-inflicted rate-limit problem.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public WeatherAlertsService(HttpClient http, IMemoryCache cache, ILogger<WeatherAlertsService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<WeatherAlert>> GetActiveAlertsAsync(double lat, double lon)
    {
        var key = $"wx:alerts:{lat:F2}:{lon:F2}";
        if (_cache.TryGetValue(key, out List<WeatherAlert>? cached) && cached is not null)
            return cached;

        try
        {
            var url = $"alerts/active?point={lat:F4},{lon:F4}";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var alerts = new List<WeatherAlert>();
            if (doc.RootElement.TryGetProperty("features", out var features) &&
                features.ValueKind == JsonValueKind.Array)
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (!feature.TryGetProperty("properties", out var props)) continue;

                    var eventName = props.TryGetProperty("event", out var evEl) ? evEl.GetString() ?? "Alert" : "Alert";
                    var headline = props.TryGetProperty("headline", out var hEl) ? hEl.GetString() ?? eventName : eventName;
                    var severity = props.TryGetProperty("severity", out var sEl) ? sEl.GetString() ?? "Unknown" : "Unknown";

                    DateTime? expires = null;
                    if (props.TryGetProperty("expires", out var expEl) &&
                        DateTimeOffset.TryParse(expEl.GetString(), out var expOffset))
                        expires = expOffset.UtcDateTime;

                    alerts.Add(new WeatherAlert(eventName, headline, severity, expires));
                }
            }

            _cache.Set(key, alerts, CacheTtl);
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NWS alerts request failed for {Lat},{Lon}", lat, lon);
            return [];
        }
    }
}
