using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace SpeebrunConsistencyTracker.UnitTests;

// Shared session builder for the trajectory tests. PracticeSession opens attempt 0 in its own
// constructor, so each row below fills the attempt that is already open and then opens the next;
// the trailing empty attempt completes no room and TrajectoryModel drops it.
internal static class TrajectorySessions
{
    public static PracticeSession Of(int roomCount, params long[][] attempts)
    {
        PracticeSession session = new(new StubSegmentShape(roomCount: roomCount));
        foreach (long[] rooms in attempts)
        {
            foreach (long ticks in rooms) session.CompleteRoom(new TimeTicks(ticks));
            session.StartNewAttempt();
        }
        return session;
    }

    public static bool[] AllVisible(int roomCount) => new bool[roomCount];

    public static bool[] Hiding(int roomCount, params int[] hiddenRooms)
    {
        bool[] hidden = new bool[roomCount];
        foreach (int r in hiddenRooms) hidden[r] = true;
        return hidden;
    }
}
