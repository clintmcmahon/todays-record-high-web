using System.Net;
using TodaysRecordHigh.Web.Tests.Integration.TestSupport;

namespace TodaysRecordHigh.Web.Tests.Integration;

/// <summary>
/// ACIS is a third-party government service with no uptime guarantee, and every
/// station page fires 8-12 requests to it in parallel. One failing request must
/// degrade to an empty/null result for that one feature, never take down the whole
/// page. These tests hit the two failure modes PostStnDataAsync's catch block is
/// actually there for: a non-2xx response, and a 200 whose body isn't valid JSON
/// (a maintenance page, a truncated response, etc).
/// </summary>
public class ErrorHandlingTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CustomRangeReview_ServerError_ReturnsNullNotThrow(HttpStatusCode code)
    {
        var service = AcisServiceFactory.CreateWithHandler(FailingHttpHandler.ReturningStatus(code));

        var result = await service.GetCustomRangeReviewAsync("MSPthr", "06-15", "08-20");

        Assert.Null(result);
    }

    [Fact]
    public async Task CustomRangeReview_MalformedJson_ReturnsNullNotThrow()
    {
        var service = AcisServiceFactory.CreateWithHandler(
            FailingHttpHandler.ReturningMalformedJson("<html>502 Bad Gateway</html>"));

        var result = await service.GetCustomRangeReviewAsync("MSPthr", "06-15", "08-20");

        Assert.Null(result);
    }

    [Fact]
    public async Task RecordHighs_ServerError_ReturnsEmptyListNotThrow()
    {
        var service = AcisServiceFactory.CreateWithHandler(
            FailingHttpHandler.ReturningStatus(HttpStatusCode.InternalServerError));

        var result = await service.GetRecordHighsForDateAsync("MSPthr", "06-24");

        Assert.Empty(result);
    }

    [Fact]
    public async Task AllTimeExtremes_ServerError_ReturnsNullNotThrow()
    {
        var service = AcisServiceFactory.CreateWithHandler(
            FailingHttpHandler.ReturningStatus(HttpStatusCode.InternalServerError));

        var result = await service.GetAllTimeExtremesAsync("MSPthr");

        Assert.Null(result);
    }

    [Fact]
    public async Task PeriodOfRecord_ServerError_ReturnsNullNotThrow()
    {
        // GetPeriodOfRecordAsync hits StnMeta, a different endpoint with its own
        // try/catch — worth covering separately from the StnData path.
        var service = AcisServiceFactory.CreateWithHandler(
            FailingHttpHandler.ReturningStatus(HttpStatusCode.InternalServerError));

        var result = await service.GetPeriodOfRecordAsync("MSPthr");

        Assert.Null(result);
    }

    [Fact]
    public async Task DailyObs_MalformedJson_ReturnsEmptyList_AndDownstreamFeaturesDegradeGracefully()
    {
        var service = AcisServiceFactory.CreateWithHandler(
            FailingHttpHandler.ReturningMalformedJson("not json at all"));

        // GetHotDaysPerYearAsync, GetClimateRecordsAsync, and GetCustomRangeReviewAsync
        // all sit on top of the shared daily-obs fetch — none of them should throw when
        // it's empty, they should report "nothing to show" the same way a brand-new
        // station with no data yet would.
        var hotDays = await service.GetHotDaysPerYearAsync("MSPthr");
        var climateRecords = await service.GetClimateRecordsAsync("MSPthr");
        var customRange = await service.GetCustomRangeReviewAsync("MSPthr", "06-15", "08-20");

        Assert.Empty(hotDays);
        Assert.Null(climateRecords);
        Assert.Null(customRange);
    }
}
