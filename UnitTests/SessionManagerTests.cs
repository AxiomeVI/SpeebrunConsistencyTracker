using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// SessionManager is static, and so is Srt.Tool: no other test class may touch either, or xUnit's
// parallel classes race on them.
public sealed class SessionManagerTests : IDisposable
{
    private const string Slot = "SaveSlot@2";

    private readonly ISpeedrunTool _previous = Srt.Tool;
    private readonly StubSpeedrunTool _srt = new() { TimerType = RoomTimerMode.NextRoom, NumberOfRooms = 6 };

    public SessionManagerTests()
    {
        Srt.Tool = _srt;
        SessionManager.ClearAll();
    }

    public void Dispose()
    {
        SessionManager.ClearAll();
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
}
