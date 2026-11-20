namespace TodaysRecordHigh.Web.Tests.Unit;

public class SiteClockTests
{
    [Fact]
    public void UtcEvening_StaysOnTheCentralCalendarDate()
    {
        // 1:00 AM UTC on Nov 21 is 7:00 PM CST on Nov 20; the old UTC server said "Nov 21".
        var central = SiteClock.ToCentral(new DateTime(2026, 11, 21, 1, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 11, 20, 19, 0, 0), central);
    }

    [Fact]
    public void Summer_UsesDaylightTime()
    {
        // CDT is UTC-5
        var central = SiteClock.ToCentral(new DateTime(2026, 7, 4, 3, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 7, 3, 22, 30, 0), central);
    }
}
