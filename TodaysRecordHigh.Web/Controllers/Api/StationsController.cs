using Microsoft.AspNetCore.Mvc;
using TodaysRecordHigh.Web.Models;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Controllers.Api;

[ApiController]
[Route("api/stations")]
public class StationsController : ControllerBase
{
    private readonly IStationService _stationService;
    private readonly IAcisService _acisService;
    private readonly ICurrentWeatherService _weather;
    private readonly IWeatherAlertsService _alerts;

    public StationsController(IStationService stationService, IAcisService acisService,
        ICurrentWeatherService weather, IWeatherAlertsService alerts)
    {
        _stationService = stationService;
        _acisService = acisService;
        _weather = weather;
        _alerts = alerts;
    }

    // GET /api/stations?search=denver
    // Lightweight list for the mobile app's location picker. Optional `search`
    // matches against city, state code, and state name (case-insensitive, substring).
    [HttpGet]
    public IActionResult List([FromQuery] string? search)
    {
        IEnumerable<Station> stations = _stationService.GetAll();

        if (!string.IsNullOrWhiteSpace(search))
        {
            stations = stations.Where(s =>
                s.City.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.State.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.StateName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var results = stations
            .OrderBy(s => s.StateName)
            .ThenBy(s => s.City)
            .Select(ToSummaryDto);

        return Ok(results);
    }

    // GET /api/stations/denver-co?monthDay=07-04
    // Full station detail — mirrors StationsController.Index (the Razor page) but as JSON.
    // `monthDay` (MM-dd) lets callers request records for a date other than today, e.g. the
    // mobile app's date-navigation arrows. Defaults to today when omitted or invalid.
    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, [FromQuery] string? monthDay, CancellationToken ct)
    {
        var station = _stationService.GetBySlug(slug);
        if (station is null)
            return NotFound(new { error = $"No station found for slug '{slug}'" });

        var today = DateTime.Now;
        var effectiveMonthDay = ParseMonthDay(monthDay) ?? today.ToString("MM-dd");
        var dateLabel = DateTime.ParseExact($"2024-{effectiveMonthDay}", "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture).ToString("MMMM d");

        if (!station.HasAcisData)
            return Ok(ToDetailDto(station, dateLabel, effectiveMonthDay, null));

        var sid = station.ThreadexSid!;
        var year = today.Year;

        var recordHighsTask = _acisService.GetRecordHighsForDateAsync(sid, effectiveMonthDay, topN: 10);
        var recordLowsTask = _acisService.GetRecordLowsForDateAsync(sid, effectiveMonthDay, topN: 5);
        var normalMaxTask = _acisService.GetNormalMaxTempAsync(sid, effectiveMonthDay);
        var normalMinTask = _acisService.GetNormalMinTempAsync(sid, effectiveMonthDay);
        var porTask = _acisService.GetPeriodOfRecordAsync(sid);
        var extremesTask = _acisService.GetAllTimeExtremesAsync(sid);
        var hotDaysTask = _acisService.GetHotDaysPerYearAsync(sid);
        var ytdTask = _acisService.GetYearToDateDeparturesAsync(sid, year);
        var warmNightsTask = _acisService.GetWarmSummerNightsAsync(sid);
        var precipTask = _acisService.GetPrecipDataAsync(sid);
        var heatWaveTask = _acisService.GetHeatWaveDataAsync(sid);
        var onThisDayTask = _acisService.GetOnThisDayHistoryAsync(sid, effectiveMonthDay);
        var wxTask = station.Latitude.HasValue && station.Longitude.HasValue
            ? _weather.GetCurrentAsync(station.Latitude.Value, station.Longitude.Value)
            : Task.FromResult<CurrentConditions?>(null);
        var alertsTask = station.Latitude.HasValue && station.Longitude.HasValue
            ? _alerts.GetActiveAlertsAsync(station.Latitude.Value, station.Longitude.Value)
            : Task.FromResult<List<WeatherAlert>>([]);

        await Task.WhenAll(recordHighsTask, recordLowsTask, normalMaxTask, normalMinTask,
            porTask, extremesTask, hotDaysTask, ytdTask, warmNightsTask, precipTask,
            heatWaveTask, onThisDayTask, wxTask, alertsTask);

        var records = recordHighsTask.Result;
        var lowRecords = recordLowsTask.Result;
        var por = porTask.Result;

        var acis = new AcisDetailDto(
            AllTimeRecordTemp: records.Count > 0 ? records[0].Temp : null,
            AllTimeRecordDate: records.Count > 0 ? records[0].Date : null,
            TopRecords: records.Select(r => new RecordEntryDto(r.Temp, r.Date)).ToList(),
            AllTimeRecordLowTemp: lowRecords.Count > 0 ? lowRecords[0].Temp : null,
            AllTimeRecordLowDate: lowRecords.Count > 0 ? lowRecords[0].Date : null,
            TopRecordLows: lowRecords.Select(r => new RecordEntryDto(r.Temp, r.Date)).ToList(),
            NormalMaxTemp: normalMaxTask.Result,
            NormalMinTemp: normalMinTask.Result,
            PeriodOfRecordStart: por?.Start,
            PeriodOfRecordEnd: por?.End,
            Extremes: extremesTask.Result,
            HotDaysPerYear: hotDaysTask.Result.Select(x => new YearCountDto(x.Year, x.Count)).ToList(),
            YtdDepartures: ytdTask.Result,
            WarmNights: ToWarmNightsDto(warmNightsTask.Result),
            Precip: ToPrecipDto(precipTask.Result),
            HeatWaves: ToHeatWaveDto(heatWaveTask.Result),
            OnThisDayHistory: onThisDayTask.Result,
            Current: wxTask.Result,
            ActiveAlerts: alertsTask.Result.Select(a => new WeatherAlertDto(a.Event, a.Headline, a.Severity, a.Expires)).ToList()
        );

        return Ok(ToDetailDto(station, dateLabel, effectiveMonthDay, acis));
    }

    // Accepts "MM-dd"; returns null (falls back to today) for missing/malformed input.
    private static string? ParseMonthDay(string? monthDay)
    {
        if (string.IsNullOrWhiteSpace(monthDay))
            return null;

        // Validate against a leap year so "02-29" parses correctly.
        return DateTime.TryParseExact($"2024-{monthDay}", "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _)
            ? monthDay
            : null;
    }

    private static StationSummaryDto ToSummaryDto(Station s) => new(
        Slug: s.Slug,
        Name: s.Name,
        City: s.City,
        State: s.State,
        StateName: s.StateName,
        Latitude: s.Latitude,
        Longitude: s.Longitude,
        HasAcisData: s.HasAcisData
    );

    private static StationDetailDto ToDetailDto(Station s, string dateLabel, string monthDay, AcisDetailDto? acis) => new(
        Slug: s.Slug,
        Name: s.Name,
        City: s.City,
        State: s.State,
        StateName: s.StateName,
        Latitude: s.Latitude,
        Longitude: s.Longitude,
        HasAcisData: s.HasAcisData,
        TodayLabel: dateLabel,
        MonthDay: monthDay,
        Acis: acis
    );

    private static WarmSummerNightsDto ToWarmNightsDto(WarmSummerNightsData? d) => d is null
        ? new WarmSummerNightsDto([], [], 0, null, null, null)
        : new WarmSummerNightsDto(
            d.WarmNightsPerYear.Select(x => new YearCountDto(x.Year, x.Count)).ToList(),
            d.AvgLowByDecade.Select(x => new DecadeValueDto(x.Label, x.AvgLow)).ToList(),
            d.ThisSummerCount, d.HistoricalAvg, d.RecordYear, d.RecordCount);

    private static PrecipDataDto? ToPrecipDto(PrecipData? d) => d is null ? null : new PrecipDataDto(
        d.YtdActual, d.YtdNormal, d.MonthActual, d.MonthNormal, d.MonthFullNormal,
        d.SummerActual, d.SummerNormal, d.SummerFullNormal,
        d.AnnualTotals.Select(x => new AnnualTotalDto(x.Year, x.Actual, x.Normal)).ToList(),
        d.MonthlyLast24.Select(x => new MonthlyTotalDto(x.Year, x.Month, x.Actual, x.Normal)).ToList(),
        d.HeavyRainDaysPerYear.Select(x => new YearCountDto(x.Year, x.Count)).ToList(),
        d.CurrentYearHeavyDays);

    private static HeatWaveDataDto? ToHeatWaveDto(HeatWaveData? d) => d is null ? null : new HeatWaveDataDto(
        d.CurrentStreak, d.StreakStartDate, d.StreakIsHeatWave, d.TopEvents, d.MostRecentEvent,
        d.EventsPerYear.Select(x => new YearCountDto(x.Year, x.Count)).ToList(),
        d.AvgDurationByDecade.Select(x => new DecadeValueDto(x.Decade, x.AvgDuration)).ToList(),
        d.DangerousNightsThisYear,
        d.DangerousNightsPerYear.Select(x => new YearCountDto(x.Year, x.Count)).ToList(),
        d.HistoricalAvgDuration, d.HistoricalAvgPeak,
        d.AvgHighByDecade.Select(x => new DecadeValueDto(x.Decade, x.AvgHigh)).ToList(),
        d.HeatWaveThreshold);
}

// ── JSON response DTOs ──────────────────────────────────────────────────────
// Kept separate from the ValueTuple-based internal models (AllTimeExtremes,
// PrecipData, etc.) since tuples serialize as {"item1":...} rather than named
// fields, which isn't a contract worth exposing publicly.

public record StationSummaryDto(
    string Slug, string Name, string City, string State, string StateName,
    double? Latitude, double? Longitude, bool HasAcisData);

public record StationDetailDto(
    string Slug, string Name, string City, string State, string StateName,
    double? Latitude, double? Longitude, bool HasAcisData,
    string TodayLabel, string MonthDay,
    AcisDetailDto? Acis);

public record AcisDetailDto(
    int? AllTimeRecordTemp, string? AllTimeRecordDate, List<RecordEntryDto> TopRecords,
    int? AllTimeRecordLowTemp, string? AllTimeRecordLowDate, List<RecordEntryDto> TopRecordLows,
    double? NormalMaxTemp, double? NormalMinTemp,
    string? PeriodOfRecordStart, string? PeriodOfRecordEnd,
    AllTimeExtremes? Extremes,
    List<YearCountDto> HotDaysPerYear,
    List<DailyDeparture> YtdDepartures,
    WarmSummerNightsDto WarmNights,
    PrecipDataDto? Precip,
    HeatWaveDataDto? HeatWaves,
    List<OnThisDayRecord> OnThisDayHistory,
    CurrentConditions? Current,
    List<WeatherAlertDto> ActiveAlerts);

public record RecordEntryDto(int Temp, string Date);

public record WeatherAlertDto(string Event, string Headline, string Severity, DateTime? Expires);

public record YearCountDto(int Year, int Count);

public record DecadeValueDto(string Decade, double Value);

public record AnnualTotalDto(int Year, double Actual, double? Normal);

public record MonthlyTotalDto(int Year, int Month, double Actual, double? Normal);

public record WarmSummerNightsDto(
    List<YearCountDto> WarmNightsPerYear,
    List<DecadeValueDto> AvgLowByDecade,
    int ThisSummerCount,
    double? HistoricalAvg,
    int? RecordYear,
    int? RecordCount);

public record PrecipDataDto(
    double YtdActual, double YtdNormal,
    double MonthActual, double MonthNormal, double MonthFullNormal,
    double? SummerActual, double? SummerNormal, double? SummerFullNormal,
    List<AnnualTotalDto> AnnualTotals,
    List<MonthlyTotalDto> MonthlyLast24,
    List<YearCountDto> HeavyRainDaysPerYear,
    int CurrentYearHeavyDays);

public record HeatWaveDataDto(
    int CurrentStreak, string? StreakStartDate, bool StreakIsHeatWave,
    List<HeatWaveEvent> TopEvents, HeatWaveEvent? MostRecentEvent,
    List<YearCountDto> EventsPerYear,
    List<DecadeValueDto> AvgDurationByDecade,
    int DangerousNightsThisYear,
    List<YearCountDto> DangerousNightsPerYear,
    double? HistoricalAvgDuration, double? HistoricalAvgPeak,
    List<DecadeValueDto> AvgHighByDecade,
    int HeatWaveThreshold);
