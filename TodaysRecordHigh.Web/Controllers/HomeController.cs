using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using TodaysRecordHigh.Web.Models;
using TodaysRecordHigh.Web.Models.ViewModels;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWeatherDataService _weatherDataService;
    private readonly IWebHostEnvironment _env;

    public HomeController(ILogger<HomeController> logger, IWeatherDataService weatherDataService, IWebHostEnvironment env)
    {
        _logger = logger;
        _weatherDataService = weatherDataService;
        _env = env;
    }

    public async Task<IActionResult> Index(string? selectedState, string? selectedStationId, DateTime? selectedDate)
    {
        var model = new HomeViewModel();

        // If selectedState is null or empty, use the default
        if (string.IsNullOrEmpty(selectedState))
        {
            selectedState = "AL"; // Default state
        }
        else
        {
            if (string.IsNullOrEmpty(selectedStationId))
            {
                var stationsByState = _weatherDataService.GetStationDataByState(selectedState);
                var defaultStation = stationsByState.Stations.FirstOrDefault();
                selectedStationId = defaultStation.Sids[0];
                model.IsDefault = true;
            }
        }

        // If stationId is null or empty, use the default
        if (string.IsNullOrEmpty(selectedStationId))
        {
            selectedStationId = "BHMthr 9"; // Default station ID
            model.IsDefault = true;
        }

        // If selectedDate is null or empty, use today's date in the specified format
        if (!selectedDate.HasValue)
        {
            selectedDate = DateTime.Now; // Default to today's date
        }

        var year = DateTime.Now.Year;
        var recordsTask = _weatherDataService.GetRecords(selectedStationId, selectedDate.Value.ToString("MM-dd"), selectedDate.Value.ToString("MM-dd"));
        var normalsTask = _weatherDataService.GetNormals(selectedStationId, selectedDate.Value.ToString("yyyy-MM-dd"), selectedDate.Value.ToString("yyyy-MM-dd"));
        var monthNormalsObservedTask = _weatherDataService.GetMonthNormalObserved(selectedStationId, selectedDate.Value.ToString("yyyy-MM-dd"));
        var dailyHistoryTask = _weatherDataService.GetDailyHistory(selectedStationId, selectedDate.Value.ToString("MM-dd"));


        // Await tasks to complete
        await Task.WhenAll(recordsTask, normalsTask, monthNormalsObservedTask, dailyHistoryTask);

        // Retrieve the results of each task
        var records = await recordsTask;
        var normals = await normalsTask;
        var monthNormalsObserved = await monthNormalsObservedTask;
        var dailyHistory = await dailyHistoryTask;

        records.StationId = selectedStationId;
        records.SelectedState = selectedState;
        records.RecordDate = selectedDate;

        model.WeatherRecords = records;
        model.WeatherNormals = normals;
        model.MonthNormalObserved = monthNormalsObserved;
        model.DailyHistory = dailyHistory;

        // Construct the full path to the file
        var filePath = Path.Combine(_env.WebRootPath, "js", "stationData.json");

        // Read the file's contents
        var jsonData = System.IO.File.ReadAllText(filePath);

        // Deserialize the JSON into a dynamic object or a specific class if you have one
        var stationData = JsonSerializer.Deserialize<dynamic>(jsonData);
        model.StationData = stationData;

        var selectedStationState = _weatherDataService.GetStationDataByState(selectedState);
        model.SelectedStateName = selectedStationState.Name;
        model.SelectedStationName = selectedStationState.Stations.Where(x => x.Sids[0] == selectedStationId).FirstOrDefault().Name;
        return View(model);
    }

    public async Task<JsonResult> GetWeatherRecords(string selectedStationId, string startDate, string endDate)
    {
        // Ensure the parameters are not null or empty; if they are, use defaults or return an error
        if (string.IsNullOrEmpty(selectedStationId) || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
        {
            // Handle the error according to your application's requirements
            return Json(new { error = "Invalid parameters" });
        }

        // Call the service to get the records
        var records = await _weatherDataService.GetRecords(selectedStationId, startDate, endDate);

        // Check if the operation was successful and return the records
        if (records != null)
        {
            return Json(records);
        }
        else
        {
            // Handle the scenario when records are null or the operation was unsuccessful
            return Json(new { error = "No records found or an error occurred" });
        }
    }

    public async Task<JsonResult> GetWeatherNormals(string selectedStationId, string startDate, string endDate)
    {
        // Ensure the parameters are not null or empty; if they are, use defaults or return an error
        if (string.IsNullOrEmpty(selectedStationId) || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
        {
            // Handle the error according to your application's requirements
            return Json(new { error = "Invalid parameters" });
        }

        // Call the service to get the normalos
        var normals = await _weatherDataService.GetNormals(selectedStationId, $"{DateTime.Now.Year}-{startDate}", $"{DateTime.Now.Year}-{endDate}");

        // Check if the operation was successful and return the normals
        if (normals != null)
        {
            return Json(normals);
        }
        else
        {
            // Handle the scenario when normals are null or the operation was unsuccessful
            return Json(new { error = "No normals found or an error occurred" });
        }
    }

    public async Task<JsonResult> GetMonthlyNormalObserved(string selectedStationId, string startDate)
    {
        // Ensure the parameters are not null or empty; if they are, use defaults or return an error
        if (string.IsNullOrEmpty(selectedStationId) || string.IsNullOrEmpty(startDate))
        {
            // Handle the error according to your application's requirements
            return Json(new { error = "Invalid parameters" });
        }

        // Call the service to get the normalos
        var monthNormalObserved = await _weatherDataService.GetMonthNormalObserved(selectedStationId, startDate);

        // Check if the operation was successful and return the normals
        if (monthNormalObserved != null)
        {
            return Json(monthNormalObserved);
        }
        else
        {
            // Handle the scenario when normals are null or the operation was unsuccessful
            return Json(new { error = "No normals or observed found or an error occurred" });
        }
    }


    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
