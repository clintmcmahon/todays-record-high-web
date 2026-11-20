using TodaysRecordHigh.Web.Models;

namespace TodaysRecordHigh.Web.Tests.Unit;

public class TempFormatTests
{
    // The whole reason TempFormat.Round1 exists: Math.Round's default banker's rounding
    // turns 78.85 into 78.8, which reads as wrong next to published NWS figures that
    // round half away from zero. If this regresses, every displayed average is off by
    // 0.1 on exact-half values.
    [Theory]
    [InlineData(78.85, 78.9)]
    [InlineData(78.75, 78.8)]
    [InlineData(-2.25, -2.3)]   // negative halves round away from zero too (more negative)
    [InlineData(-2.15, -2.2)]
    [InlineData(0.05, 0.1)]
    [InlineData(50.0, 50.0)]
    public void Round1_RoundsHalfAwayFromZero(double input, double expected)
    {
        Assert.Equal(expected, TempFormat.Round1(input));
    }

    [Fact]
    public void Round1_BankersRoundingWouldGiveWrongAnswer()
    {
        // Sanity check that the test above is actually exercising the fix, not a no-op:
        // plain Math.Round with default MidpointRounding would give 78.8 here.
        Assert.Equal(78.8, Math.Round(78.85, 1));
        Assert.Equal(78.9, TempFormat.Round1(78.85));
    }

    [Theory]
    [InlineData(78.85, "78.9")]
    [InlineData(50.0, "50.0")]
    [InlineData(-10.04, "-10.0")]
    public void Temp1_FormatsToOneDecimal(double input, string expected)
    {
        Assert.Equal(expected, TempFormat.Temp1(input));
    }
}
