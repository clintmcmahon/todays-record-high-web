using Microsoft.AspNetCore.Mvc;
using TodaysRecordHigh.Web.Services;
using TodaysRecordHigh.Web.ViewModels;

namespace TodaysRecordHigh.Web.Controllers;

public class StationsController : Controller
{
    private readonly IStationService _stationService;
    private readonly IAcisService _acisService;
    private readonly ICurrentWeatherService _weather;

    public StationsController(IStationService stationService, IAcisService acisService,
        ICurrentWeatherService weather)
    {
        _stationService = stationService;
        _acisService = acisService;
        _weather = weather;
    }

    [HttpGet("/stations")]
    public IActionResult List()
    {
        var byState = _stationService.GetAll()
            .OrderBy(s => s.StateName)
            .ThenBy(s => s.City)
            .GroupBy(s => s.StateName)
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewData["Title"] = "All Stations – Today's Record High";
        ViewData["MetaDescription"] = "Browse all U.S. weather stations tracked by Today's Record High. View all-time record temperatures for hundreds of cities.";
        return View("List", byState);
    }

    [HttpGet("/stations/{slug}")]
    public async Task<IActionResult> Index(string slug, int? year, int? month, int? fall,
        string? rangeStart, string? rangeEnd, int? rangeYear)
    {
        var station = _stationService.GetBySlug(slug);
        if (station is null)
            return NotFound();

        // Month in Review and Fall in Review were retired in favor of Custom Range,
        // the same computation generalized to any window — a month or a season is
        // just a specific range. Old links redirect to their equivalent range rather
        // than 404ing or silently ignoring the params.
        if (month is >= 1 and <= 12 && year.HasValue)
        {
            var lastDay = DateTime.DaysInMonth(year.Value, month.Value);
            return RedirectPermanent(
                $"/stations/{slug}?rangeStart={month:00}-01&rangeEnd={month:00}-{lastDay:00}&rangeYear={year}");
        }
        if (fall.HasValue)
        {
            return RedirectPermanent($"/stations/{slug}?rangeStart=09-01&rangeEnd=11-30&rangeYear={fall}");
        }

        var today = DateTime.Now;
        var monthDay = today.ToString("MM-dd");

        var vm = new StationDetailViewModel
        {
            Station = station,
            TodayLabel = today.ToString("MMMM d"),
            MonthDay = monthDay
        };

        if (!station.HasAcisData)
            return View(vm);

        var sid = station.ThreadexSid!;
        var currentYear = today.Year;

        var recordHighsTask = _acisService.GetRecordHighsForDateAsync(sid, monthDay, topN: 10);
        var recordLowsTask  = _acisService.GetRecordLowsForDateAsync(sid, monthDay, topN: 5);
        var normalMaxTask   = _acisService.GetNormalMaxTempAsync(sid, monthDay);
        var normalMinTask   = _acisService.GetNormalMinTempAsync(sid, monthDay);
        var porTask         = _acisService.GetPeriodOfRecordAsync(sid);
        var extremesTask    = _acisService.GetAllTimeExtremesAsync(sid);
        var hotDaysTask     = _acisService.GetHotDaysPerYearAsync(sid);
        var ytdTask         = _acisService.GetYearToDateDeparturesAsync(sid, currentYear);
        var warmNightsTask  = _acisService.GetWarmSummerNightsAsync(sid);
        var precipTask      = _acisService.GetPrecipDataAsync(sid);
        var heatWaveTask    = _acisService.GetHeatWaveDataAsync(sid);
        var climateRecordsTask = _acisService.GetClimateRecordsAsync(sid);

        // Custom Range: defaults to the trailing 30 days (ending yesterday, since that's
        // as current as Threadex data gets) so the tab shows something useful before a
        // reader has picked their own window. Both ends clamp to Jan 1 rather than cross
        // into the prior year — the engine doesn't support wraparound windows yet, so
        // early January gets a short default window instead of an invalid one.
        var yearStart = new DateTime(today.Year, 1, 1);
        var defaultRangeEnd   = today.AddDays(-1) < yearStart ? yearStart : today.AddDays(-1);
        var defaultRangeStart = defaultRangeEnd.AddDays(-30) < yearStart ? yearStart : defaultRangeEnd.AddDays(-30);
        var effectiveRangeStart = string.IsNullOrWhiteSpace(rangeStart)
            ? defaultRangeStart.ToString("MM-dd") : rangeStart;
        var effectiveRangeEnd = string.IsNullOrWhiteSpace(rangeEnd)
            ? defaultRangeEnd.ToString("MM-dd") : rangeEnd;
        var customRangeTask = _acisService.GetCustomRangeReviewAsync(
            sid, effectiveRangeStart, effectiveRangeEnd, rangeYear);

        var wxTask          = station.Latitude.HasValue && station.Longitude.HasValue
                              ? _weather.GetCurrentAsync(station.Latitude.Value, station.Longitude.Value)
                              : Task.FromResult<Services.CurrentConditions?>(null);

        await Task.WhenAll(recordHighsTask, recordLowsTask, normalMaxTask, normalMinTask,
                           porTask, extremesTask, hotDaysTask, ytdTask, warmNightsTask, precipTask,
                           heatWaveTask, climateRecordsTask, customRangeTask, wxTask);

        var records    = recordHighsTask.Result;
        var lowRecords = recordLowsTask.Result;

        vm.TopRecords           = records.Select(r => new RecordEntry { Temp = r.Temp, Date = r.Date }).ToList();
        vm.AllTimeRecordTemp    = records.Count > 0 ? records[0].Temp : null;
        vm.AllTimeRecordDate    = records.Count > 0 ? records[0].Date : null;
        vm.TopRecordLows        = lowRecords.Select(r => new RecordEntry { Temp = r.Temp, Date = r.Date }).ToList();
        vm.AllTimeRecordLowTemp = lowRecords.Count > 0 ? lowRecords[0].Temp : null;
        vm.AllTimeRecordLowDate = lowRecords.Count > 0 ? lowRecords[0].Date : null;
        vm.NormalMaxTemp        = normalMaxTask.Result;
        vm.NormalMinTemp        = normalMinTask.Result;
        vm.PeriodOfRecordStart  = porTask.Result?.Start;
        vm.PeriodOfRecordEnd    = porTask.Result?.End;
        vm.Extremes             = extremesTask.Result;
        vm.HotDaysPerYear       = hotDaysTask.Result;
        vm.YtdDepartures        = ytdTask.Result;
        vm.WarmNights           = warmNightsTask.Result;
        vm.Precip               = precipTask.Result;
        vm.HeatWaves            = heatWaveTask.Result;
        vm.ClimateRecords       = climateRecordsTask.Result;
        vm.CustomRangeReview    = customRangeTask.Result;
        vm.Current              = wxTask.Result;

        return View(vm);
    }
}
