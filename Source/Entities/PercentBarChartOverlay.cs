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
        private readonly string primaryLabel;
        private readonly string secondaryLabel;
        private readonly int maxValue = 100;

        private int   _hoveredBarIndex = -1;
        private float _hoveredBarWidth;
        private float _hoveredBarTopY;

        /// <summary>
        /// Single-layer percentage bar chart (e.g. DNF % only).
        /// </summary>
        public PercentBarChartOverlay(
            string title,
            List<string> labels,
            List<double> values,
            string legendLabel = null,
            Vector2? pos = null)
            : this(title, labels, values, null, legendLabel, null, pos) { }

        /// <summary>
        /// Stacked percentage bar chart with primary + secondary layers.
        /// </summary>
        public PercentBarChartOverlay(
            string title,
            List<string> labels,
            List<double> primaryValues,
            List<double> secondaryValues,
            string primaryLabel,
            string secondaryLabel,
            Vector2? pos = null)
            : base(title, pos)
        {
            this.labels          = labels;
            this.primaryValues   = primaryValues;
            this.secondaryValues = secondaryValues;
            this.primaryLabel    = primaryLabel;
            this.secondaryLabel  = secondaryLabel;
        }

        protected override void DrawGrid(float x, float y, float w, float h) =>
            DrawPercentGrid(x, y, w, h);

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (primaryValues.Count == 0) return;

            Color primaryColor   = SpeebrunConsistencyTrackerModule.Settings.PrimaryChartColorFinal;
            Color secondaryColor = SpeebrunConsistencyTrackerModule.Settings.SecondaryChartColorFinal;

            float barWidth       = Math.Min(w / primaryValues.Count, MAX_BAR_WIDTH);
            float barSpacing     = barWidth * ChartConstants.BarLayout.GroupSpacingRatio;
            float actualBarWidth = barWidth - barSpacing;

            for (int i = 0; i < primaryValues.Count; i++)
            {
                float barX = x + i * barWidth + barSpacing / 2;

                double pct = primaryValues[i];
                float primaryY      = MathF.Round(y + h - (float)(pct / maxValue) * h);
                float primaryHeight = (y + h) - primaryY;

                if (primaryHeight > 0)
                    Draw.Rect(barX, primaryY, actualBarWidth, primaryHeight, primaryColor);

                float secondaryHeight = 0;
                if (secondaryValues != null && i < secondaryValues.Count)
                {
                    double secPct = secondaryValues[i];
                    float secondaryY = MathF.Round(primaryY - (float)(secPct / maxValue) * h);
                    secondaryHeight  = primaryY - secondaryY;
                    if (secondaryHeight > 0)
                        Draw.Rect(barX, secondaryY, actualBarWidth, secondaryHeight, secondaryColor);
                }

                float totalHeight = primaryHeight + secondaryHeight;
                if (totalHeight > ChartConstants.BarLayout.StackedLabelMinHeight)
                {
                    double totalPct = pct + (secondaryValues != null && i < secondaryValues.Count ? secondaryValues[i] : 0);
                    string pctText  = $"{totalPct:0.#}%";
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

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            if (mouseHudPos.X < gx || mouseHudPos.X > gx + gw ||
                mouseHudPos.Y < gy || mouseHudPos.Y > gy + gh)
            {
                _hoveredBarIndex = -1;
                return null;
            }

            float barWidth   = System.Math.Min(gw / System.Math.Max(primaryValues.Count, 1), MAX_BAR_WIDTH);
            float barSpacing = barWidth * ChartConstants.BarLayout.GroupSpacingRatio;
            float actualBarWidth = barWidth - barSpacing;
            int idx = (int)((mouseHudPos.X - gx) / barWidth);
            idx = System.Math.Clamp(idx, 0, primaryValues.Count - 1);

            // Only hit when inside the actual bar rect (not spacing), and bar has non-zero height
            float barX      = gx + idx * barWidth + barSpacing / 2f;
            double totalPct = primaryValues[idx] + (secondaryValues != null && idx < secondaryValues.Count ? secondaryValues[idx] : 0);
            float barTopY   = MathF.Round(gy + gh - (float)(totalPct / maxValue) * gh);

            if (mouseHudPos.X < barX || mouseHudPos.X > barX + actualBarWidth || totalPct <= 0 || mouseHudPos.Y < barTopY)
            {
                _hoveredBarIndex = -1;
                return null;
            }

            _hoveredBarIndex = idx;
            _hoveredBarWidth = barWidth;
            _hoveredBarTopY  = barTopY;

            float barCenterX = barX + actualBarWidth / 2f;
            string label = BuildPercentHoverLabel(idx);
            int    lineCount = label.Split('\n').Length;
            float  lineHeight = ActiveFont.Measure("A").Y * ChartConstants.FontScale.AxisLabelMedium;
            float  labelY = barTopY - lineCount * lineHeight - ChartConstants.Interactivity.TooltipBgPadding;
            return new HoverInfo(label, new Vector2(barCenterX, labelY));
        }

        private string BuildPercentHoverLabel(int i)
        {
            double pct = primaryValues[i];
            if (secondaryValues != null && i < secondaryValues.Count)
            {
                double secPct = secondaryValues[i];
                string pLabel = primaryLabel  ?? "Primary";
                string sLabel = secondaryLabel ?? "Secondary";
                return $"{sLabel}: {secPct:0.#}%\n{pLabel}: {pct:0.#}%";
            }
            string lbl = primaryLabel ?? "Value";
            return $"{lbl}: {pct:0.#}%";
        }

        public override void DrawHighlight()
        {
            if (_hoveredBarIndex < 0) return;

            float gx = position.X + marginH;
            float gh = height - margin * 2;
            float gy = position.Y + margin;

            float barSpacing     = _hoveredBarWidth * ChartConstants.BarLayout.GroupSpacingRatio;
            float actualBarWidth = _hoveredBarWidth - barSpacing;
            float barX           = gx + _hoveredBarIndex * _hoveredBarWidth + barSpacing / 2f;
            float barH           = gy + gh - _hoveredBarTopY;

            Draw.HollowRect(barX, _hoveredBarTopY, actualBarWidth, barH, Color.White * 0.8f);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            Color primaryColor   = SpeebrunConsistencyTrackerModule.Settings.PrimaryChartColorFinal;
            Color secondaryColor = SpeebrunConsistencyTrackerModule.Settings.SecondaryChartColorFinal;

            float barWidth = Math.Min(w / Math.Max(labels.Count, 1), MAX_BAR_WIDTH);

            DrawTitle();
            DrawPercentYAxisLabels(x, y, w, h);
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
