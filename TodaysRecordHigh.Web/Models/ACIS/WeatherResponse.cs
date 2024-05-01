namespace TodaysRecordHigh.Web.Models.ACIS;

public class WeatherResponse
{
    public MetaData Meta { get; set; }
    public List<List<List<string>>> Smry { get; set; }
    public List<List<string>> Data { get; set; }
}
