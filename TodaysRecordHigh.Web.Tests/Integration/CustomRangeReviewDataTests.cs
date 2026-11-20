using TodaysRecordHigh.Web.Tests.Integration.TestSupport;

namespace TodaysRecordHigh.Web.Tests.Integration;

/// <summary>
/// GetCustomRangeReviewAsync — the generalized "any date range" engine behind Month/Fall
/// in Review — against the same real, gap-free 25-year MSP daily fixture (2000-01-01
/// through 2024-12-31) used by ClimateRecordsDataTests. Expected values for the Aug 10 -
/// Sep 9 window were computed independently in a standalone Python script against that
/// same fixture (mean of (maxt+mint)/2 per day, summed pcpn, ranked by counting), not
/// read back out of the app.
/// </summary>
public class CustomRangeReviewDataTests
{
    private static async Task<TodaysRecordHigh.Web.Models.CustomRangeReview?> LoadAsync(
        string start = "08-10", string end = "09-09", int? year = 2024)
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("msp_daily_2000_2024.json"));
        return await service.GetCustomRangeReviewAsync("MSPthr", start, end, year);
    }

    [Fact]
    public async Task Temp_MatchesIndependentPythonComputation()
    {
        var review = await LoadAsync();

        Assert.NotNull(review);
        Assert.False(review!.IsInProgress);
        Assert.NotNull(review.Temp);
        Assert.Equal(70.37096774193549, review.Temp!.AvgTemp, precision: 6);
        Assert.Equal(15, review.Temp.RankWarmest);
        Assert.Equal(11, review.Temp.RankCoolest);
        Assert.Equal(25, review.Temp.TotalSeasons);
        Assert.Equal(70.59831029185868, review.Temp.Normal!.Value, precision: 6);
    }

    [Fact]
    public async Task Precip_MatchesIndependentPythonComputation()
    {
        var review = await LoadAsync();

        Assert.NotNull(review!.Precip);
        Assert.Equal(3.29, review.Precip!.Total, precision: 2);
        Assert.Equal(14, review.Precip.RankWettest);
        Assert.Equal(12, review.Precip.RankDriest);
        Assert.Equal(25, review.Precip.TotalSeasons);
        Assert.Equal(4.08, review.Precip.Normal!.Value, precision: 2);
    }

    [Fact]
    public async Task WarmestAndCoolestInRange_MatchIndependentComputation()
    {
        var review = await LoadAsync();

        var warmest = review!.TempHistory.OrderByDescending(t => t.AvgTemp).First();
        var coldest = review.TempHistory.OrderBy(t => t.AvgTemp).First();

        Assert.Equal(2013, warmest.Year);
        Assert.Equal(75.90322580645162, warmest.AvgTemp!.Value, precision: 6);
        Assert.Equal(2004, coldest.Year);
        Assert.Equal(66.24193548387096, coldest.AvgTemp!.Value, precision: 6);
    }

    [Fact]
    public async Task DayCount_MatchesTheInclusiveSpanOfTheRange()
    {
        var review = await LoadAsync();
        // Aug 10 through Sep 9 inclusive: 22 days left in August + 9 in September.
        Assert.Equal(31, review!.DayCount);
    }

    [Fact]
    public async Task RangeLabel_FormatsAsMonthDayDash()
    {
        var review = await LoadAsync();
        Assert.Equal("Aug 10 – Sep 9", review!.RangeLabel);
    }

    [Fact]
    public async Task ReversedRange_IsRejectedRatherThanMisinterpreted()
    {
        // start > end (e.g. a typo, or an attempt at a Dec-to-Jan wraparound this engine
        // doesn't support yet) must fail closed, not silently compute something wrong.
        var review = await LoadAsync(start: "09-09", end: "08-10");
        Assert.Null(review);
    }

    [Fact]
    public async Task MalformedMonthDay_ReturnsNullNotThrow()
    {
        var review = await LoadAsync(start: "13-40", end: "09-09");
        Assert.Null(review);
    }

    [Fact]
    public async Task RequestingAnUnavailableYear_FallsBackToTheSameDefaultAsNoYearAtAll()
    {
        var withUnavailableYear = await LoadAsync(year: 1700);
        var withNoYear = await LoadAsync(year: null);

        Assert.NotNull(withUnavailableYear);
        Assert.NotNull(withNoYear);
        Assert.Equal(withNoYear!.Year, withUnavailableYear!.Year);
        Assert.True(withUnavailableYear.IsLatest);
    }
}
