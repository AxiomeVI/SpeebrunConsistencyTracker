using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class HistogramOverlay : Entity
    {
        private const long ONE_FRAME = 170000;

        private readonly string roomName;
        private readonly List<TimeTicks> times;
        private readonly Color barColor;
        
        // Graph settings
        private readonly Vector2 position;
        private readonly float width = 1800f;
        private readonly float height = 900f;
        private readonly float margin = 80f;
        
        // Colors
        private readonly Color backgroundColor = Color.Black * 0.8f;
        private readonly Color gridColor = Color.Gray * 0.5f;
        private readonly Color axisColor = Color.White;
        
        // Histogram data (cached)
        private List<(long minTick, long maxTick, int count)> buckets;
        private int maxCount;
        
        public HistogramOverlay(string roomName, List<TimeTicks> times, Color barColor, Vector2? pos = null)
        {
            this.roomName = roomName;
            this.times = times;
            this.barColor = barColor;
            
            Depth = -100;
            
            position = pos ?? new Vector2(
                (1920 - width) / 2,
                (1080 - height) / 2
            );
            
            Tag = Tags.HUD | Tags.Global;
            
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
            
            long min = times.Min(t => t.Ticks);
            long max = times.Max(t => t.Ticks);
            double minTime = (double)min;
            double maxTime = (double)max;
            double range = maxTime - minTime;
            
            // 1. Determine bin resolution - 3% of min time or 1 frame, whichever is larger
            double targetWidth = minTime * 0.03;
            double binWidth = Math.Max(targetWidth, ONE_FRAME);

            // 2. Calculate bin count
            int binCount;
            if (range <= 0)
                binCount = 1;
            else
            {
                binCount = (int)Math.Ceiling(range / binWidth);
                binCount = Math.Clamp(binCount, 5, 50); // Keep reasonable amount of bins, just in case
            }

            // 3. Recalculate exact bin width to perfectly divide the range
            double finalBinWidth = (binCount > 1) ? range / binCount : binWidth;
            
            int[] bins = new int[binCount];
            foreach (var time in times)
            {
                int binIdx = (range <= 0) 
                    ? 0 
                    : (int)Math.Floor((time.Ticks - min) / finalBinWidth);
                
                binIdx = Math.Clamp(binIdx, 0, binCount - 1);
                bins[binIdx]++;
            }
            
            // 5. Convert to bucket format
            buckets = [];
            for (int i = 0; i < binCount; i++)
            {
                long bucketMin = min + (long)(i * finalBinWidth);
                long bucketMax = (i == binCount - 1) 
                    ? max 
                    : min + (long)((i + 1) * finalBinWidth);
                
                buckets.Add((bucketMin, bucketMax, bins[i]));
            }
            
            maxCount = buckets.Max(b => b.count);
        }
        
        public override void Render()
        {
            base.Render();
            
            // Draw background
            Draw.Rect(position, width, height, backgroundColor);
            
            // Calculate drawable area
            float graphX = position.X + margin;
            float graphY = position.Y + margin;
            float graphWidth = width - margin * 2;
            float graphHeight = height - margin * 2;
            
            DrawAxes(graphX, graphY, graphWidth, graphHeight);
            DrawBars(graphX, graphY, graphWidth, graphHeight);
            DrawLabels(graphX, graphY, graphWidth, graphHeight);
        }
        
        private void DrawAxes(float x, float y, float w, float h)
        {
            // X axis
            Draw.Line(new Vector2(x, y + h), new Vector2(x + w, y + h), axisColor, 2f);
            
            // Y axis
            Draw.Line(new Vector2(x, y), new Vector2(x, y + h), axisColor, 2f);
        }
        
        private void DrawBars(float x, float y, float w, float h)
        {
            if (buckets.Count == 0 || maxCount == 0) return;
            
            float barWidth = w / buckets.Count;
            float barSpacing = barWidth * 0.1f; // 10% spacing between bars
            
            for (int i = 0; i < buckets.Count; i++)
            {
                var (_, _, count) = buckets[i];
                
                // Calculate bar height based on count
                float barHeight = (float)count / maxCount * h;
                
                // Calculate bar position
                float barX = x + i * barWidth + barSpacing / 2;
                float barY = y + h - barHeight;
                float actualBarWidth = barWidth - barSpacing;
                
                // Draw bar
                Draw.Rect(barX, barY, actualBarWidth, barHeight, barColor);
                
                // Draw count on top of bar if space permits
                if (barHeight > 20)
                {
                    string countText = count.ToString();
                    Vector2 countSize = ActiveFont.Measure(countText) * 0.3f;
                    ActiveFont.DrawOutline(
                        countText,
                        new Vector2(barX + actualBarWidth / 2 - countSize.X / 2, barY - countSize.Y - 5),
                        new Vector2(0f, 0f),
                        Vector2.One * 0.3f,
                        Color.White,
                        2f,
                        Color.Black
                    );
                }
            }
        }
        
        private void DrawLabels(float x, float y, float w, float h)
        {
            string title = $"Time Distribution - {roomName}";
            Vector2 titleSize = ActiveFont.Measure(title) * 0.7f;
            ActiveFont.DrawOutline(
                title,
                new Vector2(position.X + width / 2 - titleSize.X / 2, position.Y + 10),
                new Vector2(0f, 0f),
                Vector2.One * 0.7f,
                Color.White,
                2f,
                Color.Black
            );
            
            // Y axis label (count)
            string yAxisLabel = "Count";
            Vector2 yAxisSize = ActiveFont.Measure(yAxisLabel) * 0.5f;
            ActiveFont.DrawOutline(
                yAxisLabel,
                new Vector2(x - yAxisSize.X - 25, y + h / 2 - yAxisSize.Y / 2),
                new Vector2(0f, 0f),
                Vector2.One * 0.5f,
                Color.White,
                2f,
                Color.Black
            );
            
            // Y axis tick labels
            int yLabelCount = Math.Min(5, maxCount);
            if (yLabelCount == 0) yLabelCount = 1; // Sanity check to show at least one label

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
                    Color.White,
                    2f,
                    Color.Black
                );
            }
            
            // X axis labels (bin edges)
            if (buckets.Count > 0)
            {
                float barWidth = w / buckets.Count;
                
                // Draw label for each bin edge
                for (int i = 0; i <= buckets.Count; i++)
                {
                    long edgeTick;
                    if (i == 0)
                    {
                        edgeTick = buckets[0].minTick;
                    }
                    else if (i == buckets.Count)
                    {
                        edgeTick = buckets[^1].maxTick;
                    }
                    else
                    {
                        // Shared edge between bins
                        edgeTick = buckets[i].minTick;
                    }
                    float tickX = x + i * barWidth;
                    // Alternate Y position for labels
                    bool isEven = i % 2 == 0;
                    float labelY = isEven ? y + h + 10 : y + h + 30;
                    
                    // Draw tick mark - longer for labels below
                    float tickStartY = y + h;
                    float tickEndY = isEven ? y + h + 5 : y + h + 25; // Longer for odd (lower labels)
                    
                    Draw.Line(
                        new Vector2(tickX, tickStartY),
                        new Vector2(tickX, tickEndY),
                        axisColor,
                        1f
                    );
                    
                    DrawEdgeLabel(edgeTick, tickX, labelY);
                }
            }
            
            string stats = $"Total: {times.Count}";
            Vector2 statsSize = ActiveFont.Measure(stats) * 0.4f;
            ActiveFont.DrawOutline(
                stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + 60),
                new Vector2(0f, 0f),
                Vector2.One * 0.4f,
                Color.LightGray,
                2f,
                Color.Black
            );
        }
        
        private void DrawEdgeLabel(long tick, float x, float y)
        {
            string label = new TimeTicks(tick).ToString();
            Vector2 labelSize = ActiveFont.Measure(label) * 0.3f;
            
            ActiveFont.DrawOutline(
                label,
                new Vector2(x - labelSize.X / 2, y),
                new Vector2(0f, 0f),
                Vector2.One * 0.3f,
                barColor,
                2f,
                Color.Black
            );
        }
    }
}