using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Abstract base for grouped (side-by-side) two-series bar charts.
    /// Subclasses provide value-to-height mapping, bar label formatting, and Y-axis drawing.
    /// </summary>
    public abstract class GroupedChartBase<T> : BarChartBase
    {
        protected readonly List<string> _labels;
        protected readonly List<T> _primaryValues;
        protected readonly List<T> _secondaryValues;
        protected readonly string _primaryLabel;
        protected readonly string _secondaryLabel;

        protected readonly float _cachedGroupWidth;
        protected readonly float _cachedBarWidth;
        protected readonly float _cachedBarSpacing;
        protected readonly float _cachedGroupSpacing;

        private int   _hoveredGroupIndex  = -1;
        private float _hoveredHighlightX  = 0f;
        private float _hoveredHighlightY  = 0f;
        private float _hoveredHighlightW  = 0f;
        private float _hoveredHighlightH  = 0f;

        /// <summary>Returns the tooltip label for group index i. Override in subclasses.</summary>
        protected virtual string BuildHoverLabel(int i, bool isPrimary, bool isSecondary) => "";

        protected GroupedChartBase(
            string title,
            List<string> labels,
            List<T> primaryValues,
            List<T> secondaryValues,
            string primaryLabel,
            string secondaryLabel,
            Vector2? pos = null)
            : base(title, pos)
        {
            _labels          = labels;
            _primaryValues   = primaryValues;
            _secondaryValues = secondaryValues;
            _primaryLabel    = primaryLabel;
            _secondaryLabel  = secondaryLabel;

            ComputeBarLayout(width - marginH * 2, _labels.Count,
                out _cachedGroupWidth, out _cachedGroupSpacing, out _cachedBarSpacing, out _cachedBarWidth);
        }

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            _hoveredGroupIndex = -1;

            if (mouseHudPos.X < gx || mouseHudPos.X > gx + gw ||
                mouseHudPos.Y < gy || mouseHudPos.Y > gy + gh)
                return null;

            // Find which column the mouse is in by iterating through column positions
            float normalW = ComputeNormalGroupWidth(gw);
            int idx = -1;
            float colX = gx;
            for (int j = 0; j < _primaryValues.Count; j++)
            {
                float colW = _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
                if (mouseHudPos.X >= colX && mouseHudPos.X < colX + colW) { idx = j; break; }
                colX += colW;
            }
            if (idx < 0) return null;

            if (_hiddenColumns.Contains(idx)) return null;

            float groupX      = GetGroupX(gx, gw, idx) + _cachedGroupSpacing / 2f;
            float primaryTopY = gy + gh - GetBarHeight(_primaryValues[idx], gh);

            bool  secondaryExists = idx < _secondaryValues.Count;
            float secondaryX      = groupX + _cachedBarWidth + _cachedBarSpacing;
            float secondaryTopY   = secondaryExists ? gy + gh - GetBarHeight(_secondaryValues[idx], gh) : gy + gh;

            bool overPrimary   = mouseHudPos.X >= groupX    && mouseHudPos.X <= groupX + _cachedBarWidth
                               && mouseHudPos.Y >= primaryTopY && mouseHudPos.Y <= gy + gh
                               && primaryTopY < gy + gh;
            bool overSecondary = secondaryExists
                               && mouseHudPos.X >= secondaryX  && mouseHudPos.X <= secondaryX + _cachedBarWidth
                               && mouseHudPos.Y >= secondaryTopY && mouseHudPos.Y <= gy + gh
                               && secondaryTopY < gy + gh;

            if (!overPrimary && !overSecondary)
                return null;

            _hoveredGroupIndex = idx;

            float labelX;
            if (overPrimary && overSecondary)
            {
                _hoveredHighlightX = groupX;
                _hoveredHighlightY = System.Math.Min(primaryTopY, secondaryTopY);
                _hoveredHighlightW = _cachedBarWidth * 2 + _cachedBarSpacing;
                _hoveredHighlightH = gy + gh - _hoveredHighlightY;
                labelX = groupX + (_cachedBarWidth * 2 + _cachedBarSpacing) / 2f;
            }
            else if (overPrimary)
            {
                _hoveredHighlightX = groupX;
                _hoveredHighlightY = primaryTopY;
                _hoveredHighlightW = _cachedBarWidth;
                _hoveredHighlightH = gy + gh - primaryTopY;
                labelX = groupX + _cachedBarWidth / 2f;
            }
            else
            {
                _hoveredHighlightX = secondaryX;
                _hoveredHighlightY = secondaryTopY;
                _hoveredHighlightW = _cachedBarWidth;
                _hoveredHighlightH = gy + gh - secondaryTopY;
                labelX = secondaryX + _cachedBarWidth / 2f;
            }

            string hoverLabel = BuildHoverLabel(idx, overPrimary, overSecondary);
            int    lineCount  = hoverLabel.Split('\n').Length;
            float  lineHeight = ActiveFont.Measure("A").Y * ChartConstants.FontScale.AxisLabelMedium;
            float  labelY     = _hoveredHighlightY - lineCount * lineHeight - ChartConstants.Interactivity.TooltipBgPadding;
            return new HoverInfo(hoverLabel, new Vector2(labelX, labelY));
        }

        public override void DrawHighlight()
        {
            if (_hoveredGroupIndex < 0) return;
            Draw.HollowRect(_hoveredHighlightX, _hoveredHighlightY, _hoveredHighlightW, _hoveredHighlightH, Color.White * 0.8f);
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

            float normalW = ComputeNormalGroupWidth(gw);
            float colX = gx;
            for (int i = 0; i < _primaryValues.Count; i++)
            {
                float colW = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
                var (stripX, stripW) = ColumnStripRect(colX, colW);
                if (mousePos.X >= stripX && mousePos.X < stripX + stripW) { _hoveredColumnIndex = i; return i; }
                colX += colW;
            }
            _hoveredColumnIndex = -1;
            return null;
        }

        protected abstract float GetBarHeight(T value, float chartHeight);
        protected abstract string FormatBarLabel(T value);
        protected abstract void DrawYAxis(float x, float y, float w, float h);

        protected abstract void DrawYAxisGrid(float x, float y, float w, float h);

        protected override void DrawGrid(float x, float y, float w, float h) =>
            DrawYAxisGrid(x, y, w, h);

        private float ComputeNormalGroupWidth(float gw)
        {
            int visibleCount = _primaryValues.Count - _hiddenColumns.Count;
            if (visibleCount <= 0) return _cachedGroupWidth;
            float available = gw - _hiddenColumns.Count * ChartConstants.Interactivity.HiddenColumnStubWidth;
            return System.Math.Min(available / visibleCount, MAX_BAR_WIDTH);
        }

        private float GetGroupX(float gx, float gw, int i)
        {
            float normalW = ComputeNormalGroupWidth(gw);
            float x = gx;
            for (int j = 0; j < i; j++)
                x += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            return x;
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_primaryValues.Count == 0) return;

            Color primaryColor   = SpeebrunConsistencyTrackerModule.Settings.PrimaryChartColorFinal;
            Color secondaryColor = SpeebrunConsistencyTrackerModule.Settings.SecondaryChartColorFinal;

            for (int i = 0; i < _primaryValues.Count; i++)
            {
                if (_hiddenColumns.Contains(i)) continue;

                float groupX = GetGroupX(x, w, i) + _cachedGroupSpacing / 2f;

                float primaryHeight = GetBarHeight(_primaryValues[i], h);
                float primaryY      = y + h - primaryHeight;

                float secondaryX      = groupX + _cachedBarWidth + _cachedBarSpacing;
                bool  secondaryExists = i < _secondaryValues.Count;
                float secondaryHeight = secondaryExists ? GetBarHeight(_secondaryValues[i], h) : 0f;
                float secondaryY      = y + h - secondaryHeight;

                if (primaryHeight > 0)
                    Draw.Rect(groupX, primaryY, _cachedBarWidth, primaryHeight, primaryColor);

                if (secondaryExists && secondaryHeight > 0)
                    Draw.Rect(secondaryX, secondaryY, _cachedBarWidth, secondaryHeight, secondaryColor);

                bool primaryVisible   = primaryHeight > 0;
                bool secondaryVisible = secondaryExists && secondaryHeight > 0;
                if (primaryVisible || secondaryVisible)
                    DrawBarPairLabels(i, groupX, primaryY, secondaryX, secondaryY,
                                      secondaryVisible, primaryVisible);
            }
        }

        /// <summary>
        /// Draws value labels above each bar pair. Override to customise label placement
        /// (e.g. centred across both bars when the values are equal).
        /// </summary>
        protected virtual void DrawBarPairLabels(
            int i,
            float groupX, float primaryTopY,
            float secondaryX, float secondaryTopY,
            bool secondaryExists, bool primaryExists = true)
        {
            if (primaryExists)
                DrawBarLabel(FormatBarLabel(_primaryValues[i]), groupX + _cachedBarWidth / 2, primaryTopY, _cachedBarWidth);
            if (secondaryExists)
                DrawBarLabel(FormatBarLabel(_secondaryValues[i]), secondaryX + _cachedBarWidth / 2, secondaryTopY, _cachedBarWidth);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            Color primaryColor   = SpeebrunConsistencyTrackerModule.Settings.PrimaryChartColorFinal;
            Color secondaryColor = SpeebrunConsistencyTrackerModule.Settings.SecondaryChartColorFinal;

            DrawTitle();
            DrawYAxis(x, y, w, h);

            // X-axis labels and column strip highlights
            float baseLabelY = y + h + ChartConstants.XAxisLabel.BaseOffsetY;
            float normalW2   = ComputeNormalGroupWidth(w);
            for (int i = 0; i < _labels.Count; i++)
            {
                float colW  = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW2;
                float groupX = GetGroupX(x, w, i);
                DrawColumnStrip(i, groupX, colW, y + h);

                if (_hiddenColumns.Contains(i)) continue;
                float labelX   = groupX + normalW2 / 2f;
                string label   = _labels[i];
                float labelY   = _labels.Count > ChartConstants.XAxisLabel.StaggerThreshold
                    ? (i % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                    : baseLabelY;
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;
                ActiveFont.DrawOutline(label,
                    new Vector2(labelX - labelSize.X / 2, labelY),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                    Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            float legendY = y + h + ChartConstants.Legend.LegendOffsetY;
            float legendX = x + w;
            DrawLegendEntry(legendX, legendY, _secondaryLabel, secondaryColor, ChartConstants.FontScale.AxisLabel, right: true);
            float offset = ActiveFont.Measure(_secondaryLabel).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;
            DrawLegendEntry(legendX - offset, legendY, _primaryLabel, primaryColor, ChartConstants.FontScale.AxisLabel, right: true);
        }
    }
}
