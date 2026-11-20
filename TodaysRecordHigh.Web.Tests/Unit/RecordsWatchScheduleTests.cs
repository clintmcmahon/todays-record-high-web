using TodaysRecordHigh.Web.Services;

namespace TodaysRecordHigh.Web.Tests.Unit;

// November dates, so Central is UTC-6 (CST).
public class RecordsWatchScheduleTests
{
    private static DateTime Utc(int day, int hour, int minute = 0) =>
        new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void MidAfternoon_ScansAgainInOneHour()
    {
        // 2:00 PM CT
        Assert.Equal(TimeSpan.FromHours(1), RecordsWatchService.DelayUntilNextScan(Utc(20, 20)));
    }

    [Fact]
    public void AfterLastScanOfTheDay_SleepsUntil9amCentralTomorrow()
    {
        // 11:05 PM CT on the 20th → next scan 9:00 AM CT on the 21st (15:00 UTC)
        var now = Utc(21, 5, 5);
        Assert.Equal(Utc(21, 15) - now, RecordsWatchService.DelayUntilNextScan(now));
    }

    [Fact]
    public void Overnight_SleepsUntil9amCentralSameDay()
    {
        // 3:00 AM CT → 9:00 AM CT
        Assert.Equal(TimeSpan.FromHours(6), RecordsWatchService.DelayUntilNextScan(Utc(20, 9)));
    }

    [Fact]
    public void EarlyMorningJustBeforeWindow_ScansAtTopOfWindowOrSooner()
    {
        // 8:30 AM CT → next hour lands at 9:30, inside the window
        Assert.Equal(TimeSpan.FromHours(1), RecordsWatchService.DelayUntilNextScan(Utc(20, 14, 30)));
    }

    [Fact]
    public void DailyScanCount_StaysWellUnderOpenMeteoQuota()
    {
        // Walk one full day from midnight CT, counting scans the schedule allows.
        var t = Utc(20, 6);
        var end = t.AddDays(1);
        var scans = 0;
        while (t < end)
        {
            t += RecordsWatchService.DelayUntilNextScan(t);
            if (t < end) scans++;
        }
        Assert.InRange(scans * 415, 1, 7_000);
    }
}

public class OpenMeteoBackoffTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 20, 37, 0, DateTimeKind.Utc);

    [Fact]
    public void Daily_BlocksUntilMidnightUtc() =>
        Assert.Equal(new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc),
            CurrentWeatherService.BackoffUntil("""{"error":true,"reason":"Daily API request limit exceeded. Please try again tomorrow."}""", Now));

    [Fact]
    public void Hourly_BlocksUntilTopOfNextHour() =>
        Assert.Equal(new DateTime(2026, 11, 20, 21, 0, 0, DateTimeKind.Utc),
            CurrentWeatherService.BackoffUntil("""{"error":true,"reason":"Hourly API request limit exceeded. Please try again in the next hour."}""", Now));

    [Fact]
    public void Minutely_BlocksOneMinute() =>
        Assert.Equal(Now.AddMinutes(1),
            CurrentWeatherService.BackoffUntil("""{"error":true,"reason":"Minutely API request limit exceeded. Please try again in one minute."}""", Now));

    [Fact]
    public void Concurrency_DoesNotBlock() =>
        Assert.Null(CurrentWeatherService.BackoffUntil("""{"error":true,"reason":"Too many concurrent requests"}""", Now));
}
