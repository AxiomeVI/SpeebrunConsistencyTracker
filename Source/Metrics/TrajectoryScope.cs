using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // SoB, Best and Last are three logical lines that may be the same attempt. The four cases
    // are named once here; the line drawing, the legend and the right-axis labels switch on
    // Coincidence rather than re-deriving the predicate.
    public enum LineCoincidence { None, SobIsBest, LastIsBest, AllThree }

    // Everything about a TrajectoryModel that depends on which columns are hidden. Every value
    // below is a function of one input — the last visible room — which is why the overlay may
    // guard the recompute on that number alone. Scattering these across collaborators would make
    // that guard a claim nobody can check locally, so they move together or not at all.
    public sealed class TrajectoryScope
    {
        private readonly TrajectoryModel _model;

        public TrajectoryScope(TrajectoryModel model) => _model = model;

        public int  LastVisibleRoom { get; private set; } = -1;
        public int  BestIdx         { get; private set; } = -1;   // index into Attempts of the best attempt up to LastVisibleRoom
        public bool LastIsBest      { get; private set; }         // BestIdx == Attempts.Count - 1
        public bool SobIsBest       { get; private set; }         // SoB dev == best dev at LastVisibleRoom
        public bool AnyCompleted    { get; private set; }         // any attempt reaches beyond LastVisibleRoom
        public bool SobReachesEnd   { get; private set; }         // SoB reaches beyond LastVisibleRoom
        public bool LastReachesEnd  { get; private set; }         // Attempts[^1] reaches beyond LastVisibleRoom

        public long RoomAveragesSum      { get; private set; }     // sum of RoomAverages[0..LastVisibleRoom] inclusive
        // Floored at one frame, not one tick: with a single run every deviation is 0, and half a
        // dozen axis labels all read +-0.000 because the whole axis spanned two ticks.
        internal const long MinHalfRange = 170_000L;

        public long MaxUpwardDeviation   { get; private set; } = MinHalfRange; // max magnitude of negative cumulative dev up to LastVisibleRoom
        public long MaxDownwardDeviation { get; private set; } = MinHalfRange; // max magnitude of positive cumulative dev up to LastVisibleRoom
        public long TotalRange           { get; private set; } = MinHalfRange * 2; // MaxUpwardDeviation + MaxDownwardDeviation

        public LineCoincidence Coincidence =>
            SobIsBest && LastIsBest ? LineCoincidence.AllThree
            : SobIsBest             ? LineCoincidence.SobIsBest
            : LastIsBest            ? LineCoincidence.LastIsBest
            : LineCoincidence.None;

        // hiddenColumns is indexed by room and has TotalRooms entries.
        public void Recompute(bool[] hiddenColumns)
        {
            IReadOnlyList<AttemptLine> attempts = _model.Attempts;

            int lastVis = -1;
            for (int r = _model.TotalRooms - 1; r >= 0; r--)
                if (!hiddenColumns[r]) { lastVis = r; break; }
            LastVisibleRoom = lastVis;

            if (lastVis < 0 || attempts.Count == 0)
            {
                BestIdx              = attempts.Count > 0 ? attempts.Count - 1 : -1;
                LastIsBest           = BestIdx == attempts.Count - 1;
                SobIsBest            = false;
                AnyCompleted         = false;
                SobReachesEnd        = false;
                LastReachesEnd       = false;
                RoomAveragesSum      = 0;
                MaxUpwardDeviation   = MinHalfRange;
                MaxDownwardDeviation = MinHalfRange;
                TotalRange           = MinHalfRange * 2;
                return;
            }

            // Best = lowest cumulative deviation at lastVis among attempts that reached it;
            // failing that, the furthest reached, ties broken by lowest final deviation.
            int best;
            {
                var reached = Enumerable.Range(0, attempts.Count)
                    .Where(i => attempts[i].RoomsCompleted > lastVis)
                    .ToList();
                if (reached.Count > 0)
                {
                    best = reached.OrderBy(i => attempts[i].CumulativeDeviations[lastVis]).First();
                }
                else
                {
                    best = Enumerable.Range(0, attempts.Count)
                        .OrderByDescending(i => attempts[i].RoomsCompleted)
                        .ThenBy(i => attempts[i].RoomsCompleted > 0 ? attempts[i].CumulativeDeviations[attempts[i].RoomsCompleted - 1] : 0)
                        .First();
                }
            }
            BestIdx    = best;
            LastIsBest = BestIdx == attempts.Count - 1;

            long bestDev = TrajectoryModel.DevAtRoom(attempts[BestIdx],  lastVis);
            long sobDev  = TrajectoryModel.DevAtRoom(_model.SobLine,     lastVis);
            SobIsBest = sobDev == bestDev;

            AnyCompleted   = attempts.Any(a => a.RoomsCompleted > lastVis);
            SobReachesEnd  = _model.SobLine.RoomsCompleted > lastVis;
            LastReachesEnd = attempts[^1].RoomsCompleted > lastVis;

            RoomAveragesSum = 0;
            for (int r = 0; r <= lastVis; r++) RoomAveragesSum += _model.RoomAverages[r];

            // Hidden rooms in the middle still contribute cumulative deviation; rooms beyond
            // lastVis do not.
            long maxUp   = MinHalfRange;
            long maxDown = MinHalfRange;
            foreach (var attempt in attempts)
            {
                int limit = Math.Min(attempt.RoomsCompleted - 1, lastVis);
                for (int r = 0; r <= limit; r++)
                {
                    long d = attempt.CumulativeDeviations[r];
                    if (d < 0) maxUp   = Math.Max(maxUp,  -d);
                    else       maxDown = Math.Max(maxDown,  d);
                }
            }
            {
                int limit = Math.Min(_model.SobLine.RoomsCompleted - 1, lastVis);
                for (int r = 0; r <= limit; r++)
                {
                    long d = _model.SobLine.CumulativeDeviations[r];
                    if (d < 0) maxUp   = Math.Max(maxUp,  -d);
                    else       maxDown = Math.Max(maxDown,  d);
                }
            }
            MaxUpwardDeviation   = maxUp;
            MaxDownwardDeviation = maxDown;
            TotalRange           = maxUp + maxDown;
        }
    }
}
