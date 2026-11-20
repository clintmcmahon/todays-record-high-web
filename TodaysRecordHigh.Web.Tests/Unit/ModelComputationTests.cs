using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Tests.Unit;

public class RankFormatTests
{
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]   // the 11-13 teens exception is the classic ordinal bug
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    [InlineData(101, "101st")]
    [InlineData(111, "111th")]  // 111 ends in 11 -> "th", not "1st"
    public void Ordinal_HandlesTeensException(int n, string expected)
    {
        Assert.Equal(expected, RankFormat.Ordinal(n));
    }

    [Theory]
    [InlineData(1, "year")]
    [InlineData(2, "years")]
    [InlineData(0, "years")]
    public void Pluralize_SingularOnlyAtExactlyOne(int n, string expected)
    {
        Assert.Equal(expected, RankFormat.Pluralize(n, "year"));
    }
}

public class TrendTests
{
    private static Trend Fit(params (int Year, double Value)[] points)
    {
        var n = points.Length;
        var meanX = points.Average(p => (double)p.Year);
        var meanY = points.Average(p => p.Value);
        double sxy = 0, sxx = 0;
        foreach (var (x, y) in points)
        {
            var dx = x - meanX;
            sxy += dx * (y - meanY);
            sxx += dx * dx;
        }
        var slope = sxy / sxx;
        return new Trend(slope, meanY - slope * meanX, points[0].Year, points[^1].Year, n);
    }

    [Fact]
    public void PerDecade_IsTenTimesPerYear()
    {
        var t = new Trend(PerYear: 0.05, Intercept: 0, FirstYear: 2000, LastYear: 2020, Count: 21);
        Assert.Equal(0.5, t.PerDecade, precision: 10);
    }

    [Fact]
    public void TotalChange_SpansFirstToLastYear()
    {
        var t = new Trend(PerYear: 0.1, Intercept: 0, FirstYear: 2000, LastYear: 2020, Count: 21);
        Assert.Equal(2.0, t.TotalChange, precision: 10);
    }

    [Fact]
    public void PerDecadeDisplay_AddsExplicitPlusSign()
    {
        var t = new Trend(PerYear: 0.02, Intercept: 0, FirstYear: 2000, LastYear: 2020, Count: 21);
        Assert.Equal("+0.20", t.PerDecadeDisplay());
    }

    [Fact]
    public void PerDecadeDisplay_NegativeKeepsMinusOnce()
    {
        var t = new Trend(PerYear: -0.02, Intercept: 0, FirstYear: 2000, LastYear: 2020, Count: 21);
        Assert.Equal("-0.20", t.PerDecadeDisplay());
    }

    [Fact]
    public void PerDecadeDisplay_TinyNegativeSlopeDoesNotPrintNegativeZero()
    {
        // A slope so small it rounds to 0.00 must never render as "-0.00" — that
        // reads as a bug to anyone comparing two "flat" stations side by side.
        var t = new Trend(PerYear: -0.0001, Intercept: 0, FirstYear: 2000, LastYear: 2020, Count: 21);
        Assert.Equal("0.00", t.PerDecadeDisplay());
    }

    [Fact]
    public void IsFlat_TrueOnlyBelowEpsilon()
    {
        var flat = new Trend(PerYear: 0.001, Intercept: 0, FirstYear: 2000, LastYear: 2020, Count: 21);
        var steep = new Trend(PerYear: 0.05, Intercept: 0, FirstYear: 2000, LastYear: 2020, Count: 21);
        Assert.True(flat.IsFlat(0.05));   // 0.01 per decade < 0.05 epsilon
        Assert.False(steep.IsFlat(0.05)); // 0.5 per decade > 0.05 epsilon
    }

    [Fact]
    public void At_ReproducesFittedLineForKnownData()
    {
        // A perfect line: value = 2*year - 3980 (so 2000 -> 20, 2020 -> 60).
        var t = Fit((2000, 20), (2010, 40), (2020, 60));
        Assert.Equal(20.0, t.At(2000), precision: 6);
        Assert.Equal(60.0, t.At(2020), precision: 6);
        Assert.Equal(2.0, t.PerYear, precision: 6);
    }
}

public class DateEventTests
{
    [Fact]
    public void Doy_MatchesCalendarDayOfYear()
    {
        var e = new DateEvent(2024, new DateTime(2024, 2, 1), 32);
        Assert.Equal(32, e.Doy); // Jan (31) + Feb 1
    }

    [Fact]
    public void Display_IsMonthAndDayOnly()
    {
        var e = new DateEvent(2024, new DateTime(2024, 10, 25), 32);
        Assert.Equal("Oct 25", e.Display);
        Assert.Equal("October 25, 2024", e.LongDisplay);
    }

    [Theory]
    [InlineData(0.4, "0.4")]
    [InlineData(1.0, "1")]
    [InlineData(90.0, "90")]
    public void ValueDisplay_DropsTrailingZerosButKeepsSignificantDigits(double value, string expected)
    {
        var e = new DateEvent(2024, new DateTime(2024, 1, 1), value);
        Assert.Equal(expected, e.ValueDisplay);
    }
}

public class SeasonMarkerTests
{
    private static SeasonMarker MakeMarker(DateEvent? current, double? normalDoy, bool provisional = false) =>
        new(
            Key: "test", Label: "Test Marker", Note: "note", Group: "Fall", Unit: "°F",
            Current: current, NormalDoy: normalDoy, Earliest: current, Latest: current,
            DoyTrend: null, History: current is null ? [] : [current], YearsOnRecord: 1,
            IsProvisional: provisional);

    [Fact]
    public void VsNormalDisplay_ExactMatchReadsOnTime()
    {
        var current = new DateEvent(2024, new DateTime(2024, 10, 14), 32); // doy 288
        var marker = MakeMarker(current, normalDoy: 288);
        Assert.Equal("on time", marker.VsNormalDisplay);
    }

    [Fact]
    public void VsNormalDisplay_LaterUsesSingularDayAndLaterWord()
    {
        var current = new DateEvent(2024, new DateTime(2024, 10, 15), 32); // doy 289, 1 day late
        var marker = MakeMarker(current, normalDoy: 288);
        Assert.Equal("1 day later", marker.VsNormalDisplay);
    }

    [Fact]
    public void VsNormalDisplay_EarlierUsesPluralAndEarlierWord()
    {
        var current = new DateEvent(2024, new DateTime(2024, 10, 12), 32); // doy 286, 2 days early
        var marker = MakeMarker(current, normalDoy: 288);
        Assert.Equal("2 days earlier", marker.VsNormalDisplay);
    }

    [Fact]
    public void ProvisionalMarker_SuppressesVsNormalEvenWithCurrentAndNormalPresent()
    {
        // A "last 90° so far" for the in-progress year cannot be honestly compared to
        // the normal date yet — the year might still produce a later one. Regressing
        // this would make an in-progress season look falsely early or late.
        var current = new DateEvent(2026, new DateTime(2026, 9, 1), 91);
        var marker = MakeMarker(current, normalDoy: 240, provisional: true);
        Assert.Null(marker.DaysVsNormal);
        Assert.Null(marker.VsNormalDisplay);
    }

    [Fact]
    public void HasHappened_FalseWhenCurrentIsNull()
    {
        var marker = MakeMarker(current: null, normalDoy: 288);
        Assert.False(marker.HasHappened);
        Assert.Null(marker.VsNormalDisplay);
    }

    [Fact]
    public void NormalDisplay_RendersDayOfYearAsMonthDay()
    {
        var marker = MakeMarker(current: null, normalDoy: 288); // Oct 15 in a non-leap reference year
        Assert.Equal("Oct 15", marker.NormalDisplay);
    }
}

public class GrowingSeasonTests
{
    [Fact]
    public void VsNormalDisplay_LongerSeasonReadsLonger()
    {
        var gs = new GrowingSeason(
            LastSpringFreeze: null, FirstFallFreeze: null,
            Days: 200, NormalDays: 172, DaysTrend: null,
            History: [], Longest: null, Shortest: null);

        Assert.Equal(28, gs.DaysVsNormal);
        Assert.Equal("28 days longer than normal", gs.VsNormalDisplay);
    }

    [Fact]
    public void VsNormalDisplay_ShorterSeasonReadsShorter()
    {
        var gs = new GrowingSeason(
            LastSpringFreeze: null, FirstFallFreeze: null,
            Days: 150, NormalDays: 172, DaysTrend: null,
            History: [], Longest: null, Shortest: null);

        Assert.Equal(-22, gs.DaysVsNormal);
        Assert.Equal("22 days shorter than normal", gs.VsNormalDisplay);
    }

    [Fact]
    public void VsNormalDisplay_NullWhenEitherSideMissing()
    {
        var gs = new GrowingSeason(null, null, Days: null, NormalDays: 172, DaysTrend: null,
            History: [], Longest: null, Shortest: null);
        Assert.Null(gs.VsNormalDisplay);
    }
}

public class RangePrecipSummaryTests
{
    private static RangePrecipSummary Make(double total, double? normal, int rankWettest, int rankDriest) =>
        new(Total: total, Normal: normal,
            RankWettest: rankWettest, RankDriest: rankDriest,
            TotalSeasons: 156, Wettest: [], Driest: []);

    [Fact]
    public void RankSuperlative_PicksTheEndTheMonthActuallySitsNear()
    {
        // 4th-driest of 156 is a much more notable rank than 152nd-wettest — the same
        // month, described from its more informative end.
        var dry = Make(total: 0.46, normal: 4.33, rankWettest: 152, rankDriest: 4);
        Assert.Equal("driest", dry.RankSuperlative);
        Assert.Equal("4th", dry.RankOrdinal);

        var wet = Make(total: 8.64, normal: 4.55, rankWettest: 13, rankDriest: 146);
        Assert.Equal("wettest", wet.RankSuperlative);
        Assert.Equal("13th", wet.RankOrdinal);
    }

    [Fact]
    public void PercentOfNormal_RoundsToNearestWholePercent()
    {
        var m = Make(total: 0.46, normal: 4.33, rankWettest: 152, rankDriest: 4);
        Assert.Equal(11, m.PercentOfNormal); // 0.46/4.33 = 10.62% -> rounds to 11
    }

    [Fact]
    public void PercentOfNormal_NullWhenNormalIsZeroOrMissing()
    {
        var m = Make(total: 0.0, normal: 0.0, rankWettest: 1, rankDriest: 1);
        Assert.Null(m.PercentOfNormal);
    }

    [Fact]
    public void DepartureDisplay_HasExplicitSign()
    {
        var dry = Make(total: 0.46, normal: 4.33, rankWettest: 152, rankDriest: 4);
        Assert.Equal("-3.87", dry.DepartureDisplay);

        var wet = Make(total: 8.64, normal: 4.55, rankWettest: 13, rankDriest: 146);
        Assert.Equal("+4.09", wet.DepartureDisplay);
    }
}

public class RangeTempSummaryTests
{
    private static RangeTempSummary Make(double avg, double? normal, int rankWarm, int rankCool) =>
        new(AvgTemp: avg, Normal: normal, RankWarmest: rankWarm, RankCoolest: rankCool,
            TotalSeasons: 150, Warmest: [], Coldest: []);

    [Fact]
    public void RankSuperlative_PicksTheCloserEnd()
    {
        var warm = Make(avg: 55.8, normal: 51.5, rankWarm: 4, rankCool: 147);
        Assert.Equal("warmest", warm.RankSuperlative);
        Assert.Equal("4th", warm.RankOrdinal);
    }

    [Fact]
    public void IsNotable_TrueWithinTopFiveEitherEnd()
    {
        Assert.True(Make(avg: 55.8, normal: 51.5, rankWarm: 4, rankCool: 147).IsNotable);
        Assert.True(Make(avg: 40.0, normal: 51.5, rankWarm: 147, rankCool: 4).IsNotable);
        Assert.False(Make(avg: 50.0, normal: 51.5, rankWarm: 70, rankCool: 80).IsNotable);
    }

    [Fact]
    public void DepartureDisplay_UsesRoundHalfAwayFromZero()
    {
        // Exercises TempFormat.Round1 through the Fall model's own Departure property.
        var m = Make(avg: 55.75, normal: 55.0, rankWarm: 10, rankCool: 100);
        Assert.Equal("+0.8", m.DepartureDisplay);
    }
}
