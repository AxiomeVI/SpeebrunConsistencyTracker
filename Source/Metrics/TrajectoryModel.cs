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

            long[] roomAverages = [.. Enumerable.Range(0, totalRooms).Select(r =>
            {
                var times = new List<long>();
                for (int a = 0; a < attemptCount; a++)
                {
                    if (session.ContiguousCount(a) > r)
                    {
                        var cell = session.GetCell(a, r);
                        if (cell.HasTime) times.Add(cell.Time.Ticks);
                    }
                }
                return times.Count == 0 ? 0L : (long)times.Average();
            })];

            List<AttemptLine> attempts = [];
            for (int a = 0; a < attemptCount; a++)
            {
                int contiguous = session.ContiguousCount(a);
                if (contiguous == 0) continue;
                long cumulative = 0;
                var deviations = new List<long>();
                var roomTks = new List<long>();
                for (int r = 0; r < contiguous && r < totalRooms; r++)
                {
                    long t = session.GetCell(a, r).Time.Ticks; // safe: ContiguousCount guarantees all cells 0..contiguous-1 are Completed
                    roomTks.Add(t);
                    cumulative += t - roomAverages[r];
                    deviations.Add(cumulative);
                }
                attempts.Add(new AttemptLine([.. deviations], [.. roomTks], deviations.Count, a + 1));
            }

            int[] bestSoFarIdx = new int[totalRooms];
            for (int r = 0; r < totalRooms; r++)
            {
                int bestI = -1;
                long bestDev = long.MaxValue;
                for (int i = 0; i < attempts.Count - 1; i++) // "vs Best Split" compares against prior runs only
                {
                    if (attempts[i].RoomsCompleted <= r) continue;
                    if (attempts[i].CumulativeDeviations[r] < bestDev)
                    {
                        bestDev = attempts[i].CumulativeDeviations[r];
                        bestI   = i;
                    }
                }
                bestSoFarIdx[r] = bestI; // -1 if no attempt reaches r
            }

            long sobCumulative     = 0;
            var  sobDeviations     = new long[totalRooms];
            var  sobRoomTimes      = new long[totalRooms];
            int  sobRoomsCompleted = 0;
            for (int r = 0; r < totalRooms; r++)
            {
                var times = session.GetRoomTimes(r).ToList();
                if (times.Count == 0) break;
                long best        = times.Min(t => t.Ticks);
                sobRoomTimes[r]  = best;
                sobCumulative   += best - roomAverages[r];
                sobDeviations[r] = sobCumulative;
                sobRoomsCompleted = r + 1;
            }
            var sobLine = new AttemptLine(sobDeviations, sobRoomTimes[..sobRoomsCompleted], sobRoomsCompleted, 0);

            return new TrajectoryModel(totalRooms, attempts, sobLine, roomAverages, bestSoFarIdx);
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
