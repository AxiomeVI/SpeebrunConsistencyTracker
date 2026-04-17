#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Returned by HitTest when a chart element is hovered.
    /// Label is the tooltip text (use \n for multiple lines).
    /// LabelPos is the top-center screen coordinate for the tooltip.
    /// </summary>
    /// <summary>
    /// When PinGroup is non-null, at most one pin with this group value is kept at a time.
    /// Pinning a new item replaces any existing pin in the same group.
    /// </summary>
    public sealed record HoverInfo(string Label, Vector2 LabelPos, Vector2 MouseHudPos = default, string? Key = null, string? PinGroup = null);

    /// <summary>
    /// Base class for all bar chart / histogram overlays. Handles common layout, rendering pipeline,
    /// title drawing, axis drawing, and legend entry drawing.
    /// </summary>
    public abstract class BaseChartOverlay
    {
        protected readonly string title;
        protected readonly Vector2 position;
        protected readonly float width           = ChartConstants.Layout.ChartWidth;
        protected readonly float height          = ChartConstants.Layout.ChartHeight;
        protected readonly float margin          = ChartConstants.Layout.ChartMargin;
        protected readonly float marginH         = ChartConstants.Layout.ChartMarginH;
        /// <summary>Chart background rect in HUD space, for use by GraphInteractivity.</summary>
        internal Microsoft.Xna.Framework.Rectangle ChartBounds =>
            new((int)position.X, (int)position.Y, (int)width, (int)height);
        protected readonly Color backgroundColor = ChartConstants.Colors.BackgroundColor;
        protected readonly Color axisColor       = Color.White;
        protected readonly float MAX_BAR_WIDTH   = ChartConstants.Layout.MaxBarWidth;
        protected readonly HashSet<int> _hiddenColumns = new();
        protected int _hoveredColumnIndex = -1;

        protected BaseChartOverlay(string title, Vector2? pos = null)
        {
            this.title = title;
            position = pos ?? new Vector2(
                (ChartConstants.Screen.ScreenWidth  - width)  / 2,
                (ChartConstants.Screen.ScreenHeight - height) / 2);
        }

        /// <summary>
        /// Returns hover info if the mouse is over an interactive element, null otherwise.
        /// mouseHudPos is in HUD coordinates (1920x1080 space).
        /// Implementations MUST set internal hover state (_hovered* fields) as a side effect —
        /// this is required by DrawHighlight(HoverInfo) to restore state for pinned items.
        /// </summary>
        public virtual HoverInfo? HitTest(Vector2 mouseHudPos) => null;

        /// <summary>
        /// Draws the highlight for the currently hovered element.
        /// Only called when HitTest returned non-null on this frame.
        /// </summary>
        public virtual void DrawHighlight() { }

        /// <summary>
        /// When true, the overlay manages its own pin state via HandleClick/ClearPins/HasPins.
        /// GraphInteractivity will call DrawHighlight() (no-arg) for hover instead of DrawHighlight(HoverInfo).
        /// </summary>
        public virtual bool ManagesPins => false;

        /// <summary>
        /// Called when the user clicks on a hovered element. Return true if the overlay
        /// handled the click itself (skips GraphInteractivity's generic pin toggle logic).
        /// </summary>
        public virtual bool HandleClick(HoverInfo hover) => false;

        /// <summary>
        /// Returns true if the overlay has any overlay-managed pins (used to show the clear button).
        /// </summary>
        public virtual bool HasPins => false;

        /// <summary>
        /// Called by GraphInteractivity.Clear() to reset any overlay-managed pin state.
        /// </summary>
        public virtual void ClearPins() { }

        /// <summary>Clears all hidden columns, restoring full visibility.</summary>
        public virtual void ClearHiddenColumns() => _hiddenColumns.Clear();

        /// <summary>
        /// Toggles visibility of the given column index.
        /// Called by GraphInteractivity when the user clicks the label zone.
        /// </summary>
        public virtual void ToggleColumn(int columnIndex)
        {
            if (!_hiddenColumns.Remove(columnIndex))
                _hiddenColumns.Add(columnIndex);
        }

        /// <summary>
        /// Returns the column index if mousePos falls in the label-zone strip below the X-axis,
        /// null otherwise. Override in per-room chart subclasses.
        /// </summary>
        public virtual int? ColumnHitTest(Vector2 mousePos) => null;

        /// <summary>
        /// Draws the tint overlay for a single column's label-zone strip.
        /// Call once per column inside DrawLabels, after computing colX and colW.
        /// Stubs are tinted at rest; visible columns are tinted only on hover.
        /// </summary>
        // Returns the drawn strip X and width for a column, capped and centered.
        // Used by both DrawColumnStrip and ColumnHitTest so they stay in sync.
        protected static (float drawX, float drawW) ColumnStripRect(float colX, float colW)
        {
            float drawW = Math.Min(colW, ChartConstants.Interactivity.ColumnStripMaxWidth);
            float drawX = colX + (colW - drawW) / 2f;
            return (drawX, drawW);
        }

        protected void DrawColumnStrip(int columnIndex, float colX, float colW, float axisBottomY)
        {
            // Strip spans from the X-axis bottom to the end of the hit zone.
            const float stripH = ChartConstants.XAxisLabel.BaseOffsetY + ChartConstants.Interactivity.ColumnLabelHitZoneH;
            bool isHidden  = _hiddenColumns.Contains(columnIndex);
            bool isHovered = _hoveredColumnIndex == columnIndex;

            float alpha = isHidden
                ? (isHovered ? 0.35f : 0.15f)   // stub: always visible, brighter on hover
                : (isHovered ? 0.25f : 0f);      // visible column: tint on hover only

            if (alpha <= 0f) return;

            var (drawX, drawW) = ColumnStripRect(colX, colW);
            Draw.Rect(drawX, axisBottomY, drawW, stripH, Color.White * alpha);
        }

        /// <summary>
        /// When true, GraphInteractivity shows a "Delete runs" button next to "Clear pins"
        /// when at least one item is pinned. The overlay must expose GetPinnedAttemptIndices().
        /// </summary>
        public virtual bool SupportsDeleteRuns => false;

        /// <summary>
        /// Draws the highlight for a specific HoverInfo (e.g. a pinned item).
        /// Default implementation restores internal hover state via HitTest (side effect),
        /// then calls DrawHighlight(). <paramref name="info"/> must have MouseHudPos set —
        /// use instances from GraphInteractivity.CurrentHover, not manually constructed ones.
        /// </summary>
        public virtual void DrawHighlight(HoverInfo info)
        {
            HitTest(info.MouseHudPos); // side effect: sets _hovered* fields on the subclass
            DrawHighlight();
        }

        protected abstract void DrawBars(float x, float y, float w, float h);
        protected abstract void DrawLabels(float x, float y, float w, float h);

        protected virtual void DrawGrid(float x, float y, float w, float h) { }

        protected void DrawYAxisLine(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x, y), new Vector2(x, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
        }

        protected virtual void DrawXAxisLine(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x - 1, y + h), new Vector2(x + w + 1, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
        }

        protected void DrawTitle()
        {
            Vector2 titleSize = ActiveFont.Measure(title) * ChartConstants.FontScale.Title;
            ActiveFont.DrawOutline(
                title,
                new Vector2(position.X + width / 2 - titleSize.X / 2, position.Y + 10),
                new Vector2(0f, 0f),
                Vector2.One * ChartConstants.FontScale.Title,
                Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        protected static void DrawLegendEntry(float x, float y, string text, Color color, float scale, bool right = false)
        {
            Vector2 textSize = ActiveFont.Measure(text) * scale;
            float boxSize    = ChartConstants.Legend.LegendBoxSize;
            float spacing    = ChartConstants.Legend.LegendBoxTextGap;
            float totalWidth = textSize.X + boxSize + spacing;
            float startX     = right ? x - totalWidth : x;
            float boxY       = y + (textSize.Y / 2f) - (boxSize / 2f);

            Draw.Rect(startX, boxY, boxSize, boxSize, color);
            ActiveFont.DrawOutline(
                text,
                new Vector2(startX + boxSize + spacing, y),
                new Vector2(0f, 0f),
                Vector2.One * scale,
                Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        /// <summary>
        /// Draws X-axis column labels with optional stagger for dense charts (>25 items).
        /// </summary>
        protected void DrawXAxisStaggeredLabels(
            float x, float y, float h,
            int itemCount, float columnWidth,
            Func<int, string> getLabel,
            Color labelColor)
        {
            float baseLabelY = y + h + ChartConstants.XAxisLabel.BaseOffsetY;
            for (int i = 0; i < itemCount; i++)
            {
                float labelX = x + i * columnWidth + columnWidth / 2f;
                string label = getLabel(i);
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;
                float labelY = itemCount > ChartConstants.XAxisLabel.StaggerThreshold
                    ? (i % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                    : baseLabelY;

                ActiveFont.DrawOutline(
                    label,
                    new Vector2(labelX - labelSize.X / 2, labelY),
                    new Vector2(0f, 0f),
                    Vector2.One * ChartConstants.FontScale.AxisLabel,
                    labelColor, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }

        /// <summary>
        /// Computes Y-axis step size and tick count for a frame-aligned time range.
        /// Used by time-based bar charts and scatter plot.
        /// </summary>
        protected static void GetFrameAxisSettings(long range, out long step, out int count)
        {
            if (range <= 0)
            {
                step  = ChartConstants.Time.OneFrameTicks;
                count = 1;
                return;
            }
            long totalFrames   = (long)Math.Ceiling((double)range / ChartConstants.Time.OneFrameTicks);
            long framesPerTick = (long)Math.Ceiling((double)totalFrames / ChartConstants.Axis.MaxTickMarks);
            if (framesPerTick <= 0) framesPerTick = 1;
            step  = framesPerTick * ChartConstants.Time.OneFrameTicks;
            count = (int)(range / step);
        }

        public virtual void Render()
        {
            Draw.Rect(position, width, height, backgroundColor);
            float gx = MathF.Round(position.X + marginH);
            float gy = MathF.Round(position.Y + margin);
            float gw = MathF.Round(position.X + width  - marginH) - gx;
            float gh = MathF.Round(position.Y + height - margin)  - gy;
            DrawGrid(gx, gy, gw, gh);
            DrawYAxisLine(gx, gy, gw, gh);
            DrawBars(gx, gy, gw, gh);
            DrawXAxisLine(gx, gy, gw, gh);
            DrawLabels(gx, gy, gw, gh);
        }
    }
}
