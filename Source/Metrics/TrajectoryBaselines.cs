using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // Per room: the mean and the best of the times read, and whether any was.
    public sealed record RoomStats(long[] Averages, long[] Bests, bool[] HasTime);

    // What a run is measured from, computed from a session with one run left out or none: the full
    // chart leaves none out, the widget leaves out the run in progress so it cannot move its own zero.
    public static class TrajectoryBaselines
    {
        // Once per attempt, not once per attempt per room: ContiguousCount walks the row.
        public static int[] Contiguous(PracticeSession session)
        {
            int[] contiguous = new int[session.AttemptCount];
            for (int a = 0; a < contiguous.Length; a++) contiguous[a] = session.ContiguousCount(a);
            return contiguous;
        }

        // Reads the cells inside an attempt's contiguous prefix only. GetRoomTimes also returns
        // cells sitting after a deleted one: read from it, a room could average 0 and still offer
        // a best, and the SoB line would jump a whole room time at that column.
        public static RoomStats Stats(PracticeSession session, int[] contiguous, int totalRooms, int excluded = -1)
        {
            long[] averages = new long[totalRooms];
            long[] bests    = new long[totalRooms];
            bool[] hasTime  = new bool[totalRooms];
            for (int r = 0; r < totalRooms; r++)
            {
                long sum = 0, best = long.MaxValue;
                int count = 0;
                for (int a = 0; a < contiguous.Length; a++)
                {
                    if (a == excluded || contiguous[a] <= r) continue;
                    long t = session.GetCell(a, r).Time.Ticks;
                    sum += t;
                    count++;
                    if (t < best) best = t;
                }
                hasTime[r]  = count > 0;
                averages[r] = count == 0 ? 0L : sum / count;
                bests[r]    = count == 0 ? 0L : best;
            }
            return new RoomStats(averages, bests, hasTime);
        }

        // The fastest run that completed every room, the earliest of equals; -1 when there is none.
        public static int BestRun(PracticeSession session, int[] contiguous, int totalRooms, int excluded = -1)
        {
            if (totalRooms == 0) return -1;
            int best = -1;
            long bestTime = long.MaxValue;
            for (int a = 0; a < contiguous.Length; a++)
            {
                if (a == excluded || contiguous[a] < totalRooms) continue;
                long sum = 0;
                for (int r = 0; r < totalRooms; r++) sum += session.GetCell(a, r).Time.Ticks;
                if (sum < bestTime) (best, bestTime) = (a, sum);
            }
            return best;
        }

        // A complete run's room times.
        public static long[] TimesOf(PracticeSession session, int attempt, int totalRooms)
        {
            long[] times = new long[totalRooms];
            for (int r = 0; r < totalRooms; r++) times[r] = session.GetCell(attempt, r).Time.Ticks;
            return times;
        }
    }
}
