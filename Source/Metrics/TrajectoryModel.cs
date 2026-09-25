using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // deviation[r] = actualTime[r] - roomAverage[r], so negative is faster than average and
    // draws UP. Cumulative deviation is the running sum across rooms.
    public sealed record AttemptLine(
        long[] CumulativeDeviations,
        long[] RoomTimes,            // actual room ticks per room (length = RoomsCompleted)
        int    RoomsCompleted,
        int    ChronologicalIndex);  // 1-based

    // The derived dataset behind the Run Trajectory chart. Unlike every other overlay, that
    // chart draws nothing it was given: per-room averages, one cumulative deviation series per
    // attempt, a synthetic sum-of-best line and the best-so-far index per room are all computed
    // here. Built once per overlay, from PracticeSession alone — no drawing surface, no settings.
    public sealed class TrajectoryModel
    {
        public int TotalRooms { get; }

        // Attempts in chronological order; Attempts[^1] is always the last one. Attempts that
        // completed no room are dropped, so an index here is not a session attempt index —
        // ChronologicalIndex carries the session's numbering.
        public IReadOnlyList<AttemptLine> Attempts { get; }

        public AttemptLine SobLine { get; }
        public long[] RoomAverages { get; }   // per-room average ticks
        public int[]  BestSoFarIdx { get; }   // per-room index into Attempts of the best-so-far run, -1 if none

        private TrajectoryModel(
            int totalRooms,
            IReadOnlyList<AttemptLine> attempts,
            AttemptLine sobLine,
            long[] roomAverages,
            int[] bestSoFarIdx)
        {
            TotalRooms   = totalRooms;
            Attempts     = attempts;
            SobLine      = sobLine;
            RoomAverages = roomAverages;
            BestSoFarIdx = bestSoFarIdx;
        }

        public static TrajectoryModel Build(PracticeSession session, int totalRooms)
        {
            int attemptCount = session.AttemptCount;
            if (attemptCount == 0 || totalRooms == 0)
                return new TrajectoryModel(totalRooms, [], new AttemptLine([], [], 0, 0), [], []);

            // Once per attempt, not once per attempt per room: ContiguousCount walks the row.
            int[] contiguous = new int[attemptCount];
            for (int a = 0; a < attemptCount; a++) contiguous[a] = session.ContiguousCount(a);

            // The averages and the Sum of Bests read the same cells -- those inside an attempt's
            // contiguous prefix. SoB used to read GetRoomTimes, which also returns cells sitting
            // after a deleted one, so a room could average 0 and still offer a best, and the SoB
            // line jumped a whole room time at that column.
            long[] roomAverages = new long[totalRooms];
            long[] roomBests    = new long[totalRooms];
            bool[] roomHasTime  = new bool[totalRooms];
            for (int r = 0; r < totalRooms; r++)
            {
                long sum = 0, best = long.MaxValue;
                int count = 0;
                for (int a = 0; a < attemptCount; a++)
                {
                    if (contiguous[a] <= r) continue;
                    long t = session.GetCell(a, r).Time.Ticks;
                    sum += t;
                    count++;
                    if (t < best) best = t;
                }
                roomHasTime[r]  = count > 0;
                roomAverages[r] = count == 0 ? 0L : sum / count;
                roomBests[r]    = count == 0 ? 0L : best;
            }

            List<AttemptLine> attempts = [];
            for (int a = 0; a < attemptCount; a++)
            {
                if (contiguous[a] == 0) continue;
                long cumulative = 0;
                var deviations = new List<long>();
                var roomTks = new List<long>();
                for (int r = 0; r < contiguous[a] && r < totalRooms; r++)
                {
                    long t = session.GetCell(a, r).Time.Ticks; // safe: ContiguousCount guarantees all cells 0..contiguous-1 are Completed
                    roomTks.Add(t);
                    cumulative += t - roomAverages[r];
                    deviations.Add(cumulative);
                }
                attempts.Add(new AttemptLine([.. deviations], [.. roomTks], deviations.Count, a + 1));
            }

            // "vs Best Split" compares against prior runs only, so the default reference set stops
            // one short of the end. The comparison table narrows it further when an older run is
            // pinned -- see BestSoFarBefore.
            int[] bestSoFarIdx = BestSoFarBefore(attempts, totalRooms, attempts.Count - 1);

            long sobCumulative     = 0;
            var  sobDeviations     = new long[totalRooms];
            var  sobRoomTimes      = new long[totalRooms];
            int  sobRoomsCompleted = 0;
            for (int r = 0; r < totalRooms; r++)
            {
                if (!roomHasTime[r]) break;
                sobRoomTimes[r]  = roomBests[r];
                sobCumulative   += roomBests[r] - roomAverages[r];
                sobDeviations[r] = sobCumulative;
                sobRoomsCompleted = r + 1;
            }
            var sobLine = new AttemptLine(sobDeviations, sobRoomTimes[..sobRoomsCompleted], sobRoomsCompleted, 0);

            return new TrajectoryModel(totalRooms, attempts, sobLine, roomAverages, bestSoFarIdx);
        }

        // Per room, the index of the attempt with the lowest cumulative deviation among the
        // attempts before `beforeIndex`; -1 where none of them reached that room. The pinned run
        // must not be its own reference, and neither may the runs that came after it.
        public int[] BestSoFarBefore(int beforeIndex)
            => BestSoFarBefore(Attempts, TotalRooms, beforeIndex);

        private static int[] BestSoFarBefore(IReadOnlyList<AttemptLine> attempts, int totalRooms, int beforeIndex)
        {
            int limit = Math.Min(beforeIndex, attempts.Count);
            int[] best = new int[totalRooms];
            for (int r = 0; r < totalRooms; r++)
            {
                int bestI = -1;
                long bestDev = long.MaxValue;
                for (int i = 0; i < limit; i++)
                {
                    if (attempts[i].RoomsCompleted <= r) continue;
                    if (attempts[i].CumulativeDeviations[r] < bestDev)
                    {
                        bestDev = attempts[i].CumulativeDeviations[r];
                        bestI   = i;
                    }
                }
                best[r] = bestI;
            }
            return best;
        }

        // A line that stopped short of `room` keeps the deviation it ended on, so the right-axis
        // labels and the best/SoB comparison read a value for every line at any visible room.
        public static long DevAtRoom(AttemptLine line, int room)
        {
            if (line.RoomsCompleted == 0) return 0;
            int idx = Math.Min(line.RoomsCompleted - 1, room);
            return line.CumulativeDeviations[idx];
        }
    }
}
