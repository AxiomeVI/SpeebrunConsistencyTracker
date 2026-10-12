using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class LiveTrajectoryTests
{
    private static PracticeSession Playing(PracticeSession session, params long[] rooms)
    {
        foreach (long ticks in rooms) session.CompleteRoom(new TimeTicks(ticks));
        return session;
    }

    [Fact]
    public void The_run_in_progress_is_measured_from_the_runs_before_it()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(2, [10, 10], [20, 20]), 5);

        LiveLine line = LiveTrajectory.Build(session, 2, TrajectoryBaseline.Average);

        // Against 15, the average of the two earlier runs: its own 5 is not in it.
        Assert.Equal(new long[] { -10 }, line.CumulativeDeviations);
        Assert.Equal(2, line.TotalRooms);
        Assert.True(line.HasBaseline);
    }

    [Fact]
    public void A_new_best_room_reads_as_a_gain_on_the_sum_of_bests()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(2, [10, 10]), 4);

        LiveLine line = LiveTrajectory.Build(session, 2, TrajectoryBaseline.SumOfBest);

        Assert.Equal(new long[] { -6 }, line.CumulativeDeviations);
    }

    [Fact]
    public void The_first_run_of_a_session_has_nothing_to_be_measured_from()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(2), 10);

        LiveLine line = LiveTrajectory.Build(session, 2, TrajectoryBaseline.Average);

        Assert.False(line.HasBaseline);
        Assert.Empty(line.CumulativeDeviations);
    }

    [Fact]
    public void Against_the_best_run_it_is_measured_from_that_run()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(2, [10, 30], [20, 15]), 18, 15);

        LiveLine line = LiveTrajectory.Build(session, 2, TrajectoryBaseline.BestRun);

        Assert.Equal(TrajectoryBaseline.BestRun, line.Baseline);
        Assert.Equal(new long[] { -2, -2 }, line.CumulativeDeviations);
    }

    [Fact]
    public void With_no_earlier_completed_run_the_best_run_baseline_is_the_average()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(2, [10]), 12);

        LiveLine line = LiveTrajectory.Build(session, 2, TrajectoryBaseline.BestRun);

        Assert.Equal(TrajectoryBaseline.Average, line.Baseline);
        Assert.Equal(new long[] { 2 }, line.CumulativeDeviations);
    }

    [Fact]
    public void The_line_stops_at_the_last_room_an_earlier_run_reached()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(3, [10, 10]), 10, 10, 10);

        LiveLine line = LiveTrajectory.Build(session, 3, TrajectoryBaseline.Average);

        Assert.Equal(new long[] { 0, 0 }, line.CumulativeDeviations);
    }

    [Fact]
    public void A_run_just_started_has_a_baseline_and_no_point()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 10]);

        LiveLine line = LiveTrajectory.Build(session, 2, TrajectoryBaseline.Average);

        Assert.True(line.HasBaseline);
        Assert.Empty(line.CumulativeDeviations);
    }

    [Fact]
    public void A_finished_run_keeps_its_whole_line()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(2, [10, 10]), 12, 12);

        LiveLine line = LiveTrajectory.Build(session, 2, TrajectoryBaseline.Average);

        Assert.Equal(new long[] { 2, 4 }, line.CumulativeDeviations);
    }

    [Fact]
    public void A_room_deleted_from_the_run_in_progress_ends_its_line_there()
    {
        PracticeSession session = Playing(TrajectorySessions.Of(3, [10, 10, 10]), 12, 12, 12);
        session.DeleteCell(attemptIndex: 1, visibleRoomIndex: 1);

        LiveLine line = LiveTrajectory.Build(session, 3, TrajectoryBaseline.Average);

        Assert.Equal(new long[] { 2 }, line.CumulativeDeviations);
    }

    [Fact]
    public void A_session_with_no_rooms_gives_an_empty_line()
    {
        LiveLine line = LiveTrajectory.Build(TrajectorySessions.Of(0), 0, TrajectoryBaseline.Average);

        Assert.False(line.HasBaseline);
        Assert.Empty(line.CumulativeDeviations);
    }
}
