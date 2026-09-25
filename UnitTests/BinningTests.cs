using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class BinningTests
{
    private const long Frame = 170_000L;

    private static List<long> Frames(params int[] frames) => [.. frames.Select(f => f * Frame)];

    [Fact]
    public void Every_bin_is_a_whole_number_of_frames_wide()
    {
        // A range of 37 frames with a preferred width of 2.3 frames: the old range/binCount width
        // left bins that could hold three frames next to bins that could hold two.
        List<long> ticks = [.. Enumerable.Range(0, 38).Select(f => f * Frame)];

        var buckets = Binning.FrameAligned(ticks, 2.3 * Frame, Frame, maxBins: 50);

        foreach (var b in buckets.Take(buckets.Count - 1))
            Assert.Equal(0, (b.MaxTick - b.MinTick) % Frame);
        Assert.All(buckets, b => Assert.Equal(0, (b.MinTick - ticks[0]) % Frame));
    }

    // Consecutive frames spread evenly over bins of a whole frame each. Aliasing showed up as a
    // comb: neighbouring bins holding 2 and 1 for data that is perfectly uniform.
    [Fact]
    public void Uniform_frames_land_uniformly()
    {
        List<long> ticks = [.. Enumerable.Range(0, 20).Select(f => f * Frame)];

        var buckets = Binning.FrameAligned(ticks, Frame, Frame, maxBins: 50);

        Assert.Equal(20, buckets.Count);
        Assert.All(buckets, b => Assert.Equal(1, b.Count));
    }

    // A one-frame spread used to be forced into five bins a third of a frame wide, three of which
    // could never hold anything.
    [Fact]
    public void A_one_frame_spread_gets_one_bin_per_frame()
    {
        var buckets = Binning.FrameAligned(Frames(0, 0, 1, 1, 1), Frame, Frame, maxBins: 50);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(2, buckets[0].Count);
        Assert.Equal(3, buckets[1].Count);
    }

    [Fact]
    public void One_distinct_time_gets_one_bin()
    {
        var buckets = Binning.FrameAligned(Frames(7, 7, 7), Frame, Frame, maxBins: 50);

        Assert.Single(buckets);
        Assert.Equal(3, buckets[0].Count);
    }

    [Fact]
    public void The_bin_count_never_exceeds_the_cap_and_the_width_stays_frame_aligned()
    {
        List<long> ticks = [.. Enumerable.Range(0, 500).Select(f => f * Frame)];

        var buckets = Binning.FrameAligned(ticks, Frame, Frame, maxBins: 50);

        Assert.InRange(buckets.Count, 1, 50);
        Assert.Equal(0, (buckets[1].MinTick - buckets[0].MinTick) % Frame);
        Assert.Equal(500, buckets.Sum(b => b.Count));
    }

    [Fact]
    public void No_time_is_dropped()
    {
        List<long> ticks = [.. Enumerable.Range(0, 61).Select(f => (long)(f * Frame + f % 3))];

        var buckets = Binning.FrameAligned(ticks, 2 * Frame, Frame, maxBins: 50);

        Assert.Equal(ticks.Count, buckets.Sum(b => b.Count));
    }

    [Fact]
    public void An_empty_sample_produces_no_bins()
        => Assert.Empty(Binning.FrameAligned([], Frame, Frame, maxBins: 50));
}

public class EnumLabelTests
{
    [Theory]
    [InlineData("TopLeft", "Top left")]
    [InlineData("MiddleCenter", "Middle center")]
    [InlineData("MadelineRed", "Madeline red")]
    [InlineData("BadelinePurple", "Badeline purple")]
    [InlineData("Both", "Both")]
    [InlineData("Clipboard", "Clipboard")]
    // Digits are not capitals, so a percentile keeps its shape.
    [InlineData("P90", "P90")]
    [InlineData("", "")]
    public void Humanize_splits_a_member_name_on_its_own_capitals(string member, string expected)
        => Assert.Equal(expected, Celeste.Mod.SpeebrunConsistencyTracker.Utility.EnumLabels.Humanize(member));
}
