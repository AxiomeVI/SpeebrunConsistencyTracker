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

        private float ComputeNormalBarWidth(float gw)
        {
            int visibleCount = primaryValues.Count - _hiddenColumns.Count;
            if (visibleCount <= 0) return Math.Min(gw / Math.Max(primaryValues.Count, 1), MAX_BAR_WIDTH);
            float available = gw - _hiddenColumns.Count * ChartConstants.Interactivity.HiddenColumnStubWidth;
            return Math.Min(available / visibleCount, MAX_BAR_WIDTH);
        }

        private float GetBarX(float gx, float gw, int i)
        {
            float normalW = ComputeNormalBarWidth(gw);
            float x = gx;
            for (int j = 0; j < i; j++)
                x += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            return x;
        }

        protected override void DrawGrid(float x, float y, float w, float h) =>
            DrawPercentGrid(x, y, w, h);

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (primaryValues.Count == 0) return;

            Color primaryColor   = SpeebrunConsistencyTrackerModule.Settings.PrimaryChartColorFinal;
            Color secondaryColor = SpeebrunConsistencyTrackerModule.Settings.SecondaryChartColorFinal;

            float normalBarW     = ComputeNormalBarWidth(w);
            float barSpacing     = normalBarW * ChartConstants.BarLayout.GroupSpacingRatio;
            float actualBarWidth = normalBarW - barSpacing;

            for (int i = 0; i < primaryValues.Count; i++)
            {
                if (_hiddenColumns.Contains(i)) continue;

                float barX = GetBarX(x, w, i) + barSpacing / 2;

                double pct          = primaryValues[i];
                float primaryY      = MathF.Round(y + h - (float)(pct / maxValue) * h);
                float primaryHeight = (y + h) - primaryY;

                if (primaryHeight > 0)
                    Draw.Rect(barX, primaryY, actualBarWidth, primaryHeight, primaryColor);

                float secondaryHeight = 0;
                if (secondaryValues != null && i < secondaryValues.Count)
                {
                    double secPct    = secondaryValues[i];
                    float secondaryY = MathF.Round(primaryY - (float)(secPct / maxValue) * h);
                    secondaryHeight  = primaryY - secondaryY;
                    if (secondaryHeight > 0)
                        Draw.Rect(barX, secondaryY, actualBarWidth, secondaryHeight, secondaryColor);
                }

                float totalHeight = primaryHeight + secondaryHeight;
                if (totalHeight > ChartConstants.BarLayout.StackedLabelMinHeight)
                {
                    double totalPct  = pct + (secondaryValues != null && i < secondaryValues.Count ? secondaryValues[i] : 0);
                    string pctText   = $"{totalPct:0.#}%";
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

            float normalBarW     = ComputeNormalBarWidth(gw);
            float barSpacing     = normalBarW * ChartConstants.BarLayout.GroupSpacingRatio;
            float actualBarWidth = normalBarW - barSpacing;

            // Find which column the mouse is in by iterating actual widths
            int idx = -1;
            float colX = gx;
            for (int j = 0; j < primaryValues.Count; j++)
            {
                float colW = _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalBarW;
                if (mouseHudPos.X >= colX && mouseHudPos.X < colX + colW) { idx = j; break; }
                colX += colW;
            }
            if (idx < 0)
            {
                _hoveredBarIndex = -1;
                return null;
            }

            if (_hiddenColumns.Contains(idx))
            {
                _hoveredBarIndex = -1;
                return null;
            }

            float barX      = GetBarX(gx, gw, idx) + barSpacing / 2f;
            double totalPct = primaryValues[idx] + (secondaryValues != null && idx < secondaryValues.Count ? secondaryValues[idx] : 0);
            float barTopY   = MathF.Round(gy + gh - (float)(totalPct / maxValue) * gh);

            if (mouseHudPos.X < barX || mouseHudPos.X > barX + actualBarWidth || totalPct <= 0 || mouseHudPos.Y < barTopY)
            {
                _hoveredBarIndex = -1;
                return null;
            }

            _hoveredBarIndex = idx;
            _hoveredBarWidth = normalBarW;
            _hoveredBarTopY  = barTopY;

            float barCenterX  = barX + actualBarWidth / 2f;
            string label      = BuildPercentHoverLabel(idx);
            int    lineCount  = label.Split('\n').Length;
            float  lineHeight = ActiveFont.Measure("A").Y * ChartConstants.FontScale.AxisLabelMedium;
            float  labelY     = barTopY - lineCount * lineHeight - ChartConstants.Interactivity.TooltipBgPadding;
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
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            float normalBarW  = ComputeNormalBarWidth(gw);
            float barSpacing  = normalBarW * ChartConstants.BarLayout.GroupSpacingRatio;
            float actualBarW  = normalBarW - barSpacing;
            float barX        = GetBarX(gx, gw, _hoveredBarIndex) + barSpacing / 2f;
            float barH        = gy + gh - _hoveredBarTopY;

            Draw.HollowRect(barX, _hoveredBarTopY, actualBarW, barH, Color.White * 0.8f);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            Color primaryColor   = SpeebrunConsistencyTrackerModule.Settings.PrimaryChartColorFinal;
            Color secondaryColor = SpeebrunConsistencyTrackerModule.Settings.SecondaryChartColorFinal;

            DrawTitle();
            DrawPercentYAxisLabels(x, y, w, h);

            // X-axis labels and strip highlights
            float baseLabelY = y + h + ChartConstants.XAxisLabel.BaseOffsetY;
            float normalBarW2 = ComputeNormalBarWidth(w);
            for (int i = 0; i < labels.Count; i++)
            {
                float barX2 = GetBarX(x, w, i);
                float colW  = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalBarW2;
                DrawColumnStrip(i, barX2, colW, y + h);

                if (_hiddenColumns.Contains(i)) continue;
                float labelX  = barX2 + normalBarW2 / 2f;
                string lbl    = labels[i];
                float labelY  = labels.Count > ChartConstants.XAxisLabel.StaggerThreshold
                    ? (i % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                    : baseLabelY;
                Vector2 labelSize = ActiveFont.Measure(lbl) * ChartConstants.FontScale.AxisLabel;
                ActiveFont.DrawOutline(lbl,
                    new Vector2(labelX - labelSize.X / 2, labelY),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                    Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

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

        public override int? ColumnHitTest(Vector2 mousePos)
        {
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            float hitZoneTop    = gy + gh + ChartConstants.XAxisLabel.BaseOffsetY;
            float hitZoneBottom = hitZoneTop + ChartConstants.Interactivity.ColumnLabelHitZoneH;

            if (mousePos.Y < hitZoneTop || mousePos.Y > hitZoneBottom)
            {
                _hoveredColumnIndex = -1;
                return null;
            }

            float normalBarW = ComputeNormalBarWidth(gw);
            float colX = gx;
            for (int i = 0; i < primaryValues.Count; i++)
            {
                float colW = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalBarW;
                var (stripX, stripW) = ColumnStripRect(colX, colW);
                if (mousePos.X >= stripX && mousePos.X < stripX + stripW) { _hoveredColumnIndex = i; return i; }
                colX += colW;
            }
            _hoveredColumnIndex = -1;
            return null;
        }
    }
}
