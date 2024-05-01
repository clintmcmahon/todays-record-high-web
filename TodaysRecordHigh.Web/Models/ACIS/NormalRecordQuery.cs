namespace TodaysRecordHigh.Web.Models.ACIS;

public class NormalRecordQuery
{
    public string Sid { get; set; }
    public List<NormalElement> Elems { get; set; } = new List<NormalElement>();
    public string SDate { get; set; }
    public string EDate { get; set; }
    public List<string> Meta { get; set; } = new List<string>();
}
