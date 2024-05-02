namespace TodaysRecordHigh.Web.Controllers;

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TodaysRecordHigh.Web.Models.ACIS;
using System.Xml.Linq;
using System.Web;
using System.Text;

public class SitemapController : Controller
{

    private readonly IWebHostEnvironment _env;

    public SitemapController(IWebHostEnvironment env)
    {
        _env = env;
    }

    public IActionResult Index()
    {
        // Path to the stationData.json file
        var filePath = Path.Combine(_env.WebRootPath, "js", "stationData.json");

        // Read the file's contents
        var jsonData = System.IO.File.ReadAllText(filePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var stationDataList = JsonSerializer.Deserialize<List<StationData>>(jsonData, options);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(ns + "urlset");

        // Add the root URL
        var rootUrl = new XElement(ns + "url",
            new XElement(ns + "loc", "https://www.todaysrecordhigh.com/"),
            new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-dd")),
            new XElement(ns + "changefreq", "daily"),
            new XElement(ns + "priority", "1.0"));
        urlset.Add(rootUrl);

        // Add the About page
        var aboutUrl = new XElement(ns + "url",
            new XElement(ns + "loc", "https://www.todaysrecordhigh.com/about"),
            new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-dd")),
            new XElement(ns + "changefreq", "monthly"),
            new XElement(ns + "priority", "0.8"));
        urlset.Add(aboutUrl);

        // Add URLs for each station
        foreach (var area in stationDataList)
        {
            foreach (var station in area.Stations.Where(x => x.Name.ToLower().EndsWith("area")))
            {
                var stationId = station.Sids[0];
                var url = new XElement(ns + "url",
                    new XElement(ns + "loc", $"https://www.todaysrecordhigh.com?selectedState={area.ShortCode}&selectedStationId={stationId}"),
                    new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "daily"),
                    new XElement(ns + "priority", "1.0")
                );
                urlset.Add(url);
            }
        }

        var sitemap = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            urlset
        );

        // Set the content type to 'application/xml'
        return Content(sitemap.ToString(), "application/xml", Encoding.UTF8);


    }
}
