using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// CelesteCharts draws the axes and bins; the overlay text, the tooltips and the CSV are still this
// mod's. Where the two write the same number they must write it the same way, or a chart and the
// text beside it disagree by a digit. These hold the library to what the mod already prints.
public class ChartLibraryParityTests
{
    private const long Frame = 170_000L;

    public static IEnumerable<object[]> Times() =>
        new long[] {
            0, 1, 9_999, Frame, 12_345_678, 599_990_000, 600_000_000, 600_010_000, 750_000_000,
            35_999_990_000, 36_000_000_000, 36_610_010_000, 400_000_000_000,
        }.SelectMany(t => new[] { new object[] { t }, new object[] { -t } });

    [Theory]
    [MemberData(nameof(Times))]
    public void A_time_reads_the_same_on_an_axis_as_in_the_overlay(long ticks) =>
        Assert.Equal(new TimeTicks(ticks).ToString(), Durations.Times.Format(ticks));

    [Fact]
    public void The_library_counts_a_frame_as_this_mod_does() =>
        Assert.Equal(Frame, Durations.CelesteFrameTicks);
}
