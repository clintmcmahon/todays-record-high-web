using TodaysRecordHigh.Web.Tests.Integration.TestSupport;

namespace TodaysRecordHigh.Web.Tests.Integration;

/// <summary>
/// Parsing correctness for the simpler passthrough ACIS products, each checked against
/// a real captured response. Where a fixture value also appears elsewhere in this
/// project's development history (the live station page, or an earlier verification
/// curl), that's noted — most of these are single-source parsing checks: given this
/// exact JSON, does AcisService extract exactly what's in it.
/// </summary>
public class RecordAndNormalParsingTests
{
    [Fact]
    public async Task RecordHighs_ForJune24_ParsesTempDatePairsInDescendingOrder()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("msp_record_highs_0624.json"));

        var records = await service.GetRecordHighsForDateAsync("MSPthr", "06-24", topN: 10);

        Assert.Equal(10, records.Count);
        Assert.Equal((101, "1988-06-24"), records[0]);
        Assert.Equal((96, "1931-06-24"), records[1]);
        Assert.Equal((91, "1963-06-24"), records[^1]);
    }

    [Fact]
    public async Task RecordHighs_TopNTrimsTheParsedListToTheRequestedCount()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("msp_record_highs_0624.json"));

        var top3 = await service.GetRecordHighsForDateAsync("MSPthr", "06-24", topN: 3);

        Assert.Equal(3, top3.Count);
        Assert.Equal(101, top3[0].Temp);
        Assert.Equal(95, top3[2].Temp);
    }

    [Fact]
    public async Task NormalMaxTemp_ParsesTheSingleDataRow()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("msp_normal_maxt_0624.json"));

        var normal = await service.GetNormalMaxTempAsync("MSPthr", "06-24");

        Assert.Equal(81, normal);
    }

    [Fact]
    public async Task PeriodOfRecord_ParsesValidDaterangeFromStnMeta()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnMeta", Fixture.Load("msp_stnmeta_por.json"));

        var por = await service.GetPeriodOfRecordAsync("MSPthr");

        Assert.Equal(("1872-10-01", "2026-09-06"), por);
    }

    [Fact]
    public async Task AllTimeExtremes_ParsesAllFourElementsFromOneNoGroupBySmry()
    {
        // These four figures also appear on the live Records tab (screenshotted during
        // this project's development): 108°F Jul 14 1936, -41°F Jan 21 1888,
        // 9.15" Jul 23 1987, 18.5" Nov 1 1991.
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("msp_alltime_extremes.json"));

        var extremes = await service.GetAllTimeExtremesAsync("MSPthr");

        Assert.NotNull(extremes);
        Assert.Equal(108, extremes!.MaxTemp);
        Assert.Equal("1936-07-14", extremes.MaxTempDate);
        Assert.Equal(-41, extremes.MinTemp);
        Assert.Equal("1888-01-21", extremes.MinTempDate);
        Assert.Equal(9.15, extremes.MaxPcpn);
        Assert.Equal("1987-07-23", extremes.MaxPcpnDate);
        Assert.Equal(18.5, extremes.MaxSnow);
        Assert.Equal("1991-11-01", extremes.MaxSnowDate);
    }
}
