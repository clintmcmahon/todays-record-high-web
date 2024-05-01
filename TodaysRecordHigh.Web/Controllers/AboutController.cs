using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using TodaysRecordHigh.Web.Models;
using TodaysRecordHigh.Web.Models.ViewModels;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Controllers;

public class AboutController : Controller
{
    private readonly ILogger<AboutController> _logger;
    private readonly IWeatherDataService _weatherDataService;
    private readonly IWebHostEnvironment _env;

    public AboutController(ILogger<AboutController> logger, IWeatherDataService weatherDataService, IWebHostEnvironment env)
    {
        _logger = logger;
        _weatherDataService = weatherDataService;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        return View();
    }

}
