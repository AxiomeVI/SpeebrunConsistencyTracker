using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    // Label is the tooltip text (\n for multiple lines); LabelPos its top-center HUD coordinate.
    // Key identifies the thing hovered, so pinning two of them does not collide. Every overlay
    // that can be pinned supplies one; the label fallback in GraphInteractivity is what happens
    // when one forgets, and it is why two boxes with the same empty label used to share a pin.
    public sealed record HoverInfo(string Label, Vector2 LabelPos, Vector2 MouseHudPos = default, string? Key = null);

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

        // The shared body of every ColumnHitTest: walk the columns left to right inside the label
        // strip below the X-axis. Hidden columns shrink to a stub but stay hittable, so a column
        // can be toggled back on. normalWidth is the width of a visible column; the caller owns
        // the count and how that width is computed. Sets _hoveredColumnIndex as a side effect.
        protected int? HitTestColumnStrip(Vector2 mousePos, int count, float normalWidth)
        {
            // Mirrors Render()'s rounding, so the strip is hit-tested where it was drawn.
            float gx = MathF.Round(position.X + marginH);
            float gy = MathF.Round(position.Y + margin);
            float gh = MathF.Round(position.Y + height - margin) - gy;

            float hitZoneTop    = gy + gh + ChartConstants.XAxisLabel.BaseOffsetY;
            float hitZoneBottom = hitZoneTop + ChartConstants.Interactivity.ColumnLabelHitZoneH;

            if (mousePos.Y < hitZoneTop || mousePos.Y > hitZoneBottom)
            {
                _hoveredColumnIndex = -1;
                return null;
            }

            float colX = gx;
            for (int i = 0; i < count; i++)
            {
                float colW = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalWidth;
                var (stripX, stripW) = ColumnStripRect(colX, colW);
                if (mousePos.X >= stripX && mousePos.X < stripX + stripW) { _hoveredColumnIndex = i; return i; }
                colX += colW;
            }
            _hoveredColumnIndex = -1;
            return null;
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

        // Percentage axis: the step comes from a fixed candidate list so the labels stay round.
        protected static void GetPercentageAxisSettings(double rangePct, out double stepPct, out int count)
        {
            if (rangePct <= 0) { stepPct = 5.0; count = 1; return; }
            double[] candidates = [1, 2, 5, 10, 20, 25, 50];
            stepPct = candidates[^1];
            foreach (double c in candidates)
            {
                if (rangePct / c <= ChartConstants.Axis.MaxTickMarks) { stepPct = c; break; }
            }
            count = Math.Min((int)Math.Ceiling(rangePct / stepPct), ChartConstants.Axis.MaxTickMarks);
        }

        protected static float ToPixelY(double value, double minVal, double maxVal, float y, float h)
        {
            if (maxVal == minVal) return y + h / 2;
            return y + h - (float)((value - minVal) / (maxVal - minVal)) * h;
        }

        // Median-normalised Y range over the visible columns: every column's own median is 100%,
        // so columns of different absolute lengths overlay. sortedTimesOf must return the column's
        // times in ascending order.
        protected void ComputeRelativeRanges(
            int columnCount, Func<int, List<TimeTicks>> sortedTimesOf,
            out double minPct, out double maxPct)
        {
            double pMin = double.MaxValue, pMax = double.MinValue;
            for (int i = 0; i < columnCount; i++)
            {
                if (_hiddenColumns.Contains(i)) continue;
                var times = sortedTimesOf(i);
                if (times.Count == 0) continue;
                long medTicks = MetricHelper.ComputePercentile(times, 50).Ticks;
                if (medTicks == 0) continue;
                pMin = Math.Min(pMin, (double)times[0].Ticks  / medTicks * 100.0);
                pMax = Math.Max(pMax, (double)times[^1].Ticks / medTicks * 100.0);
            }
            if (pMin == double.MaxValue) { pMin = 90.0; pMax = 110.0; }
            double pRange  = pMax - pMin;
            double pMargin = Math.Max(1.0, pRange * 0.1);
            minPct = Math.Max(0, pMin - pMargin);
            maxPct = pMax + pMargin;
        }

        // Which side of the plot a Y-axis tick label sits on. A left label is right-aligned
        // against the axis; a right label starts past the plot's right edge.
        protected enum YAxisSide { Left, Right }

        private static void DrawYAxisTickLabel(string text, float yPos, float x, float w, YAxisSide side, Color color)
        {
            Vector2 size = ActiveFont.Measure(text) * ChartConstants.FontScale.AxisLabelMedium;
            float labelX = side == YAxisSide.Left
                ? x - size.X - ChartConstants.Axis.YLabelMarginX
                : x + w + ChartConstants.Axis.RightLabelMarginX;
            ActiveFont.DrawOutline(text,
                new Vector2(labelX, yPos - size.Y / 2),
                Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                color, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        // Frame-stepped tick labels over [min, max]. A non-positive range draws nothing: the
        // tick position divides by it, and a column set that is empty or entirely hidden can
        // leave a range computed with no data fallback negative.
        protected static void DrawFrameAxisLabels(
            float x, float y, float w, float h, long min, long max, YAxisSide side, Color color)
        {
            long range = max - min;
            if (range <= 0) return;
            GetFrameAxisSettings(range, out long step, out int count);
            for (int i = 0; i <= count; i++)
            {
                float normalizedY = (float)(i * step) / range;
                float yPos        = y + h - (normalizedY * h);
                DrawYAxisTickLabel(new TimeTicks(min + i * step).ToString(), yPos, x, w, side, color);
            }
        }

        // Percentage tick labels over [minPct, maxPct]. Always the left axis: the right axis
        // carries absolute segment times, which are never median-normalised.
        protected static void DrawPercentageAxisLabels(
            float x, float y, float w, float h, double minPct, double maxPct, Color color)
        {
            double rangePct = maxPct - minPct;
            GetPercentageAxisSettings(rangePct, out double stepPct, out int count);
            for (int i = 0; i <= count; i++)
            {
                double pctValue = minPct + i * stepPct;
                if (pctValue > maxPct + 1e-9) break;
                float yPos = ToPixelY(pctValue, minPct, maxPct, y, h);
                DrawYAxisTickLabel($"{pctValue:F0}%", yPos, x, w, YAxisSide.Left, color);
            }
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
