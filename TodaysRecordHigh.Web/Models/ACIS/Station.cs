namespace TodaysRecordHigh.Web.Models.ACIS;

public class Station
{
    public string Name { get; set; }
    public List<double> Ll { get; set; }
    public List<string> Sids { get; set; }
    public string State { get; set; }
    public double? Elev { get; set; }
    public int? Uid { get; set; }
}
