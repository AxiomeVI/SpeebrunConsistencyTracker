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

    // The enabled charts, in cycle order.
    private static List<GraphType> _enabledSlots = [];
    private static int _currentSlotIndex = -1;
    private static BaseChartOverlay _currentOverlay;

    private static GraphType LastShownType {
        get => SpeebrunConsistencyTrackerModule.Settings.LastShownGraph;
        set => SpeebrunConsistencyTrackerModule.Settings.LastShownGraph = value;
    }

    // There is a session to build charts from.
    public static bool HasSession => SessionManager.CurrentSession != null;

    public static void Init()
    {
        _lastKnownSession = SessionManager.CurrentSession;
        ClearAllCharts();
        RebuildEnabledSlots();
    }

    public static void Clear()
    {
        // Before _currentOverlay is nulled: passing the null afterwards left the overlay's own
        // pins and hidden columns in place, so a cleared session came back with them.
        GraphInteractivity.Clear(_currentOverlay);
        _lastKnownSession = null;
        _currentOverlay   = null;
        _currentSlotIndex = -1;
        _enabledSlots.Clear();
        ClearAllCharts();
    }

    public static bool IsShowing() => _currentOverlay != null;

    public static BaseChartOverlay CurrentOverlay => _currentOverlay;

    // Says whether a chart is selected, and which. A string and not a GraphType member, because
    // GraphType is persisted and every member must have a ChartDefinition row. Read every frame:
    // the bounds check keeps it from throwing on a cursor left past a shrunken list.
    public const string NoSlotName = "None";

    public static string CurrentSlotName =>
        _currentSlotIndex < 0 || _currentSlotIndex >= _enabledSlots.Count
            ? NoSlotName
            : _enabledSlots[_currentSlotIndex].ToString();

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
    // GraphInteractivity hands back an intent rather than calling into this namespace: it is
    // applied here at once, in the same frame.
    public static void UpdateInteractivity(bool mouseTaken = false)
    {
        GraphInteraction interaction = GraphInteractivity.Update(_currentOverlay, mouseTaken);

        if (interaction.DeleteRequested)
        {
            SessionManager.DeletePinned([.. GraphInteractivity.PinnedItems.Select(p => p.Key)]);
            GraphInteractivity.ClearPins(_currentOverlay);
        }

        if (interaction.NavigationSteps != 0) Navigate(interaction.NavigationSteps);
    }

    public static void RebuildEnabledSlots()
    {
        GraphType prevType = _currentSlotIndex >= 0 && _enabledSlots.Count > 0
            ? _enabledSlots[_currentSlotIndex] : LastShownType;
        bool wasShowing = IsShowing();

        _enabledSlots = BuildSlots();

        int restored = FindBestSlot(prevType);
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

    private static List<GraphType> BuildSlots()
        => [.. _chartDefinitions.Where(chart => chart.Get(Settings)).Select(chart => chart.Type)];

    private static int FindBestSlot(GraphType type) => _enabledSlots.IndexOf(type);

    // Next and Previous as the player asks for them, by button or hotkey: first offered to the
    // chart showing, which may have views of its own to move among.
    public static void Navigate(int steps)
    {
        if (steps == 0 || (_currentOverlay?.Step(steps) ?? false)) return;
        if (steps > 0) NextGraph(steps);
        else PreviousGraph(-steps);
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
            int restored = FindBestSlot(LastShownType);
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

        GraphType type = _enabledSlots[_currentSlotIndex];

        // Saved here: Everest writes the settings file when Mod Options closes, not when the game does.
        if (LastShownType != type) Charts.ChartOptions.Save(true, () => LastShownType = type);

        ChartDefinition chart = DefinitionFor(type);
        _currentOverlay = chart == null ? null : GetOrCreate(chart);
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
