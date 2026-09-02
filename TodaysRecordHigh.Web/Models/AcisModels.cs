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

    // Server-side aggregation over an interval (e.g. monthly means)
    [JsonPropertyName("reduce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AcisReduce? Reduce { get; set; }

    [JsonPropertyName("smry_only")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SmryOnly { get; set; }

    // Accepts a string ("year") or a string[] (["year","06-24","06-24"])
    [JsonPropertyName("groupby")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? GroupBy { get; set; }
}

public class AcisReduce
{
    [JsonPropertyName("reduce")]
    public string Reduce { get; set; } = string.Empty;

    // "mcnt" appends a missing-day count to each reduced value
    [JsonPropertyName("add")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Add { get; set; }
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
    int CurrentYearHeavyDays,
    // Running totals for the current year, Jan 1 → yesterday: where the year stands
    // against where it normally would by the same date.
    List<(string Date, double CumActual, double CumNormal)> YtdCumulative,
    Trend? AnnualTrend          // least-squares fit across complete years only
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

// ── Month in Review ────────────────────────────────────────────────────────

public static class TempFormat
{
    /// <summary>
    /// One decimal place, rounding halves up. Math.Round's default banker's rounding
    /// turns a 78.85° month into "78.8", which reads as wrong next to published figures.
    /// </summary>
    public static double Round1(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

    public static string Temp1(double v) => Round1(v).ToString("F1");
}

public record MonthlyMean(int Year, int Month, double AvgTemp)
{
    public string Label      => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public string ShortLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    public string MonthName  => new DateTime(Year, Month, 1).ToString("MMMM");
    public string Display    => TempFormat.Temp1(AvgTemp);
}

public record HotStreak(int Days, string StartDate, string EndDate, int Year, int PeakTemp)
{
    public string FormattedRange
    {
        get
        {
            if (!DateTime.TryParse(StartDate, out var s) || !DateTime.TryParse(EndDate, out var e))
                return $"{StartDate} – {EndDate}";
            if (s.Date == e.Date)
                return $"{s:MMM} {s.Day}, {e:yyyy}";
            return s.Month == e.Month
                ? $"{s:MMM} {s.Day}–{e.Day}, {e:yyyy}"
                : $"{s:MMM} {s.Day} – {e:MMM} {e.Day}, {e:yyyy}";
        }
    }
}

public record PeerMonthRank(
    string City, string State, string Slug,
    double AvgTemp, int RankSameMonth, int TotalSameMonths)
{
    public string Display     => TempFormat.Temp1(AvgTemp);
    public string RankOrdinal => MonthInReview.Ordinal(RankSameMonth);
}

public record MonthlyPrecip(int Year, int Month, double Total)
{
    public string Label      => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public string ShortLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    public string MonthName  => new DateTime(Year, Month, 1).ToString("MMMM");
    public string Display    => Total.ToString("F2");
}

/// <summary>
/// Least-squares fit over a year-indexed series. Slope is carried per year so a
/// chart can draw the line, and surfaced per decade because that is the unit
/// climate trends are quoted in.
/// </summary>
public record Trend(double PerYear, double Intercept, int FirstYear, int LastYear, int Count)
{
    public double PerDecade => PerYear * 10;
    public double At(int year) => Intercept + PerYear * year;

    /// <summary>Fitted change from the first year of the series to the last.</summary>
    public double TotalChange => PerYear * (LastYear - FirstYear);

    /// <summary>Signed per-decade change, e.g. "+0.31" or "-1.4".</summary>
    public string PerDecadeDisplay(string format = "F2")
    {
        var text = PerDecade.ToString(format);
        var bare = text.TrimStart('-');
        // A slope that rounds away to nothing should not print as "-0.00".
        if (bare.All(c => c is '0' or '.' or ',')) return bare;
        return PerDecade >= 0 ? "+" + text : text;
    }

    public bool IsFlat(double epsilon) => Math.Abs(PerDecade) < epsilon;
}

public record MonthPrecipReview(
    int Year,
    int Month,
    double Total,
    double? NormalTotal,                // 1991–2020 mean for this calendar month
    int RankWettestSameMonth,           // 1 = wettest such month on record
    int RankDriestSameMonth,
    int TotalSameMonths,
    MonthlyPrecip? WettestSameMonthEver,
    MonthlyPrecip? DriestSameMonthEver,
    List<MonthlyPrecip> TopSameMonths,      // 10 wettest of this calendar month
    List<MonthlyPrecip> DriestSameMonths,   // 10 driest of this calendar month
    List<MonthlyPrecip> SameMonthHistory,   // every year's total for this calendar month
    Trend? SameMonthTrend)
{
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");
    public string Label     => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public string TotalDisplay      => Total.ToString("F2");
    public string? NormalDisplay    => NormalTotal?.ToString("F2");

    public double? Departure => NormalTotal.HasValue ? Math.Round(Total - NormalTotal.Value, 2) : null;

    /// <summary>Departure with an explicit sign, e.g. "+1.24".</summary>
    public string? DepartureDisplay => Departure is { } d
        ? $"{(d >= 0 ? "+" : "")}{d.ToString("F2")}" : null;

    public int? PercentOfNormal => NormalTotal is > 0
        ? (int)Math.Round(Total / NormalTotal.Value * 100)
        : null;

    public bool IsWetterThanNormal => Departure >= 0;

    /// <summary>Top or bottom five for this calendar month — worth a callout.</summary>
    public bool IsNotableWet => RankWettestSameMonth <= 5;
    public bool IsNotableDry => RankDriestSameMonth <= 5;

    public string RankWettestOrdinal => MonthInReview.Ordinal(RankWettestSameMonth);
    public string RankDriestOrdinal  => MonthInReview.Ordinal(RankDriestSameMonth);

    /// <summary>Whichever end of the ranking this month actually sits near.</summary>
    public bool RankFromDryEnd => RankDriestSameMonth < RankWettestSameMonth;

    public string RankOrdinal    => RankFromDryEnd ? RankDriestOrdinal : RankWettestOrdinal;
    public string RankSuperlative => RankFromDryEnd ? "driest" : "wettest";
}

public record MonthInReview(
    int Year,
    int Month,
    double AvgTemp,
    double? NormalAvgTemp,              // 1991–2020 mean for this calendar month
    int RankAllMonths,                  // rank vs. every month on record (1 = warmest)
    int TotalMonths,
    int RankSameMonth,                  // rank vs. the same calendar month only
    int TotalSameMonths,
    MonthlyMean? WarmestMonthEver,
    MonthlyMean? WarmestSameMonthEver,
    List<MonthlyMean> TopMonths,        // top 10, any calendar month
    List<MonthlyMean> TopSameMonths,    // top 10, this calendar month
    List<MonthlyMean> SameMonthHistory, // every year's value for this calendar month
    HotStreak? LongestStreakInMonth,
    HotStreak? LongestStreakEver,
    int? LastYearWithLongerStreak,      // most recent prior year beating this month's streak
    int? LastYearTiedStreak,            // most recent prior year matching it exactly
    int HotDayThreshold,                // °F, fixed at 90 where meaningful
    int HotDaysInMonth,
    bool ShowStreak,                    // false for stations that rarely reach the threshold
    int? YearPeakHigh,
    string? YearPeakHighDate,
    int? PeakMilestone,                 // 100 / 95 / 90, whichever the year's peak cleared
    int? LastYearAtMilestone,
    List<PeerMonthRank> Peers,
    Trend? SameMonthTrend,              // temperature trend across this calendar month
    MonthPrecipReview? Precip,
    bool IsLatest,                      // false when the reader has picked an earlier month
    Dictionary<int, List<int>> AvailableYearsByMonth)
{
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");
    public string Label     => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public double? DepartureFromNormal =>
        NormalAvgTemp.HasValue ? TempFormat.Round1(AvgTemp - NormalAvgTemp.Value) : null;

    public string AvgTempDisplay    => TempFormat.Temp1(AvgTemp);
    public string? NormalAvgDisplay => NormalAvgTemp.HasValue ? TempFormat.Temp1(NormalAvgTemp.Value) : null;

    /// <summary>Departure with an explicit sign, e.g. "+4.8".</summary>
    public string? DepartureDisplay => DepartureFromNormal is { } d
        ? $"{(d >= 0 ? "+" : "")}{d.ToString("F1")}" : null;

    /// <summary>True when this month lands in the top 10 of every month on record.</summary>
    public bool IsNotableAllTime => RankAllMonths <= 10;

    public string RankAllMonthsOrdinal  => Ordinal(RankAllMonths);
    public string RankSameMonthOrdinal  => Ordinal(RankSameMonth);

    public int? YearsSinceLongerStreak =>
        LastYearWithLongerStreak.HasValue ? Year - LastYearWithLongerStreak.Value : null;

    public int? YearsSinceTiedStreak =>
        LastYearTiedStreak.HasValue ? Year - LastYearTiedStreak.Value : null;

    public int? YearsSinceMilestone =>
        LastYearAtMilestone.HasValue ? Year - LastYearAtMilestone.Value : null;

    /// <summary>No earlier year matched or beat this month's streak.</summary>
    public bool StreakIsMonthRecord =>
        LongestStreakInMonth is not null
        && !LastYearWithLongerStreak.HasValue
        && !LastYearTiedStreak.HasValue;

    public static string Pluralize(int n, string word) => n == 1 ? word : word + "s";

    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is >= 11 and <= 13
            ? "th"
            : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }
}

// ── Fall (meteorological September–November) ───────────────────────────────

/// <summary>A dated event inside one year: a first freeze, a last 90° day, a first snow.</summary>
public record DateEvent(int Year, DateTime Date, double Value)
{
    public int Doy               => Date.DayOfYear;
    public string Display        => Date.ToString("MMM d");
    public string LongDisplay    => Date.ToString("MMMM d, yyyy");
    public string ValueDisplay   => Value.ToString("0.##");
}

/// <summary>
/// One seasonal marker across the whole record: where it fell this year, where it
/// normally falls, the extremes, and whether it has been drifting.
/// </summary>
public record SeasonMarker(
    string Key,
    string Label,
    string Note,                    // what the marker means, one line
    DateEvent? Current,             // null when the season has not reached it yet
    double? NormalDoy,              // 1991–2020 mean day-of-year
    DateEvent? Earliest,
    DateEvent? Latest,
    Trend? DoyTrend,                // days per decade; positive means later
    List<DateEvent> History,        // every complete year the marker occurred, ascending
    int YearsOnRecord,
    bool IsProvisional)             // the year is still running, so "last" is only "so far"
{
    // Day-of-year rendered against a non-leap reference year, so Mar 1 never slips a day.
    public string? NormalDisplay => NormalDoy.HasValue
        ? new DateTime(2001, 1, 1).AddDays(Math.Round(NormalDoy.Value) - 1).ToString("MMM d")
        : null;

    // A provisional "last 90° so far" has no meaningful distance from its normal date:
    // the year can still produce a later one.
    public int? DaysVsNormal => Current is not null && NormalDoy.HasValue && !IsProvisional
        ? (int)Math.Round(Current.Doy - NormalDoy.Value)
        : null;

    public string? VsNormalDisplay
    {
        get
        {
            if (DaysVsNormal is not { } d) return null;
            if (d == 0) return "on time";
            var n = Math.Abs(d);
            return $"{n} {(n == 1 ? "day" : "days")} {(d > 0 ? "later" : "earlier")}";
        }
    }

    public bool IsLate  => DaysVsNormal > 0;
    public bool HasHappened => Current is not null;
}

public record GrowingSeason(
    DateEvent? LastSpringFreeze,
    DateEvent? FirstFallFreeze,
    int? Days,                              // freeze-free days between the two
    double? NormalDays,                     // 1991–2020 mean
    Trend? DaysTrend,
    List<(int Year, int Days)> History,
    (int Year, int Days)? Longest,
    (int Year, int Days)? Shortest)
{
    public int? DaysVsNormal => Days.HasValue && NormalDays.HasValue
        ? (int)Math.Round(Days.Value - NormalDays.Value)
        : null;

    public string? VsNormalDisplay
    {
        get
        {
            if (DaysVsNormal is not { } d) return null;
            if (d == 0) return "exactly normal";
            var n = Math.Abs(d);
            return $"{n} {(n == 1 ? "day" : "days")} {(d > 0 ? "longer" : "shorter")} than normal";
        }
    }
}

public record FallSeason(int Year, double? AvgTemp, double? Precip)
{
    public string Label            => $"Fall {Year}";
    public string? TempDisplay     => AvgTemp.HasValue ? TempFormat.Temp1(AvgTemp.Value) : null;
    public string? PrecipDisplay   => Precip?.ToString("F2");
}

public record FallTempSummary(
    double AvgTemp,
    double? Normal,
    int RankWarmest,
    int RankCoolest,
    int TotalSeasons,
    List<FallSeason> Warmest,
    List<FallSeason> Coldest)
{
    public string AvgDisplay     => TempFormat.Temp1(AvgTemp);
    public string? NormalDisplay => Normal.HasValue ? TempFormat.Temp1(Normal.Value) : null;

    public double? Departure => Normal.HasValue ? TempFormat.Round1(AvgTemp - Normal.Value) : null;
    public string? DepartureDisplay => Departure is { } d ? $"{(d >= 0 ? "+" : "")}{d:F1}" : null;

    public bool RankFromCoolEnd => RankCoolest < RankWarmest;
    public string RankOrdinal     => MonthInReview.Ordinal(RankFromCoolEnd ? RankCoolest : RankWarmest);
    public string RankSuperlative => RankFromCoolEnd ? "coolest" : "warmest";
    public bool IsNotable => Math.Min(RankWarmest, RankCoolest) <= 5;
}

public record FallPrecipSummary(
    double Total,
    double? Normal,
    int RankWettest,
    int RankDriest,
    int TotalSeasons,
    List<FallSeason> Wettest,
    List<FallSeason> Driest)
{
    public string TotalDisplay   => Total.ToString("F2");
    public string? NormalDisplay => Normal?.ToString("F2");

    public double? Departure => Normal.HasValue ? Math.Round(Total - Normal.Value, 2) : null;
    public string? DepartureDisplay => Departure is { } d ? $"{(d >= 0 ? "+" : "")}{d:F2}" : null;

    public int? PercentOfNormal => Normal is > 0 ? (int)Math.Round(Total / Normal.Value * 100) : null;
    public bool IsWetterThanNormal => Departure >= 0;

    public bool RankFromDryEnd => RankDriest < RankWettest;
    public string RankOrdinal     => MonthInReview.Ordinal(RankFromDryEnd ? RankDriest : RankWettest);
    public string RankSuperlative => RankFromDryEnd ? "driest" : "wettest";
    public bool IsNotable => Math.Min(RankWettest, RankDriest) <= 5;
}

public record FallReview(
    int Year,
    bool IsInProgress,                  // the season under review has not finished
    FallTempSummary? Temp,              // null while the season is still running
    FallPrecipSummary? Precip,
    List<FallSeason> TempHistory,       // every complete fall, ascending by year
    Trend? TempTrend,
    double? TempNormal,                 // 1991–2020 seasonal means, independent of the
    List<FallSeason> PrecipHistory,     // season being reviewed, so the charts always
    Trend? PrecipTrend,                 // have a reference line
    double? PrecipNormal,
    List<SeasonMarker> Markers,
    GrowingSeason? Growing,
    bool IsLatest,
    List<int> AvailableYears)
{
    public string Label => $"Fall {Year}";

    /// <summary>Meteorological fall, the window every number on the tab is built from.</summary>
    public string WindowLabel => $"September 1 – November 30, {Year}";
}

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
