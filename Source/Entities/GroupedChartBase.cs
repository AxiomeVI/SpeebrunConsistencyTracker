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
        protected readonly Color _primaryColor;
        protected readonly Color _secondaryColor;
        protected readonly string _primaryLabel;
        protected readonly string _secondaryLabel;

        protected readonly float _cachedGroupWidth;
        protected readonly float _cachedBarWidth;
        protected readonly float _cachedBarSpacing;
        protected readonly float _cachedGroupSpacing;

        protected GroupedChartBase(
            string title,
            List<string> labels,
            List<T> primaryValues,
            List<T> secondaryValues,
            Color primaryColor,
            Color secondaryColor,
            string primaryLabel,
            string secondaryLabel,
            float opacity = 1f,
            Vector2? pos = null)
            : base(title, pos)
        {
            _labels          = labels;
            _primaryValues   = primaryValues;
            _secondaryValues = secondaryValues;
            _primaryColor    = primaryColor   * (opacity / 100f);
            _secondaryColor  = secondaryColor * (opacity / 100f);
            _primaryLabel    = primaryLabel;
            _secondaryLabel  = secondaryLabel;

            ComputeBarLayout(width - margin * 2, _labels.Count,
                out _cachedGroupWidth, out _cachedGroupSpacing, out _cachedBarSpacing, out _cachedBarWidth);
        }

        protected abstract float GetBarHeight(T value, float chartHeight);
        protected abstract string FormatBarLabel(T value);
        protected abstract void DrawYAxis(float x, float y, float w, float h);

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_primaryValues.Count == 0) return;

            for (int i = 0; i < _primaryValues.Count; i++)
            {
                float groupX = x + i * _cachedGroupWidth + _cachedGroupSpacing / 2f;

                float primaryHeight = GetBarHeight(_primaryValues[i], h);
                float primaryY      = y + h - primaryHeight;

                float secondaryX      = groupX + _cachedBarWidth + _cachedBarSpacing;
                bool  secondaryExists = i < _secondaryValues.Count;
                float secondaryHeight = secondaryExists ? GetBarHeight(_secondaryValues[i], h) : 0f;
                float secondaryY      = y + h - secondaryHeight;

                if (primaryHeight > 0)
                    Draw.Rect(groupX, primaryY, _cachedBarWidth, primaryHeight, _primaryColor);

                if (secondaryExists && secondaryHeight > 0)
                    Draw.Rect(secondaryX, secondaryY, _cachedBarWidth, secondaryHeight, _secondaryColor);

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
            DrawTitle();
            DrawYAxis(x, y, w, h);
            DrawXAxisStaggeredLabels(x, y, h, _labels.Count, _cachedGroupWidth, i => _labels[i], Color.LightGray);

            float legendY = y + h + ChartConstants.Legend.LegendOffsetY;
            float legendX = x + w;
            DrawLegendEntry(legendX, legendY, _secondaryLabel, _secondaryColor, ChartConstants.FontScale.AxisLabel, right: true);
            float offset = ActiveFont.Measure(_secondaryLabel).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;
            DrawLegendEntry(legendX - offset, legendY, _primaryLabel, _primaryColor, ChartConstants.FontScale.AxisLabel, right: true);
        }
    }
}
