using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class TrajectoryModelTests
{
    [Fact]
    public void RoomAverages_is_the_mean_of_the_completed_times_truncated()
    {
        // (10 + 15) / 2 = 12.5, cast to long -> 12.
        PracticeSession session = TrajectorySessions.Of(2, [10, 10], [15, 15]);

        TrajectoryModel model = TrajectoryModel.Build(session, 2);

        Assert.Equal(new long[] { 12, 12 }, model.RoomAverages);
    }

    [Fact]
    public void RoomAverages_is_zero_for_a_room_no_attempt_completed()
    {
        PracticeSession session = TrajectorySessions.Of(3, [10, 10]);

        TrajectoryModel model = TrajectoryModel.Build(session, 3);

        Assert.Equal(new long[] { 10, 10, 0 }, model.RoomAverages);
    }

    [Fact]
    public void CumulativeDeviations_are_the_running_sum_of_time_minus_average()
    {
        // Averages are 14 in every room ((10 + 20 + 12) / 3 = 14).
        PracticeSession session = TrajectorySessions.Of(3, [10, 10, 10], [20, 20, 20], [12, 12, 12]);

        TrajectoryModel model = TrajectoryModel.Build(session, 3);

        Assert.Equal(new long[] { 14, 14, 14 }, model.RoomAverages);
        Assert.Equal(new long[] { -4,  -8, -12 }, model.Attempts[0].CumulativeDeviations);
        Assert.Equal(new long[] {  6,  12,  18 }, model.Attempts[1].CumulativeDeviations);
        Assert.Equal(new long[] { -2,  -4,  -6 }, model.Attempts[2].CumulativeDeviations);
    }

    [Fact]
    public void An_attempt_that_completed_no_room_is_dropped_but_the_numbering_is_kept()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 10], [], [30, 30]);

        TrajectoryModel model = TrajectoryModel.Build(session, 2);

        Assert.Equal(2, model.Attempts.Count);
        // 1-based over the session's own attempts, so the dropped middle one leaves a gap.
        Assert.Equal(1, model.Attempts[0].ChronologicalIndex);
        Assert.Equal(3, model.Attempts[1].ChronologicalIndex);
    }

    [Fact]
    public void An_attempt_stops_at_its_last_contiguous_room()
    {
        PracticeSession session = TrajectorySessions.Of(3, [10, 10, 10], [20, 20]);

        TrajectoryModel model = TrajectoryModel.Build(session, 3);

        Assert.Equal(3, model.Attempts[0].RoomsCompleted);
        Assert.Equal(2, model.Attempts[1].RoomsCompleted);
        Assert.Equal(new long[] { 20, 20 }, model.Attempts[1].RoomTimes);
    }

    [Fact]
    public void SobLine_takes_the_fastest_time_in_each_room()
    {
        // Averages 15 and 15; the SoB picks 10 in both rooms.
        PracticeSession session = TrajectorySessions.Of(2, [10, 20], [20, 10]);

        TrajectoryModel model = TrajectoryModel.Build(session, 2);

        Assert.Equal(new long[] { 10, 10 }, model.SobLine.RoomTimes);
        Assert.Equal(new long[] { -5, -10 }, model.SobLine.CumulativeDeviations);
        Assert.Equal(2, model.SobLine.RoomsCompleted);
    }

    [Fact]
    public void SobLine_stops_at_the_first_room_with_no_recorded_time()
    {
        PracticeSession session = TrajectorySessions.Of(3, [10, 10]);

        TrajectoryModel model = TrajectoryModel.Build(session, 3);

        Assert.Equal(2, model.SobLine.RoomsCompleted);
        Assert.Equal(new long[] { 10, 10 }, model.SobLine.RoomTimes);
        // The deviation array is always TotalRooms long; entries past RoomsCompleted stay 0.
        Assert.Equal(3, model.SobLine.CumulativeDeviations.Length);
        Assert.Equal(0L, model.SobLine.CumulativeDeviations[2]);
    }

    // "vs Best Split" compares a run against the runs before it, so the loop stops one short of
    // the end and the newest attempt is never its own reference.
    [Fact]
    public void BestSoFarIdx_ignores_the_last_attempt_however_fast_it_was()
    {
        PracticeSession session = TrajectorySessions.Of(2, [30, 30], [10, 10]);

        TrajectoryModel model = TrajectoryModel.Build(session, 2);

        Assert.Equal(new[] { 0, 0 }, model.BestSoFarIdx);
    }

    [Fact]
    public void BestSoFarIdx_is_minus_one_when_only_one_attempt_exists()
    {
        PracticeSession session = TrajectorySessions.Of(2, [10, 10]);

        TrajectoryModel model = TrajectoryModel.Build(session, 2);

        Assert.Equal(new[] { -1, -1 }, model.BestSoFarIdx);
    }

    [Fact]
    public void BestSoFarIdx_is_minus_one_for_a_room_no_prior_attempt_reached()
    {
        // Attempt 1 reaches room 1, but it is the last attempt and so is not a candidate.
        PracticeSession session = TrajectorySessions.Of(2, [10], [10, 10]);

        TrajectoryModel model = TrajectoryModel.Build(session, 2);

        Assert.Equal(new[] { 0, -1 }, model.BestSoFarIdx);
    }

    [Fact]
    public void Build_yields_an_empty_model_when_no_attempt_completed_a_room()
    {
        PracticeSession session = TrajectorySessions.Of(2);

        TrajectoryModel model = TrajectoryModel.Build(session, 2);

        Assert.Empty(model.Attempts);
        Assert.Equal(0, model.SobLine.RoomsCompleted);
        Assert.Equal(2, model.TotalRooms);
    }

    [Fact]
    public void Build_yields_an_empty_model_for_zero_rooms()
    {
        PracticeSession session = TrajectorySessions.Of(1, [10]);

        TrajectoryModel model = TrajectoryModel.Build(session, 0);

        Assert.Empty(model.Attempts);
        Assert.Empty(model.RoomAverages);
        Assert.Empty(model.BestSoFarIdx);
    }

    [Fact]
    public void DevAtRoom_holds_the_last_deviation_of_a_line_that_stopped_short()
    {
        AttemptLine line = new([-4, -8], [10, 10], RoomsCompleted: 2, ChronologicalIndex: 1);

        Assert.Equal(-4, TrajectoryModel.DevAtRoom(line, 0));
        Assert.Equal(-8, TrajectoryModel.DevAtRoom(line, 1));
        Assert.Equal(-8, TrajectoryModel.DevAtRoom(line, 9));
    }

    [Fact]
    public void DevAtRoom_is_zero_for_a_line_that_completed_nothing()
    {
        AttemptLine line = new([], [], RoomsCompleted: 0, ChronologicalIndex: 0);

        Assert.Equal(0, TrajectoryModel.DevAtRoom(line, 0));
    }
}
