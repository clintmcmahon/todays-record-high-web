using TodaysRecordHigh.Web.Models;
using TodaysRecordHigh.Web.Tests.Integration.TestSupport;

namespace TodaysRecordHigh.Web.Tests.Integration;

/// <summary>
/// GetClimateRecordsAsync's first/last-date markers and growing-season extremes against
/// a real, gap-free 25-year daily record for Minneapolis-St Paul (2000-01-01 through
/// 2024-12-31, fetched 2026-09-06 — verified to contain zero missing maxt/mint values,
/// so the coverage gate never excludes a year and Python/C# should agree exactly).
///
/// Every expected value here was computed independently in a standalone Python script
/// against that same fixture file — re-implementing the earliest/latest/growing-season
/// search from scratch rather than reading it back out of the app — and is the "verify
/// against another dataset" check: two independent implementations of the same
/// algorithm over the same raw data must produce the same answer.
///
/// This 25-year window is deliberately smaller than the station's full ~150-year
/// record, so these numbers do NOT match the all-time records shown on the live site
/// (which draw on the complete history); they're internally consistent with this
/// fixture only, which is what makes them checkable without embedding a
/// multi-megabyte fixture.
/// </summary>
public class ClimateRecordsDataTests
{
    private static async Task<ClimateRecords> LoadAsync()
    {
        var (service, handler) = AcisServiceFactory.Create();
        handler.OnPath("StnData", Fixture.Load("msp_daily_2000_2024.json"));
        var records = await service.GetClimateRecordsAsync("MSPthr");
        Assert.NotNull(records);
        return records!;
    }

    private static SeasonMarker Marker(ClimateRecords records, string key) =>
        Assert.Single(records.Markers, m => m.Key == key);

    [Fact]
    public async Task FirstFallFreeze_EarliestAndLatestMatchIndependentComputation()
    {
        var records = await LoadAsync();
        var m = Marker(records, "freeze32");

        Assert.Equal(new DateTime(2000, 9, 24), m.Earliest!.Date);
        Assert.Equal(31, m.Earliest.Value);
        Assert.Equal(new DateTime(2016, 11, 18), m.Latest!.Date);
        Assert.Equal(31, m.Latest.Value);
        Assert.Equal(25, m.YearsOnRecord);
    }

    [Fact]
    public async Task FirstNinetyDegreeDayOfYear_EarliestAndLatestMatchIndependentComputation()
    {
        // The headline-grabbing fact from this dataset: MSP hit 91°F on April 15, 2002.
        // Independently confirmed against a live curl to ACIS during development, and
        // reconfirmed here by an unrelated from-scratch Python pass over the fixture.
        var records = await LoadAsync();
        var m = Marker(records, "first90");

        Assert.Equal(new DateTime(2002, 4, 15), m.Earliest!.Date);
        Assert.Equal(91, m.Earliest.Value);
        Assert.Equal(new DateTime(2024, 7, 14), m.Latest!.Date);
        Assert.Equal(90, m.Latest.Value);
    }

    [Fact]
    public async Task LastSpringFreeze_SearchWindowNeverPicksUpAFallFreeze()
    {
        // The regression this guards: if the Jan1-Jun30 window for "last spring freeze"
        // were accidentally widened to the full year, this would report a date in
        // October or November (the much-later fall freeze) instead of April/May.
        var records = await LoadAsync();
        var m = Marker(records, "lastFreeze32");

        Assert.Equal(new DateTime(2006, 4, 8), m.Earliest!.Date);
        Assert.Equal(new DateTime(2010, 5, 9), m.Latest!.Date);
        Assert.True(m.Latest.Date.Month <= 6, "a 'last spring freeze' in July+ means the window leaked into fall");
    }

    [Fact]
    public async Task LastNinetyDegreeDay_EarliestAndLatestMatchIndependentComputation()
    {
        var records = await LoadAsync();
        var m = Marker(records, "last90");

        Assert.Equal(new DateTime(2019, 7, 19), m.Earliest!.Date);
        Assert.Equal(new DateTime(2023, 10, 1), m.Latest!.Date);
    }

    [Fact]
    public async Task FirstMeasurableSnow_UsesInchesNotDegreesUnit()
    {
        var records = await LoadAsync();
        var m = Marker(records, "snow");

        Assert.Equal("\"", m.Unit);
        Assert.Equal(new DateTime(2009, 10, 10), m.Earliest!.Date);
        Assert.Equal(0.3, m.Earliest.Value, precision: 2);
        Assert.Equal(new DateTime(2004, 11, 27), m.Latest!.Date);
    }

    [Fact]
    public async Task GrowingSeason_LongestAndShortestMatchIndependentComputation()
    {
        var records = await LoadAsync();

        Assert.Equal((2016, 219), records.LongestGrowingSeason);
        Assert.Equal((2000, 155), records.ShortestGrowingSeason);
        Assert.NotNull(records.NormalGrowingSeasonDays);
        Assert.Equal(176.8, records.NormalGrowingSeasonDays!.Value, precision: 1);
    }

    [Fact]
    public async Task SpringAndFallMarkers_AreLabeledWithTheCorrectGroup()
    {
        var records = await LoadAsync();

        Assert.Equal("Spring", Marker(records, "lastFreeze32").Group);
        Assert.Equal("Spring", Marker(records, "first90").Group);
        Assert.Equal("Fall", Marker(records, "freeze32").Group);
        Assert.Equal("Fall", Marker(records, "last90").Group);
        Assert.Equal("Fall", Marker(records, "snow").Group);
    }
}
