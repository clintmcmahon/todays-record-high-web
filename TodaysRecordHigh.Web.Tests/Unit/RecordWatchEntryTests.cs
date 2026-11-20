using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Tests.Unit;

public class RecordWatchEntryTests
{
    private static RecordWatchEntry Make(double? current, double? forecast, int? record) =>
        new("denver-co", "Denver", "CO", current, forecast, record, "1990-09-10", 85, "☀️");

    // ── The core rule this whole model exists to enforce ───────────────────

    [Fact]
    public void Broken_RequiresAnActualObservation_ForecastAloneIsNotEnough()
    {
        // The forecast alone crosses the record, but nothing has actually been measured
        // yet — this must never report Broken. Regressing this is the exact bug the
        // model was rebuilt to fix: a page stating a record is broken hours before it's
        // true, off a prediction with a few degrees of same-day error.
        var forecastOnly = Make(current: 88, forecast: 102, record: 100);
        Assert.False(forecastOnly.IsBroken);
        Assert.NotEqual(RecordWatchStatus.Broken, forecastOnly.Status);
    }

    [Fact]
    public void Broken_TrueOnlyWhenTheObservedReadingItselfMeetsOrPassesTheRecord()
    {
        Assert.True(Make(current: 100, forecast: null, record: 100).IsBroken);  // tied
        Assert.True(Make(current: 102, forecast: null, record: 100).IsBroken);  // broken
        Assert.False(Make(current: 99, forecast: null, record: 100).IsBroken);
    }

    [Fact]
    public void OnTrack_RequiresForecastOverRecordAndNoConfirmedBreakYet()
    {
        var onTrack = Make(current: 88, forecast: 102, record: 100);
        Assert.True(onTrack.IsOnTrack);
        Assert.Equal(RecordWatchStatus.OnTrack, onTrack.Status);
    }

    [Fact]
    public void OnTrack_FalseOnceTheObservationItselfHasAlreadyBrokenIt()
    {
        // Once it's confirmed, it's Broken, not merely "on track" — no double-counting
        // the same entry across two tiers.
        var confirmed = Make(current: 103, forecast: 102, record: 100);
        Assert.False(confirmed.IsOnTrack);
        Assert.Equal(RecordWatchStatus.Broken, confirmed.Status);
    }

    [Theory]
    [InlineData(90.0, null, RecordWatchStatus.Normal)]  // 10° away, outside the 5° "close" band
    [InlineData(95.0, null, RecordWatchStatus.Close)]   // exactly 5° away — inclusive boundary
    [InlineData(100.0, null, RecordWatchStatus.Broken)] // tied, observed
    [InlineData(88.0, 102.0, RecordWatchStatus.OnTrack)]  // forecast projects a break, not observed yet
    [InlineData(88.0, 96.0, RecordWatchStatus.Close)]     // forecast close but not over; observed far
    public void Status_ClassifiesCorrectly(double? current, double? forecast, RecordWatchStatus expected)
    {
        Assert.Equal(expected, Make(current, forecast, record: 100).Status);
    }

    [Fact]
    public void Status_NormalWhenRecordIsMissing()
    {
        Assert.Equal(RecordWatchStatus.Normal, Make(current: 95, forecast: null, record: null).Status);
    }

    [Fact]
    public void ObservedMarginDisplay_OnlyPopulatedWhenActuallyBroken()
    {
        Assert.Equal("+2° over", Make(current: 102, forecast: null, record: 100).ObservedMarginDisplay);
        Assert.Null(Make(current: 88, forecast: 102, record: 100).ObservedMarginDisplay); // forecast-only: no claim
        Assert.Null(Make(current: 97, forecast: null, record: 100).ObservedMarginDisplay); // close but not broken
    }

    [Fact]
    public void ForecastMarginDisplay_ReadsAsAProjectionNotAFact()
    {
        var onTrack = Make(current: 88, forecast: 102, record: 100);
        Assert.Equal("forecast +2° over", onTrack.ForecastMarginDisplay);
    }

    [Fact]
    public void MarginDisplay_FallsBackToWhicheverSignalIsPresent()
    {
        Assert.Equal("3° away", Make(current: 97, forecast: null, record: 100).MarginDisplay);
        Assert.Equal("4° away", Make(current: null, forecast: 96, record: 100).MarginDisplay);
    }

    [Fact]
    public void MarginDisplay_TakesTheCloserSignalWhenBothArePresent()
    {
        // Currently far off (12° away) but the forecast shows real momentum (4° away) —
        // that momentum is exactly what should surface here, not get hidden behind a
        // stale "12° away" just because an observed value happens to exist.
        Assert.Equal("4° away", Make(current: 88, forecast: 96, record: 100).MarginDisplay);
    }

    [Fact]
    public void RecordYear_ParsedFromRecordDate()
    {
        Assert.Equal(1990, Make(current: 95, forecast: null, record: 100).RecordYear);
    }
}

public class RecordWatchSnapshotTests
{
    private static RecordWatchEntry Entry(string city, double? current, double? forecast, int record) =>
        new(city.ToLowerInvariant(), city, "CO", current, forecast, record, "1990-01-01", null, "");

    [Fact]
    public void Broken_OnlyContainsConfirmedEntries_SortedBiggestOverageFirst()
    {
        var snapshot = new RecordWatchSnapshot(
            DateTime.UtcNow, "09-10", StationsScanned: 4, StationsWithData: 4,
            Entries:
            [
                Entry("Tied", 100, null, 100),          // confirmed, margin 0
                Entry("BigBeat", 110, null, 100),        // confirmed, +10 over
                Entry("SmallBeat", 102, null, 100),      // confirmed, +2 over
                Entry("ForecastOnly", 88, 120, 100)      // NOT confirmed — must not appear here
            ]);

        var order = snapshot.Broken.Select(e => e.City).ToList();
        Assert.Equal(new[] { "BigBeat", "SmallBeat", "Tied" }, order);
        Assert.DoesNotContain("ForecastOnly", order);
    }

    [Fact]
    public void OnTrack_OnlyContainsForecastProjectedEntries_NeverOverlapsWithBroken()
    {
        var snapshot = new RecordWatchSnapshot(
            DateTime.UtcNow, "09-10", StationsScanned: 2, StationsWithData: 2,
            Entries:
            [
                Entry("Projected", 88, 105, 100),   // forecast over, not confirmed
                Entry("Confirmed", 101, 105, 100)   // already confirmed — belongs in Broken, not OnTrack
            ]);

        Assert.Equal(["Projected"], snapshot.OnTrack.Select(e => e.City).ToList());
        Assert.Equal(["Confirmed"], snapshot.Broken.Select(e => e.City).ToList());
    }

    [Fact]
    public void Notable_OrdersConfirmedBeforeProjectedBeforeCloseCalls()
    {
        var snapshot = new RecordWatchSnapshot(
            DateTime.UtcNow, "09-10", StationsScanned: 3, StationsWithData: 3,
            Entries:
            [
                Entry("CloseCall", 96, null, 100),   // 4° away — Close
                Entry("Projected", 88, 102, 100),    // forecast over — OnTrack
                Entry("Confirmed", 101, null, 100)   // observed over — Broken
            ]);

        var order = snapshot.Notable.Select(e => e.City).ToList();
        Assert.Equal(new[] { "Confirmed", "Projected", "CloseCall" }, order);
    }

    [Fact]
    public void Close_ExcludesEntriesOutsideTheFiveDegreeBand()
    {
        var snapshot = new RecordWatchSnapshot(
            DateTime.UtcNow, "09-10", StationsScanned: 2, StationsWithData: 2,
            Entries:
            [
                Entry("TooFar", 80, null, 100),  // 20° away — not notable
                Entry("Near", 96, null, 100)     // 4° away — Close
            ]);

        Assert.Equal(["Near"], snapshot.Close.Select(e => e.City).ToList());
        Assert.DoesNotContain(snapshot.Entries.Where(e => e.City == "TooFar"), e => e.Status != RecordWatchStatus.Normal);
    }
}
