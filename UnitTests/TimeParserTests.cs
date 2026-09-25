using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class TimeParserTests
{
    [Theory]
    [InlineData("1:23.456", 0, 1, 23, 456)]
    [InlineData("12:34.567", 0, 12, 34, 567)]
    [InlineData("1:23", 0, 1, 23, 0)]
    [InlineData("23.456", 0, 0, 23, 456)]
    [InlineData("23.45", 0, 0, 23, 450)]
    [InlineData("23.4", 0, 0, 23, 400)]
    [InlineData(".456", 0, 0, 0, 456)]
    // A comma is the decimal separator on French layouts, where it is unshifted and "." is not.
    [InlineData("1:23,456", 0, 1, 23, 456)]
    [InlineData("23,4", 0, 0, 23, 400)]
    public void TryParseTime_parses_the_documented_formats(
        string input, int hours, int minutes, int seconds, int milliseconds)
    {
        Assert.True(TimeParser.TryParseTime(input, out TimeSpan result));
        Assert.Equal(new TimeSpan(0, hours, minutes, seconds, milliseconds), result);
    }

    [Theory]
    [InlineData("not a time")]
    [InlineData("1:2:3.456")]
    [InlineData("--")]
    // A negative used to reach the callers, which stored -5 in a 0..9 millisecond-digit setting.
    [InlineData("-500")]
    [InlineData("-1:23.456")]
    // Past the sliders' own range: 60 minutes cannot be represented, and truncating to
    // result.Minutes silently turned 1:00:00 into 00:00.
    [InlineData("3600")]
    [InlineData("59:60")]
    public void TryParseTime_rejects_input_it_cannot_parse(string input)
    {
        Assert.False(TimeParser.TryParseTime(input, out _));
    }

    // Pinned, not endorsed: empty input reports SUCCESS with a zero time. The target-time menu
    // relies on it, so the clipboard import guards against empty input on its own side.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParseTime_reports_success_and_zero_for_empty_input_by_design(string? input)
    {
        Assert.True(TimeParser.TryParseTime(input, out TimeSpan result));
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    public void TryParseTime_treats_a_bare_zero_as_zero(string input)
    {
        Assert.True(TimeParser.TryParseTime(input, out TimeSpan result));
        Assert.Equal(TimeSpan.Zero, result);
    }

    // A bare number is seconds at every size. It used to be seconds up to 59 (matched by the `ss`
    // format) and milliseconds from 60 up (the fallback), so "59" was 59 s and "60" was 60 ms.
    [Theory]
    [InlineData("59", 59)]
    [InlineData("60", 60)]
    [InlineData("1234", 1234)]
    public void TryParseTime_reads_a_bare_number_as_seconds_at_every_size(string input, int seconds)
    {
        Assert.True(TimeParser.TryParseTime(input, out TimeSpan result));
        Assert.Equal(TimeSpan.FromSeconds(seconds), result);
    }

    // The sliders that store the target time top out at 59:59.999, so the parser does too.
    [Fact]
    public void TryParseTime_accepts_the_largest_time_the_sliders_can_hold()
    {
        Assert.True(TimeParser.TryParseTime("59:59.999", out TimeSpan result));
        Assert.Equal(TimeParser.MaxTargetTime, result);
    }

    [Fact]
    public void TryParseTime_strips_leading_zeros_and_colons()
    {
        Assert.True(TimeParser.TryParseTime("00:23.456", out TimeSpan result));
        Assert.Equal(new TimeSpan(0, 0, 0, 23, 456), result);
    }
}

public class LevelNameTests
{
    [Theory]
    [InlineData("Celeste/1-ForsakenCity", 0, "Celeste_1-ForsakenCity")]
    [InlineData("Celeste/1-ForsakenCity", 1, "Celeste_1-ForsakenCity_B")]
    [InlineData("Celeste/1-ForsakenCity", 2, "Celeste_1-ForsakenCity_C")]
    // No '-' anywhere: this is the SID shape that used to become "unknown".
    [InlineData("Beginner/zoey", 0, "Beginner_zoey")]
    // Two maps that differ only past the first '/' must not collapse onto one folder.
    [InlineData("Pack/A/room", 0, "Pack_A_room")]
    [InlineData("Pack/B/room", 0, "Pack_B_room")]
    [InlineData(null, 0, "unknown")]
    [InlineData("  ", 0, "unknown")]
    public void ForExportFolder_keeps_the_whole_sid_and_the_side(string? sid, int mode, string expected)
        => Assert.Equal(expected, Celeste.Mod.SpeebrunConsistencyTracker.Utility.LevelNames.ForExportFolder(sid, mode));
}
