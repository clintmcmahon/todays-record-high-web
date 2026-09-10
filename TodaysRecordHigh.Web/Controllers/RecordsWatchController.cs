using Microsoft.AspNetCore.Mvc;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Controllers;

public class RecordsWatchController : Controller
{
    private readonly IRecordsWatchService _watch;

    public RecordsWatchController(IRecordsWatchService watch)
    {
        _watch = watch;
    }

    [HttpGet("/records-today")]
    public IActionResult Today()
    {
        var snapshot = _watch.GetLatest();
        var today = DateTime.Now.ToString("MMMM d");

        ViewData["Title"] = $"Cities Near a Record High Today, {today} | Today's Record High";
        ViewData["MetaDescription"] = snapshot is { Notable.Count: > 0 }
            ? $"{snapshot.Notable.Count} U.S. cities are at, tying, or within 5°F of their all-time record high for {today}. Updated every 30 minutes from NOAA Threadex data and live conditions."
            : $"Live tracker: which U.S. cities are closest to their all-time record high for {today}. Updated every 30 minutes.";

        return View(snapshot);
    }
}
