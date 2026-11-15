using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// Which room each column of the scatter shows: a room with no time has no column, and a delete
// can empty one. What the player hides is kept as the room, so a chart rebuilt with a column fewer
// still hides the room the player chose.
internal static class RoomColumns
{
    // The rooms that have a column, in order: those with at least one time.
    public static List<int> WithTimes<T>(IReadOnlyList<List<T>> rooms) =>
        [.. Enumerable.Range(0, rooms.Count).Where(i => rooms[i].Count > 0)];

    // -1 past the rooms' columns: the segment's is not a room.
    public static int RoomOf(IReadOnlyList<int> roomOfColumn, int column) =>
        column >= 0 && column < roomOfColumn.Count ? roomOfColumn[column] : -1;

    // One flag per column, from the hidden rooms. A column past the rooms' own, the segment's, is
    // never hidden.
    public static bool[] HiddenFlags(IReadOnlySet<int> hiddenRooms, IReadOnlyList<int> roomOfColumn, int count)
    {
        bool[] hidden = new bool[count];
        for (int c = 0; c < count && c < roomOfColumn.Count; c++)
            hidden[c] = hiddenRooms.Contains(roomOfColumn[c]);
        return hidden;
    }
}
