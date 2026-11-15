using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using System.Linq;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class RoomStatsTests
{
    [Fact]
    public void A_room_is_read_from_every_completed_cell_of_it()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 30], [20, 15], [30, 5]);

        RoomStats stats = RoomStats.Of(session, 2);

        Assert.Equal(new long[] { 20, 16 }, stats.Averages);   // 50 / 3 truncated, 60 / 3
        Assert.Equal(new long[] { 10, 5 }, stats.Bests);
        Assert.Equal(new[] { true, true }, stats.HasTime);
    }

    // The session the audit found: a deleted cell, and a later room that is faster.
    [Fact]
    public void A_cell_after_a_deleted_one_counts_in_its_own_room()
    {
        PracticeSession session = TrajectorySessions.Of(3, [10, 10, 10], [20, 20, 20]);
        session.DeleteCell(attemptIndex: 0, visibleRoomIndex: 1);

        RoomStats stats = RoomStats.Of(session, 3);

        Assert.Equal(new long[] { 15, 20, 15 }, stats.Averages);
        Assert.Equal(new long[] { 10, 20, 10 }, stats.Bests);
        Assert.Equal(new[] { true, true, true }, stats.HasTime);
    }

    [Fact]
    public void A_room_with_no_time_has_none_to_offer()
    {
        PracticeSession session = TrajectorySessions.Of(3, [10, 10, 10]);
        session.DeleteCell(attemptIndex: 0, visibleRoomIndex: 1);

        RoomStats stats = RoomStats.Of(session, 3);

        Assert.Equal(new[] { true, false, true }, stats.HasTime);
        Assert.Equal(0L, stats.Averages[1]);
        Assert.Equal(0L, stats.Bests[1]);
    }

    [Fact]
    public void An_excluded_run_is_not_in_the_stats()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 30], [4, 4]);

        RoomStats stats = RoomStats.Of(session, 2, excluded: 1);

        Assert.Equal(new long[] { 10, 30 }, stats.Averages);
        Assert.Equal(new long[] { 10, 30 }, stats.Bests);
    }

    [Fact]
    public void The_best_of_a_room_is_the_overlays_minimum_of_its_times()
    {
        PracticeSession session = TrajectorySessions.Of(3, [10, 10, 10], [20, 20, 20], [30, 5]);
        session.DeleteCell(attemptIndex: 0, visibleRoomIndex: 1);

        RoomStats stats = RoomStats.Of(session, 3);

        for (int r = 0; r < 3; r++)
            Assert.Equal(session.GetRoomTimes(r).Min(t => t.Ticks), stats.Bests[r]);
    }
}
