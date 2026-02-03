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
            
            // Compute histogram data once
            ComputeHistogram();
        }
        
        private void ComputeHistogram()
        {
            if (times.Count == 0)
            {
                buckets = new List<(long, long, int)>();
                maxCount = 0;
                return;
            }
            
            long minTime = times.Min(t => t.Ticks);
            long maxTime = times.Max(t => t.Ticks);
            long range = maxTime - minTime;
            
            // Determine bucket count based on data range and count
            // Use Sturges' rule: k = ceil(log2(n)) + 1
            int bucketCount = (int)Math.Ceiling(Math.Log(times.Count, 2)) + 1;
            bucketCount = Math.Max(5, Math.Min(bucketCount, 20)); // Between 5 and 20 buckets
            
            // If range is very small, use fewer buckets
            if (range < bucketCount * ONE_FRAME) // Less than bucketCount frames
            {
                bucketCount = Math.Max(3, (int)(range / ONE_FRAME) + 1);
            }
            
            long bucketSize = range / bucketCount;
            if (bucketSize == 0) bucketSize = 1;
            
            // Initialize buckets
            buckets = new List<(long, long, int)>();
            for (int i = 0; i < bucketCount; i++)
            {
                long bucketMin = minTime + i * bucketSize;
                long bucketMax = (i == bucketCount - 1) ? maxTime : bucketMin + bucketSize;
                buckets.Add((bucketMin, bucketMax, 0));
            }
            
            // Count times in each bucket
            var bucketList = buckets.ToList();
            foreach (var time in times)
            {
                for (int i = 0; i < bucketList.Count; i++)
                {
                    if (time.Ticks >= bucketList[i].minTick && time.Ticks <= bucketList[i].maxTick)
                    {
                        bucketList[i] = (bucketList[i].minTick, bucketList[i].maxTick, bucketList[i].count + 1);
                        break;
                    }
                }
            }
            buckets = bucketList;
            
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
                var bucket = buckets[i];
                
                // Calculate bar height based on count
                float barHeight = (float)bucket.count / maxCount * h;
                
                // Calculate bar position
                float barX = x + i * barWidth + barSpacing / 2;
                float barY = y + h - barHeight;
                float actualBarWidth = barWidth - barSpacing;
                
                // Draw bar
                Draw.Rect(barX, barY, actualBarWidth, barHeight, barColor);
                
                // Draw count on top of bar if space permits
                if (barHeight > 20)
                {
                    string countText = bucket.count.ToString();
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
            // Title
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
            
            // Y axis tick labels (counts)
            int yLabelCount = 5;
            for (int i = 0; i <= yLabelCount; i++)
            {
                int countValue = maxCount / yLabelCount * i;
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
            
            // X axis labels (one per bar)
            if (buckets.Count > 0)
            {
                float barWidth = w / buckets.Count;
                
                for (int i = 0; i < buckets.Count; i++)
                {
                    float labelX = x + i * barWidth + barWidth / 2;
                    DrawBucketLabel(buckets[i], labelX, y + h + 10);
                }
            }
            
            // X axis label (time)
            string xAxisLabel = "Time";
            Vector2 xAxisSize = ActiveFont.Measure(xAxisLabel) * 0.5f;
            ActiveFont.DrawOutline(
                xAxisLabel,
                new Vector2(x + w / 2 - xAxisSize.X / 2, y + h + 50),
                new Vector2(0f, 0f),
                Vector2.One * 0.5f,
                Color.White,
                2f,
                Color.Black
            );
            
            // Stats summary
            if (times.Count > 0)
            {
                string stats = $"Total: {times.Count}";
                Vector2 statsSize = ActiveFont.Measure(stats) * 0.4f;
                ActiveFont.DrawOutline(
                    stats,
                    new Vector2(position.X + width / 2 - statsSize.X / 2, position.Y + height - 20),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.4f,
                    Color.LightGray,
                    2f,
                    Color.Black
                );
            }
        }
        
        private void DrawBucketLabel((long minTick, long maxTick, int count) bucket, float x, float y)
        {
            string label = $"{new TimeTicks(bucket.minTick)}";
            Vector2 labelSize = ActiveFont.Measure(label) * 0.35f;
            ActiveFont.DrawOutline(
                label,
                new Vector2(x - labelSize.X / 2, y),
                new Vector2(0f, 0f),
                Vector2.One * 0.35f,
                barColor,
                2f,
                Color.Black
            );
        }
    }
}