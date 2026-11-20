using TodaysRecordHigh.Web.Tests.Integration.TestSupport;

namespace TodaysRecordHigh.Web.Tests.Integration;

/// <summary>
/// GetHotDaysPerYearAsync against Denver's real 2024 daily record (Fixtures/
/// denver_daily_2024.json, fetched 2026-09-06). Expected count computed independently
/// via a one-line Python filter over the same fixture, and cross-checked earlier in
/// this project's development against a live curl to ACIS while building the Fall tab
/// (2024 first freeze Oct 25, last 90° Sep 29 — both present in this fixture).
/// </summary>
public class HotDaysDataTests
{
    [Fact]
    public async Task Denver2024_HotDayCount_MatchesIndependentPythonCount()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("denver_daily_2024.json"));

        var hotDays = await service.GetHotDaysPerYearAsync("DENthr", thresholdF: 90);

        var year2024 = Assert.Single(hotDays, y => y.Year == 2024);
        Assert.Equal(64, year2024.Count);
    }

    [Fact]
    public async Task Denver2024_HigherThreshold_CountsFewerDays()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("denver_daily_2024.json"));

        var days90 = await service.GetHotDaysPerYearAsync("DENthr", thresholdF: 90);
        var days100 = await service.GetHotDaysPerYearAsync("DENthr", thresholdF: 100);

        var count90 = days90.Single(y => y.Year == 2024).Count;
        var count100 = days100.Single(y => y.Year == 2024).Count;

        Assert.True(count100 < count90,
            $"a 100°F threshold ({count100} days) must count no more days than 90°F ({count90} days)");
    }
}
