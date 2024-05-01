namespace TodaysRecordHigh.Web.Models;

public class WeatherHistoryAverageTemp
{
    public string? NormalDate { get; set; }
    public string? StationId { get; set; }
    public string? SelectedState { get; set; }
    public int? HighTemp { get; set; }
    public int? LowTemp { get; set; }
}
