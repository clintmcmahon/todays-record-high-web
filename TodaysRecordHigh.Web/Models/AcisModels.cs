using System.Text.Json;
using System.Text.Json.Serialization;

namespace TodaysRecordHigh.Web.Models;

// ── Request ────────────────────────────────────────────────────────────────

public class AcisStnDataRequest
{
    [JsonPropertyName("sid")]
    public string Sid { get; set; } = string.Empty;

    [JsonPropertyName("sdate")]
    public string SDate { get; set; } = string.Empty;

    [JsonPropertyName("edate")]
    public string EDate { get; set; } = string.Empty;

    [JsonPropertyName("elems")]
    public List<AcisElement> Elems { get; set; } = [];
}

public class AcisElement
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    // Accepts a string ("dly") or an array ([1,0,0] for yearly stepping)
    [JsonPropertyName("interval")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Interval { get; set; }

    // Accepts a string ("dly") or a number (1)
    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Duration { get; set; }

    [JsonPropertyName("normal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Normal { get; set; }

    [JsonPropertyName("smry")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AcisSmry? Smry { get; set; }

    [JsonPropertyName("smry_only")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SmryOnly { get; set; }

    // Accepts a string ("year") or a string[] (["year","06-24","06-24"])
    [JsonPropertyName("groupby")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? GroupBy { get; set; }
}

public class AcisSmry
{
    [JsonPropertyName("reduce")]
    public string Reduce { get; set; } = string.Empty;

    [JsonPropertyName("add")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Add { get; set; }

    [JsonPropertyName("n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? N { get; set; }
}

// ── Domain results ─────────────────────────────────────────────────────────

public record AllTimeExtremes(
    int? MaxTemp, string? MaxTempDate,
    int? MinTemp, string? MinTempDate,
    double? MaxPcpn, string? MaxPcpnDate,
    double? MaxSnow, string? MaxSnowDate
);

public record DailyDeparture(string Date, int? ActualTemp, double? NormalTemp)
{
    public double? Departure => ActualTemp.HasValue && NormalTemp.HasValue
        ? ActualTemp.Value - NormalTemp.Value
        : null;
    public bool IsAboveNormal => Departure > 0;
}

public record PrecipData(
    double YtdActual,           // Jan 1 → yesterday actual
    double YtdNormal,           // Jan 1 → yesterday normal (same date window)
    double MonthActual,         // current month → yesterday actual
    double MonthNormal,         // current month → yesterday normal
    double MonthFullNormal,     // full calendar-month normal (all days)
    double? SummerActual,       // Jun–Aug through yesterday (null outside Jun–Aug)
    double? SummerNormal,       // Jun–Aug normal through same date
    double? SummerFullNormal,   // full Jun–Aug normal
    List<(int Year, double Actual, double? Normal)> AnnualTotals,
    List<(int Year, int Month, double Actual, double? Normal)> MonthlyLast24,
    List<(int Year, int Count)> HeavyRainDaysPerYear,
    int CurrentYearHeavyDays
);

public record WarmSummerNightsData(
    List<(int Year, int Count)> WarmNightsPerYear,
    List<(string Label, double AvgLow)> AvgLowByDecade,
    int ThisSummerCount,
    double? HistoricalAvg,
    int? RecordYear,
    int? RecordCount
);

public record HeatWaveEvent(
    string StartDate,
    string EndDate,
    int DurationDays,
    int PeakTemp,
    string PeakTempDate,
    double AvgDailyMax,
    double AvgOvernightLow,
    int Year
)
{
    public string FormattedStart => DateTime.TryParse(StartDate, out var d) ? d.ToString("MMM d, yyyy") : StartDate;
    public string FormattedEnd   => DateTime.TryParse(EndDate,   out var d) ? d.ToString("MMM d, yyyy") : EndDate;
    public string FormattedPeak  => DateTime.TryParse(PeakTempDate, out var d) ? d.ToString("MMM d") : PeakTempDate;
};

public record OnThisDayRecord(int Year, int? High, int? Low, double? Precip, double? Snow);

public record HeatWaveData(
    int CurrentStreak,
    string? StreakStartDate,
    bool StreakIsHeatWave,              // streak >= 3 days
    List<HeatWaveEvent> TopEvents,     // top 10 by duration
    HeatWaveEvent? MostRecentEvent,
    List<(int Year, int Count)> EventsPerYear,
    List<(string Decade, double AvgDuration)> AvgDurationByDecade,
    int DangerousNightsThisYear,
    List<(int Year, int Count)> DangerousNightsPerYear,
    double? HistoricalAvgDuration,
    double? HistoricalAvgPeak,
    List<(string Decade, double AvgHigh)> AvgHighByDecade,
    int HeatWaveThreshold              // 90th percentile of station's historical Jun–Aug highs
);

// ── Response ───────────────────────────────────────────────────────────────

public class AcisStnDataResponse
{
    [JsonPropertyName("meta")]
    public AcisMeta? Meta { get; set; }

    // data rows: each row is [date, value] or just [value] depending on the query
    [JsonPropertyName("data")]
    public List<List<JsonElement>>? Data { get; set; }

    // smry rows when smry_only=1: [[temp, date], ...]
    [JsonPropertyName("smry")]
    public List<List<JsonElement>>? Smry { get; set; }
}

public class AcisMeta
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("valid_daterange")]
    public List<List<string>>? ValidDateRange { get; set; }

    [JsonPropertyName("ll")]
    public List<double>? LatLon { get; set; }
}
