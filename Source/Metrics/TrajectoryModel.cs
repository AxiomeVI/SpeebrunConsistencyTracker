using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // deviation[r] = actualTime[r] - reference[r] (the room's average, or its best), so negative is faster and
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
        public long[] RoomReference { get; }   // per room, the time deviations are measured from: its average, or its best
        public int[]  BestSoFarIdx { get; }   // per-room index into Attempts of the best run but the last, -1 if none
        // The baseline in effect: the average when the best run was asked for and no run is complete.
        public TrajectoryBaseline Baseline { get; }

        private TrajectoryModel(
            int totalRooms,
            IReadOnlyList<AttemptLine> attempts,
            AttemptLine sobLine,
            long[] roomAverages,
            int[] bestSoFarIdx,
            TrajectoryBaseline baseline)
        {
            TotalRooms   = totalRooms;
            Attempts     = attempts;
            SobLine      = sobLine;
            RoomReference = roomAverages;
            BestSoFarIdx = bestSoFarIdx;
            Baseline     = baseline;
        }

        // wanted: what the deviations are measured from. Against the sum of bests no run is ever
        // ahead of the flat line; against the best run, that run is the flat line.
        public static TrajectoryModel Build(PracticeSession session, int totalRooms, TrajectoryBaseline wanted = TrajectoryBaseline.Average)
        {
            int attemptCount = session.AttemptCount;
            if (attemptCount == 0 || totalRooms == 0)
                return new TrajectoryModel(totalRooms, [], new AttemptLine([], [], 0, 0), [], [], TrajectoryBaseline.Average);

            int[] contiguous = TrajectoryBaselines.Contiguous(session);
            RoomStats stats = TrajectoryBaselines.Stats(session, contiguous, totalRooms);
            long[] roomBests   = stats.Bests;
            bool[] roomHasTime = stats.HasTime;

            int bestRun = wanted == TrajectoryBaseline.BestRun ? TrajectoryBaselines.BestRun(session, contiguous, totalRooms) : -1;
            TrajectoryBaseline baseline = wanted == TrajectoryBaseline.BestRun && bestRun < 0 ? TrajectoryBaseline.Average : wanted;
            long[] reference = baseline switch
            {
                TrajectoryBaseline.SumOfBest => roomBests,
                TrajectoryBaseline.BestRun   => TrajectoryBaselines.TimesOf(session, bestRun, totalRooms),
                _                            => stats.Averages,
            };

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
                    cumulative += t - reference[r];
                    deviations.Add(cumulative);
                }
                attempts.Add(new AttemptLine([.. deviations], [.. roomTks], deviations.Count, a + 1));
            }

            // "vs Best Split" compares a run against every other run: here the last one, the default.
            // The comparison table asks again for the run that is pinned -- see BestAmongOthers.
            int[] bestSoFarIdx = BestAmongOthers(attempts, totalRooms, attempts.Count - 1);

            long sobCumulative     = 0;
            var  sobDeviations     = new long[totalRooms];
            var  sobRoomTimes      = new long[totalRooms];
            int  sobRoomsCompleted = 0;
            for (int r = 0; r < totalRooms; r++)
            {
                if (!roomHasTime[r]) break;
                sobRoomTimes[r]  = roomBests[r];
                sobCumulative   += roomBests[r] - reference[r];
                sobDeviations[r] = sobCumulative;
                sobRoomsCompleted = r + 1;
            }
            var sobLine = new AttemptLine(sobDeviations, sobRoomTimes[..sobRoomsCompleted], sobRoomsCompleted, 0);

            return new TrajectoryModel(totalRooms, attempts, sobLine, reference, bestSoFarIdx, baseline);
        }

        // Per room, the index of the attempt with the lowest cumulative deviation among all the
        // attempts but `excluded`; -1 where none of them reached that room. The pinned run must not
        // be its own reference: it would lead against itself. Runs made after it do count, so the
        // first run of a session is compared with the ones that followed. An index that names no
        // attempt excludes nothing: the sum of bests and the average are compared with every run.
        public int[] BestAmongOthers(int excluded)
            => BestAmongOthers(Attempts, TotalRooms, excluded);

        private static int[] BestAmongOthers(IReadOnlyList<AttemptLine> attempts, int totalRooms, int excluded)
        {
            int limit = attempts.Count;
            int[] best = new int[totalRooms];
            for (int r = 0; r < totalRooms; r++)
            {
                int bestI = -1;
                long bestDev = long.MaxValue;
                for (int i = 0; i < limit; i++)
                {
                    if (i == excluded || attempts[i].RoomsCompleted <= r) continue;
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
