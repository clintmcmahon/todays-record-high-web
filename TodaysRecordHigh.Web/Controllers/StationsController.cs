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
    public async Task<IActionResult> Index(string slug)
    {
        var station = _stationService.GetBySlug(slug);
        if (station is null)
            return NotFound();

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
        var year = today.Year;

        var recordHighsTask = _acisService.GetRecordHighsForDateAsync(sid, monthDay, topN: 10);
        var recordLowsTask  = _acisService.GetRecordLowsForDateAsync(sid, monthDay, topN: 5);
        var normalMaxTask   = _acisService.GetNormalMaxTempAsync(sid, monthDay);
        var normalMinTask   = _acisService.GetNormalMinTempAsync(sid, monthDay);
        var porTask         = _acisService.GetPeriodOfRecordAsync(sid);
        var extremesTask    = _acisService.GetAllTimeExtremesAsync(sid);
        var hotDaysTask     = _acisService.GetHotDaysPerYearAsync(sid);
        var ytdTask         = _acisService.GetYearToDateDeparturesAsync(sid, year);
        var warmNightsTask  = _acisService.GetWarmSummerNightsAsync(sid);
        var precipTask      = _acisService.GetPrecipDataAsync(sid);
        var wxTask          = station.Latitude.HasValue && station.Longitude.HasValue
                              ? _weather.GetCurrentAsync(station.Latitude.Value, station.Longitude.Value)
                              : Task.FromResult<Services.CurrentConditions?>(null);

        await Task.WhenAll(recordHighsTask, recordLowsTask, normalMaxTask, normalMinTask,
                           porTask, extremesTask, hotDaysTask, ytdTask, warmNightsTask, precipTask, wxTask);

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
        vm.Current              = wxTask.Result;

        return View(vm);
    }
}
