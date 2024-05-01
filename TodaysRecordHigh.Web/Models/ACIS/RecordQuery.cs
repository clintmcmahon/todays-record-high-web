namespace TodaysRecordHigh.Web.Models.ACIS;

public class RecordQuery
{
    public string Sid { get; set; }
    public List<Element> Elems { get; set; } = new List<Element>();
    public string SDate { get; set; }
    public string EDate { get; set; }
    public List<string> Meta { get; set; } = new List<string>();
}
