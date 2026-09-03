using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    // Label is the tooltip text (\n for multiple lines); LabelPos its top-center HUD coordinate.
    // A non-null PinGroup keeps at most one pin per group value: a new pin replaces the old one.
    public sealed record HoverInfo(string Label, Vector2 LabelPos, Vector2 MouseHudPos = default, string? Key = null, string? PinGroup = null);

    public abstract class BaseChartOverlay
    {
        protected readonly string title;
        protected readonly Vector2 position;
        protected readonly float width           = ChartConstants.Layout.ChartWidth;
        protected readonly float height          = ChartConstants.Layout.ChartHeight;
        protected readonly float margin          = ChartConstants.Layout.ChartMargin;
        protected readonly float marginH         = ChartConstants.Layout.ChartMarginH;
        // HUD space.
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

        // mouseHudPos is in HUD space (1920x1080). Implementations must set their _hovered*
        // fields as a side effect: DrawHighlight(HoverInfo) restores pinned state through them.
        public virtual HoverInfo? HitTest(Vector2 mouseHudPos) => null;

        public virtual void DrawHighlight() { }

        // When true, GraphInteractivity leaves pinning to the overlay and draws hover with the
        // no-arg DrawHighlight().
        public virtual bool ManagesPins => false;

        // Returning true skips GraphInteractivity's generic pin toggle.
        public virtual bool HandleClick(HoverInfo hover) => false;

        public virtual bool HasPins => false;

        public virtual void ClearPins() { }

        public virtual void ClearHiddenColumns() => _hiddenColumns.Clear();

        public virtual void ToggleColumn(int columnIndex)
        {
            if (!_hiddenColumns.Remove(columnIndex))
                _hiddenColumns.Add(columnIndex);
        }

        // Hits the label-zone strip below the X-axis. Overridden by per-room charts.
        public virtual int? ColumnHitTest(Vector2 mousePos) => null;

        // Used by both DrawColumnStrip and ColumnHitTest so they stay in sync.
        protected static (float drawX, float drawW) ColumnStripRect(float colX, float colW)
        {
            float drawW = Math.Min(colW, ChartConstants.Interactivity.ColumnStripMaxWidth);
            float drawX = colX + (colW - drawW) / 2f;
            return (drawX, drawW);
        }

        protected void DrawColumnStrip(int columnIndex, float colX, float colW, float axisBottomY)
        {
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

        // When true, the overlay must also expose GetPinnedAttemptIndices().
        public virtual bool SupportsDeleteRuns => false;

        // info must carry MouseHudPos: pass instances from GraphInteractivity.CurrentHover,
        // never hand-built ones.
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

        // Steps are whole frames, so ticks land on frame boundaries.
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
