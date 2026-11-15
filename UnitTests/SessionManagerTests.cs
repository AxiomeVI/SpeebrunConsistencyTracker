using System;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// SessionManager is static, and so is Srt.Tool: a test class that touches either joins this
// collection, or xUnit's parallel classes race on them.
[Collection(StaticStateCollection.Name)]
public sealed class SessionManagerTests : IDisposable
{
    private const string Slot = "SaveSlot@2";

    private readonly ISpeedrunTool _previous = Srt.Tool;
    private readonly StubSpeedrunTool _srt = new() { TimerType = RoomTimerMode.NextRoom, NumberOfRooms = 6 };

    public SessionManagerTests()
    {
        Srt.Tool = _srt;
        SessionManager.ClearAll();
        SessionManager.DropStash();
        SessionManager.LevelName = "unknown";
    }

    public void Dispose()
    {
        SessionManager.ClearAll();
        SessionManager.DropStash();
        SessionManager.LevelName = "unknown";
        Srt.Tool = _previous;
    }

    // One run through three room transitions, then SpeedrunTool's reload: BeforeLoad, LoadSlot.
    // CompleteRoom takes the segment's running time, as the room timer reports it.
    private static void RunThreeRoomsThenReload(string slot = Slot)
    {
        SessionManager.CompleteRoom(100);
        SessionManager.CompleteRoom(250);
        SessionManager.CompleteRoom(450);
        SessionManager.BeforeLoad(450);
        SessionManager.LoadSlot(slot);
    }

    // #8: two runs that never complete the timer (6 rooms, 3 reached), then a reload that puts the
    // active attempt back at room 0. Before the fix nothing counted the rooms, and RoomCount was 0.
    [Fact]
    public void Rooms_reached_by_runs_that_never_complete_the_timer_are_counted()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        RunThreeRoomsThenReload();

        // NextRoom starts the segment at room 1: rooms 1 and 2 completed, room 3 a DNF.
        Assert.Equal(3, SessionManager.RoomCount);
        Assert.Equal(2, SessionManager.CurrentSession.TotalAttempts);
        Assert.Equal(0, SessionManager.CurrentSession.CurrentRoomIndex);
    }

    [Fact]
    public void NumberOfRooms_clamps_the_room_count()
    {
        _srt.NumberOfRooms = 2;
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();

        Assert.Equal(2, SessionManager.RoomCount);
    }

    [Fact]
    public void The_room_count_follows_NumberOfRooms_read_live()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        _srt.NumberOfRooms = 1;

        SessionManager.UpdateRoomCount();

        Assert.Equal(1, SessionManager.RoomCount);
    }

    // #55: the Increase and Decrease Timed Rooms hotkeys change NumberOfRooms with nothing calling
    // UpdateRoomCount, and the trajectory widget reads RoomCount with no chart open.
    [Fact]
    public void The_room_count_follows_a_change_of_NumberOfRooms_before_any_update()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        Assert.Equal(3, SessionManager.RoomCount);

        _srt.NumberOfRooms = 2;
        Assert.Equal(2, SessionManager.RoomCount);

        _srt.NumberOfRooms = 6;
        Assert.Equal(3, SessionManager.RoomCount);
    }

    // The reload left the active attempt at room 0, so only a rescan of every attempt finds room 3.
    [Fact]
    public void A_timer_type_change_recounts_every_attempt()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        _srt.TimerType = RoomTimerMode.CurrentRoom;

        SessionManager.UpdateRoomCount();

        Assert.Equal(4, SessionManager.RoomCount);
    }

    // #37: the switch is seen while slot B is current. Slot A's count was taken under NextRoom, and
    // a load of A must count it again under CurrentRoom.
    [Fact]
    public void A_timer_type_change_recounts_a_slot_loaded_after_it()
    {
        const string Other = "SaveSlot@3";
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        SessionManager.SaveSlot(Other);
        RunThreeRoomsThenReload(Other);
        _srt.TimerType = RoomTimerMode.CurrentRoom;
        SessionManager.UpdateRoomCount();

        SessionManager.LoadSlot(Slot);
        SessionManager.UpdateRoomCount();

        Assert.Equal(4, SessionManager.RoomCount);
    }

    // #37: the trajectory widget reads RoomCount with no chart open, where nothing calls
    // UpdateRoomCount until the next room.
    [Fact]
    public void The_room_count_follows_a_timer_type_change_before_any_update()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        _srt.TimerType = RoomTimerMode.CurrentRoom;

        Assert.Equal(4, SessionManager.RoomCount);
    }

    [Fact]
    public void The_room_count_follows_a_load_of_another_slot_before_any_update()
    {
        const string Other = "SaveSlot@3";
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();

        SessionManager.LoadSlot(Other);

        Assert.Equal(0, SessionManager.RoomCount);
    }

    [Theory]
    [InlineData(RoomTimerMode.CurrentRoom, 0)]
    [InlineData(RoomTimerMode.NextRoom, 1)]
    [InlineData(RoomTimerMode.Off, 1)]
    public void The_segment_starts_where_the_timer_type_says(RoomTimerMode timerType, int startRoomIndex)
    {
        _srt.TimerType = timerType;

        Assert.Equal(startRoomIndex, SessionManager.StartRoomIndex);
    }

    [Fact]
    public void A_cleared_slot_leaves_no_session_and_no_room_count()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();

        SessionManager.ClearSlot(Slot);

        Assert.Null(SessionManager.CurrentSession);
        Assert.Equal(0, SessionManager.RoomCount);
    }

    // #33: a load on a slot with no session creates one. Its constructor already opened the attempt
    // that load starts, so a second StartNewAttempt would leave attempt 0 empty for good.
    [Fact]
    public void A_load_on_a_slot_with_no_session_starts_exactly_one_attempt()
    {
        SessionManager.LoadSlot(Slot);

        Assert.Equal(1, SessionManager.CurrentSession.AttemptCount);
    }

    // #17: the stash. A slot saved again, a clear of every slot, the hotkey and Enabled off all end
    // in SessionManager, and what they discard must be one call away from coming back.

    private const string OtherSlot = "SaveSlot@3";

    [Fact]
    public void Saving_over_a_slot_with_runs_stashes_the_replaced_session()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession before = SessionManager.CurrentSession;

        bool stashed = SessionManager.SaveSlot(Slot);

        Assert.True(stashed);
        Assert.True(SessionManager.HasStash);
        Assert.NotSame(before, SessionManager.CurrentSession);
        Assert.Equal(1, SessionManager.CurrentSession.AttemptCount);
    }

    [Fact]
    public void Saving_over_a_slot_without_runs_stashes_nothing()
    {
        SessionManager.SaveSlot(Slot);

        bool stashed = SessionManager.SaveSlot(Slot);

        Assert.False(stashed);
        Assert.False(SessionManager.HasStash);
    }

    [Fact]
    public void A_first_save_stashes_nothing()
    {
        Assert.False(SessionManager.SaveSlot(Slot));
        Assert.False(SessionManager.HasStash);
    }

    [Fact]
    public void Restoring_after_a_replaced_save_brings_the_old_session_back()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession before = SessionManager.CurrentSession;
        int attemptsBefore = before.AttemptCount;
        SessionManager.SaveSlot(Slot);

        bool restored = SessionManager.RestoreStash();

        Assert.True(restored);
        Assert.Same(before, SessionManager.CurrentSession);
        Assert.Equal(attemptsBefore, SessionManager.CurrentSession.AttemptCount);
        Assert.Equal(3, SessionManager.RoomCount);
        Assert.False(SessionManager.HasStash);
    }

    // The restored session sits in its slot: a load of that slot finds it and not a fresh one.
    [Fact]
    public void A_restored_session_is_found_by_a_load_of_its_slot()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession before = SessionManager.CurrentSession;
        SessionManager.SaveSlot(Slot);
        SessionManager.RestoreStash();

        SessionManager.LoadSlot(Slot);

        Assert.Same(before, SessionManager.CurrentSession);
    }

    [Fact]
    public void Clearing_every_slot_stashes_them_all_and_restore_brings_them_back()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession first = SessionManager.CurrentSession;
        SessionManager.SaveSlot(OtherSlot);
        RunThreeRoomsThenReload(OtherSlot);
        PracticeSession second = SessionManager.CurrentSession;

        SessionManager.ClearAll();

        Assert.Null(SessionManager.CurrentSession);
        Assert.Equal(0, SessionManager.RoomCount);
        Assert.True(SessionManager.HasStash);

        Assert.True(SessionManager.RestoreStash());

        Assert.Same(second, SessionManager.CurrentSession);
        SessionManager.LoadSlot(Slot);
        Assert.Same(first, SessionManager.CurrentSession);
        Assert.Equal(3, SessionManager.RoomCount);
    }

    [Fact]
    public void A_second_wipe_replaces_the_stash()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        SessionManager.ClearAll();
        SessionManager.SaveSlot(OtherSlot);
        SessionManager.CompleteRoom(100);
        PracticeSession newer = SessionManager.CurrentSession;

        SessionManager.ClearAll();
        SessionManager.RestoreStash();

        Assert.Same(newer, SessionManager.CurrentSession);
        // The first wipe's session is gone: its slot holds a fresh one on the next load.
        SessionManager.LoadSlot(Slot);
        Assert.Equal(1, SessionManager.CurrentSession.AttemptCount);
    }

    // The clear hotkey pressed twice: the second press finds nothing to discard.
    [Fact]
    public void A_clear_with_no_runs_keeps_the_stash()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession before = SessionManager.CurrentSession;
        SessionManager.ClearAll();
        SessionManager.ClearAll();

        SessionManager.RestoreStash();

        Assert.Same(before, SessionManager.CurrentSession);
    }

    [Fact]
    public void Restoring_with_no_stash_changes_nothing()
    {
        SessionManager.SaveSlot(Slot);
        PracticeSession current = SessionManager.CurrentSession;

        Assert.False(SessionManager.RestoreStash());
        Assert.Same(current, SessionManager.CurrentSession);
    }

    [Fact]
    public void Restoring_twice_restores_once()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        SessionManager.ClearAll();

        Assert.True(SessionManager.RestoreStash());
        Assert.False(SessionManager.RestoreStash());
    }

    [Fact]
    public void AnyRuns_is_true_only_while_a_slot_holds_a_run()
    {
        SessionManager.SaveSlot(Slot);
        Assert.False(SessionManager.AnyRuns);

        SessionManager.CompleteRoom(100);
        Assert.True(SessionManager.AnyRuns);

        SessionManager.ClearAll();
        Assert.False(SessionManager.AnyRuns);
    }

    // SpeedrunTool clears a slot just before it saves over it (ClearBeforeSave), so this, and not
    // SaveSlot's own replacement, is how a re-save reaches the mod in game.
    [Fact]
    public void A_re_save_as_SpeedrunTool_makes_it_stashes_the_session_and_restores_it()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession before = SessionManager.CurrentSession;
        int attemptsBefore = before.AttemptCount;

        SessionManager.ClearSlot(Slot);
        bool replaced = SessionManager.SaveSlot(Slot);

        Assert.True(replaced);
        Assert.True(SessionManager.HasStash);
        Assert.NotSame(before, SessionManager.CurrentSession);

        SessionManager.RestoreStash();

        Assert.Same(before, SessionManager.CurrentSession);
        Assert.Equal(attemptsBefore, SessionManager.CurrentSession.AttemptCount);
        Assert.Equal(3, SessionManager.RoomCount);
    }

    [Fact]
    public void A_re_save_over_a_slot_without_runs_is_not_a_replacement()
    {
        SessionManager.SaveSlot(Slot);

        SessionManager.ClearSlot(Slot);

        Assert.False(SessionManager.SaveSlot(Slot));
        Assert.False(SessionManager.HasStash);
    }

    // A save that follows a load, not a clear, is no replacement of the earlier clear's session.
    [Fact]
    public void A_load_ends_the_link_between_a_clear_and_the_next_save()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        SessionManager.ClearSlot(Slot);

        SessionManager.LoadSlot(Slot);

        Assert.False(SessionManager.SaveSlot(Slot));
    }

    [Fact]
    public void Restoring_a_cleared_slot_that_is_not_current_leaves_the_current_one()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession first = SessionManager.CurrentSession;
        SessionManager.SaveSlot(OtherSlot);
        PracticeSession second = SessionManager.CurrentSession;

        SessionManager.ClearSlot(Slot);
        SessionManager.RestoreStash();

        Assert.Same(second, SessionManager.CurrentSession);
        SessionManager.LoadSlot(Slot);
        Assert.Same(first, SessionManager.CurrentSession);
    }

    // The restored session replaces a newer one in the same slot, and that one is current.
    [Fact]
    public void Restoring_over_the_current_session_makes_the_restored_one_current()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession first = SessionManager.CurrentSession;
        SessionManager.ClearSlot(Slot);
        SessionManager.LoadSlot(Slot);

        SessionManager.RestoreStash();

        Assert.Same(first, SessionManager.CurrentSession);
    }

    // A stash only holds sessions of the level it was made in.

    [Fact]
    public void Entering_another_level_drops_the_stash_and_refuses_a_restore()
    {
        SessionManager.LevelName = "Level A";
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        SessionManager.ClearAll();
        Assert.True(SessionManager.HasStash);

        SessionManager.LevelName = "Level B";

        Assert.False(SessionManager.HasStash);
        Assert.False(SessionManager.RestoreStash());
        Assert.Null(SessionManager.CurrentSession);
    }

    [Fact]
    public void Entering_the_same_level_again_keeps_the_stash()
    {
        SessionManager.LevelName = "Level A";
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession before = SessionManager.CurrentSession;
        SessionManager.ClearAll();

        SessionManager.LevelName = "Level A";

        Assert.True(SessionManager.HasStash);
        Assert.True(SessionManager.RestoreStash());
        Assert.Same(before, SessionManager.CurrentSession);
    }

    // SpeedrunTool's scene switch into another level runs the mod's OnLoadLevel first and clears the
    // old level's slots after: the session is of level A while the manager already says B.
    [Fact]
    public void A_session_of_another_level_cleared_after_the_switch_is_not_stashed()
    {
        SessionManager.LevelName = "Level A";
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        SessionManager.LevelName = "Level B";

        SessionManager.ClearSlot(Slot);

        Assert.False(SessionManager.HasStash);
        Assert.False(SessionManager.RestoreStash());
    }

    // SpeedrunTool's scene-switch clear calls once per saved slot, in one frame (the same wipe).
    [Fact]
    public void Slot_clears_in_one_frame_are_one_wipe()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession first = SessionManager.CurrentSession;
        SessionManager.SaveSlot(OtherSlot);
        RunThreeRoomsThenReload(OtherSlot);
        PracticeSession second = SessionManager.CurrentSession;

        SessionManager.ClearSlot(Slot, frame: 40);
        SessionManager.ClearSlot(OtherSlot, frame: 40);
        SessionManager.RestoreStash();

        SessionManager.LoadSlot(Slot);
        Assert.Same(first, SessionManager.CurrentSession);
        SessionManager.LoadSlot(OtherSlot);
        Assert.Same(second, SessionManager.CurrentSession);
    }

    [Fact]
    public void Slot_clears_in_different_frames_are_two_wipes()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession first = SessionManager.CurrentSession;
        SessionManager.SaveSlot(OtherSlot);
        RunThreeRoomsThenReload(OtherSlot);
        PracticeSession second = SessionManager.CurrentSession;

        SessionManager.ClearSlot(Slot, frame: 40);
        SessionManager.ClearSlot(OtherSlot, frame: 41);
        SessionManager.RestoreStash();

        Assert.Same(second, SessionManager.CurrentSession);
        SessionManager.LoadSlot(Slot);
        Assert.NotSame(first, SessionManager.CurrentSession);
    }

    [Fact]
    public void Slot_clears_given_no_frame_never_gather()
    {
        SessionManager.SaveSlot(Slot);
        RunThreeRoomsThenReload();
        PracticeSession first = SessionManager.CurrentSession;
        SessionManager.SaveSlot(OtherSlot);
        RunThreeRoomsThenReload(OtherSlot);

        SessionManager.ClearSlot(Slot);
        SessionManager.ClearSlot(OtherSlot);
        SessionManager.RestoreStash();

        SessionManager.LoadSlot(Slot);
        Assert.NotSame(first, SessionManager.CurrentSession);
    }

    // ---------------------------------------------------------------- #15: real splits and end points

    // About one frame of room timer, in the ticks these tests use.
    private const long Frame = 17;

    // One call of SpeedrunTool's UpdateTimerState as the detour sees it: the room the player is in
    // and the room timer's running time.
    private void Call(string room, long time, bool endPoint = false)
    {
        _srt.RoomName = room;
        _srt.CurrentRoomTime = time;
        SessionManager.TimerStateUpdating(endPoint);
    }

    // SpeedrunTool calls UpdateTimerState on every frame while the player stands on an end point,
    // stands in the room-id end point's room, or after the level completed. Time keeps moving
    // unless the level completed.
    private void CallEveryFrame(string room, long from, int frames, bool endPoint = false, bool timeMoves = true)
    {
        for (int f = 1; f <= frames; f++)
            Call(room, timeMoves ? from + f * Frame : from, endPoint);
    }

    private void Save(string room = "1")
    {
        _srt.RoomName = room;
        _srt.CurrentRoomTime = 0;
        SessionManager.SaveSlot(Slot);
    }

    // BeforeLoad reads the abandoned attempt's time; the load restores the save's room and zeroes
    // the timer.
    private void Reload(string saveRoom = "1")
    {
        SessionManager.BeforeLoad(_srt.CurrentRoomTime);
        _srt.RoomName = saveRoom;
        _srt.CurrentRoomTime = 0;
        _srt.LevelCompleted = false;
        _srt.ReachedRoomIdEndPoint = false;
        SessionManager.LoadSlot(Slot);
    }

    // Rooms 1 to 4 under NextRoom: entering 2 records the unshown room 1, then R1 is 150 and R2 is 200.
    private void EnterRoomsTwoToFour()
    {
        Call("2", 100);
        Call("3", 250);
        Call("4", 450);
    }

    // The route to an end point entity in room 4, touched 50 into the room: R3 is 50, and the
    // segment is 400. Then a second of standing on it.
    private void RunToEndPointEntity()
    {
        EnterRoomsTwoToFour();
        Call("4", 500, endPoint: true);
        CallEveryFrame("4", 500, 60, endPoint: true);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(1)]
    public void An_end_point_ends_the_segment_whatever_NumberOfRooms_says(int numberOfRooms)
    {
        _srt.NumberOfRooms = numberOfRooms;
        _srt.EndPointExists = true;
        Save();
        RunToEndPointEntity();
        Reload();
        RunToEndPointEntity();
        Reload();

        Assert.Equal(3, SessionManager.RoomCount);
        Assert.Equal(2, SessionManager.CurrentSession.TotalCompleted);
        Assert.Equal(0, SessionManager.CurrentSession.TotalDnfs);
        Assert.Equal([400L, 400L], SessionManager.CurrentSession.GetSegmentTimes().Select(t => t.Ticks));
    }

    [Fact]
    public void Standing_on_an_end_point_records_one_room()
    {
        _srt.EndPointExists = true;
        Save();

        RunToEndPointEntity();

        Assert.Equal(4, SessionManager.CurrentSession.CurrentRoomIndex);
    }

    [Fact]
    public void A_reload_after_the_end_point_records_no_reset()
    {
        _srt.EndPointExists = true;
        Save();
        RunToEndPointEntity();

        Reload();

        // R4, the room after the end point's split, is where the reset would have gone.
        Assert.Equal(RoomCellState.NotReached, SessionManager.CurrentSession.GetCell(0, 3).State);
        Assert.Equal(0, SessionManager.CurrentSession.TotalDnfs);
    }

    // The entry into the room-id end point's room is the finishing split, and SpeedrunTool calls
    // again on every frame the player stays there.
    [Fact]
    public void A_room_id_end_point_finishes_on_entry_and_standing_there_records_nothing()
    {
        _srt.NumberOfRooms = 6;
        _srt.EndPointExists = true;
        Save();
        Call("2", 100);
        Call("3", 250);
        _srt.ReachedRoomIdEndPoint = true;
        Call("4", 450);
        CallEveryFrame("4", 450, 60);
        Assert.Equal(3, SessionManager.CurrentSession.CurrentRoomIndex);

        Reload();

        Assert.Equal(2, SessionManager.RoomCount);
        Assert.Equal(1, SessionManager.CurrentSession.TotalCompleted);
        Assert.Equal(0, SessionManager.CurrentSession.TotalDnfs);
    }

    // The chapter's last split happens in the room the player is in. Without an end point,
    // NumberOfRooms still clamps: SpeedrunTool's timer completes on whichever comes first.
    [Theory]
    [InlineData(9, 3)]
    [InlineData(2, 2)]
    public void A_level_end_finishes_the_attempt(int numberOfRooms, int roomCount)
    {
        _srt.NumberOfRooms = numberOfRooms;
        Save();
        for (int run = 0; run < 2; run++)
        {
            EnterRoomsTwoToFour();
            _srt.LevelCompleted = true;
            Call("4", 500);
            CallEveryFrame("4", 500, 60, timeMoves: false);
            Reload();
        }

        Assert.Equal(roomCount, SessionManager.RoomCount);
        Assert.Equal(2, SessionManager.CurrentSession.TotalCompleted);
        Assert.Equal(0, SessionManager.CurrentSession.TotalDnfs);
    }

    // #7: a mod that hides level.Completed from the timer but not from the manager makes the
    // per-frame calls with the clock still moving.
    [Fact]
    public void A_completed_level_records_one_room_while_the_clock_still_moves()
    {
        Save();
        EnterRoomsTwoToFour();
        _srt.LevelCompleted = true;

        CallEveryFrame("4", 450, 60);

        Assert.Equal(4, SessionManager.CurrentSession.CurrentRoomIndex);
    }

    [Fact]
    public void Running_past_the_end_point_records_the_later_rooms_outside_the_segment()
    {
        _srt.EndPointExists = true;
        Save();
        RunToEndPointEntity();
        Call("5", 1600);
        Call("6", 2000);
        Assert.Equal(6, SessionManager.CurrentSession.CurrentRoomIndex);

        Reload();

        Assert.Equal(3, SessionManager.RoomCount);
        Assert.Equal(1, SessionManager.CurrentSession.TotalCompleted);
        Assert.Equal(0, SessionManager.CurrentSession.TotalDnfs);
        Assert.Equal([400L], SessionManager.CurrentSession.GetSegmentTimes().Select(t => t.Ticks));
        Assert.Equal(RoomCellState.Completed, SessionManager.CurrentSession.GetCell(0, 4).State);
    }

    [Fact]
    public void Backtracking_into_an_earlier_room_is_a_room()
    {
        _srt.NumberOfRooms = 9;
        Save();

        Call("2", 100);
        Call("3", 250);
        Call("2", 400);

        Assert.Equal(3, SessionManager.CurrentSession.CurrentRoomIndex);
    }

    // #7: another mod's extra calls, made with no room change and no end of the timer.
    [Fact]
    public void Repeated_calls_in_one_room_record_nothing()
    {
        Save();
        Call("2", 100);

        CallEveryFrame("2", 100, 30);

        Assert.Equal(1, SessionManager.CurrentSession.CurrentRoomIndex);
    }

    // The last attempt ended in room 2, and the save is in room 1: the next entry into room 2 is
    // a room.
    [Fact]
    public void A_reload_starts_again_from_the_save_states_room()
    {
        Save();
        Call("2", 100);
        Reload();

        Call("2", 100);

        Assert.Equal(1, SessionManager.CurrentSession.CurrentRoomIndex);
    }

    // A route with one room fewer, run after the first: the segment is the route last finished.
    [Fact]
    public void With_an_end_point_the_most_recent_finished_attempt_sets_the_segment()
    {
        _srt.EndPointExists = true;
        Save();
        RunToEndPointEntity();
        Reload();
        Call("2", 100);
        Call("4", 300);
        Call("4", 350, endPoint: true);
        Reload();
        Assert.Equal(2, SessionManager.RoomCount);

        RunToEndPointEntity();
        Reload();

        Assert.Equal(3, SessionManager.RoomCount);
    }

    [Fact]
    public void Before_any_attempt_finishes_an_end_point_lifts_the_NumberOfRooms_clamp()
    {
        _srt.NumberOfRooms = 1;
        _srt.EndPointExists = true;
        Save();
        EnterRoomsTwoToFour();
        Reload();

        // R1 and R2 completed, R3 the reset.
        Assert.Equal(3, SessionManager.RoomCount);
        Assert.Equal(1, SessionManager.CurrentSession.TotalDnfs);
    }

    [Fact]
    public void The_room_count_follows_an_end_point_set_or_removed_before_any_update()
    {
        _srt.NumberOfRooms = 2;
        _srt.EndPointExists = true;
        Save();
        RunToEndPointEntity();
        Reload();
        Assert.Equal(3, SessionManager.RoomCount);

        _srt.EndPointExists = false;
        Assert.Equal(2, SessionManager.RoomCount);

        _srt.EndPointExists = true;
        Assert.Equal(3, SessionManager.RoomCount);
    }

    // Under NextRoom SpeedrunTool's timer has not started before the first room change, so an end
    // point touched there finishes nothing.
    [Fact]
    public void An_end_point_touched_before_the_first_room_change_under_NextRoom_finishes_nothing()
    {
        _srt.EndPointExists = true;
        Save();

        Call("1", 50, endPoint: true);
        CallEveryFrame("1", 50, 30, endPoint: true);
        Assert.Equal(0, SessionManager.CurrentSession.CurrentRoomIndex);

        Call("2", 600);
        Assert.Equal(1, SessionManager.CurrentSession.CurrentRoomIndex);
    }

    // The NumberOfRooms path is the one the earlier tests above pin through CompleteRoom; this is
    // the same session through the detour's calls.
    [Fact]
    public void Without_an_end_point_the_room_changes_record_as_before()
    {
        Save();
        EnterRoomsTwoToFour();
        Reload();
        EnterRoomsTwoToFour();
        Reload();

        Assert.Equal(3, SessionManager.RoomCount);
        Assert.Equal(2, SessionManager.CurrentSession.TotalAttempts);
        Assert.Equal(2, SessionManager.CurrentSession.TotalDnfs);
    }
}
