using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
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
        protected readonly Color backgroundColor = ChartConstants.Colors.BackgroundColor;
        protected readonly Color axisColor       = Color.White;
        protected readonly float MAX_BAR_WIDTH   = ChartConstants.Layout.MaxBarWidth;

        protected BaseChartOverlay(string title, Vector2? pos = null)
        {
            this.title = title;
            position = pos ?? new Vector2(
                (ChartConstants.Screen.ScreenWidth  - width)  / 2,
                (ChartConstants.Screen.ScreenHeight - height) / 2);
        }

        protected abstract void DrawBars(float x, float y, float w, float h);
        protected abstract void DrawLabels(float x, float y, float w, float h);

        protected virtual void DrawAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x, y + h), new Vector2(x + w, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x, y),     new Vector2(x, y + h),     axisColor, ChartConstants.Stroke.OutlineSize);
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
                Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
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
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;
            DrawAxes(gx, gy, gw, gh);
            DrawBars(gx, gy, gw, gh);
            DrawLabels(gx, gy, gw, gh);
        }
    }
}
