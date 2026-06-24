using System.Text.Json.Serialization;

namespace TodaysRecordHigh.Web.Models;

public class Station
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("stateName")]
    public string StateName { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("threadexSid")]
    public string? ThreadexSid { get; set; }

    [JsonIgnore]
    public bool HasAcisData => !string.IsNullOrEmpty(ThreadexSid);

    [JsonIgnore]
    public string Slug => (City + "-" + State).Replace(" ", "-").ToLowerInvariant();

    [JsonIgnore]
    public string DisplayName => $"{City}, {State}";
}
