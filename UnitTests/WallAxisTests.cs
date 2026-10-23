using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Charts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class WallAxisTests
{
    private const long F = 170_000;

    private static TimeTicks[] Frames(params long[] frames) => [.. frames.OrderBy(f => f).Select(f => new TimeTicks(f * F))];

    [Fact]
    public void Fit_has_no_shared_axis()
    {
        Assert.Null(WallAxis.Of(WallScale.Fit, [Frames(60, 61, 62)], F));
    }

    // The point of a shared axis: a room 2 frames wide and a room 40 frames wide no longer fill their
    // tiles alike.
    [Fact]
    public void A_tight_room_spans_less_of_the_shared_axis_than_a_loose_one()
    {
        TimeTicks[] tight = Frames(100, 101, 101, 102, 101, 100);
        TimeTicks[] loose = Frames(100, 110, 120, 130, 140, 105);
        WallAxis axis = WallAxis.Of(WallScale.Absolute, [tight, loose], F);

        double Span(int room, TimeTicks[] t) => axis.At(room, t[^1].Ticks) - axis.At(room, t[0].Ticks);
        Assert.True(Span(0, tight) * 5 < Span(1, loose));
        Assert.True(axis.Max - axis.Min >= Span(1, loose));
    }

    // Absolute: every room's median is 0 on the axis, whatever the room's length.
    [Fact]
    public void Absolute_puts_every_median_at_zero()
    {
        WallAxis axis = WallAxis.Of(WallScale.Absolute, [Frames(30, 31, 32), Frames(600, 610, 620)], F);

        Assert.Equal(0, axis.At(0, 31 * F));
        Assert.Equal(0, axis.At(1, 610 * F));
        Assert.Equal(10 * F, axis.At(1, 620 * F));
    }

    [Fact]
    public void Relative_reads_a_time_as_percent_away_from_the_median()
    {
        WallAxis axis = WallAxis.Of(WallScale.Relative, [Frames(90, 100, 110)], F);

        Assert.Equal(10.0, axis.At(0, 110 * F), 6);
        Assert.Equal(-10.0, axis.At(0, 90 * F), 6);
    }

    // Every time is counted once, in a bin of whole frames: none is lost to an edge.
    [Fact]
    public void Every_time_lands_in_one_bin()
    {
        TimeTicks[] room = Frames(100, 101, 102, 103, 104, 105, 106, 107, 108, 120);
        WallAxis axis = WallAxis.Of(WallScale.Absolute, [room], F);

        var (bins, below, above) = axis.Bins(0, room);

        Assert.Equal(room.Length, bins.Sum(b => b.Count));
        Assert.Equal(0, below + above);
        Assert.All(bins, b => Assert.True(b.From < b.To));
    }

    // One disaster must not squash every tile: it folds into the end bin and is reported.
    [Fact]
    public void A_far_outlier_folds_into_the_end_bin()
    {
        TimeTicks[] room = Frames(100, 101, 100, 102, 101, 100, 101, 102, 101, 1000);
        WallAxis axis = WallAxis.Of(WallScale.Absolute, [room], F);

        var (bins, _, above) = axis.Bins(0, room);

        Assert.Equal(1, above);
        Assert.Equal(room.Length, bins.Sum(b => b.Count));
        Assert.True(axis.Max < 900 * F);
        // Counted in a bar that is drawn whole, not one cut down to a sliver at the edge.
        var last = bins[^1];
        Assert.True(last.To - last.From > 0.5 * (bins[0].To - bins[0].From));
    }

    // A room where every run took the same time still has an axis a few bins wide.
    [Fact]
    public void A_perfectly_consistent_session_is_not_one_full_width_bar()
    {
        TimeTicks[] room = Frames(100, 100, 100, 100);
        WallAxis axis = WallAxis.Of(WallScale.Absolute, [room], F);

        var (bins, _, _) = axis.Bins(0, room);

        Assert.Single(bins);
        Assert.True((bins[0].To - bins[0].From) * 3 < axis.Max - axis.Min);
    }

    // Relative: a short room's bin is still a whole number of frames, so frames do not alias.
    [Fact]
    public void A_short_room_never_gets_a_bin_under_a_frame()
    {
        TimeTicks[] longRoom = Frames(1000, 1001, 1002, 1003, 1004, 1005);
        TimeTicks[] shortRoom = Frames(20, 21, 22, 20, 21, 22);
        WallAxis axis = WallAxis.Of(WallScale.Relative, [longRoom, shortRoom], F);

        var (bins, _, _) = axis.Bins(1, shortRoom);

        Assert.Equal(3, bins.Count);
        Assert.All(bins, b => Assert.Equal(2, b.Count));
    }
}
