using Microsoft.AspNetCore.Mvc;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Controllers.Api;

[ApiController]
[Route("api/records")]
public class RecordsController : ControllerBase
{
    private readonly IStationService _stationService;
    private readonly IAcisService _acisService;
    private readonly ICurrentWeatherService _weather;

    public RecordsController(IStationService stationService, IAcisService acisService,
        ICurrentWeatherService weather)
    {
        _stationService = stationService;
        _acisService = acisService;
        _weather = weather;
    }

    // GET /api/records/today?slugs=denver-co,chicago-il,...
    [HttpGet("today")]
    public async Task<IActionResult> Today([FromQuery] string slugs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slugs))
            return BadRequest(new { error = "slugs parameter required" });

        var monthDay = DateTime.Now.ToString("MM-dd");

        var stationPairs = slugs
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(slug => (slug, station: _stationService.GetBySlug(slug)))
            .Where(x => x.station is not null)
            .ToList();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(12));

        var tasks = stationPairs.Select(async pair =>
        {
            var station = pair.station!;
            double? currentTemp = null;
            string? currentTempAsOf = null;

            try
            {
                // Kick off current conditions alongside ACIS calls when lat/lon available
                var wxTask = station.Latitude.HasValue && station.Longitude.HasValue
                    ? _weather.GetCurrentAsync(station.Latitude.Value, station.Longitude.Value)
                    : Task.FromResult<Services.CurrentConditions?>(null);

                var sid = station.ThreadexSid;
                if (string.IsNullOrEmpty(sid))
                {
                    var wx0 = await wxTask;
                    currentTemp = wx0?.TempF;
                    currentTempAsOf = wx0?.AsOf.ToString("h:mm tt");
                    return BuildResult(pair.slug, station.City, station.State,
                        null, null, null, currentTemp, currentTempAsOf, wx0?.TimezoneAbbr ?? "", wx0?.WeatherEmoji ?? "");
                }

                var recordsTask = _acisService.GetRecordHighsForDateAsync(sid, monthDay, topN: 1);
                var normalTask  = _acisService.GetNormalMaxTempAsync(sid, monthDay);
                await Task.WhenAll(recordsTask, normalTask, wxTask);

                var records = recordsTask.Result;
                var wx = wxTask.Result;
                currentTemp = wx?.TempF;
                currentTempAsOf = wx?.AsOf != default ? wx!.AsOf.ToString("h:mm tt") : null;
                var tzAbbr = wx?.TimezoneAbbr ?? "";
                var weatherEmoji = wx?.WeatherEmoji ?? "";

                return BuildResult(pair.slug, station.City, station.State,
                    records.Count > 0 ? records[0].Temp : null,
                    records.Count > 0 ? records[0].Date : null,
                    normalTask.Result,
                    currentTemp, currentTempAsOf, tzAbbr, weatherEmoji);
            }
            catch
            {
                return BuildResult(pair.slug, station.City, station.State,
                    null, null, null, null, null, "", "");
            }
        });

        var results = await Task.WhenAll(tasks);
        return Ok(results);
    }

    private static object BuildResult(string slug, string city, string state,
        int? recordTemp, string? recordDate, double? normalTemp,
        double? currentTemp, string? currentTempAsOf, string tzAbbr, string weatherEmoji) => new
    {
        slug,
        city,
        state,
        recordTemp,
        recordDate,
        normalTemp,
        currentTemp = currentTemp.HasValue ? Math.Round(currentTemp.Value, 1) : (double?)null,
        currentTempAsOf,
        tzAbbr,
        weatherEmoji
    };
}
