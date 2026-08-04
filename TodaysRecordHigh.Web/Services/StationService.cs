using System.Text.Json;
using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Services;

public class StationService : IStationService
{
    private readonly IReadOnlyList<Station> _stations;
    private readonly string _stationsJson;

    public StationService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.WebRootPath, "js", "filtered_stationData.json");
        var raw = File.ReadAllText(path);

        _stations = JsonSerializer.Deserialize<List<Station>>(raw) ?? [];

        // Re-serialize with computed Slug included for client-side use; camelCase for JS consumers
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        _stationsJson = JsonSerializer.Serialize(_stations.Select(s => new
        {
            s.Name,
            s.City,
            s.State,
            s.StateName,
            s.Id,
            s.Latitude,
            s.Longitude,
            s.Slug
        }), camel);
    }

    public IReadOnlyList<Station> GetAll() => _stations;

    public Station? GetBySlug(string slug) =>
        _stations.FirstOrDefault(s => s.Slug == slug.ToLowerInvariant());

    // Pre-serialized JSON for embedding in views — avoids re-serializing per request
    public string GetAllJson() => _stationsJson;

    public IReadOnlyList<Station> GetNearest(Station origin, int count, double maxMiles = 350)
    {
        if (!origin.Latitude.HasValue || !origin.Longitude.HasValue)
            return [];

        return _stations
            .Where(s => s.HasAcisData
                     && s.Slug != origin.Slug
                     && s.Latitude.HasValue && s.Longitude.HasValue)
            .Select(s => (Station: s, Miles: HaversineMiles(
                origin.Latitude.Value, origin.Longitude.Value,
                s.Latitude!.Value, s.Longitude!.Value)))
            .Where(x => x.Miles <= maxMiles)
            .OrderBy(x => x.Miles)
            .Take(count)
            .Select(x => x.Station)
            .ToList();
    }

    private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMiles = 3958.8;
        double ToRad(double deg) => deg * Math.PI / 180.0;

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusMiles * 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }
}
