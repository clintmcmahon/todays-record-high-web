using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using TodaysRecordHigh.Web.Models;
using TodaysRecordHigh.Web.Models.ACIS;

namespace TodaysRecordHigh.Web.Services;

public class WeatherDataService : IWeatherDataService
{
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _env;
    private const string DATA_URL = "https://data.rcc-acis.org/StnData";
    private const String META_URL = "https://data.rcc-acis.org/StnMeta";
    public WeatherDataService(IWebHostEnvironment env)
    {
        _httpClient = new HttpClient();
        _env = env;
    }

    public async Task<WeatherRecords> GetRecords(string selectedStation, string startDate, string endDate)
    {
        var recordsQuery = new
        {
            sid = selectedStation,
            elems = new[]
     {
        new
        {
            name = "maxt",
            interval = "dly",
            duration = "dly",
            smry = new { reduce = "max", add = "date" },
            smry_only = 1,
            groupby = new[] { "year", startDate, endDate }
        },
        new
        {
            name = "mint",
            interval = "dly",
            duration = "dly",
            smry = new { reduce = "min", add = "date" },
            smry_only = 1,
            groupby = new[] { "year", startDate, endDate }
        },
        new
        {
            name = "maxt",
            interval = "dly",
            duration = "dly",
            smry = new { reduce = "min", add = "date" },
            smry_only = 1,
            groupby = new[] { "year", startDate, endDate }
        },
        new
        {
            name = "mint",
            interval = "dly",
            duration = "dly",
            smry = new { reduce = "max", add = "date" },
            smry_only = 1,
            groupby = new[] { "year", startDate, endDate }
        },
        new
        {
            name = "snow",
            interval = "dly",
            duration = "dly",
            smry = new { reduce = "max", add = "date" },
            smry_only = 1,
            groupby = new[] { "year", startDate, endDate }
        },
        new
        {
            name = "pcpn",
            interval = "dly",
            duration = "dly",
            smry = new { reduce = "max", add = "date" },
            smry_only = 1,
            groupby = new[] { "year", startDate, endDate }
        }
    },
            sDate = "por",
            eDate = "por",
            meta = new[] { "name", "state", "valid_daterange", "sids" }
        };

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var content = new StringContent(JsonSerializer.Serialize(recordsQuery, serializerOptions), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(DATA_URL, content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var jsonData = JsonSerializer.Deserialize<WeatherResponse>(json, serializerOptions);

        var weatherRecords = new WeatherRecords();

        if (jsonData?.Smry.Count >= 6)
        {
            weatherRecords.HighTemp = ParseInt(jsonData.Smry[0][0][0]);
            weatherRecords.HighDate = ParseDate(jsonData.Smry[0][0][1]);
            weatherRecords.LowTemp = ParseInt(jsonData.Smry[1][0][0]);
            weatherRecords.LowDate = ParseDate(jsonData.Smry[1][0][1]);
            weatherRecords.ColdHigh = ParseInt(jsonData.Smry[2][0][0]);
            weatherRecords.ColdDate = ParseDate(jsonData.Smry[2][0][1]);
            weatherRecords.WarmLow = ParseInt(jsonData.Smry[3][0][0]);
            weatherRecords.WarmDate = ParseDate(jsonData.Smry[3][0][1]);
            weatherRecords.MostSnow = ParseDouble(jsonData.Smry[4][0][0]);
            weatherRecords.MostSnowDate = ParseDate(jsonData.Smry[4][0][1]);
            weatherRecords.MostPrecip = ParseDouble(jsonData.Smry[5][0][0]);
            weatherRecords.MostPrecipDate = ParseDate(jsonData.Smry[5][0][1]);
        }

        return weatherRecords;
    }

    public async Task<WeatherNormals> GetNormals(string selectedStation, string startDate, string endDate)
    {
        var recordsQuery = new NormalRecordQuery
        {
            Sid = selectedStation,
            Elems = new List<NormalElement>
            {
                new NormalElement
                {
                    Name = "maxt",
                    Interval = "dly",
                    Duration = "dly",
                    Normal = "1",
                    Prec = 0
                },
                new NormalElement
                {
                    Name = "mint",
                    Interval = "dly",
                    Duration = "dly",
                    Normal = "1",
                    Prec = 0
                }
            },
            SDate = startDate,
            EDate = endDate
        };

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var content = new StringContent(JsonSerializer.Serialize(recordsQuery, serializerOptions), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(DATA_URL, content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var jsonData = JsonSerializer.Deserialize<WeatherResponse>(json, serializerOptions);

        var weatherNormals = new WeatherNormals();

        if (jsonData?.Data.Count >= 0)
        {
            weatherNormals.HighTemp = ParseInt(jsonData.Data[0][1]);
            weatherNormals.LowTemp = ParseInt(jsonData.Data[0][2]);
        }

        return weatherNormals;
    }

    public async Task<MonthNormalObserved> GetMonthNormalObserved(string selectedStation, string selectedDate)
    {

        var startDate = DateTime.Parse(selectedDate).AddDays(-30);
        var query = new
        {
            sid = selectedStation,
            elems = new dynamic[]
            {
                new { Name = "maxt" },
                new { Name = "mint" },
                new { Name = "maxt", Duration = "dly", Normal = "91", Prec = 1 },
                new { Name = "mint", Duration = "dly", Normal = "91", Prec = 1 }
            },
            sDate = $"{startDate:yyyy-MM-dd}",
            eDate = $"{selectedDate:yyyy-MM-dd}"
        };

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var content = new StringContent(JsonSerializer.Serialize(query, serializerOptions), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(DATA_URL, content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var jsonData = JsonSerializer.Deserialize<WeatherResponse>(json, serializerOptions);

        int daysAboveNormalHigh = 0;
        int daysHighBelowNormalHigh = 0;

        foreach (var record in jsonData.Data)
        {
            // Attempt to parse temperatures and the date
            string date = record[0];
            float highTemp, normalHigh;

            bool isHighTempValid = float.TryParse(record[1], out highTemp);
            bool isNormalHighValid = float.TryParse(record[3], out normalHigh);

            if (isHighTempValid && isNormalHighValid)
            {
                // Check if the high temperature is above the normal high
                if (highTemp > normalHigh)
                {
                    daysAboveNormalHigh++;
                }

                // Check if the high temperature is below the normal high
                if (highTemp < normalHigh)
                {
                    daysHighBelowNormalHigh++;
                }
            }
            else
            {
                // Handle the error, log it or decide how to proceed if the data is not valid
                Console.WriteLine($"Invalid data for date {date}. High Temp: {record[1]}, Normal High: {record[3]}");
            }
        }

        return new MonthNormalObserved()
        {
            DaysAboveNormal = daysAboveNormalHigh,
            DaysBelowNormal = daysHighBelowNormalHigh,
            Data = jsonData.Data
        };

    }



    public StationData GetStationDataByState(string selectedState)
    {
        // Path to the stationData.json file
        var filePath = Path.Combine(_env.WebRootPath, "js", "stationData.json");

        try
        {
            // Read the file's contents
            var jsonData = File.ReadAllText(filePath);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var stationDataList = JsonSerializer.Deserialize<List<StationData>>(jsonData, options);

            // Find the first matching object
            var matchingData = stationDataList.FirstOrDefault(sd =>
                sd.ShortCode.Equals(selectedState, StringComparison.OrdinalIgnoreCase) &&
                sd.Stations.Any(st => st.Name.EndsWith(" Area", StringComparison.OrdinalIgnoreCase)));

            return matchingData;
        }
        catch (Exception ex)
        {
            // Handle or log the exception as needed
            Console.WriteLine("An error occurred: " + ex.Message);
        }

        return null; // Return null if no match found or an error occurs
    }

    private int? ParseInt(string value)
    {
        if (int.TryParse(value, out int result))
        {
            return result;
        }
        return null;
    }

    private double? ParseDouble(string value)
    {
        if (double.TryParse(value, out double result))
        {
            return result;
        }
        return null;
    }

    private DateTime? ParseDate(string value)
    {
        if (DateTime.TryParse(value, out DateTime result))
        {
            return result;
        }
        return null;
    }
}

public interface IWeatherDataService
{
    Task<WeatherRecords> GetRecords(string selectedStation, string startDate, string endDate);
    Task<WeatherNormals> GetNormals(string selectedStation, string startDate, string endDate);
    Task<MonthNormalObserved> GetMonthNormalObserved(string selectedStation, string startDate);
    StationData GetStationDataByState(string selectedState);

}
