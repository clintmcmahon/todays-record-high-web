using System.Text.Json;
using TodaysRecordHigh.Web.Tests.Integration.TestSupport;

namespace TodaysRecordHigh.Web.Tests.Integration;

/// <summary>
/// Asserts the exact JSON AcisService sends to RCC ACIS for each product. These are
/// the requests documented (and hard-won) in the project's ACIS knowledge notes —
/// e.g. monthly aggregates need interval [0,1] and duration "mly" as a sibling of
/// "reduce", not nested inside "smry" the way record-high queries are. A refactor
/// that silently changes one of these shapes would still compile and would fail
/// against the real API in a way these tests catch immediately, without touching
/// the network.
/// </summary>
public class RequestShapeTests
{
    private static JsonElement Body(FakeAcisHttpHandler handler, int index = 0) =>
        handler.Requests[index].ParamsJson();

    [Fact]
    public async Task RecordHighs_PostsSmryGroupByShapeToStnData()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", """{"smry":[[[["101","1988-06-24"]]]]}""");

        await service.GetRecordHighsForDateAsync("MSPthr", "06-24", topN: 10);

        var req = handler.Requests.Single();
        Assert.Equal("/StnData", req.PathAndQuery);
        var p = Body(handler);
        Assert.Equal("MSPthr", p.GetProperty("sid").GetString());
        Assert.Equal("por", p.GetProperty("sdate").GetString());
        Assert.Equal("por", p.GetProperty("edate").GetString());

        var elem = p.GetProperty("elems")[0];
        Assert.Equal("maxt", elem.GetProperty("name").GetString());
        Assert.Equal("dly", elem.GetProperty("interval").GetString());
        Assert.Equal("dly", elem.GetProperty("duration").GetString());
        Assert.Equal(1, elem.GetProperty("smry_only").GetInt32());
        Assert.Equal("max", elem.GetProperty("smry").GetProperty("reduce").GetString());
        Assert.Equal("date", elem.GetProperty("smry").GetProperty("add").GetString());
        // acisN is max(topN, 5): asking for 10 should request 10, not the floor of 5.
        Assert.Equal(10, elem.GetProperty("smry").GetProperty("n").GetInt32());

        var groupBy = elem.GetProperty("groupby");
        Assert.Equal("year", groupBy[0].GetString());
        Assert.Equal("06-24", groupBy[1].GetString());
        Assert.Equal("06-24", groupBy[2].GetString());
    }

    [Fact]
    public async Task RecordHighs_RequestsAtLeastFiveEvenWhenTopNIsSmaller()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", """{"smry":[[[["101","1988-06-24"]]]]}""");

        await service.GetRecordHighsForDateAsync("MSPthr", "06-24", topN: 2);

        var n = Body(handler).GetProperty("elems")[0].GetProperty("smry").GetProperty("n").GetInt32();
        Assert.Equal(5, n);
    }

    [Fact]
    public async Task RecordLows_UsesMintAndMinReduce()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", """{"smry":[[[["-41","1888-01-21"]]]]}""");

        await service.GetRecordLowsForDateAsync("MSPthr", "01-21", topN: 5);

        var elem = Body(handler).GetProperty("elems")[0];
        Assert.Equal("mint", elem.GetProperty("name").GetString());
        Assert.Equal("min", elem.GetProperty("smry").GetProperty("reduce").GetString());
    }

    [Fact]
    public async Task NormalMaxTemp_QueriesSingleDateWithNormalFlag_NotPeriodOfRecord()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", """{"data":[["2026-06-24","81"]]}""");

        await service.GetNormalMaxTempAsync("MSPthr", "06-24");

        var p = Body(handler);
        // Normals are a single-date query, not "por" — a regression that changed this
        // to "por" would silently start averaging every June 24 instead of reading
        // the precomputed 1991–2020 normal ACIS returns for that one date.
        Assert.EndsWith("-06-24", p.GetProperty("sdate").GetString());
        Assert.Equal(p.GetProperty("sdate").GetString(), p.GetProperty("edate").GetString());

        var elem = p.GetProperty("elems")[0];
        Assert.Equal("maxt", elem.GetProperty("name").GetString());
        Assert.Equal("1", elem.GetProperty("normal").GetString());
    }

    [Fact]
    public async Task PeriodOfRecord_PostsToStnMetaNotStnData()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnMeta", """{"meta":[{"valid_daterange":[["1872-10-01","2026-09-06"]]}]}""");

        var por = await service.GetPeriodOfRecordAsync("MSPthr");

        var req = handler.Requests.Single();
        Assert.Equal("/StnMeta", req.PathAndQuery);
        var p = Body(handler);
        Assert.Equal("MSPthr", p.GetProperty("sids").GetString()); // plural "sids", not "sid"
        Assert.Equal(("1872-10-01", "2026-09-06"), por);
    }

    [Fact]
    public async Task AllTimeExtremes_RequestsFourElementsInOneCall()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", """
            {"smry":[[["108","1936-07-14"]],[["-41","1888-01-21"]],[["9.15","1987-07-23"]],[["18.5","1991-11-01"]]]}
            """);

        await service.GetAllTimeExtremesAsync("MSPthr");

        // One round trip for all four extremes, not four separate requests.
        Assert.Single(handler.Requests);
        var elems = Body(handler).GetProperty("elems");
        Assert.Equal(4, elems.GetArrayLength());
        Assert.Equal(new[] { "maxt", "mint", "pcpn", "snow" },
            elems.EnumerateArray().Select(e => e.GetProperty("name").GetString() ?? "").ToArray());
    }

    [Fact]
    public async Task DailyObs_RequestsMaxtMintSnowFromPeriodOfRecord_NoIntervalOrDuration()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", """{"data":[["2024-10-25","55","31","0.0","0.02"]]}""");

        // GetHotDaysPerYearAsync is one of several callers of the shared daily-obs fetch.
        await service.GetHotDaysPerYearAsync("MSPthr");

        var p = Body(handler);
        Assert.Equal("por", p.GetProperty("sdate").GetString());
        var elems = p.GetProperty("elems");
        Assert.Equal(new[] { "maxt", "mint", "snow", "pcpn" },
            elems.EnumerateArray().Select(e => e.GetProperty("name").GetString() ?? "").ToArray());
        // Plain daily values, not a monthly/seasonal reduction.
        Assert.False(elems[0].TryGetProperty("interval", out _));
        Assert.False(elems[0].TryGetProperty("duration", out _));
    }

    [Fact]
    public async Task DailyObs_ReusesCacheAcrossHotDaysClimateRecordsAndCustomRangeCalls()
    {
        // GetDailyObsAsync backs GetHotDaysPerYearAsync, GetClimateRecordsAsync, and
        // GetCustomRangeReviewAsync. If the cache key or TTL logic regresses, a station
        // page would fire this multi-hundred-KB request three times per load instead
        // of once.
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", """{"data":[["2024-10-25","55","31","0.0","0.02"]]}""");

        await service.GetHotDaysPerYearAsync("MSPthr");
        await service.GetClimateRecordsAsync("MSPthr");
        await service.GetCustomRangeReviewAsync("MSPthr", "06-15", "08-20");

        Assert.Single(handler.Requests);
    }
}
