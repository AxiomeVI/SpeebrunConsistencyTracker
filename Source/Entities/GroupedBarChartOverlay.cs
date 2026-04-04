using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Bar chart showing time values per room with grouped bars (primary and secondary side by side).
    /// Used for time loss comparison (median vs average).
    /// </summary>
    public class GroupedBarChartOverlay : GroupedChartBase<long>
    {
        private readonly long _maxTicks;

        public GroupedBarChartOverlay(
            string title,
            List<string> labels,
            List<long> primaryTicks,
            List<long> secondaryTicks,
            string primaryLabel,
            string secondaryLabel,
            Vector2? pos = null)
            : base(title, labels, primaryTicks, secondaryTicks, primaryLabel, secondaryLabel, pos)
        {
            long dataMax = Enumerable.Range(0, primaryTicks.Count)
                .Select(i => Math.Max(
                    primaryTicks[i],
                    secondaryTicks != null && i < secondaryTicks.Count ? secondaryTicks[i] : 0))
                .DefaultIfEmpty(0)
                .Max();

            GetFrameAxisSettings(dataMax, out long step, out int count);
            _maxTicks = step * (count + 1);
        }

        protected override float GetBarHeight(long value, float chartHeight) =>
            _maxTicks > 0 ? MathF.Floor((float)value / _maxTicks * chartHeight) : 0f;

        protected override string FormatBarLabel(long value) =>
            "+" + new TimeTicks(value).ToString().TrimStart('0');

        protected override void DrawYAxis(float x, float y, float w, float h)
        {
            GetFrameAxisSettings(_maxTicks, out long yStep, out int yLabelCount);
            for (int i = 0; i <= yLabelCount; i++)
            {
                long tickValue = yStep * i;
                if (tickValue > _maxTicks) break;
                float yPos = y + h - (float)tickValue / _maxTicks * h;

                string timeLabel = new TimeTicks(tickValue).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabel;
                ActiveFont.DrawOutline(
                    timeLabel,
                    new Vector2(x - labelSize.X - ChartConstants.Axis.YLabelMarginX, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * ChartConstants.FontScale.AxisLabel,
                    Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);

                if (i > 0)
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), ChartConstants.Colors.GridLineColor, 1f);
            }
        }

        protected override void DrawBarPairLabels(
            int i,
            float groupX, float primaryTopY,
            float secondaryX, float secondaryTopY,
            bool secondaryExists, bool primaryExists = true)
        {
            bool equal = primaryExists && secondaryExists && _secondaryValues[i] == _primaryValues[i];
            if (equal)
            {
                float groupCenterX = groupX + _cachedBarWidth + _cachedBarSpacing / 2f;
                DrawBarLabel(FormatBarLabel(_primaryValues[i]),
                    groupCenterX, primaryTopY, _cachedBarWidth * 2 + _cachedBarSpacing);
            }
            else
            {
                if (primaryExists)
                    DrawBarLabel(FormatBarLabel(_primaryValues[i]),
                        groupX + _cachedBarWidth / 2, primaryTopY, _cachedBarWidth);
                if (secondaryExists)
                    DrawBarLabel(FormatBarLabel(_secondaryValues[i]),
                        secondaryX + _cachedBarWidth / 2, secondaryTopY, _cachedBarWidth);
            }
        }

        protected override string BuildHoverLabel(int i, bool isPrimary, bool isSecondary)
        {
            if (isPrimary && isSecondary)
            {
                string primary   = FormatBarLabel(_primaryValues[i]);
                bool   hasSecond = i < _secondaryValues.Count;
                string secondary = hasSecond ? FormatBarLabel(_secondaryValues[i]) : null;
                return hasSecond
                    ? $"{_primaryLabel}: {primary}\n{_secondaryLabel}: {secondary}"
                    : $"{_primaryLabel}: {primary}";
            }
            if (isPrimary)
                return $"{_primaryLabel}: {FormatBarLabel(_primaryValues[i])}";
            if (isSecondary && i < _secondaryValues.Count)
                return $"{_secondaryLabel}: {FormatBarLabel(_secondaryValues[i])}";
            return "";
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_maxTicks == 0) return;
            base.DrawBars(x, y, w, h);
        }
    }
}
