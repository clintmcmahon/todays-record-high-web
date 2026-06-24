using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.ViewModels;

public class HomeViewModel
{
    public string StationsJson { get; set; } = string.Empty;
    public string MapboxToken { get; set; } = string.Empty;
    public string TodayLabel { get; set; } = string.Empty;

    // Server-rendered featured cards with full record data
    public List<FeaturedStationRecord> FeaturedRecords { get; set; } = [];

    // Pre-serialized slug list so JS can overlay live current conditions
    public string FeaturedSlugsQueryString { get; set; } = string.Empty;
}

public record FeaturedStationRecord(Station Station, int? RecordTemp, string? RecordDate, double? NormalTemp)
{
    public string FormattedRecordDate
    {
        get
        {
            if (RecordDate is null) return string.Empty;
            return DateTime.TryParse(RecordDate, out var d)
                ? d.ToString("MMM d, yyyy")
                : RecordDate;
        }
    }

    public string NormalDepartureLabel => RecordTemp.HasValue && NormalTemp.HasValue
        ? $"{(RecordTemp.Value - NormalTemp.Value >= 0 ? "+" : "")}{(RecordTemp.Value - NormalTemp.Value):F1}°"
        : string.Empty;
}
