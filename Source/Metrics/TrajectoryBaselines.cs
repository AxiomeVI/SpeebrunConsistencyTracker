using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // What a run is measured from. A room's average and best are RoomStats.Of, the cells the overlay
    // reads. The best run is a real run, so it is the one baseline that needs an unbroken row.
    public static class TrajectoryBaselines
    {
        // Once per attempt, not once per attempt per room: ContiguousCount walks the row.
        public static int[] Contiguous(PracticeSession session)
        {
            int[] contiguous = new int[session.AttemptCount];
            for (int a = 0; a < contiguous.Length; a++) contiguous[a] = session.ContiguousCount(a);
            return contiguous;
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
