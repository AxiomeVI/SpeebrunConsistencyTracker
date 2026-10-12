using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public static partial class GraphManager
{
    // The target time while its metric is on the overlay, null otherwise. The charts that draw it
    // read it live, each frame: it moves from sliders, typed input, reset, import and a drag of the
    // line itself, and a chart rebuilt for each would lose its zoom and its hidden columns.
    private static TimeTicks? TargetShown()
        => MetricHelper.IsMetricEnabled(SpeebrunConsistencyTrackerModule.Settings.TargetTime, MetricOutput.Overlay)
            ? MetricEngine.GetTargetTimeTicks() : null;

    // A drag of the scatter's target line: saved at once, as a chart's own choices are.
    private static void SetTarget(TimeTicks target)
    {
        SpeebrunConsistencyTrackerModule.Settings.SetTargetTime(System.TimeSpan.FromTicks(target.Ticks));
        SpeebrunConsistencyTrackerModule.Instance.SaveSettings();
    }

    private static BaseChartOverlay BuildScatter(int roomCount)
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

        return new Charts.ScatterChart(roomTimes, roomIndices, segmentTimes, segmentIndices, TargetShown, SetTarget);
    }

    private static BaseChartOverlay BuildDnfPctChart(int roomCount)
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

        return Charts.BarsChart.Percent(
            Dialog.Clean(DialogIds.ChartResetSurvivalTitle),
            labels, dnfRates, survivalRates,
            Dialog.Clean(DialogIds.ChartResetRate), Dialog.Clean(DialogIds.ChartRunsAlive));
    }

    private static BaseChartOverlay BuildProblemRoomsChart(int roomCount)
    {
        var settings     = SpeebrunConsistencyTrackerModule.Settings;
        var labels       = Enumerable.Range(0, roomCount).Select(Utility.RoomLabels.For).ToList();
        var dnfPcts      = ComputeDnfPcts(roomCount);
        var session      = SessionManager.CurrentSession;
        // Each room's times and how many runs reached it, read once: the chart's slider asks for
        // the shares again at every step of a drag.
        var rooms = Enumerable.Range(0, roomCount).Select(i =>
        {
            long[] ticks = [.. session.GetRoomTimes(i).Select(t => t.Ticks)];
            return (Reached: session.TotalAttemptsPerRoom.GetValueOrDefault(i), Ticks: ticks, Best: ticks.Length == 0 ? 0L : ticks.Min());
        }).ToList();

        Charts.BarsChart.Retuned At(int frames)
        {
            int ms = frames * Charts.BarsChart.FrameMs;
            long threshold = ms * 10000L;
            // Seconds to the millisecond, as the charts write every other time.
            string seconds = (ms / 1000.0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            var shares = rooms.Select(r => r.Reached == 0 || r.Ticks.Length == 0
                ? 0.0
                : (double)r.Ticks.Count(t => t > r.Best + threshold) / r.Reached * 100).ToList();
            return new(Utility.DialogText.Format(DialogIds.ChartProblemRoomsTitleFmt, seconds), shares,
                       Utility.DialogText.Format(DialogIds.ChartOverThresholdFmt, seconds));
        }

        int now = Math.Clamp((int)Math.Round(settings.TimeLossThresholdMs / (double)Charts.BarsChart.FrameMs),
                             Charts.BarsChart.MinThresholdFrames, Charts.BarsChart.MaxThresholdFrames);
        return Charts.BarsChart.Stacked(labels, dnfPcts, Dialog.Clean(DialogIds.ChartResetRate), now, At);
    }

    private static BaseChartOverlay BuildTimeLossChart(int roomCount)
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

        return Charts.BarsChart.Time(
            Dialog.Clean(DialogIds.ChartTimeLossTitle),
            labels, medianTicks, averageTicks,
            Dialog.Clean(DialogIds.ChartMedianLoss), Dialog.Clean(DialogIds.ChartAvgLoss));
    }

    private static BaseChartOverlay BuildRunTrajectoryChart(int roomCount)
        => new Charts.TrajectoryChart(SessionManager.CurrentSession, roomCount);

    private static BaseChartOverlay BuildHistogramWall(int roomCount)
    {
        var session = SessionManager.CurrentSession;
        return new Charts.HistogramWallChart(
            Enumerable.Range(0, roomCount).Select(Utility.RoomLabels.For).ToList(),
            Enumerable.Range(0, roomCount).Select(i => session.GetRoomTimes(i).ToList()).ToList(),
            session.GetSegmentTimes().ToList(),
            Utility.DialogText.Format(DialogIds.ChartSegmentRoomsFmt,
                Utility.DialogText.Count(roomCount, DialogIds.ChartOneRoom, DialogIds.ChartRoomsFmt)),
            TargetShown);
    }

    private static BaseChartOverlay BuildBoxPlotChart(int roomCount)
    {
        var session      = SessionManager.CurrentSession;
        var roomTimes    = Enumerable.Range(0, roomCount)
            .Select(i => session.GetRoomTimes(i).ToList())
            .ToList();
        var segmentTimes = session.GetSegmentTimes().ToList();

        return new Charts.BoxChart(roomTimes, segmentTimes);
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
