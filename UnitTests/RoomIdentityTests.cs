using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Charts;
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
