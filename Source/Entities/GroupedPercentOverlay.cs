using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Grouped bar chart showing two side-by-side percentage bars per room.
    /// Y axis is 0–100%. All data must be pre-computed before passing to the constructor.
    /// </summary>
    public class GroupedPercentOverlay : GroupedChartBase<float>
    {
        public GroupedPercentOverlay(
            string title,
            List<string> labels,
            List<float> primaryValues,
            List<float> secondaryValues,
            string primaryLabel,
            string secondaryLabel,
            Vector2? pos = null)
            : base(title, labels, primaryValues, secondaryValues, primaryLabel, secondaryLabel, pos) { }

        protected override float GetBarHeight(float value, float chartHeight) =>
            MathF.Floor(value / 100f * chartHeight);

        protected override string FormatBarLabel(float value) =>
            $"{value:0.#}%";

        protected override void DrawYAxisGrid(float x, float y, float w, float h) =>
            DrawPercentGrid(x, y, w, h);

        protected override void DrawYAxis(float x, float y, float w, float h) =>
            DrawPercentYAxisLabels(x, y, w, h);

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
    }
}
