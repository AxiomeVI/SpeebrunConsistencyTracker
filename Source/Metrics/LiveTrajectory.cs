using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    // The run in progress, as the widget draws it: its cumulative deviation at the end of each room
    // it has finished. HasBaseline is false while no earlier run gives anything to measure from.
    // Baseline is the one in effect, as TrajectoryModel.Baseline is.
    public sealed record LiveLine(long[] CumulativeDeviations, int TotalRooms, bool HasBaseline, TrajectoryBaseline Baseline);

    public static class LiveTrajectory
    {
        // The baseline leaves the run in progress out: with it in, the run would move its own
        // zero line, and a new best room would read as equal to the best and never as a gain.
        public static LiveLine Build(PracticeSession session, int totalRooms, TrajectoryBaseline wanted)
        {
            int current = session.CurrentAttemptIndex;
            if (current < 0 || current >= session.AttemptCount || totalRooms == 0)
                return new LiveLine([], totalRooms, false, TrajectoryBaseline.Average);

            int[] contiguous = TrajectoryBaselines.Contiguous(session);
            int bestRun = wanted == TrajectoryBaseline.BestRun
                ? TrajectoryBaselines.BestRun(session, contiguous, totalRooms, excluded: current) : -1;
            TrajectoryBaseline baseline = wanted == TrajectoryBaseline.BestRun && bestRun < 0 ? TrajectoryBaseline.Average : wanted;

            long[] reference;
            int measured;   // the leading rooms that have a reference
            if (baseline == TrajectoryBaseline.BestRun)
            {
                reference = TrajectoryBaselines.TimesOf(session, bestRun, totalRooms);
                measured  = totalRooms;
            }
            else
            {
                RoomStats stats = TrajectoryBaselines.Stats(session, contiguous, totalRooms, excluded: current);
                reference = baseline == TrajectoryBaseline.SumOfBest ? stats.Bests : stats.Averages;
                measured  = 0;
                while (measured < totalRooms && stats.HasTime[measured]) measured++;
            }

            int count = Math.Min(Math.Min(contiguous[current], measured), totalRooms);
            long[] deviations = new long[count];
            long cumulative = 0;
            for (int r = 0; r < count; r++)
            {
                cumulative += session.GetCell(current, r).Time.Ticks - reference[r];
                deviations[r] = cumulative;
            }
            return new LiveLine(deviations, totalRooms, measured > 0, baseline);
        }
    }
}
