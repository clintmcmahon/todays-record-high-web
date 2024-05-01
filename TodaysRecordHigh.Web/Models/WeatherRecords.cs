namespace TodaysRecordHigh.Web.Models;

public class WeatherRecords
{
    public DateTime? RecordDate { get; set; }
    public string? StationId { get; set; }
    public string? SelectedState { get; set; }
    public int? HighTemp { get; set; }
    public DateTime? HighDate { get; set; }
    public int? LowTemp { get; set; }
    public DateTime? LowDate { get; set; }
    public int? ColdHigh { get; set; }
    public DateTime? ColdDate { get; set; }
    public int? WarmLow { get; set; }
    public DateTime? WarmDate { get; set; }
    public double? MostSnow { get; set; }
    public DateTime? MostSnowDate { get; set; }
    public double? MostPrecip { get; set; }
    public DateTime? MostPrecipDate { get; set; }
}
