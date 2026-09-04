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
    }
}
