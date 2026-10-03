using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
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

        private readonly List<TimeTicks> times;
        private readonly bool _isSegment;

        private List<(long minTick, long maxTick, int count)> buckets;
        private int maxCount;

        private int   _hoveredBucketIndex = -1;
        private float _hoveredBarWidth;
        private float _hoveredBarTopY;

        public HistogramOverlay(string roomName, List<TimeTicks> times, bool isSegment = false, Vector2? pos = null)
            : base(Utility.DialogText.Format(DialogIds.ChartHistogramTitleFmt, roomName), pos)
        {
            this.times = times;
            _isSegment = isSegment;
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

            // Bin resolution: Freedman-Diaconis or a 10% heuristic, floored at one frame.
            double q1 = MetricHelper.ComputePercentile(sortedTicks, 25);
            double q3 = MetricHelper.ComputePercentile(sortedTicks, 75);
            double iqr = q3 - q1;
            double freedmanDiaconisWidth = 2 * iqr * Math.Pow(times.Count, -1.0 / 3.0);
            double heuristicWidth = minTime * 0.1;
            double binWidth = Math.Max(Math.Min(heuristicWidth, freedmanDiaconisWidth), ChartConstants.Time.OneFrameTicks);

            buckets = [.. Binning
                .FrameAligned([.. sortedTicks.Select(t => t.Ticks)], binWidth, ChartConstants.Time.OneFrameTicks, maxBins: 50)
                .Select(b => (b.MinTick, b.MaxTick, b.Count))];

            maxCount = buckets.Max(b => b.count);
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            if (buckets.Count == 0) return;

            int yLabelCount = Math.Max(1, Math.Min(5, maxCount));
            for (int i = 1; i <= yLabelCount; i++)
            {
                float yPos = MathF.Round(y + h - h / yLabelCount * i);
                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), ChartConstants.Colors.GridLineColor, 1f);
            }

        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (buckets.Count == 0 || maxCount == 0) return;

            Color barColor = _isSegment ? ChartPalette.Current.SegmentFill : ChartPalette.Current.RoomFill;

            float barWidth = Math.Min(w / buckets.Count, MAX_BAR_WIDTH);
            float barSpacing = barWidth * ChartConstants.BarLayout.SingleBarSpacingRatio;

            for (int i = 0; i < buckets.Count; i++)
            {
                var (_, _, count) = buckets[i];
                float barX = x + i * barWidth + barSpacing / 2;
                float barY = MathF.Round(y + h - (float)count / maxCount * h);
                float barHeight = (y + h) - barY;
                float actualBarWidth = barWidth - barSpacing;

                Draw.Rect(barX, barY, actualBarWidth, barHeight, barColor);

                if (barHeight > ChartConstants.BarLayout.SingleBarLabelMinHeight)
                {
                    string countText = count.ToString();
                    Vector2 countSize = ActiveFont.Measure(countText) * ChartConstants.FontScale.AxisLabelSmall;
                    ActiveFont.DrawOutline(
                        countText,
                        new Vector2(barX + actualBarWidth / 2 - countSize.X / 2, barY - countSize.Y - ChartConstants.BarLayout.BarLabelOffsetY),
                        new Vector2(0f, 0f),
                        Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                        Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            float barWidth = Math.Min(w / buckets.Count, MAX_BAR_WIDTH);

            DrawTitle();

            int yLabelCount = Math.Max(1, Math.Min(5, maxCount));
            if (maxCount > 0) for (int i = 0; i <= yLabelCount; i++)
            {
                int countValue = (int)Math.Round((double)maxCount / yLabelCount * i);
                float yPos = MathF.Round(y + h - h / yLabelCount * i);

                string countLabel = countValue.ToString();
                Vector2 labelSize = ActiveFont.Measure(countLabel) * ChartConstants.FontScale.AxisLabelMedium;
                ActiveFont.DrawOutline(
                    countLabel,
                    new Vector2(x - labelSize.X - ChartConstants.Axis.YLabelMarginX, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                    Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

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
                    float labelY   = isEven ? y + h + ChartConstants.XAxisLabel.TickEvenLabelY   : y + h + ChartConstants.XAxisLabel.TickOddLabelY;
                    float tickEndY = isEven ? y + h + ChartConstants.XAxisLabel.TickEvenLineEndY : y + h + ChartConstants.XAxisLabel.TickOddLineEndY;

                    Draw.Line(new Vector2(tickX, y + h), new Vector2(tickX, tickEndY), axisColor, 1f);
                    DrawEdgeLabel(edgeTick, tickX, labelY);
                }
            }

            string stats = Utility.DialogText.Format(DialogIds.ChartTotalFmt, times.Count);
            Vector2 statsSize = ActiveFont.Measure(stats) * ChartConstants.FontScale.AxisLabelMedium;
            ActiveFont.DrawOutline(
                stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + ChartConstants.XAxisLabel.StatsOffsetY),
                new Vector2(0f, 0f),
                Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        private static void DrawEdgeLabel(long tick, float x, float y)
        {
            string label = new TimeTicks(tick).ToString();
            Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabelSmall;
            ActiveFont.DrawOutline(
                label,
                new Vector2(x - labelSize.X / 2, y),
                new Vector2(0f, 0f),
                Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            if (buckets.Count == 0)
            {
                _hoveredBucketIndex = -1;
                return null;
            }

            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            if (mouseHudPos.X < gx || mouseHudPos.X > gx + gw ||
                mouseHudPos.Y < gy || mouseHudPos.Y > gy + gh)
            {
                _hoveredBucketIndex = -1;
                return null;
            }

            float barWidth       = System.Math.Min(gw / buckets.Count, MAX_BAR_WIDTH);
            float barSpacing     = barWidth * ChartConstants.BarLayout.SingleBarSpacingRatio;
            float actualBarWidth = barWidth - barSpacing;
            int idx = (int)((mouseHudPos.X - gx) / barWidth);
            idx = System.Math.Clamp(idx, 0, buckets.Count - 1);

            float barX = gx + idx * barWidth + barSpacing / 2f;
            var (minTick, maxTick, count) = buckets[idx];
            float barTopY = MathF.Round(gy + gh - (float)count / maxCount * gh);

            if (mouseHudPos.X < barX || mouseHudPos.X > barX + actualBarWidth || count == 0 || mouseHudPos.Y < barTopY)
            {
                _hoveredBucketIndex = -1;
                return null;
            }

            _hoveredBucketIndex = idx;
            _hoveredBarWidth    = barWidth;
            _hoveredBarTopY     = barTopY;

            string minStr = new Domain.Time.TimeTicks(minTick).ToString();
            string maxStr = new Domain.Time.TimeTicks(maxTick).ToString();
            double pct    = times.Count > 0 ? 100.0 * count / times.Count : 0.0;
            // The range stays out of the dialog file: its loader reads "[...]" as a portrait tag.
            string runs   = Utility.DialogText.Format(count == 1 ? DialogIds.ChartBinOneRunFmt : DialogIds.ChartBinRunsFmt, count, pct.ToString("F1"));
            string label  = $"{runs}\n[{minStr}, {maxStr})";

            float barCenterX = barX + actualBarWidth / 2f;
            int   lineCount  = label.Split('\n').Length;
            float lineHeight = ActiveFont.Measure("A").Y * ChartConstants.FontScale.AxisLabelMedium;
            float labelY     = barTopY - lineCount * lineHeight - ChartConstants.Interactivity.TooltipBgPadding;
            return new HoverInfo(label, new Vector2(barCenterX, labelY), Key: $"bucket:{idx}");
        }

        public override void DrawHighlight()
        {
            if (_hoveredBucketIndex < 0) return;

            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gh = height - margin * 2;

            float barSpacing     = _hoveredBarWidth * ChartConstants.BarLayout.SingleBarSpacingRatio;
            float actualBarWidth = _hoveredBarWidth - barSpacing;
            float barX           = gx + _hoveredBucketIndex * _hoveredBarWidth + barSpacing / 2f;
            float barH           = gy + gh - _hoveredBarTopY;

            Draw.HollowRect(barX, _hoveredBarTopY, actualBarWidth, barH, Color.White * 0.8f);
        }
    }
}