namespace TodaysRecordHigh.Web.Models.ACIS;

public class Element
{
    public string Name { get; set; }
    public string Interval { get; set; }
    public string Duration { get; set; }
    public Summary Smry { get; set; }
    public int Smry_Only { get; set; }
    public List<object> Groupby { get; set; } = new List<object>();
    public string Normal { get; set; }
    public int Prec { get; set; }
}
