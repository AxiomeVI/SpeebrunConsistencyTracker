using System;
using System.Collections.Generic;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Charts;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities;

// What one frame of mouse interaction asked for, left to the caller to apply. GraphInteractivity
// returns an intent instead of driving the graph manager itself, so this folder stays free of a
// dependency on SessionManagement.
// NavigationSteps is signed: positive moves forward through the graph slots, negative back.
public readonly record struct GraphInteraction(int NavigationSteps, bool DeleteRequested)
{
    public static readonly GraphInteraction None   = new(0, false);
    public static readonly GraphInteraction Delete = new(0, true);

    public static GraphInteraction Navigate(int steps) => new(steps, false);
}

// The mouse on the chart that is showing: what it hovers, what a click pins, the buttons around
// the chart, the tooltips and the cursor. The pointer, the buttons, the tooltip placement and the
// cursor are CelesteCharts'; what a click means is decided here.
//
// Update runs from Level.Update and Render from the chart surface's Render, which may run more
// than once per update: everything a click does is done in Update.
public static class GraphInteractivity
{
    private static readonly Area Screen = new(0f, 0f, ChartConstants.Screen.ScreenWidth, ChartConstants.Screen.ScreenHeight);
    private static readonly Vector2 Nowhere = new(-9999f, -9999f);

    private static readonly List<HoverInfo> _pinnedItems = new();
    public static IReadOnlyList<HoverInfo> PinnedItems => _pinnedItems;

    public static HoverInfo? CurrentHover { get; private set; }

    // The arrows under the chart; in its bottom left corner, while something is pinned, Clear pins and,
    // where a pin is a run, Delete.
    private static readonly ButtonBar _nav = new(captureId: Charts.CaptureIds.Nav), _deleteBar = new(captureId: Charts.CaptureIds.Delete), _clearBar = new(captureId: Charts.CaptureIds.ClearPins);
    private static readonly BarButton _clear = _clearBar.Action(" ");
    private static readonly BarButton _back = _nav.Action("<"), _next = _nav.Action(">");
    private static readonly BarButton _delete = _deleteBar.Action(" ");
    private static BaseChartOverlay _placedFor, _pinsFor;
    private static int _pinsRevision;
    private static bool _placed, _showDelete, _showClear;
    private static float _placedLeft, _placedFoot;
    private static ChartHelp _placedHelp;
    // What each chart shows and what the mouse does on it, opened from the "?" in its top left
    // corner. One card per kind of chart; the library has them open and close together.
    private static readonly Dictionary<string, HintCard> _hints = [];
    private static HintCard _hint;
    // What a chart is handed while the mouse is on the card: a pointer that is nowhere and presses
    // nothing, so no click on the card reaches the chart under it.
    private static readonly Pointer _away = new();
    private static bool _mouseTaken;

    private static Sketch _tips;
    private static bool _tipsStale = true;

    // The bars are laid out against the chart, once the game's font can be measured: not at load.
    private static void Place(BaseChartOverlay overlay)
    {
        // Also when its own buttons change: the wall's are an open histogram's while one is open.
        float toolsLeft = overlay.ToolsLeft;
        // And when its help does: the scatter's names the target line only while it shows.
        ChartHelp help = overlay.Help;
        // And when Reset zoom comes or goes: Clear pins and Delete stand beside it.
        float footLeft = overlay.FootLeft;
        if (_placed && ReferenceEquals(overlay, _placedFor) && toolsLeft == _placedLeft && help == _placedHelp && footLeft == _placedFoot) return;
        _placedFoot = footLeft;
        _placedLeft = toolsLeft;
        _placedHelp = help;
        Rectangle bounds = overlay.ChartBounds;
        _placed    = true;
        _placedFor = overlay;
        ChartStyle style = SctChartStyle.Current;
        TextMetrics metrics = GameCanvas.FontMetrics();
        _nav.Place(metrics, style, bounds.X + bounds.Width / 2f, bounds.Bottom + 8f, 0.5f, 0f, Screen);
        // The label is read here and not where the button is made: the dialog files load after mods do.
        _delete.Label  = Dialog.Clean(DialogIds.ChartDelete);
        _delete.Colour = GameCanvas.ToRgba(Color.Crimson);
        _clear.Label = Dialog.Clean(DialogIds.ChartClearPins);
        // In the panel's bottom left corner, after Reset zoom.
        _clearBar.Place(metrics, style, footLeft, bounds.Bottom - 6f, 0f, 1f, Screen);
        _deleteBar.Place(metrics, style, _clearBar.Bounds.Right + 8f, bounds.Bottom - 6f, 0f, 1f, Screen);

        // The card opens from its "?" only: nothing covers a chart unasked. What the chart shows,
        // a blank line, then what the mouse does on it.
        string key = help.About + help.Mouse;
        if (!_hints.TryGetValue(key, out _hint))
            _hints[key] = _hint = new HintCard(
                Dialog.Clean(DialogIds.ChartHintTitle), [.. Lines(help.About), "", .. Lines(help.Mouse)],
                Dialog.Clean(DialogIds.ChartHintClose), captureId: Charts.CaptureIds.Hint)
            {
                // Pointing at the "?" shows the card; a click keeps it.
                OpensOnHover = true,
                // Kept open or closed across game starts.
                StartsOpen    = Charts.ChartOptions.HelpOpen,
                OnOpenChanged = open => Charts.ChartOptions.HelpOpen = open,
            };
        _hint.PlaceIn(metrics, style, new Area(bounds.X, bounds.Y, bounds.Width, bounds.Height), WidgetAnchor.TopLeft, 6f, 6f);
    }

    // A dialog line holds several lines of help, split on the bar.
    private static string[] Lines(string key) => Dialog.Clean(key).Split('|', System.StringSplitOptions.TrimEntries);

    public static GraphInteraction Update(BaseChartOverlay overlay, bool mouseTaken = false)
    {
        Pointer real = GameInput.Current();
        _away.Update(new PointerInput(Nowhere.X, Nowhere.Y, Seconds: real.Input.Seconds));
        // The trajectory widget lies over the chart and holds the mouse: the chart, its buttons
        // and its card are told the mouse is nowhere.
        _mouseTaken = mouseTaken && overlay != null;
        Pointer mouse = _mouseTaken ? _away : real;
        if (overlay == null)
        {
            SetHover(null);
            return GraphInteraction.None;
        }
        Place(overlay);

        _showDelete = _pinnedItems.Count > 0 && overlay.SupportsDeleteRuns;
        _showClear  = _pinnedItems.Count > 0 || overlay.HasPins;
        // First: the card lies over the chart and takes what is pressed on it.
        _hint.Update(mouse);
        bool onHint = _hint.Contains(mouse.X, mouse.Y);
        _nav.Update(mouse);
        if (_showDelete) _deleteBar.Update(mouse);
        if (_showClear) _clearBar.Update(mouse);
        bool onControl = overlay.UpdateControls(onHint ? _away : mouse) || onHint;
        if (overlay.TakeDroppedPins()) ClearPins(overlay);
        onControl |= _nav.Contains(mouse.X, mouse.Y) || (_showDelete && _deleteBar.Contains(mouse.X, mouse.Y))
                     || (_showClear && _clearBar.Contains(mouse.X, mouse.Y));

        // A control under the mouse takes it: the chart is asked about nowhere, which also clears
        // what it had hovered.
        Vector2 at = onControl ? Nowhere : new Vector2(mouse.X, mouse.Y);
        SetHover(overlay.HitTest(at));
        LocatePins(overlay);
        int? column = overlay.ColumnHitTest(at); // also lights the strip under the mouse

        // The caller deletes the pinned runs and then calls ClearPins.
        if (_showDelete && _delete.Clicked) return GraphInteraction.Delete;
        if (_showClear && _clear.Clicked)
        {
            ClearPins(overlay);
            return GraphInteraction.None;
        }
        int steps = _back.Clicked ? -1 : _next.Clicked ? 1 : 0;
        if (steps != 0) return GraphInteraction.Navigate(steps);
        if (onControl || !mouse.Clicked()) return GraphInteraction.None;

        if (CurrentHover != null)
        {
            if (overlay.HandleClick(CurrentHover)) return GraphInteraction.None;
            int existing = CurrentHover.Key != null
                ? _pinnedItems.FindIndex(p => p.Key == CurrentHover.Key)
                : _pinnedItems.FindIndex(p => p.Label == CurrentHover.Label);
            if (existing >= 0) _pinnedItems.RemoveAt(existing);
            else _pinnedItems.Add(CurrentHover);
            _tipsStale = true;
        }
        else if (column.HasValue)
        {
            overlay.ToggleColumn(column.Value);
            ClearPins(overlay);
        }
        return GraphInteraction.None;
    }

    private static void SetHover(HoverInfo hover)
    {
        if (hover?.Key != CurrentHover?.Key || hover?.Label != CurrentHover?.Label || hover?.Anchor != CurrentHover?.Anchor)
            _tipsStale = true;
        CurrentHover = hover;
    }

    // A pin stays where its mark goes: through a scroll, a zoom, and a chart rebuilt from new data.
    // On the same chart a mark that no longer shows keeps its pin, for when it shows again.
    private static void LocatePins(BaseChartOverlay overlay)
    {
        bool sameChart = ReferenceEquals(overlay, _pinsFor);
        int revision = overlay.Revision;
        if (sameChart && revision == _pinsRevision) return;
        (_pinsFor, _pinsRevision) = (overlay, revision);
        if (_pinnedItems.Count == 0) return;
        for (int i = _pinnedItems.Count - 1; i >= 0; i--)
        {
            HoverInfo located = overlay.Locate(_pinnedItems[i].Key);
            if (located != null) _pinnedItems[i] = located;
            else if (sameChart) _pinnedItems[i] = _pinnedItems[i] with { Mark = -1 };
            else _pinnedItems.RemoveAt(i);
        }
        _tipsStale = true;
    }

    public static void Clear(BaseChartOverlay overlay)
    {
        CurrentHover = null;
        // Not kept past the chart they were for.
        (_placedFor, _pinsFor) = (null, null);
        _tipsStale   = true;
        _pinnedItems.Clear();
        overlay?.ClearPins();
        overlay?.ClearHiddenColumns();
        overlay?.OnLeft();
    }

    public static void ClearPins(BaseChartOverlay overlay)
    {
        _pinnedItems.Clear();
        _tipsStale = true;
        overlay?.ClearPins();
    }

    public static void Render(BaseChartOverlay overlay)
    {
        if (overlay == null || !_placed) return;

        foreach (HoverInfo pinned in _pinnedItems)
            if (pinned.Mark >= 0) overlay.DrawHighlight(pinned);
        if (CurrentHover != null) overlay.DrawHighlight(CurrentHover);

        // Buttons before tooltips, so tooltips land on top.
        overlay.DrawControls();
        GameCanvas.Draw(_nav.Sketch);
        if (_showDelete) GameCanvas.Draw(_deleteBar.Sketch);
        if (_showClear) GameCanvas.Draw(_clearBar.Sketch);
        GameCanvas.Draw(_hint.Sketch);

        if (_tipsStale) BuildTooltips();
        // A tooltip is read, not looked through: drawn even while the chart is hidden for Edit mode.
        float fade = GameCanvas.Fade;
        GameCanvas.Fade = 1f;
        GameCanvas.Draw(_tips);
        GameCanvas.Fade = fade;

        // Celeste hides the system pointer; the library draws one, shared with any other mod's charts.
        if (!_mouseTaken) ChartCursor.Request(overlay.Cursor, SctChartStyle.Current);
    }

    // A tooltip over each pinned item and over the hovered one, moved off each other. Rebuilt when
    // the hover or the pins change, never per frame.
    private static void BuildTooltips()
    {
        _tipsStale = false;
        _tips ??= new Sketch(GameCanvas.FontMetrics());
        _tips.Clear();
        ChartStyle style = SctChartStyle.Current;

        List<(HoverInfo Item, bool Pinned)> shown = [];
        foreach (HoverInfo pinned in _pinnedItems)
            if (pinned.Mark >= 0 && pinned.Label.Length > 0) shown.Add((pinned, true));
        if (CurrentHover is { Label.Length: > 0 } hover && !shown.Exists(s => s.Item.Key != null && s.Item.Key == hover.Key))
            shown.Add((hover, false));
        if (shown.Count == 0) return;

        Area[] areas = new Area[shown.Count];
        for (int i = 0; i < areas.Length; i++)
        {
            (float w, float h) = Paint.TooltipSize(_tips, shown[i].Item.Label, style);
            areas[i] = TooltipLayout.At(shown[i].Item.Anchor.X, shown[i].Item.Anchor.Y, w, h, Screen);
        }
        TooltipLayout.Separate(areas, Screen);
        for (int i = 0; i < areas.Length; i++)
            Paint.TooltipBox(_tips, shown[i].Item.Label, areas[i], style, shown[i].Pinned);
    }
}

public static class PinKey
{
    private const string SegPrefix  = "seg:";
    private const string RoomPrefix = "room:";

    public static string ForSegment(int attemptIndex) => $"{SegPrefix}{attemptIndex}";
    public static string ForRoom(int attemptIndex, int visibleRoomIndex) => $"{RoomPrefix}{attemptIndex}:{visibleRoomIndex}";

    public static bool TryParseSegment(string? key, out int attemptIndex)
    {
        attemptIndex = 0;
        return key != null && key.StartsWith(SegPrefix) && int.TryParse(key.Substring(SegPrefix.Length), out attemptIndex);
    }

    public static bool TryParseRoom(string? key, out int attemptIndex, out int visibleRoomIndex)
    {
        attemptIndex = 0;
        visibleRoomIndex = 0;
        if (key == null || !key.StartsWith(RoomPrefix)) return false;
        var parts = key.Split(':');
        return parts.Length == 3
            && int.TryParse(parts[1], out attemptIndex)
            && int.TryParse(parts[2], out visibleRoomIndex);
    }
}
