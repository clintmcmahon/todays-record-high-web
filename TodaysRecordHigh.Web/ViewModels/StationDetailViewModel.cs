using TodaysRecordHigh.Web.Models;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.ViewModels;

public class StationDetailViewModel
{
    public Station Station { get; set; } = null!;
    public string TodayLabel { get; set; } = string.Empty;
    public string MonthDay { get; set; } = string.Empty;

    // ── Today's date records ─────────────────────────────────────────────
    public int? AllTimeRecordTemp { get; set; }
    public string? AllTimeRecordDate { get; set; }
    public List<RecordEntry> TopRecords { get; set; } = [];

    public int? AllTimeRecordLowTemp { get; set; }
    public string? AllTimeRecordLowDate { get; set; }
    public List<RecordEntry> TopRecordLows { get; set; } = [];

    // ── Climate normals ──────────────────────────────────────────────────
    public double? NormalMaxTemp { get; set; }
    public double? NormalMinTemp { get; set; }

    public double? RecordHighDepartureFromNormal => AllTimeRecordTemp.HasValue && NormalMaxTemp.HasValue
        ? AllTimeRecordTemp.Value - NormalMaxTemp.Value : null;
    public double? RecordLowDepartureFromNormal => AllTimeRecordLowTemp.HasValue && NormalMinTemp.HasValue
        ? AllTimeRecordLowTemp.Value - NormalMinTemp.Value : null;

    // ── All-time extremes (any date, any year) ────────────────────────────
    public AllTimeExtremes? Extremes { get; set; }

    // ── Chart data (serialized for JS) ────────────────────────────────────
    public List<(int Year, int Count)> HotDaysPerYear { get; set; } = [];
    public List<DailyDeparture> YtdDepartures { get; set; } = [];
    public WarmSummerNightsData? WarmNights { get; set; }
    public PrecipData? Precip { get; set; }

    // ── Current conditions ────────────────────────────────────────────────
    public CurrentConditions? Current { get; set; }

    public string? RecordProximityLabel
    {
        get
        {
            if (Current is null || AllTimeRecordTemp is null) return null;
            var diff = AllTimeRecordTemp.Value - Current.TempF;
            if (diff <= 0)  return $"At or above today's record of {AllTimeRecordTemp}°F!";
            if (diff <= 3)  return $"{diff:F0}° from today's record of {AllTimeRecordTemp}°F";
            if (diff <= 8)  return $"{diff:F0}° below today's record of {AllTimeRecordTemp}°F";
            return null;
        }
    }

    public string RecordProximityCssClass => AllTimeRecordTemp.HasValue && Current is not null
        ? (AllTimeRecordTemp.Value - Current.TempF) <= 0  ? "danger"
        : (AllTimeRecordTemp.Value - Current.TempF) <= 3  ? "danger"
        : (AllTimeRecordTemp.Value - Current.TempF) <= 8  ? "warning"
        : "secondary"
        : "secondary";

    // ── Period of record ──────────────────────────────────────────────────
    public string? PeriodOfRecordStart { get; set; }
    public string? PeriodOfRecordEnd { get; set; }

    public string PeriodOfRecordLabel
    {
        get
        {
            if (PeriodOfRecordStart is null || PeriodOfRecordEnd is null) return "Unknown";
            if (DateTime.TryParse(PeriodOfRecordStart, out var s) &&
                DateTime.TryParse(PeriodOfRecordEnd, out var e))
                return $"{s.Year}–{e.Year}";
            return $"{PeriodOfRecordStart} to {PeriodOfRecordEnd}";
        }
    }

    public bool HasData => TopRecords.Count > 0;
}

public class RecordEntry
{
    public int Temp { get; set; }
    public string Date { get; set; } = string.Empty;

    public string FormattedDate
    {
        get
        {
            if (DateTime.TryParse(Date, out var d))
                return d.ToString("MMMM d, yyyy");
            return Date;
        }
    }
}
