using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class HistogramOverlay : BaseChartOverlay
    {
        private const long ONE_FRAME = 170000;

        private readonly List<TimeTicks> times;
        private readonly Color barColor;

        // Histogram data (cached)
        private List<(long minTick, long maxTick, int count)> buckets;
        private int maxCount;

        public HistogramOverlay(string roomName, List<TimeTicks> times, Color barColor, float opacity = 1f, Vector2? pos = null)
            : base($"Time Distribution - {roomName}", pos)
        {
            this.times = times;
            this.barColor = barColor * (opacity / 100);
            ComputeHistogram();
        }

        private void ComputeHistogram()
        {
            if (times.Count == 0)
            {
                buckets = [];
                maxCount = 0;
                return;
            }

            TimeTicks[] sortedTicks = [.. times.OrderBy(t => t)];
            long minTime = sortedTicks[0].Ticks;
            long maxTime = sortedTicks[^1].Ticks;
            double range = maxTime - minTime;

            // 1. Determine bin resolution — Freedman-Diaconis or 10% heuristic, min 1 frame
            double q1 = MetricHelper.ComputePercentile(sortedTicks, 25);
            double q3 = MetricHelper.ComputePercentile(sortedTicks, 75);
            double iqr = q3 - q1;
            double freedmanDiaconisWidth = 2 * iqr * Math.Pow(times.Count, -1.0 / 3.0);
            double heuristicWidth = minTime * 0.1;
            double binWidth = Math.Max(Math.Min(heuristicWidth, freedmanDiaconisWidth), ONE_FRAME);

            // 2. Calculate bin count
            int binCount;
            if (range <= 0)
                binCount = 1;
            else
            {
                binCount = (int)Math.Ceiling(range / binWidth);
                binCount = Math.Clamp(binCount, 5, 50);
            }

            // 3. Recalculate exact bin width to perfectly divide the range
            double finalBinWidth = binCount > 1 ? range / binCount : binWidth;

            // 4. Assign each time to a bin
            int[] bins = new int[binCount];
            foreach (var time in times)
            {
                int binIdx = range <= 0
                    ? 0
                    : (int)Math.Floor((time.Ticks - minTime) / finalBinWidth);
                binIdx = Math.Clamp(binIdx, 0, binCount - 1);
                bins[binIdx]++;
            }

            // 5. Convert to bucket format
            buckets = [];
            for (int i = 0; i < binCount; i++)
            {
                long bucketMin = minTime + (long)(i * finalBinWidth);
                long bucketMax = i == binCount - 1
                    ? maxTime
                    : minTime + (long)((i + 1) * finalBinWidth);
                buckets.Add((bucketMin, bucketMax, bins[i]));
            }

            maxCount = buckets.Max(b => b.count);
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (buckets.Count == 0 || maxCount == 0) return;

            float barWidth = Math.Min(w / buckets.Count, MAX_BAR_WIDTH);
            float barSpacing = barWidth * 0.1f;

            for (int i = 0; i < buckets.Count; i++)
            {
                var (_, _, count) = buckets[i];
                float barHeight = (float)count / maxCount * h;
                float barX = x + i * barWidth + barSpacing / 2;
                float barY = y + h - barHeight;
                float actualBarWidth = barWidth - barSpacing;

                Draw.Rect(barX, barY, actualBarWidth, barHeight, barColor);

                if (barHeight > 20)
                {
                    string countText = count.ToString();
                    Vector2 countSize = ActiveFont.Measure(countText) * 0.3f;
                    ActiveFont.DrawOutline(
                        countText,
                        new Vector2(barX + actualBarWidth / 2 - countSize.X / 2, barY - countSize.Y - 5),
                        new Vector2(0f, 0f),
                        Vector2.One * 0.3f,
                        Color.White, 2f, Color.Black);
                }
            }
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            float barWidth = Math.Min(w / buckets.Count, MAX_BAR_WIDTH);

            DrawTitle();

            // Y axis label
            string yAxisLabel = "Count";
            Vector2 yAxisSize = ActiveFont.Measure(yAxisLabel) * 0.5f;
            ActiveFont.DrawOutline(
                yAxisLabel,
                new Vector2(x - yAxisSize.X - 25, y + h / 2 - yAxisSize.Y / 2),
                new Vector2(0f, 0f),
                Vector2.One * 0.5f,
                Color.White, 2f, Color.Black);

            // Y axis tick labels
            int yLabelCount = Math.Max(1, Math.Min(5, maxCount));
            for (int i = 0; i <= yLabelCount; i++)
            {
                int countValue = (int)Math.Round((double)maxCount / yLabelCount * i);
                float yPos = y + h - h / yLabelCount * i;

                string countLabel = countValue.ToString();
                Vector2 labelSize = ActiveFont.Measure(countLabel) * 0.4f;
                ActiveFont.DrawOutline(
                    countLabel,
                    new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.4f,
                    Color.White, 2f, Color.Black);
            }

            // X axis tick labels
            if (buckets.Count > 0)
            {
                for (int i = 0; i <= buckets.Count; i++)
                {
                    long edgeTick = i == 0
                        ? buckets[0].minTick
                        : i == buckets.Count
                            ? buckets[^1].maxTick
                            : buckets[i].minTick;

                    float tickX = x + i * barWidth;
                    bool isEven = i % 2 == 0;
                    float labelY = isEven ? y + h + 10 : y + h + 30;
                    float tickEndY = isEven ? y + h + 5 : y + h + 25;

                    Draw.Line(new Vector2(tickX, y + h), new Vector2(tickX, tickEndY), axisColor, 1f);
                    DrawEdgeLabel(edgeTick, tickX, labelY);
                }
            }

            // Stats
            string stats = $"Total: {times.Count}";
            Vector2 statsSize = ActiveFont.Measure(stats) * 0.4f;
            ActiveFont.DrawOutline(
                stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + 58),
                new Vector2(0f, 0f),
                Vector2.One * 0.4f,
                Color.LightGray, 2f, Color.Black);
        }

        private static void DrawEdgeLabel(long tick, float x, float y)
        {
            string label = new TimeTicks(tick).ToString();
            Vector2 labelSize = ActiveFont.Measure(label) * 0.3f;
            ActiveFont.DrawOutline(
                label,
                new Vector2(x - labelSize.X / 2, y),
                new Vector2(0f, 0f),
                Vector2.One * 0.3f,
                Color.White, 2f, Color.Black);
        }
    }
}