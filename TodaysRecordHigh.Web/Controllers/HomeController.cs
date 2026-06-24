using Microsoft.AspNetCore.Mvc;
using TodaysRecordHigh.Web.Services;
using TodaysRecordHigh.Web.ViewModels;

namespace TodaysRecordHigh.Web.Controllers;

public class HomeController : Controller
{
    private static readonly string[] FeaturedSlugs =
    [
        "boston-ma",
        "new-york-central-park-ny",
        "philadelphia-pa",
        "miami-fl",
        "atlanta-ga",
        "charlotte-nc",
        "chicago-il",
        "detroit-mi",
        "minneapolis-st-paul-mn",
        "dallas-tx",
        "houston-tx",
        "new-orleans-la",
        "denver-co",
        "phoenix-az",
        "los-angeles-downtown-ca",
        "seattle-tacoma-wa",
        "salt-lake-city-ut",
        "las-vegas-nv"
    ];

    private readonly IStationService _stationService;
    private readonly IAcisService _acisService;
    private readonly IConfiguration _config;

    public HomeController(IStationService stationService, IAcisService acisService, IConfiguration config)
    {
        _stationService = stationService;
        _acisService = acisService;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Now;
        var monthDay = today.ToString("MM-dd");

        var featuredStations = FeaturedSlugs
            .Select(slug => _stationService.GetBySlug(slug))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        // Fetch all record highs and normals in parallel — same pattern as station detail page
        var recordTasks = featuredStations.Select(s =>
            !string.IsNullOrEmpty(s.ThreadexSid)
                ? _acisService.GetRecordHighsForDateAsync(s.ThreadexSid, monthDay, topN: 1)
                : Task.FromResult<List<(int Temp, string Date)>>([]));

        var normalTasks = featuredStations.Select(s =>
            !string.IsNullOrEmpty(s.ThreadexSid)
                ? _acisService.GetNormalMaxTempAsync(s.ThreadexSid, monthDay)
                : Task.FromResult<double?>(null));

        var allRecords = await Task.WhenAll(recordTasks);
        var allNormals = await Task.WhenAll(normalTasks);

        var featuredRecords = featuredStations.Select((s, i) =>
        {
            var recs = allRecords[i];
            return new FeaturedStationRecord(
                Station:    s,
                RecordTemp: recs.Count > 0 ? recs[0].Temp : null,
                RecordDate: recs.Count > 0 ? recs[0].Date : null,
                NormalTemp: allNormals[i]
            );
        }).ToList();

        var vm = new HomeViewModel
        {
            StationsJson            = _stationService.GetAllJson(),
            MapboxToken             = _config["MapboxToken"] ?? string.Empty,
            TodayLabel              = today.ToString("MMMM d"),
            FeaturedRecords         = featuredRecords,
            FeaturedSlugsQueryString = string.Join(",", featuredStations.Select(s => s.Slug))
        };

        ViewData["MetaDescription"] = $"Today is {vm.TodayLabel}. See today's all-time record high temperatures for major U.S. cities — Boston, New York, Chicago, Miami, Denver, Phoenix, Seattle, and more. Climate data since the 1800s via NOAA Threadex.";

        return View(vm);
    }

    [HttpGet("/about")]
    public IActionResult About()
    {
        ViewData["Title"] = "About";
        return View();
    }
}
