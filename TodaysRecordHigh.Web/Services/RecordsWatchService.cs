using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Services;

/// <summary>
/// Scans every ACIS-backed station in the background and keeps the latest "how close is
/// today to a record" snapshot in memory, so the /records-today page and the homepage
/// teaser both read an instant, pre-computed answer instead of fanning out ~800 requests
/// per page view. Runs on a timer rather than per-request: a synchronous scan across ~415
/// stations would take too long for any one visitor to wait on, and re-running it for
/// every visitor would be needlessly hard on ACIS and Open-Meteo, both free services this
/// project depends on for good behavior more than a contract.
/// </summary>
public class RecordsWatchService : BackgroundService, IRecordsWatchService
{
    private readonly IStationService _stations;
    private readonly IAcisService _acis;
    private readonly ICurrentWeatherService _weather;
    private readonly ILogger<RecordsWatchService> _logger;

    // Each scan makes ~415 Open-Meteo calls, and Open-Meteo's free tier allows 10,000 a
    // day shared with every page on the site. The old 30-minute, around-the-clock cadence
    // burned ~20,000/day and knocked out current conditions site-wide once the quota ran
    // dry. Hourly scans inside the window below cost ~6,200/day and leave the rest for
    // visitors. Record highs happen in the afternoon, so overnight scans add nothing.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);
    private const int ActiveStartHourCentral = 9;   // 9 AM CT = 10 AM ET
    private const int ActiveEndHourCentral   = 23;  // last scan 11 PM CT = 9 PM PT

    // Bounds how many stations are in flight at once. Open-Meteo answers 429 "Too many
    // concurrent requests" at 24, and that cap is shared with live page traffic, so keep
    // the background scan small. A full scan takes under a minute at this setting.
    private const int MaxConcurrency = 4;

    private volatile RecordWatchSnapshot? _latest;

    public RecordsWatchService(IStationService stations, IAcisService acis,
        ICurrentWeatherService weather, ILogger<RecordsWatchService> logger)
    {
        _stations = stations;
        _acis = acis;
        _weather = weather;
        _logger = logger;
    }

    public RecordWatchSnapshot? GetLatest() => _latest;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _latest = await ScanAsync(stoppingToken);
                _logger.LogInformation(
                    "Records watch scan complete: {WithData}/{Scanned} stations, {Notable} notable",
                    _latest.StationsWithData, _latest.StationsScanned, _latest.Notable.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed scan leaves the previous snapshot in place — stale data the
                // page labels with its timestamp beats no data at all.
                _logger.LogError(ex, "Records watch scan failed");
            }

            try
            {
                await Task.Delay(DelayUntilNextScan(DateTime.UtcNow), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One interval when the next scan still lands inside today's active window;
    /// otherwise sleep until the window opens. Always scans once at startup (the caller
    /// runs the first scan before asking) so a deploy never leaves the page empty.
    /// </summary>
    public static TimeSpan DelayUntilNextScan(DateTime utcNow)
    {
        var localNow = SiteClock.ToCentral(utcNow);
        var next = localNow + RefreshInterval;
        if (next.Hour >= ActiveStartHourCentral && next.Hour <= ActiveEndHourCentral && next.Date == localNow.Date)
            return RefreshInterval;

        var opens = localNow.Date.AddHours(ActiveStartHourCentral);
        if (opens <= localNow) opens = opens.AddDays(1);
        var opensUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(opens, DateTimeKind.Unspecified), SiteClock.Central);
        return opensUtc - utcNow;
    }

    private async Task<RecordWatchSnapshot> ScanAsync(CancellationToken ct)
    {
        var monthDay = SiteClock.Now.ToString("MM-dd");
        var stations = _stations.GetAll().Where(s => s.HasAcisData).ToList();

        using var semaphore = new SemaphoreSlim(MaxConcurrency);

        var tasks = stations.Select(async station =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await ScanOneAsync(station, monthDay, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        var entries = results.Where(e => e is not null).Select(e => e!).ToList();

        return new RecordWatchSnapshot(
            GeneratedAtUtc:    DateTime.UtcNow,
            MonthDay:          monthDay,
            StationsScanned:   stations.Count,
            StationsWithData:  entries.Count(e => e.RecordTemp.HasValue),
            Entries:           entries);
    }

    private async Task<RecordWatchEntry?> ScanOneAsync(Station station, string monthDay, CancellationToken ct)
    {
        // One slow or dead station must not sink the whole scan.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var sid = station.ThreadexSid!;
            var recordsTask = _acis.GetRecordHighsForDateAsync(sid, monthDay, topN: 1);
            var normalTask  = _acis.GetNormalMaxTempAsync(sid, monthDay);
            var wxTask = station.Latitude.HasValue && station.Longitude.HasValue
                ? _weather.GetCurrentAsync(station.Latitude.Value, station.Longitude.Value)
                : Task.FromResult<CurrentConditions?>(null);

            await Task.WhenAll(recordsTask, normalTask, wxTask).WaitAsync(cts.Token);

            var records = recordsTask.Result;
            var wx = wxTask.Result;

            return new RecordWatchEntry(
                Slug:          station.Slug,
                City:          station.City,
                State:         station.State,
                CurrentTemp:   wx?.TempF,
                ForecastHigh:  wx?.DailyMaxF,
                RecordTemp:    records.Count > 0 ? records[0].Temp : null,
                RecordDate:    records.Count > 0 ? records[0].Date : null,
                NormalTemp:    normalTask.Result,
                WeatherEmoji:  wx?.WeatherEmoji ?? "");
        }
        catch (Exception ex)
        {
            // The app shutting down mid-scan should stop the scan, not get swallowed
            // as if this one station merely timed out.
            if (ct.IsCancellationRequested) throw;

            _logger.LogDebug(ex, "Records watch: skipping {Slug}", station.Slug);
            return null;
        }
    }
}
