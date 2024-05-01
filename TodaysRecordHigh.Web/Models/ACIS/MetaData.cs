namespace TodaysRecordHigh.Web.Models.ACIS;

public class MetaData
{
    public string State { get; set; }
    public List<string> Sids { get; set; }
    public string Name { get; set; }
    public List<List<string>> ValidDaterange { get; set; }
}
