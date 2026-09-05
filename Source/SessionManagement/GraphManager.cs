using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public static partial class GraphManager
{
    private static PracticeSession _lastKnownSession;

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
        GraphInteractivity.Clear(_currentOverlay);
    }

    public static bool IsShowing() => _currentOverlay != null;

    public static BaseChartOverlay CurrentOverlay => _currentOverlay;

    // GetCurrentSlot() answers (Scatter, -1) both when the scatter is the selected slot and when
    // nothing is selected at all, so a caller cannot tell the two apart. These two say which it is.
    // A string rather than a GraphType.None member: GraphType is persisted, and a member with no
    // ChartDefinition row would fail Every_graph_type_has_exactly_one_definition for a value that
    // is not a chart. The bounds check is wider than GetCurrentSlot's on purpose — these are read
    // every frame by the test poll and must not throw on a cursor left past a shrunken list.
    public const string NoSlotName = "None";

    public static string CurrentSlotName =>
        _currentSlotIndex < 0 || _currentSlotIndex >= _enabledSlots.Count
            ? NoSlotName
            : _enabledSlots[_currentSlotIndex].Type.ToString();

    public static int CurrentSlotRoom =>
        _currentSlotIndex < 0 || _currentSlotIndex >= _enabledSlots.Count
            ? -1
            : _enabledSlots[_currentSlotIndex].RoomIndex;

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
            GraphInteractivity.Render(_currentOverlay);
        }
    }

    // Single entry point for one frame of mouse interaction, called while a graph is showing.
    // GraphInteractivity hands back an intent rather than calling into this namespace; applying it
    // here, with nothing in between, keeps the frame order it used to run inline.
    public static void UpdateInteractivity()
    {
        GraphInteraction interaction = GraphInteractivity.Update(_currentOverlay);

        if (interaction.DeleteRequested)
        {
            SessionManager.DeletePinned([.. GraphInteractivity.PinnedItems.Select(p => p.Key)]);
            GraphInteractivity.ClearPins(_currentOverlay);
        }

        if (interaction.NavigationSteps > 0)
        {
            NextGraph(interaction.NavigationSteps);
        }
        else if (interaction.NavigationSteps < 0)
        {
            PreviousGraph(-interaction.NavigationSteps);
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
        var slots = new List<GraphSlot>();

        foreach (ChartDefinition chart in _chartDefinitions)
        {
            if (!chart.Get(Settings)) continue;
            foreach (int room in chart.Slots())
                slots.Add(new GraphSlot(chart.Type, room));
        }

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

    public static void NextGraph(int steps = 1)
    {
        GraphInteractivity.Clear(_currentOverlay);
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        _currentSlotIndex = (_currentSlotIndex + steps) % _enabledSlots.Count;
        ShowCurrentSlot();
    }

    public static void PreviousGraph(int steps = 1)
    {
        GraphInteractivity.Clear(_currentOverlay);
        _currentOverlay = null;

        if (_enabledSlots.Count == 0)
        {
            ShowNoGraphsMessage();
            return;
        }

        _currentSlotIndex = (_currentSlotIndex - steps + _enabledSlots.Count * steps) % _enabledSlots.Count;
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

        ChartDefinition chart = DefinitionFor(slot.Type);
        _currentOverlay = chart == null ? null : GetOrCreate(chart, slot.RoomIndex);
    }

    private static void ShowNoGraphsMessage()
    {
        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupNoGraphId));
    }

    public static void HideGraph()
    {
        GraphInteractivity.Clear(_currentOverlay);
        _currentOverlay = null;
    }
}
