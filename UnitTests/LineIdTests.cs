using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// A trajectory line's identity leaves the overlay as a HoverInfo key string and comes back
// through HandleClick, so the encode and the decode have to agree over the whole range.
public class LineIdTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void An_attempt_id_survives_the_round_trip_through_its_key(int attemptIndex)
    {
        LineId id = LineId.Attempt(attemptIndex);

        Assert.Equal(id, LineId.FromKey(id.ToKey(), attemptCount: 5));
        Assert.True(id.IsAttempt);
    }

    [Fact]
    public void The_sob_line_sits_one_past_the_last_attempt()
    {
        LineId sob = LineId.Sob(attemptCount: 5);

        Assert.Equal("5", sob.ToKey());
        Assert.Equal(sob, LineId.FromKey("5", attemptCount: 5));
        Assert.True(sob.IsSob);
    }

    [Fact]
    public void The_baseline_sits_two_past_the_last_attempt()
    {
        LineId baseline = LineId.Baseline(attemptCount: 5);

        Assert.Equal("6", baseline.ToKey());
        Assert.Equal(baseline, LineId.FromKey("6", attemptCount: 5));
        Assert.True(baseline.IsBaseline);
    }

    [Theory]
    [InlineData("7")]     // past the baseline
    [InlineData("-1")]
    [InlineData("")]
    [InlineData("seg:2")] // another overlay's pin key
    [InlineData(null)]
    public void A_key_outside_the_three_ranges_decodes_to_None(string? key)
    {
        LineId id = LineId.FromKey(key, attemptCount: 5);

        Assert.True(id.IsNone);
        Assert.Equal(LineId.None, id);
    }

    // With no attempt recorded, key "0" is still the SoB line rather than attempt 0.
    [Fact]
    public void With_no_attempts_the_first_key_is_the_sob_line()
    {
        Assert.True(LineId.FromKey("0", attemptCount: 0).IsSob);
        Assert.True(LineId.FromKey("1", attemptCount: 0).IsBaseline);
    }

    [Fact]
    public void None_is_not_equal_to_any_real_line()
    {
        Assert.NotEqual(LineId.None, LineId.Attempt(0));
        Assert.NotEqual(LineId.None, LineId.Sob(0));
        Assert.False(LineId.Attempt(0).IsNone);
    }

    // Kind is part of the identity, so the SoB line and the attempt that shares its number are
    // different lines even though they carry the same wire value.
    [Fact]
    public void Two_ids_with_the_same_value_but_different_kinds_are_not_equal()
    {
        Assert.NotEqual(LineId.Attempt(5), LineId.Sob(5));
    }
}
