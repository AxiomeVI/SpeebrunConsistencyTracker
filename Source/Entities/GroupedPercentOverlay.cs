using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Grouped bar chart showing per-room survival rate and DNF rate as percentages.
    /// Left bar (green): percentage of runs still alive after each room.
    /// Right bar (red): percentage of runs that died in each room.
    /// Y axis is 0–100%.
    /// </summary>
    public class GroupedPercentOverlay : BaseChartOverlay
    {
        private const int Y_TICK_COUNT = 10; // 0%, 10%, 20%, ... 100%

        private readonly List<string> _labels;
        private readonly List<float> _survivalRates;  // % of runs still alive after room
        private readonly List<float> _dnfRates;       // % of runs that died in room
        private readonly Color _survivalColor;
        private readonly Color _dnfColor;

        // Cached bar layout — computed once in DrawBars, reused in DrawLabels
        private float _cachedGroupWidth;
        private float _cachedBarWidth;
        private float _cachedBarSpacing;
        private float _cachedGroupSpacing;

        public GroupedPercentOverlay(
            List<string> labels,
            List<int> roomDnfCounts,
            int totalAttempts,
            Color primaryColor,
            Color secondaryColor,
            float opacity = 1f,
            Vector2? pos = null)
            : base("DNF Rate per Room & Runs Remaining", pos)
        {
            _labels = labels;
            _survivalColor = primaryColor * (opacity / 100f);
            _dnfColor = secondaryColor * (opacity / 100f);

            _survivalRates = [];
            _dnfRates = [];

            if (totalAttempts <= 0)
            {
                ComputeBarLayout(width - margin * 2);
                return;
            }

            int alive = totalAttempts;
            foreach (int dnf in roomDnfCounts)
            {
                float dnfRate = alive > 0 ? (float)dnf / totalAttempts * 100f : 0f;
                alive -= dnf;
                float survivalRate = (float)alive / totalAttempts * 100f;
                _survivalRates.Add(survivalRate);
                _dnfRates.Add(dnfRate);
            }
            ComputeBarLayout(width - margin * 2);
        }

        private void ComputeBarLayout(float w)
        {
            _cachedGroupWidth   = System.Math.Min(w / System.Math.Max(_labels.Count, 1), MAX_BAR_WIDTH);
            _cachedGroupSpacing = _cachedGroupWidth * 0.15f;
            float usableWidth   = _cachedGroupWidth - _cachedGroupSpacing;
            _cachedBarSpacing   = usableWidth * 0.05f;
            _cachedBarWidth     = (usableWidth - _cachedBarSpacing) / 2f;
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_survivalRates.Count == 0) return;

            for (int i = 0; i < _survivalRates.Count; i++)
            {
                float groupX = x + i * _cachedGroupWidth + _cachedGroupSpacing / 2f;

                // Left bar — DNF rate
                float dnfHeight = _dnfRates[i] / 100f * h;
                if (dnfHeight > 0)
                {
                    float dnfY = y + h - dnfHeight;
                    Draw.Rect(groupX, dnfY, _cachedBarWidth, dnfHeight, _dnfColor);
                    DrawBarLabel($"{_dnfRates[i]:0.#}%", groupX + _cachedBarWidth / 2, dnfY, _cachedBarWidth);
                }

                // Right bar — survival rate
                float survivalHeight = _survivalRates[i] / 100f * h;
                if (survivalHeight > 0)
                {
                    float survivalX = groupX + _cachedBarWidth + _cachedBarSpacing;
                    float survivalY = y + h - survivalHeight;
                    Draw.Rect(survivalX, survivalY, _cachedBarWidth, survivalHeight, _survivalColor);
                    DrawBarLabel($"{_survivalRates[i]:0.#}%", survivalX + _cachedBarWidth / 2, survivalY, _cachedBarWidth);
                }
            }
        }

        private static void DrawBarLabel(string text, float barCenterX, float barTopY, float barWidth)
        {
            float scale = barWidth > 30 ? 0.3f : barWidth > 15 ? 0.22f : 0f;
            if (scale == 0f) return;

            Vector2 textSize = ActiveFont.Measure(text) * scale;
            ActiveFont.DrawOutline(
                text,
                new Vector2(barCenterX - textSize.X / 2, barTopY - textSize.Y - 3),
                new Vector2(0f, 0f),
                Vector2.One * scale,
                Color.White, 2f, Color.Black);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            DrawTitle();

            float groupWidth = _cachedGroupWidth;

            // Y axis ticks — 0% to 100% in steps of 10%
            for (int i = 0; i <= Y_TICK_COUNT; i++)
            {
                float pct = i * 10f;
                float yPos = y + h - (pct / 100f * h);
                string label = $"{pct:0}%";
                Vector2 labelSize = ActiveFont.Measure(label) * 0.35f;

                ActiveFont.DrawOutline(
                    label,
                    new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.35f,
                    Color.White, 2f, Color.Black);

                if (i > 0)
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), Color.Gray * 0.5f, 1f);
            }

            // X axis labels
            float baseLabelY = y + h + 10;
            for (int i = 0; i < _labels.Count; i++)
            {
                float labelX = x + i * groupWidth + groupWidth / 2;
                string label = _labels[i];
                Vector2 labelSize = ActiveFont.Measure(label) * 0.35f;
                float labelY = _labels.Count > 25 ? i % 2 == 0 ? baseLabelY : baseLabelY + 20 : baseLabelY;

                ActiveFont.DrawOutline(
                    label,
                    new Vector2(labelX - labelSize.X / 2, labelY),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.35f,
                    Color.LightGray, 2f, Color.Black);
            }

            // Legend
            float legendY = y + h + 55;
            float legendX = x + w;
            DrawLegendEntry(legendX, legendY, "Runs Remaining", _survivalColor, 0.35f, right: true);
            float offset = ActiveFont.Measure("Runs Remaining").X * 0.35f + 40;
            DrawLegendEntry(legendX - offset, legendY, "DNF rate", _dnfColor, 0.35f, right: true);
        }
    }
}