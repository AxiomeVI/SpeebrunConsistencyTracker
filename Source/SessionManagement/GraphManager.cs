using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public enum GraphType
{
    Scatter,
    RoomHistogram,
    SegmentHistogram,
    DnfPercent,
    ProblemRooms,
    InconsistentRooms
}

public class GraphManager
{
    private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;
    private readonly bool showRoomTimeDistributionPlots = SpeebrunConsistencyTrackerModule.Settings.ShowRoomTimeDistributionPlots;

    private readonly List<List<TimeTicks>> _roomTimes;
    private readonly List<TimeTicks> _segmentTimes;
    private readonly IReadOnlyDictionary<int, int> _dnfData;
    private readonly IReadOnlyDictionary<int, int> _attemptsByRoom;
    private readonly int _totalRooms;
    private readonly TimeTicks? _targetTime;

    // Graph cache
    private GraphOverlay _scatterGraph;
    private readonly Dictionary<int, HistogramOverlay> _roomHistograms = [];
    private HistogramOverlay _segmentHistogram;
    private PercentBarChartOverlay _dnfPctChart;
    private PercentBarChartOverlay _problemRoomsChart;
    private PercentBarChartOverlay _inconsistentRoomsChart;

    // Cycling state
    // A slot is (GraphType, roomIndex) where roomIndex is only meaningful for RoomHistogram
    private record GraphSlot(GraphType Type, int RoomIndex = -1);
    private List<GraphSlot> _enabledSlots = [];
    private int _currentSlotIndex = -1; // -1 = nothing shown yet
    private Entity _currentOverlay;

    // Persists the last shown graph type across GraphManager rebuilds (e.g. room count changed)
    // Defaults to Scatter so the first open always shows the scatter plot
    private static GraphType _lastShownType = GraphType.Scatter;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public GraphManager(
        List<List<TimeTicks>> rooms,
        List<TimeTicks> segment,
        IReadOnlyDictionary<int, int> dnfPerRoom,
        IReadOnlyDictionary<int, int> totalAttemptsPerRoom,
        TimeTicks? target = null)
    {
        _roomTimes       = rooms;
        _segmentTimes    = segment;
        _dnfData         = dnfPerRoom;
        _attemptsByRoom  = totalAttemptsPerRoom;
        _totalRooms      = rooms.Count;
        _targetTime      = target;

        RebuildEnabledSlots();
        // Restore last shown type — on very first run _lastShownType is Scatter,
        // so scatter will be index 0 and NextGraph will show it first.
        // If the type is disabled, RestoreSlot returns -1 and NextGraph falls back to index 0.
        RestoreSlot(_lastShownType, -1);
    }

    // -------------------------------------------------------------------------
    // Public API — settings / state queries
    // -------------------------------------------------------------------------

    public bool SameSettings(int segmentLength) => _roomTimes.Count == segmentLength;

    public (GraphType Type, int RoomIndex) GetCurrentSlot()
    {
        if (_currentSlotIndex < 0 || _enabledSlots.Count == 0)
            return (GraphType.Scatter, -1);
        var slot = _enabledSlots[_currentSlotIndex];
        return (slot.Type, slot.RoomIndex);
    }

    public bool IsShowing() => _currentOverlay != null;

    // -------------------------------------------------------------------------
    // Slot management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the enabled slot list from current settings.
    /// If the currently displayed graph is no longer in the list, advances to
    /// the next enabled graph (or shows the "no graphs" message).
    /// Call this whenever a graph toggle changes in the settings menu.
    /// </summary>
    public void RebuildEnabledSlots(Level level = null)
    {
        var (prevType, prevRoom) = GetCurrentSlot();
        bool wasShowing = IsShowing();

        _enabledSlots = BuildSlots();

        // Try to stay on the same slot
        int restored = FindBestSlot(prevType, prevRoom);
        if (restored >= 0)
        {
            _currentSlotIndex = restored;
            // No need to re-render — same graph is still valid
        }
        else if (wasShowing && level != null)
        {
            // Current graph was disabled — advance to next enabled one
            _currentSlotIndex = -1;
            NextGraph(level);
        }
        else
        {
            _currentSlotIndex = -1;
        }

        index = showRoomTimeDistributionPlots ? displayedGraphIndex - roomTimes.Count : displayedGraphIndex;
        return false;
    }

    /// <summary>
    /// After a GraphManager rebuild (e.g. room count changed), tries to restore
    /// the same graph type / room that was previously showing.
    /// </summary>
    public void RestoreSlot(GraphType prevType, int prevRoomIndex)
    {
        int idx = FindBestSlot(prevType, prevRoomIndex);
        _currentSlotIndex = idx >= 0 ? idx : -1;
    }

    private List<GraphSlot> BuildSlots()
    {
        var slots = new List<GraphSlot>();

        if (_settings.GraphScatter)
            slots.Add(new GraphSlot(GraphType.Scatter));

        if (_settings.GraphRoomHistogram)
            for (int i = 0; i < _roomTimes.Count; i++)
                slots.Add(new GraphSlot(GraphType.RoomHistogram, i));

        if (_settings.GraphSegmentHistogram)
            slots.Add(new GraphSlot(GraphType.SegmentHistogram));

        if (_settings.GraphDnfPercent)
            slots.Add(new GraphSlot(GraphType.DnfPercent));

        if (_settings.GraphProblemRooms)
            slots.Add(new GraphSlot(GraphType.ProblemRooms));

        if (_settings.GraphInconsistentRooms)
            slots.Add(new GraphSlot(GraphType.InconsistentRooms));

        return slots;
    }

    /// <summary>
    /// Finds the best matching slot index for a given type + room.
    /// For RoomHistogram: tries exact room, then nearest valid room, then first room slot.
    /// For other types: finds the first slot of that type.
    /// Returns -1 if no match found.
    /// </summary>
    private int FindBestSlot(GraphType type, int roomIndex)
    {
        if (type == GraphType.RoomHistogram)
        {
            // Exact match
            int exact = _enabledSlots.FindIndex(s => s.Type == GraphType.RoomHistogram && s.RoomIndex == roomIndex);
            if (exact >= 0) return exact;

            // Nearest valid room
            int nearest = _enabledSlots
                .Select((s, i) => (s, i))
                .Where(x => x.s.Type == GraphType.RoomHistogram)
                .OrderBy(x => System.Math.Abs(x.s.RoomIndex - roomIndex))
                .Select(x => x.i)
                .FirstOrDefault(-1);
            if (nearest >= 0) return nearest;
        }

        // Any other type — find first slot of that type
        return _enabledSlots.FindIndex(s => s.Type == type);
    }

    // -------------------------------------------------------------------------
    // Navigation
    // -------------------------------------------------------------------------

    public void NextGraph(Level level)
    {
        _currentOverlay?.RemoveSelf();
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        _currentSlotIndex = (_currentSlotIndex + 1) % _enabledSlots.Count;
        ShowCurrentSlot(level);
    }

    public void PreviousGraph(Level level)
    {
        _currentOverlay?.RemoveSelf();
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        _currentSlotIndex = (_currentSlotIndex - 1 + _enabledSlots.Count) % _enabledSlots.Count;
        ShowCurrentSlot(level);
    }

    public void CurrentGraph(Level level)
    {
        if (_currentSlotIndex < 0)
        {
            NextGraph(level);
            return;
        }

        _currentOverlay?.RemoveSelf();
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        ShowCurrentSlot(level);
    }

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    private void ShowCurrentSlot(Level level)
    {
        if (_currentSlotIndex < 0 || _currentSlotIndex >= _enabledSlots.Count) return;

        GraphSlot slot = _enabledSlots[_currentSlotIndex];
        _lastShownType = slot.Type;

        _currentOverlay = slot.Type switch
        {
            GraphType.Scatter           => GetOrCreateScatter(),
            GraphType.RoomHistogram     => GetOrCreateRoomHistogram(slot.RoomIndex),
            GraphType.SegmentHistogram  => GetOrCreateSegmentHistogram(),
            GraphType.DnfPercent        => GetOrCreateDnfPctChart(),
            GraphType.ProblemRooms      => GetOrCreateProblemRoomsChart(),
            GraphType.InconsistentRooms => GetOrCreateInconsistentRoomsChart(),
            _                           => null
        };

        if (_currentOverlay != null)
            level.Add(_currentOverlay);
    }

    private static void ShowNoGraphsMessage()
    {
        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupNoGraphid));
    }

    // -------------------------------------------------------------------------
    // Graph factories
    // -------------------------------------------------------------------------

    private GraphOverlay GetOrCreateScatter()
    {
        return _scatterGraph ??= new GraphOverlay(_roomTimes, _segmentTimes, null, _targetTime);
    }

    private HistogramOverlay GetOrCreateRoomHistogram(int roomIndex)
    {
        if (!_roomHistograms.TryGetValue(roomIndex, out HistogramOverlay value))
        {
            value = new HistogramOverlay(
                $"Room {roomIndex + 1}",
                _roomTimes[roomIndex],
                GraphOverlay.ToColor(_settings.RoomColor));
            _roomHistograms[roomIndex] = value;
        }
        return value;
    }

    private HistogramOverlay GetOrCreateSegmentHistogram()
    {
        return _segmentHistogram ??= new HistogramOverlay(
            "Segment",
            _segmentTimes,
            GraphOverlay.ToColor(_settings.SegmentColor));
    }

    private PercentBarChartOverlay GetOrCreateDnfPctChart()
    {
        if (_dnfPctChart != null) return _dnfPctChart;

        var labels  = Enumerable.Range(1, _totalRooms).Select(i => $"R{i}").ToList();
        var dnfPcts = ComputeDnfPcts();

        _dnfPctChart = new PercentBarChartOverlay(
            "DNF % per Room",
            labels, dnfPcts,
            Color.CornflowerBlue,
            "DNF %");

        return _dnfPctChart;
    }

    private PercentBarChartOverlay GetOrCreateProblemRoomsChart()
    {
        if (_problemRoomsChart != null) return _problemRoomsChart;

        var labels       = Enumerable.Range(1, _totalRooms).Select(i => $"R{i}").ToList();
        long threshold   = _settings.TimeLossThresholdMs * 10000L;
        var dnfPcts      = ComputeDnfPcts();
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
            Color.CornflowerBlue, Color.IndianRed,
            "DNF %", $">{_settings.TimeLossThresholdMs}ms over gold");

        return _problemRoomsChart;
    }

    private PercentBarChartOverlay GetOrCreateInconsistentRoomsChart()
    {
        if (_inconsistentRoomsChart != null) return _inconsistentRoomsChart;

        var labels = Enumerable.Range(1, _totalRooms).Select(i => $"R{i}").ToList();

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
            double variance = times.Sum(t => Math.Pow(t.Ticks - mean, 2)) / times.Count;
            double stddev = Math.Sqrt(variance);
            return stddev / mean * 100;
        }).ToList();

        _inconsistentRoomsChart = new PercentBarChartOverlay(
            "Inconsistent Rooms",
            labels, rmadPcts, rstddevPcts,
            Color.CornflowerBlue, Color.IndianRed,
            "RMAD %", "RStdDev %");

        return _inconsistentRoomsChart;
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

    public void ClearScatterGraph()
    {
        _scatterGraph = null;
    }

    public void ClearHistogram()
    {
        _roomHistograms.Clear();
        _segmentHistogram = null;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public void HideGraph()
    {
        _currentOverlay?.RemoveSelf();
        _currentOverlay = null;
    }

    public void RemoveGraphs()
    {
        _currentOverlay?.RemoveSelf();
        _scatterGraph?.RemoveSelf();
        _segmentHistogram?.RemoveSelf();
        _dnfPctChart?.RemoveSelf();
        _problemRoomsChart?.RemoveSelf();
        _inconsistentRoomsChart?.RemoveSelf();
        foreach (HistogramOverlay graph in _roomHistograms.Values)
            graph?.RemoveSelf();
    }

    public void Dispose()
    {
        RemoveGraphs();
        _currentOverlay    = null;
        _scatterGraph      = null;
        _segmentHistogram  = null;
        _dnfPctChart       = null;
        _problemRoomsChart       = null;
        _inconsistentRoomsChart  = null;
        _roomHistograms.Clear();
        _enabledSlots.Clear();
    }
}
