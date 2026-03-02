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
    public class GroupedBarChartOverlay : BaseChartOverlay
    {
        private const long ONE_FRAME = 170000;

        private readonly List<string> labels;
        private readonly List<long> primaryTicks;
        private readonly List<long> secondaryTicks;
        private readonly Color primaryColor;
        private readonly Color secondaryColor;
        private readonly string primaryLabel;
        private readonly string secondaryLabel;
        private readonly long maxTicks;

        // Cached bar layout — computed once in DrawBars, reused in DrawLabels
        private float _cachedGroupWidth;
        private float _cachedBarWidth;
        private float _cachedBarSpacing;
        private float _cachedGroupSpacing;

        public GroupedBarChartOverlay(
            string title,
            List<string> labels,
            List<long> primaryTicks,
            List<long> secondaryTicks,
            Color primaryColor,
            Color secondaryColor,
            string primaryLabel,
            string secondaryLabel,
            float opacity = 1f,
            Vector2? pos = null)
            : base(title, pos)
        {
            this.labels = labels;
            this.primaryTicks = primaryTicks;
            this.secondaryTicks = secondaryTicks;
            this.primaryColor = primaryColor * (opacity / 100);
            this.secondaryColor = secondaryColor * (opacity / 100);
            this.primaryLabel = primaryLabel;
            this.secondaryLabel = secondaryLabel;

            long dataMax = Enumerable.Range(0, primaryTicks.Count)
                .Select(i => Math.Max(
                    primaryTicks[i],
                    secondaryTicks != null && i < secondaryTicks.Count ? secondaryTicks[i] : 0))
                .DefaultIfEmpty(0)
                .Max();

            GetAxisSettings(dataMax, out long step, out int count);
            maxTicks = step * (count + 1);
        }

        private static void GetAxisSettings(long range, out long step, out int count)
        {
            if (range <= 0)
            {
                step = ONE_FRAME;
                count = 1;
                return;
            }
            long totalFrames = (long)Math.Ceiling((double)range / ONE_FRAME);
            long framesPerTick = (long)Math.Ceiling((double)totalFrames / 11);
            if (framesPerTick <= 0) framesPerTick = 1;
            step = framesPerTick * ONE_FRAME;
            count = (int)(range / step);
        }

        private void ComputeBarLayout(float w)
        {
            _cachedGroupWidth   = Math.Min(w / Math.Max(primaryTicks.Count, 1), MAX_BAR_WIDTH);
            _cachedGroupSpacing = _cachedGroupWidth * 0.15f;
            float usableWidth   = _cachedGroupWidth - _cachedGroupSpacing;
            _cachedBarSpacing   = usableWidth * 0.05f;
            _cachedBarWidth     = (usableWidth - _cachedBarSpacing) / 2f;
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (primaryTicks.Count == 0 || maxTicks == 0) return;
            ComputeBarLayout(w);
            for (int i = 0; i < primaryTicks.Count; i++)
            {
                float groupX = x + i * _cachedGroupWidth + _cachedGroupSpacing / 2f;

                // Primary (left) bar
                float primaryHeight = (float)primaryTicks[i] / maxTicks * h;
                float primaryY = y + h - primaryHeight;

                bool secondaryExists = secondaryTicks != null && i < secondaryTicks.Count && secondaryTicks[i] > 0;
                bool equal = secondaryExists && secondaryTicks[i] == primaryTicks[i];

                if (primaryHeight > 0)
                {
                    Draw.Rect(groupX, primaryY, _cachedBarWidth, primaryHeight, primaryColor);

                    if (equal)
                    {
                        // Shared centered label above both bars
                        float groupCenterX = groupX + _cachedBarWidth + _cachedBarSpacing / 2f;
                        DrawBarLabel(
                            "+" + new TimeTicks(primaryTicks[i]).ToString().TrimStart('0'),
                            groupCenterX,
                            primaryY, // same height since equal
                            _cachedBarWidth * 2 + _cachedBarSpacing);
                    }
                    else
                    {
                        DrawBarLabel(
                            "+" + new TimeTicks(primaryTicks[i]).ToString().TrimStart('0'),
                            groupX + _cachedBarWidth / 2,
                            primaryY,
                            _cachedBarWidth);
                    }
                }

                // Secondary (right) bar
                if (secondaryExists)
                {
                    float secondaryHeight = (float)secondaryTicks[i] / maxTicks * h;
                    float secondaryX = groupX + _cachedBarWidth + _cachedBarSpacing;
                    float secondaryY = y + h - secondaryHeight;

                    Draw.Rect(secondaryX, secondaryY, _cachedBarWidth, secondaryHeight, secondaryColor);

                    if (!equal)
                    {
                        DrawBarLabel(
                            "+" + new TimeTicks(secondaryTicks[i]).ToString().TrimStart('0'),
                            secondaryX + _cachedBarWidth / 2,
                            secondaryY,
                            _cachedBarWidth);
                    }
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
            // Reuse cached layout from DrawBars (called first in base Render)
            float groupWidth = _cachedGroupWidth > 0
                ? _cachedGroupWidth
                : Math.Min(w / Math.Max(labels.Count, 1), MAX_BAR_WIDTH);

            DrawTitle();

            // Y axis ticks — frame-aligned, at most 11 labels
            GetAxisSettings(maxTicks, out long yStep, out int yLabelCount);
            for (int i = 0; i <= yLabelCount; i++)
            {
                long tickValue = yStep * i;
                if (tickValue > maxTicks) break;
                float yPos = y + h - (float)tickValue / maxTicks * h;

                string timeLabel = new TimeTicks(tickValue).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.35f;

                ActiveFont.DrawOutline(
                    timeLabel,
                    new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.35f,
                    Color.White, 2f, Color.Black);

                if (i > 0)
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), Color.Gray * 0.5f, 1f);
            }

            // X axis labels — centered under each group
            if (labels.Count > 0)
            {
                float baseLabelY = y + h + 10;

                for (int i = 0; i < labels.Count; i++)
                {
                    float labelX = x + i * groupWidth + groupWidth / 2;
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

            // Legend — same left-to-right order as bars (primary left, secondary right)
            float legendY = y + h + 55;
            float legendX = x + w;

            if (secondaryLabel != null && secondaryTicks != null)
                DrawLegendEntry(legendX, legendY, secondaryLabel, secondaryColor, 0.35f, right: true);

            if (primaryLabel != null)
            {
                float offset = secondaryLabel != null ? ActiveFont.Measure(secondaryLabel).X * 0.35f + 40 : 0;
                DrawLegendEntry(legendX - offset, legendY, primaryLabel, primaryColor, 0.35f, right: true);
            }
        }
    }
}