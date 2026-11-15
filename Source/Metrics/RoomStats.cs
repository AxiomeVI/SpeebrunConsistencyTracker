using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // Per room: the mean and the best of the times read, and whether any was.
    public sealed record RoomStats(long[] Averages, long[] Bests, bool[] HasTime)
    {
        // The one definition of a room's best and average, for the sum of bests of the overlay and the
        // CSV and for the Run Trajectory's baselines: every completed cell of that room, whatever
        // happened earlier in the attempt. A deleted cell does not make that run's later rooms less real.
        // `excluded` leaves one attempt out (the widget leaves out the run in progress so it cannot move
        // its own zero). A room with no time has no average and no best, and HasTime says so: the
        // trajectory ends its sum-of-bests line there.
        public static RoomStats Of(PracticeSession session, int totalRooms, int excluded = -1)
        {
            long[] averages = new long[totalRooms];
            long[] bests    = new long[totalRooms];
            bool[] hasTime  = new bool[totalRooms];
            for (int r = 0; r < totalRooms; r++)
            {
                long sum = 0, best = long.MaxValue;
                int count = 0;
                for (int a = 0; a < session.AttemptCount; a++)
                {
                    if (a == excluded) continue;
                    var cell = session.GetCell(a, r);
                    if (!cell.HasTime) continue;
                    long t = cell.Time.Ticks;
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
    }
}
