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
    public class PercentBarChartOverlay : BaseChartOverlay
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

            float barWidth = Math.Min(w / primaryValues.Count, MAX_BAR_WIDTH);
            float barSpacing = barWidth * 0.15f;
            float actualBarWidth = barWidth - barSpacing;

            for (int i = 0; i < primaryValues.Count; i++)
            {
                float barX = x + i * barWidth + barSpacing / 2;

                // Primary (bottom) bar
                double pct = primaryValues[i];
                float primaryHeight = (float)(pct / maxValue) * h;
                float primaryY = y + h - primaryHeight;

                if (primaryHeight > 0)
                    Draw.Rect(barX, primaryY, actualBarWidth, primaryHeight, primaryColor);

                // Secondary (stacked on top) bar
                float secondaryHeight = 0;
                if (secondaryValues != null && i < secondaryValues.Count)
                {
                    double secPct = secondaryValues[i];
                    secondaryHeight = (float)(secPct / maxValue) * h;
                    float secondaryY = primaryY - secondaryHeight;

                    if (secondaryHeight > 0)
                        Draw.Rect(barX, secondaryY, actualBarWidth, secondaryHeight, secondaryColor);
                }

                // Percentage label on top
                float totalHeight = primaryHeight + secondaryHeight;
                if (totalHeight > 15)
                {
                    double totalPct = pct + (secondaryValues != null && i < secondaryValues.Count ? secondaryValues[i] : 0);
                    string pctText = $"{totalPct:F0}%";
                    Vector2 textSize = ActiveFont.Measure(pctText) * 0.3f;
                    float topOfBar = y + h - totalHeight;

                    ActiveFont.DrawOutline(
                        pctText,
                        new Vector2(barX + actualBarWidth / 2 - textSize.X / 2, topOfBar - textSize.Y - 3),
                        new Vector2(0f, 0f),
                        Vector2.One * 0.3f,
                        Color.White, 2f, Color.Black);
                }
            }
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            float barWidth = Math.Min(w / Math.Max(labels.Count, 1), MAX_BAR_WIDTH);

            DrawTitle();

            // Y axis ticks (0% to 100%)
            int yLabelCount = 10;
            for (int i = 0; i <= yLabelCount; i++)
            {
                double pctValue = (double)maxValue / yLabelCount * i;
                float yPos = y + h - h / yLabelCount * i;

                string countLabel = $"{pctValue:F0}%";
                Vector2 labelSize = ActiveFont.Measure(countLabel) * 0.35f;

                ActiveFont.DrawOutline(
                    countLabel,
                    new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.35f,
                    Color.White, 2f, Color.Black);

                if (i > 0)
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), Color.Gray * 0.5f, 1f);
            }

            // X axis labels
            if (labels.Count > 0)
            {
                float baseLabelY = y + h + 10;

                for (int i = 0; i < labels.Count; i++)
                {
                    float labelX = x + i * barWidth + barWidth / 2;
                    string label = labels[i];
                    Vector2 labelSize = ActiveFont.Measure(label) * 0.35f;
                    float labelY = labels.Count > 25 ? i % 2 == 0 ? baseLabelY : baseLabelY + 20 : baseLabelY;

                    ActiveFont.DrawOutline(
                        label,
                        new Vector2(labelX - labelSize.X / 2, labelY),
                        new Vector2(0f, 0f),
                        Vector2.One * 0.35f,
                        Color.LightGray, 2f, Color.Black);
                }
            }

            // Legend (bottom right) — secondary rightmost, primary offset left to match bar order
            float legendY = y + h + 55;
            float legendX = x + w;
            if (secondaryLabel != null && secondaryValues != null)
                DrawLegendEntry(legendX, legendY, secondaryLabel, secondaryColor, 0.35f, right: true);
            if (primaryLabel != null)
            {
                float offset = secondaryLabel != null ? ActiveFont.Measure(secondaryLabel).X * 0.35f + 40 : 0;
                DrawLegendEntry(legendX - offset, legendY, primaryLabel, primaryColor, 0.35f, right: true);
            }
        }
    }
}