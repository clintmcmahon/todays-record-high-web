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
}
