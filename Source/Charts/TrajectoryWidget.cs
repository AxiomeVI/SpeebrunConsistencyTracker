using System;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// The run in progress against the baseline, in a corner while the player plays: one line that grows
// a room at a time, in the Ahead colour where the run is ahead and the Behind colour where it is
// behind. It answers the mouse only while it is being placed: under the Run Trajectory chart, once
// its Edit widget button is on.
// It is then moved by its middle and resized by its edges. Under a chart it is otherwise hidden.
//
// Nothing allocates per frame: the sketch is rebuilt when the session's version, the room count,
// the baseline, the gap option, the size or the style changes.
internal static class TrajectoryWidget
{
    // Clear of the chart's own buttons, which stand in its panel's top right corner: on them, the
    // widget would cover the button that turns it off.
    private static readonly WidgetPlacement Default = new(WidgetAnchor.TopRight, 24f, 140f, 170f, 125f);
    // The plot's height stands for at least twice this much, so a two-frame gap does not fill it.
    private const long MinHalfRange = TimeSpan.TicksPerSecond / 2;
    private const float Pad = 6f;

    private static SpeebrunConsistencyTrackerModuleSettings Settings => SpeebrunConsistencyTrackerModule.Settings;

    private static LiveWidget _widget;
    private static Sketch _chart, _frame;
    // What the mouse is while the widget must not answer it: nowhere, pressing nothing.
    private static readonly Pointer _away = new();

    private static PracticeSession _builtFor;
    private static uint _builtVersion;
    private static int _builtRooms;
    private static TrajectoryBaseline _builtBaseline;
    private static (float W, float H) _builtSize;
    private static ChartStyle _builtStyle;
    private static bool _builtDelta;
    private static int _builtTimer;
    // Whether the widget held the mouse on the last update: it then asks for the cursor.
    private static bool _taken;

    // Asked for from the Run Trajectory chart, and over with it: never kept, so a chart opens clean.
    internal static bool Placing { get; set; }

    // Moved and resized only while placed; hidden under a chart otherwise.
    internal static bool Taken => _taken;
    // How strongly the chart under the widget is drawn: 0 while the widget is being placed. It
    // gets there and back over FadeSeconds; gone in one frame, the chart looks like it broke.
    internal static float ChartFade { get; private set; } = 1f;
    private const float FadeSeconds = 0.2f;
    internal static bool Editing => Placing && GraphManager.CurrentOverlay is TrajectoryChart;
    private static bool Shown => Settings.Enabled && Settings.TrajectoryWidget && SessionManager.CurrentSession != null
                                 && (GraphManager.CurrentOverlay == null || Editing);

    // Never moved nor resized: where a player who has not placed it yet finds it.
    internal static bool AtDefault => Widget().Placement == Default;

    // The chart's own On | Off. Switched on, the widget shows at once to be placed, but only while
    // it is still where it ships: a player who placed it has nothing left to do there.
    internal static void Switch(bool on)
    {
        ChartOptions.TrajectoryWidget = on;
        Placing = on && AtDefault;
    }

    // Where it is, as the settings hold it. For the tests.
    internal static string LayoutText => Widget().Placement.ToText();

    private static LiveWidget Widget()
    {
        if (_widget != null) return _widget;
        WidgetPlacement placement = Default;
        WidgetPlacement.TryParse(Settings.TrajectoryWidgetLayout, ref placement);
        // A settings file can hold any size and place: bring it inside the screen and the sizes a
        // resize allows.
        Area screen = new(0f, 0f, ChartConstants.Screen.ScreenWidth, ChartConstants.Screen.ScreenHeight);
        Area read = placement.Bounds(screen);
        // A hand-edited NaN passes the library's parse and every clamp.
        if (!float.IsFinite(read.X + read.Y + read.Width + read.Height)) read = Default.Bounds(screen);
        float w = Math.Clamp(read.Width, 120f, 960f), h = Math.Clamp(read.Height, 60f, 540f);
        placement = WidgetPlacement.Nearest(
            new Area(Math.Clamp(read.X, 0f, screen.Width - w), Math.Clamp(read.Y, 0f, screen.Height - h), w, h), screen);
        return _widget = new LiveWidget(CaptureIds.TrajectoryWidget, placement) { Resizable = true };
    }

    // One frame of the mouse. Answers whether the widget holds it: the chart under it is then
    // told the mouse is nowhere.
    internal static bool Update(Pointer mouse)
    {
        if (GraphManager.CurrentOverlay is not TrajectoryChart || !Settings.TrajectoryWidget) Placing = false;
        // Another chart, or none, is never faint: only the trajectory chart fades, around Edit mode.
        ChartFade = GraphManager.CurrentOverlay is not TrajectoryChart ? 1f
            : Monocle.Calc.Approach(ChartFade, Editing ? 0f : 1f, Monocle.Engine.RawDeltaTime / FadeSeconds);
        if (!Shown && _widget == null) return _taken = false;
        LiveWidget widget = Widget();
        widget.IdleOpacity = Math.Clamp(Settings.TrajectoryWidgetOpacity, 0, 100) / 100f;
        bool editing = Shown && Editing;
        _away.Update(new PointerInput(-9999f, -9999f, Seconds: mouse.Input.Seconds));
        // Not editing, it still updates: a drag the chart's closing interrupted ends here, and is saved.
        // A drag of the chart that crosses the widget is the chart's: the widget does not light up.
        widget.Update(editing && mouse.FreeFor(CaptureIds.TrajectoryWidget) ? mouse : _away, 0);
        // A static lambda: one that captured a local would allocate on every call of this method.
        if (widget.Placed)
            ChartOptions.Save(true, static () => Settings.TrajectoryWidgetLayout = _widget.Placement.ToText());
        // Held, just released, or under the mouse: the release of a resize does not click the chart.
        return _taken = editing && (widget.Dragging || widget.Placed || widget.Hovered);
    }

    // From ChartSurface, after the chart: over its panel.
    internal static void Render()
    {
        if (!Shown) return;
        LiveWidget widget = Widget();
        PracticeSession session = SessionManager.CurrentSession;
        ChartStyle style = SctChartStyle.Current;
        Area bounds = widget.Bounds;
        (float, float) size = (bounds.Width, bounds.Height);
        int rooms = SessionManager.RoomCount;
        TrajectoryBaseline baseline = ChartOptions.TrajectoryBaseline;
        bool delta = Settings.TrajectoryWidgetDelta;
        // The room timer's type moves the segment without a new session version, as ChartCache knows.
        int timer = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        if (_chart == null || !ReferenceEquals(session, _builtFor) || session.Version != _builtVersion || rooms != _builtRooms
            || baseline != _builtBaseline || size != _builtSize || !ReferenceEquals(style, _builtStyle) || delta != _builtDelta || timer != _builtTimer)
        {
            (_builtFor, _builtVersion, _builtRooms, _builtBaseline, _builtSize, _builtStyle, _builtDelta, _builtTimer) = (session, session.Version, rooms, baseline, size, style, delta, timer);
            _chart ??= new Sketch(GameCanvas.FontMetrics());
            _chart.Clear();
            Build(_chart, new Area(0f, 0f, bounds.Width, bounds.Height), LiveTrajectory.Build(session, rooms, baseline), delta, style);
        }
        GameCanvas.Draw(_chart, bounds.X, bounds.Y, bounds, widget.Opacity);

        if (!Editing) return;
        // A frame while it can be moved, so it reads as a thing to grab over the chart.
        _frame ??= new Sketch(_chart.Metrics);
        _frame.Clear();
        _frame.Outline(bounds, style.Accent, 2f);
        GameCanvas.Draw(_frame);
        // Editing, the chart is hidden and is told the mouse is nowhere: the pointer is asked for here.
        if (_taken) ChartCursor.Request(widget.Cursor, style);
        else if (Editing) ChartCursor.Request(CursorShape.Crosshair, style);
    }

    // In the widget's own coordinates, from (0, 0). Ahead is a negative deviation, drawn up.
    private static void Build(Sketch sketch, Area area, LiveLine line, bool showDelta, ChartStyle style)
    {
        sketch.Fill(area, style.Panel);
        Area plot = area.Inset(Pad);
        // The number has a row of its own above the plot: a line that reaches the right edge near
        // the top would run under it.
        float head = showDelta ? MathF.Ceiling(sketch.LineHeight(style.Label)) : 0f;
        float numberY = plot.Y;
        plot = plot with { Y = plot.Y + head, Height = plot.Height - head };

        long[] deviations = line.CumulativeDeviations;
        bool drawn = line.HasBaseline && deviations.Length > 0 && line.TotalRooms > 0;
        // From the furthest ahead to the furthest behind the run has been, zero included: zero sits
        // where that puts it. A range under the least one is widened, on both sides against the
        // average, where a run starts as likely ahead as behind, and downwards against the best run
        // or the sum of bests, which a run is behind nearly always.
        long lo = 0, hi = 0;
        if (drawn) foreach (long d in deviations) (lo, hi) = (Math.Min(lo, d), Math.Max(hi, d));
        long missing = Math.Max(0, 2 * MinHalfRange - (hi - lo));
        if (line.Baseline == TrajectoryBaseline.Average) (lo, hi) = (lo - missing / 2, hi + missing - missing / 2);
        else hi += missing;
        Scale<long> y = new(Durations.Deltas, lo, hi, plot.Y, plot.Bottom);
        // Where the scale puts zero, unrounded: the line is split there, so the side a point is
        // coloured on is the side of its sign. Only the axis is put on a whole pixel.
        float zeroY = y.At(0);
        sketch.Fill(new Area(plot.X, MathF.Round(zeroY), plot.Width, 1f), style.Axis);
        if (!drawn) return;

        // The line starts at zero on the left edge; room r ends at its share of the width.
        float[] xs = new float[deviations.Length + 1], ys = new float[deviations.Length + 1];
        (xs[0], ys[0]) = (plot.X, zeroY);
        for (int r = 0; r < deviations.Length; r++)
        {
            xs[r + 1] = plot.X + plot.Width * (r + 1) / line.TotalRooms;
            ys[r + 1] = y.At(deviations[r]);
        }
        (LinePoints ahead, LinePoints behind) = Polyline.SplitAtY(new LinePoints(xs, ys), zeroY);
        ChartPalette palette = ChartPalette.Current;
        Rgba aheadColour = GameCanvas.ToRgba(palette.Ahead), behindColour = GameCanvas.ToRgba(palette.Behind);
        if (ahead.Count > 1)
        {
            Paint.AreaUnder(sketch, ahead, zeroY, aheadColour.Times(0.25f));
            Paint.Line(sketch, ahead, aheadColour, 2f);
        }
        if (behind.Count > 1)
        {
            Paint.AreaUnder(sketch, behind, zeroY, behindColour.Times(0.25f));
            Paint.Line(sketch, behind, behindColour, 2f);
        }
        // The gap so far as a number, in the colour of its side: an option, on unless switched off.
        if (showDelta)
        {
            long last = deviations[^1];
            sketch.Text(TrajectoryChart.FormatDev(last), plot.Right, numberY, style.Label with { Colour = last <= 0 ? aheadColour : behindColour }, 1f, 0f);
        }
    }
}
