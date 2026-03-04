using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Bar chart showing percentage values per room, with optional stacked second layer.
    /// Used for DNF% and the combined DNF% + time-loss% chart.
    /// </summary>
    public class PercentBarChartOverlay : BarChartBase
    {
        private readonly List<string> labels;
        private readonly List<double> primaryValues;
        private readonly List<double> secondaryValues;
        private readonly Color primaryColor;
        private readonly Color secondaryColor;
        private readonly string primaryLabel;
        private readonly string secondaryLabel;
        private readonly int maxValue = 100;

        /// <summary>
        /// Single-layer percentage bar chart (e.g. DNF % only).
        /// </summary>
        public PercentBarChartOverlay(
            string title,
            List<string> labels,
            List<double> values,
            Color barColor,
            string legendLabel = null,
            float opacity = 1f,
            Vector2? pos = null)
            : this(title, labels, values, null, barColor, Color.Transparent, legendLabel, null, opacity, pos) { }

        /// <summary>
        /// Stacked percentage bar chart with primary + secondary layers.
        /// </summary>
        public PercentBarChartOverlay(
            string title,
            List<string> labels,
            List<double> primaryValues,
            List<double> secondaryValues,
            Color primaryColor,
            Color secondaryColor,
            string primaryLabel,
            string secondaryLabel,
            float opacity = 1f,
            Vector2? pos = null)
            : base(title, pos)
        {
            this.labels = labels;
            this.primaryValues = primaryValues;
            this.secondaryValues = secondaryValues;
            this.primaryColor = primaryColor * (opacity / 100);
            this.secondaryColor = secondaryColor * (opacity / 100);
            this.primaryLabel = primaryLabel;
            this.secondaryLabel = secondaryLabel;
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (primaryValues.Count == 0) return;

            float barWidth      = Math.Min(w / primaryValues.Count, MAX_BAR_WIDTH);
            float barSpacing    = barWidth * ChartConstants.BarLayout.GroupSpacingRatio;
            float actualBarWidth = barWidth - barSpacing;

            for (int i = 0; i < primaryValues.Count; i++)
            {
                float barX = x + i * barWidth + barSpacing / 2;

                double pct = primaryValues[i];
                float primaryHeight = (float)(pct / maxValue) * h;
                float primaryY = y + h - primaryHeight;

                if (primaryHeight > 0)
                    Draw.Rect(barX, primaryY, actualBarWidth, primaryHeight, primaryColor);

                float secondaryHeight = 0;
                if (secondaryValues != null && i < secondaryValues.Count)
                {
                    double secPct = secondaryValues[i];
                    secondaryHeight = (float)(secPct / maxValue) * h;
                    float secondaryY = primaryY - secondaryHeight;
                    if (secondaryHeight > 0)
                        Draw.Rect(barX, secondaryY, actualBarWidth, secondaryHeight, secondaryColor);
                }

                float totalHeight = primaryHeight + secondaryHeight;
                if (totalHeight > ChartConstants.BarLayout.StackedLabelMinHeight)
                {
                    double totalPct = pct + (secondaryValues != null && i < secondaryValues.Count ? secondaryValues[i] : 0);
                    string pctText  = $"{totalPct:F0}%";
                    Vector2 textSize = ActiveFont.Measure(pctText) * ChartConstants.FontScale.AxisLabelSmall;
                    float topOfBar   = y + h - totalHeight;
                    ActiveFont.DrawOutline(
                        pctText,
                        new Vector2(barX + actualBarWidth / 2 - textSize.X / 2, topOfBar - textSize.Y - ChartConstants.BarLayout.BarLabelOffsetY),
                        new Vector2(0f, 0f),
                        Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                        Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            float barWidth = Math.Min(w / Math.Max(labels.Count, 1), MAX_BAR_WIDTH);

            DrawTitle();
            DrawPercentYAxis(x, y, w, h);
            DrawXAxisStaggeredLabels(x, y, h, labels.Count, barWidth, i => labels[i], Color.LightGray);

            float legendY = y + h + ChartConstants.Legend.LegendOffsetY;
            float legendX = x + w;
            if (secondaryLabel != null && secondaryValues != null)
                DrawLegendEntry(legendX, legendY, secondaryLabel, secondaryColor, ChartConstants.FontScale.AxisLabel, right: true);
            if (primaryLabel != null)
            {
                float offset = secondaryLabel != null
                    ? ActiveFont.Measure(secondaryLabel).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing
                    : 0;
                DrawLegendEntry(legendX - offset, legendY, primaryLabel, primaryColor, ChartConstants.FontScale.AxisLabel, right: true);
            }
        }
    }
}
