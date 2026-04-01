using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
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
    InconsistentRooms,
    TimeLoss,
    RunTrajectory,
    BoxPlot
}

public partial class GraphManager
{
    private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

    private readonly List<List<TimeTicks>> _roomTimes;
    private readonly List<TimeTicks> _segmentTimes;
    private readonly IReadOnlyDictionary<int, int> _dnfData;
    private readonly IReadOnlyDictionary<int, int> _attemptsByRoom;
    private readonly IReadOnlyList<Attempt> _attempts;
    private readonly int _totalRooms;
    private readonly TimeTicks? _targetTime;

    // Cycling state
    // A slot is (GraphType, roomIndex) where roomIndex is only meaningful for RoomHistogram
    private record GraphSlot(GraphType Type, int RoomIndex = -1);
    private List<GraphSlot> _enabledSlots = [];
    private int _currentSlotIndex = -1; // -1 = nothing shown yet
    private BaseChartOverlay _currentOverlay;

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
        IReadOnlyList<Attempt> attempts = null,
        TimeTicks? target = null)
    {
        _roomTimes      = rooms;
        _segmentTimes   = segment;
        _dnfData        = dnfPerRoom;
        _attemptsByRoom = totalAttemptsPerRoom;
        _attempts       = attempts ?? [];
        _totalRooms     = rooms.Count;
        _targetTime     = target;

        RebuildEnabledSlots();
        // Restore last shown type — on very first run _lastShownType is Scatter,
        // so scatter will be index 0 and NextGraph will show it first.
        // If the type is disabled, RestoreSlot returns -1 and NextGraph falls back to index 0.
        RestoreSlot(_lastShownType, -1);
    }

    // -------------------------------------------------------------------------
    // Public API — settings / state queries
    // -------------------------------------------------------------------------

    public bool SameLength(int segmentLength) => _roomTimes.Count == segmentLength;

    public (GraphType Type, int RoomIndex) GetCurrentSlot()
    {
        if (_currentSlotIndex < 0 || _enabledSlots.Count == 0)
            return (GraphType.Scatter, -1);
        var slot = _enabledSlots[_currentSlotIndex];
        return (slot.Type, slot.RoomIndex);
    }

    public bool IsShowing() => _currentOverlay != null;

    public void Render() => _currentOverlay?.Render();

    // -------------------------------------------------------------------------
    // Slot management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the enabled slot list from current settings.
    /// If the currently displayed graph is no longer in the list, advances to
    /// the next enabled graph (or shows the "no graphs" message).
    /// Call this whenever a graph toggle changes in the settings menu.
    /// </summary>
    public void RebuildEnabledSlots()
    {
        var (prevType, prevRoom) = GetCurrentSlot();
        bool wasShowing = IsShowing();

        _enabledSlots = BuildSlots();

        int restored = FindBestSlot(prevType, prevRoom);
        if (restored >= 0)
        {
            _currentSlotIndex = restored;
        }
        else if (wasShowing)
        {
            _currentSlotIndex = -1;
            NextGraph();
        }
        else
        {
            _currentSlotIndex = -1;
        }
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

        if (_settings.GraphBoxPlot)
            slots.Add(new GraphSlot(GraphType.BoxPlot));

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

        if (_settings.GraphTimeLoss)
            slots.Add(new GraphSlot(GraphType.TimeLoss));

        if (_settings.GraphRunTrajectory)
            slots.Add(new GraphSlot(GraphType.RunTrajectory));

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
            int exact = _enabledSlots.FindIndex(s => s.Type == GraphType.RoomHistogram && s.RoomIndex == roomIndex);
            if (exact >= 0) return exact;

            int nearest = _enabledSlots
                .Select((s, i) => (s, i))
                .Where(x => x.s.Type == GraphType.RoomHistogram)
                .OrderBy(x => Math.Abs(x.s.RoomIndex - roomIndex))
                .Select(x => x.i)
                .FirstOrDefault(-1);
            if (nearest >= 0) return nearest;
        }

        return _enabledSlots.FindIndex(s => s.Type == type);
    }

    // -------------------------------------------------------------------------
    // Navigation
    // -------------------------------------------------------------------------

    public void NextGraph()
    {
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        _currentSlotIndex = (_currentSlotIndex + 1) % _enabledSlots.Count;
        ShowCurrentSlot();
    }

    public void PreviousGraph()
    {
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        _currentSlotIndex = (_currentSlotIndex - 1 + _enabledSlots.Count) % _enabledSlots.Count;
        ShowCurrentSlot();
    }

    public void CurrentGraph()
    {
        if (_currentSlotIndex < 0)
        {
            NextGraph();
            return;
        }

        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        ShowCurrentSlot();
    }

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    private void ShowCurrentSlot()
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
            GraphType.TimeLoss          => GetOrCreateTimeLossChart(),
            GraphType.RunTrajectory     => GetOrCreateRunTrajectoryChart(),
            GraphType.BoxPlot           => GetOrCreateBoxPlotChart(),
            _                           => null
        };
    }

    private static void ShowNoGraphsMessage()
    {
        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupNoGraphid));
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public void HideGraph()
    {
        _currentOverlay = null;
    }

    public void RemoveGraphs()
    {
        _currentOverlay = null;
    }

    public void Dispose()
    {
        _currentOverlay         = null;
        _scatterGraph           = null;
        _segmentHistogram       = null;
        _dnfPctChart            = null;
        _problemRoomsChart      = null;
        _inconsistentRoomsChart = null;
        _timeLossChart          = null;
        _runTrajectoryChart     = null;
        _boxPlotChart           = null;
        _roomHistograms.Clear();
        _enabledSlots.Clear();
    }
}