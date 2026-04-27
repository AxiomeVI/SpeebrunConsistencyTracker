using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public static partial class GraphManager
{
    private static ScatterPlotOverlay _scatterGraph;
    private static uint _scatterVersion;
    private static int  _scatterRoomCount;
    private static int  _scatterRoomTimerType;

    private static readonly Dictionary<int, HistogramOverlay> _roomHistograms     = [];
    private static readonly Dictionary<int, uint>             _roomHistogramVersion   = [];
    private static readonly Dictionary<int, int>              _roomHistogramTimerType = [];

    private static HistogramOverlay _segmentHistogram;
    private static uint _segmentHistogramVersion;
    private static int  _segmentHistogramRoomCount;
    private static int  _segmentHistogramTimerType;

    private static GroupedPercentOverlay _dnfPctChart;
    private static uint _dnfPctVersion;
    private static int  _dnfPctRoomCount;
    private static int  _dnfPctTimerType;

    private static PercentBarChartOverlay _problemRoomsChart;
    private static uint _problemRoomsVersion;
    private static int  _problemRoomsRoomCount;
    private static int  _problemRoomsTimerType;

    private static GroupedBarChartOverlay _timeLossChart;
    private static uint _timeLossVersion;
    private static int  _timeLossRoomCount;
    private static int  _timeLossTimerType;

    private static RunTrajectoryOverlay _runTrajectoryChart;
    private static uint _runTrajectoryVersion;
    private static int  _runTrajectoryRoomCount;
    private static int  _runTrajectoryTimerType;

    private static BoxPlotOverlay _boxPlotChart;
    private static uint _boxPlotVersion;
    private static int  _boxPlotRoomCount;
    private static int  _boxPlotTimerType;

    private static ScatterPlotOverlay GetOrCreateScatter()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_scatterGraph != null && (
            _scatterVersion       != curVersion   ||
            _scatterRoomCount     != curRoomCount ||
            _scatterRoomTimerType != curTimerType))
        {
            _scatterGraph = null;
        }

        if (_scatterGraph == null)
        {
            var session = SessionManager.CurrentSession;
            // Build times and parallel attempt-index lists together, then filter rooms with no data.
            var roomPairs = Enumerable.Range(0, curRoomCount)
                .Select(i => (times: session.GetRoomTimes(i).ToList(), indices: session.GetRoomAttemptIndices(i).ToList()))
                .Where(p => p.times.Count > 0)
                .ToList();
            var roomTimes   = roomPairs.Select(p => p.times).ToList();
            var roomIndices = roomPairs.Select(p => p.indices).ToList();

            var segmentIndices = session.GetCompletedAttemptIndices().ToList();
            var segmentTimes   = segmentIndices.Select(session.SegmentTime).ToList();

            TimeTicks? target = MetricHelper.IsMetricEnabled(SpeebrunConsistencyTrackerModule.Settings.TargetTime, MetricOutput.Overlay)
                ? MetricEngine.GetTargetTimeTicks() : null;

            _scatterGraph         = new ScatterPlotOverlay(roomTimes, roomIndices, segmentTimes, segmentIndices, null, target);
            _scatterVersion       = curVersion;
            _scatterRoomCount     = curRoomCount;
            _scatterRoomTimerType = curTimerType;
        }

        return _scatterGraph;
    }

    private static HistogramOverlay GetOrCreateRoomHistogram(int roomIndex)
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_roomHistograms.ContainsKey(roomIndex) && (
            _roomHistogramVersion.GetValueOrDefault(roomIndex)   != curVersion  ||
            _roomHistogramTimerType.GetValueOrDefault(roomIndex) != curTimerType))
        {
            _roomHistograms.Remove(roomIndex);
        }

        if (!_roomHistograms.TryGetValue(roomIndex, out HistogramOverlay value))
        {
            value = new HistogramOverlay(
                $"Room {roomIndex + 1}",
                SessionManager.CurrentSession.GetRoomTimes(roomIndex).ToList(),
                isSegment: false);
            _roomHistograms[roomIndex]         = value;
            _roomHistogramVersion[roomIndex]   = curVersion;
            _roomHistogramTimerType[roomIndex] = curTimerType;
        }

        return value;
    }

    private static HistogramOverlay GetOrCreateSegmentHistogram()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_segmentHistogram != null && (
            _segmentHistogramVersion   != curVersion   ||
            _segmentHistogramRoomCount != curRoomCount ||
            _segmentHistogramTimerType != curTimerType))
        {
            _segmentHistogram = null;
        }

        if (_segmentHistogram == null)
        {
            string label   = curRoomCount == 1 ? "1 room" : $"{curRoomCount} rooms";
            _segmentHistogram          = new HistogramOverlay(
                $"Segment ({label})",
                SessionManager.CurrentSession.GetSegmentTimes().ToList(),
                isSegment: true);
            _segmentHistogramVersion   = curVersion;
            _segmentHistogramRoomCount = curRoomCount;
            _segmentHistogramTimerType = curTimerType;
        }

        return _segmentHistogram;
    }

    private static GroupedPercentOverlay GetOrCreateDnfPctChart()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_dnfPctChart != null && (
            _dnfPctVersion   != curVersion   ||
            _dnfPctRoomCount != curRoomCount ||
            _dnfPctTimerType != curTimerType))
        {
            _dnfPctChart = null;
        }

        if (_dnfPctChart == null)
        {
            var labels   = Enumerable.Range(1, curRoomCount).Select(i => $"R{i}").ToList();
            var dnfPcts  = ComputeDnfPcts(curRoomCount);
            var dnfRates = dnfPcts.Select(p => (float)p).ToList();

            var survivalRates = new List<float>(curRoomCount);
            double survival = 100.0;
            foreach (double dnfPct in dnfPcts)
            {
                survivalRates.Add((float)survival);
                survival *= (1.0 - dnfPct / 100.0);
            }

            _dnfPctChart   = new GroupedPercentOverlay(
                "DNF Rate per Room & Segment Survival Rate",
                labels, dnfRates, survivalRates,
                "DNF rate", "Remaining (%)");
            _dnfPctVersion   = curVersion;
            _dnfPctRoomCount = curRoomCount;
            _dnfPctTimerType = curTimerType;
        }

        return _dnfPctChart;
    }

    private static PercentBarChartOverlay GetOrCreateProblemRoomsChart()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_problemRoomsChart != null && (
            _problemRoomsVersion   != curVersion   ||
            _problemRoomsRoomCount != curRoomCount ||
            _problemRoomsTimerType != curTimerType))
        {
            _problemRoomsChart = null;
        }

        if (_problemRoomsChart == null)
        {
            var settings     = SpeebrunConsistencyTrackerModule.Settings;
            var labels       = Enumerable.Range(1, curRoomCount).Select(i => $"R{i}").ToList();
            long threshold   = settings.TimeLossThresholdMs * 10000L;
            var dnfPcts      = ComputeDnfPcts(curRoomCount);
            var session      = SessionManager.CurrentSession;
            var timeLossPcts = Enumerable.Range(0, curRoomCount).Select(i =>
            {
                int reached  = session.TotalAttemptsPerRoom.GetValueOrDefault(i);
                if (reached == 0) return 0.0;
                var times    = session.GetRoomTimes(i).ToList();
                if (times.Count == 0) return 0.0;
                long best    = times.Min(t => t.Ticks);
                int slowCount = times.Count(t => t.Ticks > best + threshold);
                return (double)slowCount / reached * 100;
            }).ToList();

            _problemRoomsChart   = new PercentBarChartOverlay(
                $"Problem Rooms (threshold: {settings.TimeLossThresholdMs}ms)",
                labels, dnfPcts, timeLossPcts,
                "DNF rate", $">{settings.TimeLossThresholdMs}ms over gold");
            _problemRoomsVersion   = curVersion;
            _problemRoomsRoomCount = curRoomCount;
            _problemRoomsTimerType = curTimerType;
        }

        return _problemRoomsChart;
    }


    private static GroupedBarChartOverlay GetOrCreateTimeLossChart()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_timeLossChart != null && (
            _timeLossVersion   != curVersion   ||
            _timeLossRoomCount != curRoomCount ||
            _timeLossTimerType != curTimerType))
        {
            _timeLossChart = null;
        }

        if (_timeLossChart == null)
        {
            var session   = SessionManager.CurrentSession;
            var labels    = Enumerable.Range(1, curRoomCount).Select(i => $"R{i}").ToList();

            var medianTicks = Enumerable.Range(0, curRoomCount).Select(i =>
            {
                var times = session.GetRoomTimes(i).ToList();
                if (times.Count == 0) return 0L;
                long gold = times.Min(t => t.Ticks);
                List<TimeTicks> losses = [.. times.Select(t => new TimeTicks(t.Ticks - gold)).OrderBy(t => t)];
                return MetricHelper.ComputePercentile(losses, 50).Ticks;
            }).ToList();

            var averageTicks = Enumerable.Range(0, curRoomCount).Select(i =>
            {
                var times = session.GetRoomTimes(i).ToList();
                if (times.Count == 0) return 0L;
                long gold = times.Min(t => t.Ticks);
                return (long)times.Average(t => (double)(t.Ticks - gold));
            }).ToList();

            _timeLossChart   = new GroupedBarChartOverlay(
                "Time Loss per Room",
                labels, medianTicks, averageTicks,
                "Median loss", "Avg loss");
            _timeLossVersion   = curVersion;
            _timeLossRoomCount = curRoomCount;
            _timeLossTimerType = curTimerType;
        }

        return _timeLossChart;
    }

    private static RunTrajectoryOverlay GetOrCreateRunTrajectoryChart()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_runTrajectoryChart != null && (
            _runTrajectoryVersion   != curVersion   ||
            _runTrajectoryRoomCount != curRoomCount ||
            _runTrajectoryTimerType != curTimerType))
        {
            _runTrajectoryChart = null;
        }

        if (_runTrajectoryChart == null)
        {
            var session = SessionManager.CurrentSession;

            _runTrajectoryChart   = new RunTrajectoryOverlay(
                session,
                curRoomCount);
            _runTrajectoryVersion   = curVersion;
            _runTrajectoryRoomCount = curRoomCount;
            _runTrajectoryTimerType = curTimerType;
        }

        return _runTrajectoryChart;
    }

    private static BoxPlotOverlay GetOrCreateBoxPlotChart()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_boxPlotChart != null && (
            _boxPlotVersion   != curVersion   ||
            _boxPlotRoomCount != curRoomCount ||
            _boxPlotTimerType != curTimerType))
        {
            _boxPlotChart = null;
        }

        if (_boxPlotChart == null)
        {
            var session      = SessionManager.CurrentSession;
            var roomTimes    = Enumerable.Range(0, curRoomCount)
                .Select(i => session.GetRoomTimes(i).ToList())
                .ToList();
            var segmentTimes = session.GetSegmentTimes().ToList();

            _boxPlotChart   = new BoxPlotOverlay(roomTimes, segmentTimes);
            _boxPlotVersion   = curVersion;
            _boxPlotRoomCount = curRoomCount;
            _boxPlotTimerType = curTimerType;
        }

        return _boxPlotChart;
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

    public static void ClearScatterGraph()
    {
        _scatterGraph         = null;
        _scatterVersion       = 0;
        _scatterRoomCount     = 0;
        _scatterRoomTimerType = 0;
    }

    public static void ClearRoomHistograms()
    {
        _roomHistograms.Clear();
        _roomHistogramVersion.Clear();
        _roomHistogramTimerType.Clear();
    }

    public static void ClearSegmentHistogram()
    {
        _segmentHistogram          = null;
        _segmentHistogramVersion   = 0;
        _segmentHistogramRoomCount = 0;
        _segmentHistogramTimerType = 0;
    }

    public static void ClearDnfPctChart()
    {
        _dnfPctChart   = null;
        _dnfPctVersion   = 0;
        _dnfPctRoomCount = 0;
        _dnfPctTimerType = 0;
    }

    public static void ClearProblemRoomsChart()
    {
        _problemRoomsChart   = null;
        _problemRoomsVersion   = 0;
        _problemRoomsRoomCount = 0;
        _problemRoomsTimerType = 0;
    }

    public static void ClearTimeLossChart()
    {
        _timeLossChart   = null;
        _timeLossVersion   = 0;
        _timeLossRoomCount = 0;
        _timeLossTimerType = 0;
    }

    public static void ClearRunTrajectoryChart()
    {
        _runTrajectoryChart   = null;
        _runTrajectoryVersion   = 0;
        _runTrajectoryRoomCount = 0;
        _runTrajectoryTimerType = 0;
    }

    public static void ClearBoxPlotChart()
    {
        _boxPlotChart   = null;
        _boxPlotVersion   = 0;
        _boxPlotRoomCount = 0;
        _boxPlotTimerType = 0;
    }

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
