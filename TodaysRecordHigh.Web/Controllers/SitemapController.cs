using Microsoft.AspNetCore.Mvc;
using System.Text;
using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Controllers;

public class SitemapController : Controller
{
    private readonly IStationService _stationService;

    public SitemapController(IStationService stationService)
    {
        _stationService = stationService;
    }

    [HttpGet("/sitemap.xml")]
    public IActionResult Index()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var stations = _stationService.GetAll();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        // Home page
        sb.AppendLine($"""
  <url>
    <loc>{baseUrl}/</loc>
    <lastmod>{today}</lastmod>
    <changefreq>daily</changefreq>
    <priority>1.0</priority>
  </url>
""");

        // All Stations list
        sb.AppendLine($"""
  <url>
    <loc>{baseUrl}/stations</loc>
    <changefreq>monthly</changefreq>
    <priority>0.7</priority>
  </url>
""");

        // About
        sb.AppendLine($"""
  <url>
    <loc>{baseUrl}/about</loc>
    <changefreq>monthly</changefreq>
    <priority>0.5</priority>
  </url>
""");

        // One URL per station
        foreach (var station in stations)
        {
            sb.AppendLine($"""
  <url>
    <loc>{baseUrl}/stations/{station.Slug}</loc>
    <lastmod>{today}</lastmod>
    <changefreq>daily</changefreq>
    <priority>0.8</priority>
  </url>
""");
        }

        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}
