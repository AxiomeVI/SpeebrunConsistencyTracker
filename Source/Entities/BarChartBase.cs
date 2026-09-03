using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public abstract class BarChartBase : BaseChartOverlay
    {
        protected BarChartBase(string title, Vector2? pos = null) : base(title, pos) { }

        // Two bars per group.
        protected void ComputeBarLayout(
            float w, int itemCount,
            out float groupWidth, out float groupSpacing,
            out float barSpacing, out float barWidth)
        {
            groupWidth   = Math.Min(w / Math.Max(itemCount, 1), MAX_BAR_WIDTH);
            groupSpacing = groupWidth * ChartConstants.BarLayout.GroupSpacingRatio;
            float usable = groupWidth - groupSpacing;
            barSpacing   = usable * ChartConstants.BarLayout.BarSpacingRatio;
            barWidth     = (usable - barSpacing) / 2f;
        }

        // Font scales with bar width; the label is dropped when the bar is too narrow.
        protected static void DrawBarLabel(string text, float barCenterX, float barTopY, float barWidth)
        {
            float scale = barWidth > ChartConstants.BarLayout.WideBarThreshold
                ? ChartConstants.FontScale.AxisLabelSmall
                : barWidth > ChartConstants.BarLayout.NarrowBarThreshold
                    ? ChartConstants.FontScale.BarValueTiny
                    : 0f;
            if (scale == 0f) return;

            Vector2 textSize = ActiveFont.Measure(text) * scale;
            ActiveFont.DrawOutline(
                text,
                new Vector2(barCenterX - textSize.X / 2, barTopY - textSize.Y - ChartConstants.BarLayout.BarLabelOffsetY),
                new Vector2(0f, 0f),
                Vector2.One * scale,
                Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        // Gridlines every 10%. Call from a DrawGrid override.
        protected void DrawPercentGrid(float x, float y, float w, float h)
        {
            for (int i = 1; i <= ChartConstants.Axis.PercentTickCount; i++)
            {
                float pct  = i * 10f;
                float yPos = y + h - (pct / 100f * h);
                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos),
                          ChartConstants.Colors.GridLineColor, 1f);
            }
        }

        // Call from DrawLabels.
        protected void DrawPercentYAxisLabels(float x, float y, float w, float h)
        {
            for (int i = 0; i <= ChartConstants.Axis.PercentTickCount; i++)
            {
                float pct  = i * 10f;
                float yPos = y + h - (pct / 100f * h);
                string label = $"{pct:0}%";
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;

                ActiveFont.DrawOutline(
                    label,
                    new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * ChartConstants.FontScale.AxisLabel,
                    Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }
    }
}
