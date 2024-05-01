using TodaysRecordHigh.Web.Models.ACIS;

namespace TodaysRecordHigh.Web.Models;

public class MonthNormalObserved
{
    public int? DaysAboveNormal { get; set; }
    public int? DaysBelowNormal { get; set; }
    public List<List<string>> Data { get; set; }
}
