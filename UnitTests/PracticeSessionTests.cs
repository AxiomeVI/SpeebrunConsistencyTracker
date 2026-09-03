using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class PracticeSessionTests
{
    private static TimeTicks T(long ticks) => new(ticks);

    private static PracticeSession NewSession(StubSegmentShape shape) => new(shape);

    // Records one full attempt, without starting the next one.
    private static void CompleteRooms(PracticeSession session, params long[] roomTimes)
    {
        foreach (long t in roomTimes) session.CompleteRoom(T(t));
    }

    [Fact]
    public void A_new_session_opens_its_first_attempt()
    {
        PracticeSession session = NewSession(new StubSegmentShape());

        Assert.Equal(1, session.AttemptCount);
        Assert.Equal(0, session.CurrentAttemptIndex);
        Assert.Equal(0, session.CurrentRoomIndex);
        Assert.Equal(TimeTicks.Zero, session.RunningSegmentTime);
    }

    [Fact]
    public void The_constructor_rejects_a_null_segment_shape()
    {
        Assert.Throws<System.ArgumentNullException>(() => new PracticeSession(null));
    }

    [Fact]
    public void CompleteRoom_records_the_time_and_advances()
    {
        PracticeSession session = NewSession(new StubSegmentShape(roomCount: 3));

        CompleteRooms(session, 100, 200);

        Assert.Equal(2, session.CurrentRoomIndex);
        Assert.Equal(300, session.RunningSegmentTime.Ticks);
        Assert.Equal(100, session.GetCell(0, 0).Time.Ticks);
        Assert.Equal(200, session.GetCell(0, 1).Time.Ticks);
        Assert.Equal(RoomCellState.NotReached, session.GetCell(0, 2).State);
    }

    // The room an attempt died in is marked, which is what tells a reset from a room never reached.
    [Fact]
    public void Starting_a_new_attempt_marks_the_abandoned_room_as_DNF()
    {
        PracticeSession session = NewSession(new StubSegmentShape(roomCount: 3));
        CompleteRooms(session, 100, 200);

        session.StartNewAttempt();

        Assert.Equal(RoomCellState.DNF, session.GetCell(0, 2).State);
        Assert.Equal(2, session.AttemptCount);
        Assert.Equal(0, session.CurrentRoomIndex);
    }

    [Fact]
    public void An_attempt_abandoned_before_its_first_room_records_no_DNF()
    {
        PracticeSession session = NewSession(new StubSegmentShape(roomCount: 3));

        session.StartNewAttempt();

        Assert.Equal(RoomCellState.NotReached, session.GetCell(0, 0).State);
    }

    [Fact]
    public void IsCompleted_requires_every_visible_room_in_an_unbroken_run()
    {
        StubSegmentShape shape = new(roomCount: 3);
        PracticeSession session = NewSession(shape);

        CompleteRooms(session, 100, 200);
        Assert.False(session.IsCompleted(0));

        session.CompleteRoom(T(300));
        Assert.True(session.IsCompleted(0));
    }

    [Fact]
    public void SegmentTime_sums_the_visible_rooms_of_a_completed_attempt()
    {
        StubSegmentShape shape = new(roomCount: 3);
        PracticeSession session = NewSession(shape);

        CompleteRooms(session, 100, 200, 300);

        Assert.Equal(600, session.SegmentTime(0).Ticks);
    }

    [Fact]
    public void SegmentTime_is_zero_when_a_visible_room_was_reached_but_not_completed()
    {
        StubSegmentShape shape = new(roomCount: 3);
        PracticeSession session = NewSession(shape);
        CompleteRooms(session, 100, 200);
        session.StartNewAttempt();          // marks room 2 of attempt 0 as DNF, allocating the column

        Assert.Equal(TimeTicks.Zero, session.SegmentTime(0));
    }

    // Pinned trap, not endorsed: the loop is bounded by `c < row.Length` too, so a matrix that
    // never grew to the full window returns a PARTIAL sum that looks like a finished segment.
    // Safe only because both callers guard on IsCompleted; a third that forgets would not be.
    [Fact]
    public void SegmentTime_returns_a_partial_sum_when_the_matrix_never_reached_the_window()
    {
        StubSegmentShape shape = new(roomCount: 3);
        PracticeSession session = NewSession(shape);

        CompleteRooms(session, 100, 200);   // room 2 never allocated: ColumnCount stays at 2

        Assert.False(session.IsCompleted(0));
        Assert.Equal(300, session.SegmentTime(0).Ticks);
    }

    // StartRoomIndex is 1 when SpeedrunTool times whole segments: the first physical room is the
    // run-up, outside the measured segment.
    [Fact]
    public void A_non_zero_start_index_shifts_the_visible_window()
    {
        StubSegmentShape shape = new(startRoomIndex: 1, roomCount: 2);
        PracticeSession session = NewSession(shape);

        CompleteRooms(session, 100, 200, 300);

        Assert.Equal(200, session.GetCell(0, 0).Time.Ticks);
        Assert.Equal(300, session.GetCell(0, 1).Time.Ticks);
        Assert.Equal(500, session.SegmentTime(0).Ticks);
        Assert.True(session.IsCompleted(0));
    }

    [Fact]
    public void GetRoomTimes_returns_only_completed_cells_of_one_room()
    {
        StubSegmentShape shape = new(roomCount: 2);
        PracticeSession session = NewSession(shape);

        CompleteRooms(session, 100, 150);
        session.StartNewAttempt();
        CompleteRooms(session, 110);
        session.StartNewAttempt();
        CompleteRooms(session, 120, 170);

        Assert.Equal([100L, 110L, 120L], session.GetRoomTimes(0).Select(t => t.Ticks));
        Assert.Equal([150L, 170L], session.GetRoomTimes(1).Select(t => t.Ticks));
        Assert.Equal([0, 1, 2], session.GetRoomAttemptIndices(0));
        Assert.Equal([0, 2], session.GetRoomAttemptIndices(1));
    }

    [Fact]
    public void GetRoomTimes_is_empty_for_a_room_no_attempt_has_reached()
    {
        PracticeSession session = NewSession(new StubSegmentShape(roomCount: 5));
        CompleteRooms(session, 100);

        Assert.Empty(session.GetRoomTimes(4));
    }

    [Fact]
    public void DeleteCell_removes_a_time_from_the_room_statistics()
    {
        StubSegmentShape shape = new(roomCount: 2);
        PracticeSession session = NewSession(shape);
        CompleteRooms(session, 100, 150);

        session.DeleteCell(0, 0);

        Assert.Equal(RoomCellState.Deleted, session.GetCell(0, 0).State);
        Assert.Empty(session.GetRoomTimes(0));
        Assert.False(session.IsCompleted(0));
    }

    [Fact]
    public void DeleteCell_is_a_no_op_on_a_cell_that_holds_nothing()
    {
        PracticeSession session = NewSession(new StubSegmentShape(roomCount: 3));
        CompleteRooms(session, 100);
        uint versionBefore = session.Version;

        session.DeleteCell(0, 2);

        Assert.Equal(versionBefore, session.Version);
        Assert.Equal(RoomCellState.NotReached, session.GetCell(0, 2).State);
    }

    [Fact]
    public void DeleteAttempt_deletes_every_cell_the_attempt_reached()
    {
        StubSegmentShape shape = new(roomCount: 2);
        PracticeSession session = NewSession(shape);
        CompleteRooms(session, 100, 150);
        session.StartNewAttempt();
        CompleteRooms(session, 110, 160);

        session.DeleteAttempt(0);

        Assert.Equal(RoomCellState.Deleted, session.GetCell(0, 0).State);
        Assert.Equal(RoomCellState.Deleted, session.GetCell(0, 1).State);
        Assert.Equal([110L], session.GetRoomTimes(0).Select(t => t.Ticks));
    }

    [Fact]
    public void ReachedRoomCount_counts_up_to_the_furthest_touched_room_gaps_included()
    {
        StubSegmentShape shape = new(roomCount: 4);
        PracticeSession session = NewSession(shape);
        CompleteRooms(session, 100, 200);
        session.DeleteCell(0, 0);

        // Room 0 is Deleted, room 1 still Completed, so the furthest touched room is still 1.
        Assert.Equal(2, session.ReachedRoomCount(0));
        // ContiguousCount stops at the first non-Completed cell, so it disagrees by design.
        Assert.Equal(0, session.ContiguousCount(0));
    }

    [Fact]
    public void Aggregates_count_completed_runs_and_resets()
    {
        StubSegmentShape shape = new(roomCount: 2);
        PracticeSession session = NewSession(shape);
        CompleteRooms(session, 100, 150);
        session.StartNewAttempt();
        CompleteRooms(session, 110);
        session.StartNewAttempt();
        CompleteRooms(session, 120, 170);

        Assert.Equal(3, session.TotalAttempts);
        Assert.Equal(2, session.TotalCompleted);
        Assert.Equal(1, session.TotalDnfs);
        Assert.Equal([250L, 290L], session.GetSegmentTimes().Select(t => t.Ticks));
        Assert.Equal([0, 2], session.GetCompletedAttemptIndices());
    }

    // RoomCount moves under a running session without bumping Version, which is why the per-room
    // caches key on (Version, RoomCount, StartRoomIndex) rather than Version alone.
    [Fact]
    public void Aggregates_follow_a_room_count_change_that_does_not_bump_the_version()
    {
        StubSegmentShape shape = new(roomCount: 3);
        PracticeSession session = NewSession(shape);
        CompleteRooms(session, 100, 150);

        Assert.Equal(0, session.TotalCompleted);
        uint versionBefore = session.Version;

        shape.RoomCount = 2;

        Assert.Equal(versionBefore, session.Version);
        Assert.Equal(1, session.TotalCompleted);
    }

    [Fact]
    public void Aggregates_follow_a_start_index_change_that_does_not_bump_the_version()
    {
        StubSegmentShape shape = new(startRoomIndex: 0, roomCount: 2);
        PracticeSession session = NewSession(shape);
        CompleteRooms(session, 100, 150, 200);

        Assert.Equal([100L], session.GetRoomTimes(0).Select(t => t.Ticks));
        uint versionBefore = session.Version;

        shape.StartRoomIndex = 1;

        Assert.Equal(versionBefore, session.Version);
        Assert.Equal([150L], session.GetRoomTimes(0).Select(t => t.Ticks));
    }

    [Fact]
    public void Recording_a_room_inside_the_visible_segment_bumps_the_version()
    {
        PracticeSession session = NewSession(new StubSegmentShape(roomCount: 3));
        uint before = session.Version;

        session.CompleteRoom(T(100));

        Assert.True(session.Version > before);
    }

    // Past the visible segment nothing downstream can change, so the bump is skipped on purpose.
    [Fact]
    public void Recording_a_room_past_the_visible_segment_does_not_bump_the_version()
    {
        PracticeSession session = NewSession(new StubSegmentShape(roomCount: 2));
        CompleteRooms(session, 100, 150);
        uint before = session.Version;

        session.CompleteRoom(T(200));

        Assert.Equal(before, session.Version);
    }
}
