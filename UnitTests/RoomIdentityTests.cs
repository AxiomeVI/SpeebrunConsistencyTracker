using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Charts;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// A hidden column and a pin must name a room, not a position: the chart under them is rebuilt with
// a column more or fewer, and a Delete acts on what the pin names (issue #27).
public class RoomIdentityTests
{
    private static List<List<int>> Rooms(int rooms, int runs)
    {
        List<List<int>> times = [];
        for (int r = 0; r < rooms; r++)
        {
            List<int> room = [];
            for (int i = 0; i < runs; i++) room.Add(1000 + i);
            times.Add(room);
        }
        return times;
    }

    // Hide R5, then delete every R3 run: R3's column goes, and R5 must stay hidden, not R6.
    [Fact]
    public void A_hidden_room_stays_hidden_when_an_earlier_room_loses_its_column()
    {
        List<List<int>> times = Rooms(6, 3);
        List<int> before = RoomColumns.WithTimes(times);
        HashSet<int> hidden = [RoomColumns.RoomOf(before, 4)];

        times[2].Clear();
        List<int> after = RoomColumns.WithTimes(times);
        bool[] flags = RoomColumns.HiddenFlags(hidden, after, after.Count + 1);

        // R1, R2, R4, R5, R6, then the segment.
        Assert.Equal([false, false, false, true, false, false], flags);
    }

    [Fact]
    public void A_hidden_room_with_no_column_hides_nothing_and_comes_back_hidden()
    {
        List<List<int>> times = Rooms(4, 2);
        HashSet<int> hidden = [RoomColumns.RoomOf(RoomColumns.WithTimes(times), 1)];

        List<int> emptied = RoomColumns.WithTimes([times[0], [], times[2], times[3]]);
        Assert.Equal([false, false, false, false], RoomColumns.HiddenFlags(hidden, emptied, emptied.Count + 1));

        List<int> back = RoomColumns.WithTimes(times);
        Assert.Equal([false, true, false, false, false], RoomColumns.HiddenFlags(hidden, back, back.Count + 1));
    }

    [Fact]
    public void The_segment_column_names_no_room()
    {
        List<int> columns = RoomColumns.WithTimes(Rooms(3, 1));
        Assert.Equal(-1, RoomColumns.RoomOf(columns, 3));
    }

    [Fact]
    public void The_segment_column_is_never_hidden()
    {
        List<int> columns = RoomColumns.WithTimes(Rooms(3, 1));
        // Room 3 does not exist in a three-room chart: its index is the segment's column.
        bool[] flags = RoomColumns.HiddenFlags(new HashSet<int> { 3 }, columns, columns.Count + 1);
        Assert.Equal([false, false, false, false], flags);
    }
}

// The box plot's segment stands after the rooms: its pin must not name a room's box once the room
// count moves under an open chart, nor a room's pin the segment.
public class BoxPinKeyTests
{
    [Fact]
    public void The_segment_pin_names_the_segment_after_a_room_is_added()
    {
        string key = BoxChart.KeyOf(column: 5, rooms: 5);
        Assert.Equal(6, BoxChart.ColumnOf(key, rooms: 6));
    }

    [Fact]
    public void The_segment_pin_names_the_segment_after_a_room_is_dropped()
    {
        string key = BoxChart.KeyOf(column: 5, rooms: 5);
        Assert.Equal(4, BoxChart.ColumnOf(key, rooms: 4));
    }

    [Fact]
    public void A_room_pin_does_not_name_the_segment_once_its_room_is_gone()
    {
        string key = BoxChart.KeyOf(column: 4, rooms: 6);
        Assert.Equal(-1, BoxChart.ColumnOf(key, rooms: 4));
    }

    [Fact]
    public void A_room_pin_names_its_room_whatever_the_room_count()
    {
        string key = BoxChart.KeyOf(column: 2, rooms: 4);
        Assert.Equal(2, BoxChart.ColumnOf(key, rooms: 4));
        Assert.Equal(2, BoxChart.ColumnOf(key, rooms: 7));
    }

    [Fact]
    public void Another_charts_key_names_no_box()
    {
        Assert.Equal(-1, BoxChart.ColumnOf("bar:2", rooms: 4));
        Assert.Equal(-1, BoxChart.ColumnOf(null, rooms: 4));
    }
}

// A pin and a hidden column name a visible room index, and PracticeSession adds the live
// StartRoomIndex to it. A switch of SpeedrunTool's room timer type under an open chart moves that
// index: what the player hid or pinned is then dropped, not carried to the room beside it.
public class RenumberedChartTests
{
    private const int NextRoom = 1, CurrentRoom = 2;

    private sealed class Chart : BaseChartOverlay
    {
        public Chart() : base("test") { }
        public override void Render() { }
        internal override ChartHelp Help => default;
        public IReadOnlyCollection<int> Hidden => _hiddenRooms;
    }

    private static ChartCache<Chart> Cache() =>
        new(_ => new Chart(), static (previous, fresh, renumbered) => fresh.Replace(previous, renumbered));

    [Fact]
    public void A_move_of_the_first_room_drops_the_hidden_rooms_and_the_pins()
    {
        ChartCache<Chart> cache = Cache();
        Chart before = cache.Get(1, 5, NextRoom, curStartRoomIndex: 1);
        before.ToggleColumn(2);

        Chart after = cache.Get(1, 5, CurrentRoom, curStartRoomIndex: 0);

        Assert.NotSame(before, after);
        Assert.Empty(after.Hidden);
        Assert.True(after.TakeDroppedPins());
    }

    [Fact]
    public void A_new_room_time_keeps_the_hidden_rooms_and_the_pins()
    {
        ChartCache<Chart> cache = Cache();
        Chart before = cache.Get(1, 5, NextRoom, curStartRoomIndex: 1);
        before.ToggleColumn(2);

        Chart after = cache.Get(2, 5, NextRoom, curStartRoomIndex: 1);

        Assert.NotSame(before, after);
        Assert.Equal([2], after.Hidden);
        Assert.False(after.TakeDroppedPins());
    }

    // Off and NextRoom both start the segment at raw room 1: the rooms keep their numbers.
    [Fact]
    public void A_timer_type_switch_that_keeps_the_first_room_keeps_the_view()
    {
        const int Off = 0;
        ChartCache<Chart> cache = Cache();
        Chart before = cache.Get(1, 5, NextRoom, curStartRoomIndex: 1);
        before.ToggleColumn(3);

        Chart after = cache.Get(1, 5, Off, curStartRoomIndex: 1);

        Assert.NotSame(before, after);
        Assert.Equal([3], after.Hidden);
        Assert.False(after.TakeDroppedPins());
    }
}
