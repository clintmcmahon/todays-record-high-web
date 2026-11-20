namespace TodaysRecordHigh.Web.Models;

public enum RecordWatchStatus
{
    Broken,     // the OBSERVED temperature has met or passed the record — a fact
    OnTrack,    // the forecast high would break it, but the observation hasn't caught up yet — a projection
    Close,      // within the "close call" margin, not projected to break
    Normal      // everything else — not shown on the watch page
}

/// <summary>
/// One station's standing against today's record high, as of the last background scan.
///
/// Observed and forecast are kept as two separate signals on purpose, never silently
/// merged into one "peak" — "the record broke" is a claim of fact and must come only from
/// an actual reading; the forecast is displayed too because it adds real anticipation
/// ("this city is on pace for a record"), but it's a prediction with a few degrees of
/// day-of error, which is exactly the margin this page is built around. Blending them
/// would let the page assert a broken record hours before it's true, and same-day highs
/// miss by enough that a meaningful share of those claims would turn out wrong by evening.
/// </summary>
public record RecordWatchEntry(
    string Slug,
    string City,
    string State,
    double? CurrentTemp,
    double? ForecastHigh,
    int? RecordTemp,
    string? RecordDate,       // e.g. "1936-07-14" — the year is what readers want ("since 1936")
    double? NormalTemp,
    string WeatherEmoji)
{
    /// <summary>Degrees below record using the OBSERVED reading only. Zero or negative: broken, for real.</summary>
    public double? ObservedMargin => RecordTemp.HasValue && CurrentTemp.HasValue
        ? Math.Round(RecordTemp.Value - CurrentTemp.Value, 1)
        : null;

    /// <summary>Degrees below record using the forecast high only — the anticipation signal.</summary>
    public double? ForecastMargin => RecordTemp.HasValue && ForecastHigh.HasValue
        ? Math.Round(RecordTemp.Value - ForecastHigh.Value, 1)
        : null;

    public bool IsBroken => ObservedMargin is <= 0;
    public bool IsOnTrack => !IsBroken && ForecastMargin is <= 0;

    /// <summary>
    /// The closer of the two gaps, for the "Close call" tier only — never used to claim
    /// broken. Taking the smaller rather than preferring one source means a city that's
    /// currently far off but trending toward the record by forecast still shows up as
    /// worth watching, and one that was close earlier but is forecast to drift away still
    /// gets credit for where it stands right now.
    /// </summary>
    public double? Margin => (ObservedMargin, ForecastMargin) switch
    {
        ({ } om, { } fm) => Math.Min(om, fm),
        ({ } om, null)   => om,
        (null, { } fm)   => fm,
        _                => null
    };

    public RecordWatchStatus Status => this switch
    {
        { IsBroken: true }             => RecordWatchStatus.Broken,
        { IsOnTrack: true }            => RecordWatchStatus.OnTrack,
        _ when Margin is > 0 and <= 5  => RecordWatchStatus.Close,
        _                              => RecordWatchStatus.Normal
    };

    public int? RecordYear => RecordDate is not null && DateTime.TryParse(RecordDate, out var d) ? d.Year : null;

    /// <summary>For the Broken tier: how far over, from the actual reading.</summary>
    public string? ObservedMarginDisplay => ObservedMargin is { } m && m <= 0
        ? $"+{Math.Abs(m):F0}° over" : null;

    /// <summary>For the OnTrack tier: how far the forecast projects past the record.</summary>
    public string? ForecastMarginDisplay => ForecastMargin is { } m && m <= 0
        ? $"forecast +{Math.Abs(m):F0}° over" : null;

    /// <summary>For the Close tier: plain distance, whichever source it came from.</summary>
    public string? MarginDisplay => Margin is { } m and > 0 ? $"{m:F0}° away" : null;
}

/// <summary>The result of one full background scan across every ACIS-backed station.</summary>
public record RecordWatchSnapshot(
    DateTime GeneratedAtUtc,
    string MonthDay,             // the calendar date this snapshot was computed for
    int StationsScanned,
    int StationsWithData,
    List<RecordWatchEntry> Entries)
{
    /// <summary>Confirmed by an actual observation — safe to state as fact.</summary>
    public List<RecordWatchEntry> Broken => Entries
        .Where(e => e.Status == RecordWatchStatus.Broken)
        .OrderBy(e => e.ObservedMargin)
        .ToList();

    /// <summary>Forecast to break, but not there yet — momentum, not a claim.</summary>
    public List<RecordWatchEntry> OnTrack => Entries
        .Where(e => e.Status == RecordWatchStatus.OnTrack)
        .OrderBy(e => e.ForecastMargin)
        .ToList();

    public List<RecordWatchEntry> Close => Entries
        .Where(e => e.Status == RecordWatchStatus.Close)
        .OrderBy(e => e.Margin)
        .ToList();

    /// <summary>Confirmed first, then on-track, then closest-to-broken — the order a "watch" list reads in.</summary>
    /// <summary>"November 20" for the date this snapshot covers, which overnight is yesterday.</summary>
    public string DateLabel => DateTime.TryParseExact($"2000-{MonthDay}", "yyyy-MM-dd",
        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
        ? d.ToString("MMMM d") : MonthDay;

    public List<RecordWatchEntry> Notable => Broken.Concat(OnTrack).Concat(Close).ToList();
}
