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
    public void An_excluded_run_is_not_the_best_run()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 30], [4, 4]);

        Assert.Equal(0, TrajectoryBaselines.BestRun(session, TrajectoryBaselines.Contiguous(session), 2, excluded: 1));
    }

    // The best run is a real run: a run with a deleted cell is not complete, however fast the rest.
    [Fact]
    public void A_run_with_a_deleted_cell_is_not_the_best_run()
    {
        PracticeSession session = TrajectorySessions.Of(2, [1, 1], [20, 20]);
        session.DeleteCell(attemptIndex: 0, visibleRoomIndex: 0);

        Assert.Equal(1, TrajectoryBaselines.BestRun(session, TrajectoryBaselines.Contiguous(session), 2));
    }

    [Fact]
    public void A_session_with_no_rooms_has_no_best_run()
    {
        PracticeSession session = TrajectorySessions.Of(0);

        Assert.Equal(-1, TrajectoryBaselines.BestRun(session, TrajectoryBaselines.Contiguous(session), 0));
    }
}
