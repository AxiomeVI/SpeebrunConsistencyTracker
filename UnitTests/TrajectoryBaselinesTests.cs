using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class TrajectoryBaselinesTests
{
    [Fact]
    public void The_best_run_is_the_fastest_run_that_completed_every_room()
    {
        // 40, then 35, then a run that stopped after one room.
        PracticeSession session = TrajectorySessions.Of(2, [10, 30], [20, 15], [5]);

        Assert.Equal(1, TrajectoryBaselines.BestRun(session, TrajectoryBaselines.Contiguous(session), 2));
    }

    [Fact]
    public void There_is_no_best_run_until_one_is_complete()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10], [20]);

        Assert.Equal(-1, TrajectoryBaselines.BestRun(session, TrajectoryBaselines.Contiguous(session), 2));
    }

    [Fact]
    public void Of_two_equal_runs_the_first_is_the_best()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 20], [20, 10]);

        Assert.Equal(0, TrajectoryBaselines.BestRun(session, TrajectoryBaselines.Contiguous(session), 2));
    }

    [Fact]
    public void An_excluded_run_is_neither_the_best_nor_in_the_stats()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 30], [4, 4]);
        int[] contiguous = TrajectoryBaselines.Contiguous(session);

        Assert.Equal(0, TrajectoryBaselines.BestRun(session, contiguous, 2, excluded: 1));
        RoomStats stats = TrajectoryBaselines.Stats(session, contiguous, 2, excluded: 1);
        Assert.Equal(new long[] { 10, 30 }, stats.Averages);
        Assert.Equal(new long[] { 10, 30 }, stats.Bests);
    }

    [Fact]
    public void A_session_with_no_rooms_has_no_best_run()
    {
        PracticeSession session = TrajectorySessions.Of(0);

        Assert.Equal(-1, TrajectoryBaselines.BestRun(session, TrajectoryBaselines.Contiguous(session), 0));
    }
}
