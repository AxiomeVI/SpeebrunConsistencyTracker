using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public static partial class GraphManager
{
    // The scatter's target-time line follows a setting no other chart reads. Keying on it beats
    // a Clear() in every place that can move the target — sliders, typed input, reset, import.
    private static int ScatterTargetKey()
    {
        var s = SpeebrunConsistencyTrackerModule.Settings;
        bool enabled = MetricHelper.IsMetricEnabled(s.TargetTime, MetricOutput.Overlay);
        return System.HashCode.Combine(enabled, MetricEngine.GetTargetTimeTicks().Ticks);
    }

    private static ScatterPlotOverlay BuildScatter(int roomCount)
    {
        var session = SessionManager.CurrentSession;
        // Every room, empty ones included. ScatterPlotOverlay drops the empty ones itself and
        // keeps their original indices for the labels and the pins; filtering here as well made
        // that map an identity over an already-filtered list, so once a middle room had every
        // time deleted the later columns were labelled one room low and a pin's Delete targeted
        // an already-deleted cell.
        var roomPairs = Enumerable.Range(0, roomCount)
            .Select(i => (times: session.GetRoomTimes(i).ToList(), indices: session.GetRoomAttemptIndices(i).ToList()))
            .ToList();
        var roomTimes   = roomPairs.Select(p => p.times).ToList();
        var roomIndices = roomPairs.Select(p => p.indices).ToList();

        var segmentIndices = session.GetCompletedAttemptIndices().ToList();
        var segmentTimes   = segmentIndices.Select(session.SegmentTime).ToList();

        TimeTicks? target = MetricHelper.IsMetricEnabled(SpeebrunConsistencyTrackerModule.Settings.TargetTime, MetricOutput.Overlay)
            ? MetricEngine.GetTargetTimeTicks() : null;

        return new ScatterPlotOverlay(roomTimes, roomIndices, segmentTimes, segmentIndices, null, target);
    }

    private static HistogramOverlay BuildRoomHistogram(int roomIndex)
        => new(
            Utility.RoomLabels.For(roomIndex),
            SessionManager.CurrentSession.GetRoomTimes(roomIndex).ToList(),
            isSegment: false);

    private static HistogramOverlay BuildSegmentHistogram(int roomCount)
    {
        string label = roomCount == 1 ? "1 room" : $"{roomCount} rooms";
        return new HistogramOverlay(
            $"Segment ({label})",
            SessionManager.CurrentSession.GetSegmentTimes().ToList(),
            isSegment: true);
    }

    private static GroupedPercentOverlay BuildDnfPctChart(int roomCount)
    {
        var labels   = Enumerable.Range(0, roomCount).Select(Utility.RoomLabels.For).ToList();
        var dnfPcts  = ComputeDnfPcts(roomCount);
        var dnfRates = dnfPcts.Select(p => (float)p).ToList();

        var survivalRates = new List<float>(roomCount);
        double survival = 100.0;
        foreach (double dnfPct in dnfPcts)
        {
            survivalRates.Add((float)survival);
            survival *= (1.0 - dnfPct / 100.0);
        }

        return new GroupedPercentOverlay(
            "Reset Rate per Room & Segment Survival Rate",
            labels, dnfRates, survivalRates,
            "Reset rate", "Runs still alive");
    }

    private static PercentBarChartOverlay BuildProblemRoomsChart(int roomCount)
    {
        var settings     = SpeebrunConsistencyTrackerModule.Settings;
        var labels       = Enumerable.Range(0, roomCount).Select(Utility.RoomLabels.For).ToList();
        long threshold   = settings.TimeLossThresholdMs * 10000L;
        var dnfPcts      = ComputeDnfPcts(roomCount);
        var session      = SessionManager.CurrentSession;
        var timeLossPcts = Enumerable.Range(0, roomCount).Select(i =>
        {
            int reached  = session.TotalAttemptsPerRoom.GetValueOrDefault(i);
            if (reached == 0) return 0.0;
            var times    = session.GetRoomTimes(i).ToList();
            if (times.Count == 0) return 0.0;
            long best    = times.Min(t => t.Ticks);
            int slowCount = times.Count(t => t.Ticks > best + threshold);
            return (double)slowCount / reached * 100;
        }).ToList();

        return new PercentBarChartOverlay(
            $"Problem Rooms (threshold: {settings.TimeLossThresholdMs}ms over session best)",
            labels, dnfPcts, timeLossPcts,
            "Reset rate", $">{settings.TimeLossThresholdMs}ms over session best");
    }

    private static GroupedBarChartOverlay BuildTimeLossChart(int roomCount)
    {
        var session = SessionManager.CurrentSession;
        var labels  = Enumerable.Range(0, roomCount).Select(Utility.RoomLabels.For).ToList();

        // One walk of each room's times: both series are derived from the same list.
        var medianTicks  = new List<long>(roomCount);
        var averageTicks = new List<long>(roomCount);
        for (int i = 0; i < roomCount; i++)
        {
            var times = session.GetRoomTimes(i).ToList();
            if (times.Count == 0)
            {
                medianTicks.Add(0L);
                averageTicks.Add(0L);
                continue;
            }
            long best = times.Min(t => t.Ticks);
            List<TimeTicks> losses = [.. times.Select(t => new TimeTicks(t.Ticks - best)).OrderBy(t => t)];
            medianTicks.Add(MetricHelper.ComputePercentile(losses, 50).Ticks);
            averageTicks.Add((long)times.Average(t => (double)(t.Ticks - best)));
        }

        return new GroupedBarChartOverlay(
            "Time Loss per Room",
            labels, medianTicks, averageTicks,
            "Median loss", "Avg loss");
    }

    private static RunTrajectoryOverlay BuildRunTrajectoryChart(int roomCount)
        => new(SessionManager.CurrentSession, roomCount);

    private static BoxPlotOverlay BuildBoxPlotChart(int roomCount)
    {
        var session      = SessionManager.CurrentSession;
        var roomTimes    = Enumerable.Range(0, roomCount)
            .Select(i => session.GetRoomTimes(i).ToList())
            .ToList();
        var segmentTimes = session.GetSegmentTimes().ToList();

        return new BoxPlotOverlay(roomTimes, segmentTimes);
    }

    private static List<double> ComputeDnfPcts(int roomCount)
    {
        var session = SessionManager.CurrentSession;
        return [.. Enumerable.Range(0, roomCount).Select(i =>
        {
            int reached = session.TotalAttemptsPerRoom.GetValueOrDefault(i);
            if (reached == 0) return 0.0;
            return (double)session.DnfPerRoom.GetValueOrDefault(i) / reached * 100;
        })];
    }
}
