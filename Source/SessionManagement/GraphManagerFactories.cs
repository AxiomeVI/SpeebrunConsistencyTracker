using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public static partial class GraphManager
{
    private static readonly ChartCache<ScatterPlotOverlay>     _scatterGraph       = new(BuildScatter, extraKey: ScatterTargetKey);
    private static readonly ChartCache<HistogramOverlay>       _segmentHistogram   = new(BuildSegmentHistogram);
    private static readonly ChartCache<GroupedPercentOverlay>  _dnfPctChart        = new(BuildDnfPctChart);
    private static readonly ChartCache<PercentBarChartOverlay> _problemRoomsChart  = new(BuildProblemRoomsChart);
    private static readonly ChartCache<GroupedBarChartOverlay> _timeLossChart      = new(BuildTimeLossChart);
    private static readonly ChartCache<RunTrajectoryOverlay>   _runTrajectoryChart = new(BuildRunTrajectoryChart);
    private static readonly ChartCache<BoxPlotOverlay>         _boxPlotChart       = new(BuildBoxPlotChart);

    private static readonly Dictionary<int, ChartCache<HistogramOverlay>> _roomHistograms = [];

    // The scatter's target-time line follows a setting no other chart reads. Keying on it beats
    // a Clear() in each of the six setters that can move the target.
    private static int ScatterTargetKey()
    {
        var s = SpeebrunConsistencyTrackerModule.Settings;
        bool enabled = MetricHelper.IsMetricEnabled(s.TargetTime, MetricOutput.Overlay);
        return System.HashCode.Combine(enabled, MetricEngine.GetTargetTimeTicks().Ticks);
    }

    private static ScatterPlotOverlay     GetOrCreateScatter()           => _scatterGraph.Get();
    private static HistogramOverlay       GetOrCreateSegmentHistogram()  => _segmentHistogram.Get();
    private static GroupedPercentOverlay  GetOrCreateDnfPctChart()       => _dnfPctChart.Get();
    private static PercentBarChartOverlay GetOrCreateProblemRoomsChart() => _problemRoomsChart.Get();
    private static GroupedBarChartOverlay GetOrCreateTimeLossChart()     => _timeLossChart.Get();
    private static RunTrajectoryOverlay   GetOrCreateRunTrajectoryChart()=> _runTrajectoryChart.Get();
    private static BoxPlotOverlay         GetOrCreateBoxPlotChart()      => _boxPlotChart.Get();

    private static HistogramOverlay GetOrCreateRoomHistogram(int roomIndex)
    {
        if (!_roomHistograms.TryGetValue(roomIndex, out ChartCache<HistogramOverlay> cache))
        {
            cache = new ChartCache<HistogramOverlay>(_ => BuildRoomHistogram(roomIndex), keyOnRoomCount: false);
            _roomHistograms[roomIndex] = cache;
        }

        return cache.Get();
    }

    private static ScatterPlotOverlay BuildScatter(int roomCount)
    {
        var session = SessionManager.CurrentSession;
        // The parallel attempt-index lists must be built and filtered alongside the times.
        var roomPairs = Enumerable.Range(0, roomCount)
            .Select(i => (times: session.GetRoomTimes(i).ToList(), indices: session.GetRoomAttemptIndices(i).ToList()))
            .Where(p => p.times.Count > 0)
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
            $"Room {roomIndex + 1}",
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
        var labels   = Enumerable.Range(1, roomCount).Select(i => $"R{i}").ToList();
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
            "DNF Rate per Room & Segment Survival Rate",
            labels, dnfRates, survivalRates,
            "DNF rate", "Remaining (%)");
    }

    private static PercentBarChartOverlay BuildProblemRoomsChart(int roomCount)
    {
        var settings     = SpeebrunConsistencyTrackerModule.Settings;
        var labels       = Enumerable.Range(1, roomCount).Select(i => $"R{i}").ToList();
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
            $"Problem Rooms (threshold: {settings.TimeLossThresholdMs}ms)",
            labels, dnfPcts, timeLossPcts,
            "DNF rate", $">{settings.TimeLossThresholdMs}ms over gold");
    }

    private static GroupedBarChartOverlay BuildTimeLossChart(int roomCount)
    {
        var session = SessionManager.CurrentSession;
        var labels  = Enumerable.Range(1, roomCount).Select(i => $"R{i}").ToList();

        var medianTicks = Enumerable.Range(0, roomCount).Select(i =>
        {
            var times = session.GetRoomTimes(i).ToList();
            if (times.Count == 0) return 0L;
            long gold = times.Min(t => t.Ticks);
            List<TimeTicks> losses = [.. times.Select(t => new TimeTicks(t.Ticks - gold)).OrderBy(t => t)];
            return MetricHelper.ComputePercentile(losses, 50).Ticks;
        }).ToList();

        var averageTicks = Enumerable.Range(0, roomCount).Select(i =>
        {
            var times = session.GetRoomTimes(i).ToList();
            if (times.Count == 0) return 0L;
            long gold = times.Min(t => t.Ticks);
            return (long)times.Average(t => (double)(t.Ticks - gold));
        }).ToList();

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

    public static void ClearScatterGraph()       => _scatterGraph.Clear();
    public static void ClearRoomHistograms()     => _roomHistograms.Clear();
    public static void ClearSegmentHistogram()   => _segmentHistogram.Clear();
    public static void ClearDnfPctChart()        => _dnfPctChart.Clear();
    public static void ClearProblemRoomsChart()  => _problemRoomsChart.Clear();
    public static void ClearTimeLossChart()      => _timeLossChart.Clear();
    public static void ClearRunTrajectoryChart() => _runTrajectoryChart.Clear();
    public static void ClearBoxPlotChart()       => _boxPlotChart.Clear();

    private static void ClearAllCharts()
    {
        ClearScatterGraph();
        ClearRoomHistograms();
        ClearSegmentHistogram();
        ClearDnfPctChart();
        ClearProblemRoomsChart();
        ClearTimeLossChart();
        ClearRunTrajectoryChart();
        ClearBoxPlotChart();
    }
}
