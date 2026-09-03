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

    // Pinned as-is: a bare integer that no time format matches falls back to milliseconds.
    [Fact]
    public void TryParseTime_falls_back_to_milliseconds_for_a_bare_integer()
    {
        Assert.True(TimeParser.TryParseTime("1234", out TimeSpan result));
        Assert.Equal(TimeSpan.FromMilliseconds(1234), result);
    }

    [Fact]
    public void TryParseTime_strips_leading_zeros_and_colons()
    {
        Assert.True(TimeParser.TryParseTime("00:23.456", out TimeSpan result));
        Assert.Equal(new TimeSpan(0, 0, 0, 23, 456), result);
    }
}
