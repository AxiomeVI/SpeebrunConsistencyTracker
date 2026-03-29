using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public partial class GraphManager
{
    // Graph cache
    private ScatterPlotOverlay _scatterGraph;
    private readonly Dictionary<int, HistogramOverlay> _roomHistograms = [];
    private HistogramOverlay _segmentHistogram;
    private GroupedPercentOverlay _dnfPctChart;
    private PercentBarChartOverlay _problemRoomsChart;
    private PercentBarChartOverlay _inconsistentRoomsChart;
    private GroupedBarChartOverlay _timeLossChart;
    private RunTrajectoryOverlay _runTrajectoryChart;
    private BoxPlotOverlay _boxPlotChart;

    // -------------------------------------------------------------------------
    // Graph factories
    // -------------------------------------------------------------------------

    private ScatterPlotOverlay GetOrCreateScatter()
    {
        return _scatterGraph ??= new ScatterPlotOverlay(_roomTimes, _segmentTimes, null, _targetTime);
    }

    private HistogramOverlay GetOrCreateRoomHistogram(int roomIndex)
    {
        if (!_roomHistograms.TryGetValue(roomIndex, out HistogramOverlay value))
        {
            value = new HistogramOverlay(
                $"Room {roomIndex + 1}",
                _roomTimes[roomIndex],
                isSegment: false);
            _roomHistograms[roomIndex] = value;
        }
        return value;
    }

    private HistogramOverlay GetOrCreateSegmentHistogram()
    {
        int roomCount = SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms;
        string roomLabel = roomCount == 1 ? "1 room" : $"{roomCount} rooms";
        return _segmentHistogram ??= new HistogramOverlay(
            $"Segment ({roomLabel})",
            _segmentTimes,
            isSegment: true);
    }

    private GroupedPercentOverlay GetOrCreateDnfPctChart()
    {
        if (_dnfPctChart != null) return _dnfPctChart;

        var labels        = Enumerable.Range(1, _totalRooms).Select(i => $"R{i}").ToList();
        var dnfPcts       = ComputeDnfPcts();
        var dnfRates      = dnfPcts.Select(p => (float)p).ToList();
        var survivalRates = dnfPcts.Select(p => (float)(100.0 - p)).ToList();

        _dnfPctChart = new GroupedPercentOverlay(
            "DNF Rate per Room & Segment Survival Rate",
            labels, dnfRates, survivalRates,
            Color.IndianRed, Color.CornflowerBlue,
            "DNF rate", "Remaining (%)",
            _settings.ChartOpacity);

        return _dnfPctChart;
    }

    private PercentBarChartOverlay GetOrCreateProblemRoomsChart()
    {
        if (_problemRoomsChart != null) return _problemRoomsChart;

        var labels     = Enumerable.Range(1, _totalRooms).Select(i => $"R{i}").ToList();
        long threshold = _settings.TimeLossThresholdMs * 10000L;
        var dnfPcts    = ComputeDnfPcts();
        var timeLossPcts = Enumerable.Range(0, _totalRooms).Select(i =>
        {
            int reached = _attemptsByRoom.GetValueOrDefault(i);
            if (reached == 0) return 0.0;
            var times = i < _roomTimes.Count ? _roomTimes[i] : [];
            if (times.Count == 0) return 0.0;
            long best     = times.Min(t => t.Ticks);
            int slowCount = times.Count(t => t.Ticks > best + threshold);
            return (double)slowCount / reached * 100;
        }).ToList();

        _problemRoomsChart = new PercentBarChartOverlay(
            $"Problem Rooms (threshold: {_settings.TimeLossThresholdMs}ms)",
            labels, dnfPcts, timeLossPcts,
            Color.IndianRed, Color.CornflowerBlue,
            "DNF rate", $">{_settings.TimeLossThresholdMs}ms over gold",
            _settings.ChartOpacity);

        return _problemRoomsChart;
    }

    private PercentBarChartOverlay GetOrCreateInconsistentRoomsChart()
    {
        if (_inconsistentRoomsChart != null) return _inconsistentRoomsChart;

        var rmadPcts = Enumerable.Range(0, _totalRooms).Select(i =>
        {
            var sorted = (i < _roomTimes.Count ? _roomTimes[i] : [])
                .OrderBy(t => t).ToList();
            if (sorted.Count == 0) return 0.0;
            TimeTicks median = MetricHelper.ComputePercentile(sorted, 50);
            if (median.Ticks == 0) return 0.0;
            TimeTicks mad = MetricHelper.ComputeMAD(sorted);
            return (double)mad.Ticks / median.Ticks * 100;
        }).ToList();

        var rstddevPcts = Enumerable.Range(0, _totalRooms).Select(i =>
        {
            var times = i < _roomTimes.Count ? _roomTimes[i] : [];
            if (times.Count == 0) return 0.0;
            double mean = times.Average(t => (double)t.Ticks);
            if (mean == 0) return 0.0;
            return MetricHelper.ComputeStdDev(times, mean) / mean * 100;
        }).ToList();

        // Sort rooms by total inconsistency descending
        var ranked = Enumerable.Range(0, _totalRooms)
            .OrderByDescending(i => rmadPcts[i] + rstddevPcts[i])
            .ToList();

        var rankedLabels  = ranked.Select(i => $"R{i + 1}").ToList();
        var rankedRmad    = ranked.Select(i => rmadPcts[i]).ToList();
        var rankedRstddev = ranked.Select(i => rstddevPcts[i]).ToList();

        // Normalize relative to worst room (first after sort)
        double maxTotal = ranked.Count > 0 ? rankedRmad[0] + rankedRstddev[0] : 0.0;

        List<double> scaledRmad;
        List<double> scaledRstddev;
        if (maxTotal == 0)
        {
            scaledRmad    = [.. rankedRmad.Select(_ => 0.0)];
            scaledRstddev = [.. rankedRstddev.Select(_ => 0.0)];
        }
        else
        {
            scaledRmad    = [.. rankedRmad.Select(v => v / maxTotal * 100)];
            scaledRstddev = [.. rankedRstddev.Select(v => v / maxTotal * 100)];
        }

        _inconsistentRoomsChart = new PercentBarChartOverlay(
            "Room Inconsistency (ranked)",
            rankedLabels, scaledRmad, scaledRstddev,
            Color.IndianRed, Color.CornflowerBlue,
            "RMAD", "RStdDev",
            _settings.ChartOpacity);

        return _inconsistentRoomsChart;
    }

    private GroupedBarChartOverlay GetOrCreateTimeLossChart()
    {
        if (_timeLossChart != null) return _timeLossChart;

        var labels = Enumerable.Range(1, _totalRooms).Select(i => $"R{i}").ToList();

        var medianTicks = Enumerable.Range(0, _totalRooms).Select(i =>
        {
            var times = i < _roomTimes.Count ? _roomTimes[i] : [];
            if (times.Count == 0) return 0L;
            long gold = times.Min(t => t.Ticks);
            List<TimeTicks> losses = [.. times.Select(t => new TimeTicks(t.Ticks - gold)).OrderBy(t => t)];
            return MetricHelper.ComputePercentile(losses, 50).Ticks;
        }).ToList();

        var averageTicks = Enumerable.Range(0, _totalRooms).Select(i =>
        {
            var times = i < _roomTimes.Count ? _roomTimes[i] : [];
            if (times.Count == 0) return 0L;
            long gold = times.Min(t => t.Ticks);
            return (long)times.Average(t => (double)(t.Ticks - gold));
        }).ToList();

        _timeLossChart = new GroupedBarChartOverlay(
            "Time Loss per Room",
            labels, medianTicks, averageTicks,
            Color.IndianRed, Color.CornflowerBlue,
            "Median loss", "Avg loss",
            _settings.ChartOpacity);

        return _timeLossChart;
    }

    private RunTrajectoryOverlay GetOrCreateRunTrajectoryChart()
    {
        return _runTrajectoryChart ??= new RunTrajectoryOverlay(
            _attempts,
            _roomTimes,
            _totalRooms);
    }

    private BoxPlotOverlay GetOrCreateBoxPlotChart()
    {
        return _boxPlotChart ??= new BoxPlotOverlay(
            _roomTimes,
            _segmentTimes);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private List<double> ComputeDnfPcts() =>
        [.. Enumerable.Range(0, _totalRooms).Select(i =>
        {
            int reached = _attemptsByRoom.GetValueOrDefault(i);
            if (reached == 0) return 0.0;
            return (double)_dnfData.GetValueOrDefault(i) / reached * 100;
        })];

    // -------------------------------------------------------------------------
    // Cache invalidation
    // -------------------------------------------------------------------------

    public void ClearScatterGraph() => _scatterGraph = null;

    public void ClearHistogram()
    {
        _roomHistograms.Clear();
        _segmentHistogram = null;
    }

    public void ClearBarCharts()
    {
        _dnfPctChart            = null;
        _problemRoomsChart      = null;
        _inconsistentRoomsChart = null;
        _timeLossChart          = null;
        _boxPlotChart           = null;
    }

    public void ClearProblemChart() => _problemRoomsChart = null;
}
