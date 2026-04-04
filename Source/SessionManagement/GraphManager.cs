using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
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

public static partial class GraphManager
{
    private static PracticeSession _lastKnownSession;

    // Cycling state
    private record GraphSlot(GraphType Type, int RoomIndex = -1);
    private static List<GraphSlot> _enabledSlots = [];
    private static int _currentSlotIndex = -1;
    private static BaseChartOverlay _currentOverlay;

    private static GraphType LastShownType {
        get => SpeebrunConsistencyTrackerModule.Settings.LastShownGraph;
        set => SpeebrunConsistencyTrackerModule.Settings.LastShownGraph = value;
    }

    public static bool IsInitialized => SessionManager.CurrentSession != null;

    public static void Init()
    {
        _lastKnownSession = SessionManager.CurrentSession;
        ClearAllCharts();
        RebuildEnabledSlots();
    }

    public static void Clear()
    {
        _lastKnownSession = null;
        _currentOverlay   = null;
        _currentSlotIndex = -1;
        _enabledSlots.Clear();
        ClearAllCharts();
        GraphInteractivity.Clear();
    }

    public static bool IsShowing() => _currentOverlay != null;

    public static BaseChartOverlay CurrentOverlay => _currentOverlay;

    public static (GraphType Type, int RoomIndex) GetCurrentSlot()
    {
        if (_currentSlotIndex < 0 || _enabledSlots.Count == 0)
            return (GraphType.Scatter, -1);
        var slot = _enabledSlots[_currentSlotIndex];
        return (slot.Type, slot.RoomIndex);
    }

    private static void InvalidateIfSessionChanged()
    {
        if (!ReferenceEquals(SessionManager.CurrentSession, _lastKnownSession))
        {
            _lastKnownSession = SessionManager.CurrentSession;
            ClearAllCharts();
        }
    }

    public static void Render()
    {
        if (_currentOverlay != null)
        {
            InvalidateIfSessionChanged();
            ShowCurrentSlot();
            _currentOverlay?.Render();
            GraphInteractivity.Render();
        }
    }

    public static void RebuildEnabledSlots()
    {
        GraphType prevType = _currentSlotIndex >= 0 && _enabledSlots.Count > 0
            ? _enabledSlots[_currentSlotIndex].Type : GraphType.Scatter;
        int prevRoom = _currentSlotIndex >= 0 && _enabledSlots.Count > 0
            ? _enabledSlots[_currentSlotIndex].RoomIndex : -1;
        bool wasShowing = IsShowing();

        _enabledSlots = BuildSlots();

        int restored = FindBestSlot(prevType, prevRoom);
        if (restored >= 0)
        {
            _currentSlotIndex = restored;
            if (wasShowing)
                ShowCurrentSlot();
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

    private static List<GraphSlot> BuildSlots()
    {
        var settings = SpeebrunConsistencyTrackerModule.Settings;
        var slots    = new List<GraphSlot>();

        if (settings.GraphScatter)
            slots.Add(new GraphSlot(GraphType.Scatter));

        if (settings.GraphBoxPlot)
            slots.Add(new GraphSlot(GraphType.BoxPlot));

        if (settings.GraphRoomHistogram && SessionManager.CurrentSession != null)
        {
            int roomCount = SessionManager.RoomCount;
            for (int i = 0; i < roomCount; i++)
                slots.Add(new GraphSlot(GraphType.RoomHistogram, i));
        }

        if (settings.GraphSegmentHistogram)
            slots.Add(new GraphSlot(GraphType.SegmentHistogram));

        if (settings.GraphDnfPercent)
            slots.Add(new GraphSlot(GraphType.DnfPercent));

        if (settings.GraphProblemRooms)
            slots.Add(new GraphSlot(GraphType.ProblemRooms));

        if (settings.GraphInconsistentRooms)
            slots.Add(new GraphSlot(GraphType.InconsistentRooms));

        if (settings.GraphTimeLoss)
            slots.Add(new GraphSlot(GraphType.TimeLoss));

        if (settings.GraphRunTrajectory)
            slots.Add(new GraphSlot(GraphType.RunTrajectory));

        return slots;
    }

    private static int FindBestSlot(GraphType type, int roomIndex)
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

    public static void NextGraph()
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

    public static void PreviousGraph()
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

    public static void CurrentGraph()
    {
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        if (_currentSlotIndex < 0)
        {
            int restored = FindBestSlot(LastShownType, -1);
            if (restored >= 0)
            {
                _currentSlotIndex = restored;
                ShowCurrentSlot();
                return;
            }
            NextGraph();
            return;
        }

        ShowCurrentSlot();
    }

    private static void ShowCurrentSlot()
    {
        if (_currentSlotIndex < 0 || _currentSlotIndex >= _enabledSlots.Count) return;

        GraphSlot slot = _enabledSlots[_currentSlotIndex];

        if (slot.Type == GraphType.RoomHistogram && SessionManager.CurrentSession != null)
        {
            int curRoomCount = SessionManager.RoomCount;
            if (slot.RoomIndex >= curRoomCount)
            {
                int best = FindBestSlot(GraphType.RoomHistogram, curRoomCount - 1);
                if (best < 0) best = FindBestSlot(GraphType.Scatter, -1);
                _currentSlotIndex = best >= 0 ? best : 0;
                slot = _enabledSlots[_currentSlotIndex];
            }
        }

        LastShownType = slot.Type;

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

    public static void HideGraph()
    {
        _currentOverlay = null;
    }
}
