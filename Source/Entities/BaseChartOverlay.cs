using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    // Label is the tooltip text (a line break for a line, a tab for a column); Anchor the point it
    // sits over, in HUD space. Key names the thing hovered for as long as the session's data stands:
    // a pin is kept by it. Mark is the chart's own index of it, good until the chart's Revision
    // changes; below 0, the thing is pinned but does not show.
    public sealed record HoverInfo(string Label, Vector2 Anchor, string? Key = null, int Mark = -1);

    public readonly record struct ChartHelp(string Subtitle, string About, string Mouse);

    // What GraphManager and GraphInteractivity need of a chart: where it is, what is under the
    // mouse, which columns are hidden. Drawing is SketchChart's, under Source/Charts/.
    public abstract class BaseChartOverlay
    {
        protected string title;
        protected readonly Vector2 position;
        protected readonly float width   = ChartConstants.Layout.ChartWidth;
        protected readonly float height  = ChartConstants.Layout.ChartHeight;
        protected readonly float margin  = ChartConstants.Layout.ChartMargin;
        protected readonly float marginH = ChartConstants.Layout.ChartMarginH;
        // HUD space.
        internal Microsoft.Xna.Framework.Rectangle ChartBounds =>
            new((int)position.X, (int)position.Y, (int)width, (int)height);
        protected readonly HashSet<int> _hiddenColumns = new();

        protected BaseChartOverlay(string title)
        {
            this.title = title;
            position = new Vector2(
                (ChartConstants.Screen.ScreenWidth  - width)  / 2,
                (ChartConstants.Screen.ScreenHeight - height) / 2);
        }

        public abstract void Render();

        // mouseHudPos is in HUD space (1920x1080).
        public virtual HoverInfo? HitTest(Vector2 mouseHudPos) => null;

        // What a pin's key names, where it is now: null while it does not show.
        public virtual HoverInfo? Locate(string key) => null;

        // Changes when the chart's marks may have moved: the pins are then located again.
        internal virtual int Revision => 0;

        // The mark of a HoverInfo this chart gave, lit.
        public virtual void DrawHighlight(HoverInfo info) { }

        // The chart's own controls (the Absolute | Relative switch). Update answers whether the
        // mouse is on one, which then takes the click.
        internal virtual bool UpdateControls(Pointer pointer) => false;
        internal virtual void DrawControls() { }
        // The chart's dialog lines of help: one sentence under its title, and the two parts of the
        // card the "?" opens, what the chart shows and what the mouse does on it.
        internal abstract ChartHelp Help { get; }
        // The pointer to draw while the mouse is on this chart.
        internal virtual CursorShape Cursor => CursorShape.Crosshair;
        // Where the chart's own buttons start in its top right corner: Delete runs sits left of them.
        internal virtual float ToolsLeft => ChartBounds.Right;

        // Returning true skips GraphInteractivity's generic pin toggle.
        public virtual bool HandleClick(HoverInfo hover) => false;

        // Next and Previous, by button or hotkey: a chart that holds several views (the wall's open
        // histogram) moves among them and answers true; otherwise the next chart is shown.
        internal virtual bool Step(int steps) => false;

        // The player moved to another chart or closed this one.
        internal virtual void OnLeft() { }

        // Where the pins' buttons start, at the foot of the panel: its corner, or past a button of
        // the chart's own that stands there.
        internal virtual float FootLeft => ChartBounds.X + 6f;

        public virtual bool HasPins => false;

        // A chart asks for every pin to be dropped: GraphInteractivity, which holds them, takes
        // the request on its next update.
        private bool _dropPins;
        protected void DropPins() => _dropPins = true;
        internal bool TakeDroppedPins()
        {
            bool asked = _dropPins;
            _dropPins = false;
            return asked;
        }

        public virtual void ClearPins() { }

        public virtual void ClearHiddenColumns() => _hiddenColumns.Clear();

        // This chart replaces `previous`, the same chart on older data: it takes what the player
        // did to that one (hidden rooms, scroll, zoom). An override calls base first.
        internal virtual void TakeViewFrom(BaseChartOverlay previous)
        {
            _hiddenColumns.UnionWith(previous._hiddenColumns);
        }

        public virtual void ToggleColumn(int columnIndex)
        {
            if (!_hiddenColumns.Remove(columnIndex))
                _hiddenColumns.Add(columnIndex);
        }

        // The column whose strip, under the axis, the mouse is on: a click hides or shows it.
        public virtual int? ColumnHitTest(Vector2 mousePos) => null;

        // When true, a pinned item's key names a run and a room (PinKey), and Delete runs is offered.
        public virtual bool SupportsDeleteRuns => false;
    }
}
