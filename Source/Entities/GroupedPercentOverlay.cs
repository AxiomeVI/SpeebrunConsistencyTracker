using Microsoft.Xna.Framework;
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
            Color primaryColor,
            Color secondaryColor,
            string primaryLabel,
            string secondaryLabel,
            float opacity = 1f,
            Vector2? pos = null)
            : base(title, labels, primaryValues, secondaryValues, primaryColor, secondaryColor,
                   primaryLabel, secondaryLabel, opacity, pos) { }

        protected override float GetBarHeight(float value, float chartHeight) =>
            value / 100f * chartHeight;

        protected override string FormatBarLabel(float value) =>
            $"{value:0.#}%";

        protected override void DrawYAxis(float x, float y, float w, float h) =>
            DrawPercentYAxis(x, y, w, h);
    }
}
