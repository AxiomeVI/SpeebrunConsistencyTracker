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
    public class PercentBarChartOverlay : Entity
    {
        private readonly string title;
        private readonly List<string> labels;
        private readonly List<double> primaryValues;
        private readonly List<double> secondaryValues;
        private readonly List<string> topLabels; // optional label above each bar (e.g. avg time lost)
        private readonly Color primaryColor;
        private readonly Color secondaryColor;
        private readonly string primaryLabel;
        private readonly string secondaryLabel;
        
        // Graph settings
        private readonly Vector2 position;
        private readonly float width = 1800f;
        private readonly float height = 900f;
        private readonly float margin = 80f;
        
        // Colors
        private readonly Color backgroundColor = Color.Black * 0.8f;
        private readonly Color axisColor = Color.White;
        
        private readonly int maxValue;

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
            : this(title, labels, values, null, barColor, Color.Transparent, legendLabel, null, null, opacity, pos) { }

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
            List<string> topLabels = null,
            float opacity = 1f,
            Vector2? pos = null)
        {
            this.title = title;
            this.labels = labels;
            this.primaryValues = primaryValues;
            this.secondaryValues = secondaryValues;
            this.primaryColor = primaryColor * (opacity / 100);
            this.secondaryColor = secondaryColor * (opacity / 100);
            this.primaryLabel = primaryLabel;
            this.secondaryLabel = secondaryLabel;
            this.topLabels = topLabels;
            
            Depth = -100;
            
            position = pos ?? new Vector2(
                (1920 - width) / 2,
                (1080 - height) / 2
            );
            
            Tag = Tags.HUD | Tags.Global;
            
            maxValue = 100;
        }
        
        public override void Render()
        {
            base.Render();
            
            Draw.Rect(position, width, height, backgroundColor);
            
            float graphX = position.X + margin;
            float graphY = position.Y + margin;
            float graphWidth = width - margin * 2;
            float graphHeight = height - margin * 2;
            
            DrawAxes(graphX, graphY, graphWidth, graphHeight);
            DrawBars(graphX, graphY, graphWidth, graphHeight);
            DrawLabels(graphX, graphY, graphWidth, graphHeight);
        }
        
        private void DrawAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x, y + h), new Vector2(x + w, y + h), axisColor, 2f);
            Draw.Line(new Vector2(x, y), new Vector2(x, y + h), axisColor, 2f);
        }
        
        private void DrawBars(float x, float y, float w, float h)
        {
            if (primaryValues.Count == 0) return;

            float barWidth = Math.Min(w / primaryValues.Count, 100f);
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

                float totalHeight = primaryHeight + secondaryHeight;
                float topOfBar = y + h - totalHeight;

                // Top label: use topLabels if provided, otherwise show percentage
                if (topLabels != null)
                {
                    if (i < topLabels.Count && !string.IsNullOrEmpty(topLabels[i]))
                    {
                        string topLabel = topLabels[i];
                        Vector2 textSize = ActiveFont.Measure(topLabel) * 0.3f;
                        ActiveFont.DrawOutline(
                            topLabel,
                            new Vector2(barX + actualBarWidth / 2 - textSize.X / 2, topOfBar - textSize.Y - 3),
                            new Vector2(0f, 0f),
                            Vector2.One * 0.3f,
                            Color.Yellow, 2f, Color.Black);
                    }
                }
                else if (totalHeight > 15)
                {
                    double totalPct = pct + (secondaryValues != null && i < secondaryValues.Count ? secondaryValues[i] : 0);
                    string pctText = $"{totalPct:F0}%";
                    Vector2 textSize = ActiveFont.Measure(pctText) * 0.3f;
                    ActiveFont.DrawOutline(
                        pctText,
                        new Vector2(barX + actualBarWidth / 2 - textSize.X / 2, topOfBar - textSize.Y - 3),
                        new Vector2(0f, 0f),
                        Vector2.One * 0.3f,
                        Color.White, 2f, Color.Black);
                }
            }
        }
        
        private void DrawLabels(float x, float y, float w, float h)
        {
            float barWidth = Math.Min(w / Math.Max(labels.Count, 1), 100f);

            // Title
            Vector2 titleSize = ActiveFont.Measure(title) * 0.7f;
            ActiveFont.DrawOutline(
                title,
                new Vector2(position.X + width / 2 - titleSize.X / 2, position.Y + 10),
                new Vector2(0f, 0f),
                Vector2.One * 0.7f,
                Color.White, 2f, Color.Black);

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
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), Color.Gray * 0.3f, 1f);
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

            // Legend (bottom right)
            float legendY = y + h + 55;
            float legendX = x + w;

            if (primaryLabel != null)
                DrawLegendEntry(legendX, legendY, primaryLabel, primaryColor, 0.35f, right: true);

            if (secondaryLabel != null && secondaryValues != null)
            {
                float offset = primaryLabel != null ? ActiveFont.Measure(primaryLabel).X * 0.35f + 40 : 0;
                DrawLegendEntry(legendX - offset, legendY, secondaryLabel, secondaryColor, 0.35f, right: true);
            }

            // Top labels legend entry
            if (topLabels != null)
            {
                float offset = 0;
                if (primaryLabel != null)   offset += ActiveFont.Measure(primaryLabel).X * 0.35f + 40;
                if (secondaryLabel != null) offset += ActiveFont.Measure(secondaryLabel).X * 0.35f + 40;
                DrawLegendEntry(legendX - offset, legendY, "Median time lost", Color.Yellow, 0.35f, right: true);
            }
        }
        
        private static void DrawLegendEntry(float x, float y, string text, Color color, float scale, bool right = false)
        {
            Vector2 textSize = ActiveFont.Measure(text) * scale;
            float boxSize = 12f;
            float spacing = 5f;
            float totalWidth = textSize.X + boxSize + spacing;
            
            float startX = right ? x - totalWidth : x;
            float boxY = y + (textSize.Y / 2f) - (boxSize / 2f);

            Draw.Rect(startX, boxY, boxSize, boxSize, color);

            ActiveFont.DrawOutline(
                text,
                new Vector2(startX + boxSize + spacing, y),
                new Vector2(0f, 0f),
                Vector2.One * scale,
                Color.White, 2f, Color.Black);
        }
    }
}