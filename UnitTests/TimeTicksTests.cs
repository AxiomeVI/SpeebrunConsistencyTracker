using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class TimeTicksTests
{
    private static TimeTicks FromSeconds(double s) => new((long)(s * TimeSpan.TicksPerSecond));

    [Fact]
    public void ToString_omits_the_minute_field_below_one_minute()
    {
        Assert.Equal("23.456", FromSeconds(23.456).ToString());
    }

    [Fact]
    public void ToString_shows_minutes_from_one_minute_up()
    {
        Assert.Equal("1:23.456", FromSeconds(83.456).ToString());
    }

    // The boundary is TotalSeconds < 60, so exactly one minute takes the long form.
    [Fact]
    public void ToString_uses_the_long_form_at_exactly_one_minute()
    {
        Assert.Equal("1:00.000", FromSeconds(60).ToString());
    }

    [Fact]
    public void ToString_prefixes_a_negative_value_with_a_sign()
    {
        Assert.StartsWith("-", FromSeconds(-1.5).ToString());
    }

    [Fact]
    public void Arithmetic_and_comparison_operate_on_ticks()
    {
        TimeTicks a = new(100);
        TimeTicks b = new(40);

        Assert.Equal(140, (a + b).Ticks);
        Assert.Equal(60, (a - b).Ticks);
        Assert.True(a > b);
        Assert.True(b < a);
        Assert.True(a >= new TimeTicks(100));
        Assert.True(a <= new TimeTicks(100));
        Assert.Equal(1, a.CompareTo(b));
    }

    [Fact]
    public void Subtraction_can_go_negative()
    {
        Assert.Equal(-60, (new TimeTicks(40) - new TimeTicks(100)).Ticks);
    }

    [Fact]
    public void Implicit_conversion_to_double_yields_the_tick_count()
    {
        double asDouble = new TimeTicks(1234);
        Assert.Equal(1234d, asDouble);
    }

    [Fact]
    public void Default_value_is_zero()
    {
        Assert.Equal(0, default(TimeTicks).Ticks);
        Assert.Equal(0, TimeTicks.Zero.Ticks);
    }
}
