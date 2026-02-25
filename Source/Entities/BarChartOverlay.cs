using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class BarChartOverlay : Entity
    {
        private readonly string title;
        private readonly List<string> labels;
        private readonly List<int> values;
        private readonly Color barColor;
        
        // Graph settings
        private readonly Vector2 position;
        private readonly float width = 1800f;
        private readonly float height = 900f;
        private readonly float margin = 80f;
        
        // Colors
        private readonly Color backgroundColor = Color.Black * 0.8f;
        private readonly Color axisColor = Color.White;
        
        private readonly int maxValue;

        public BarChartOverlay(string title, List<string> labels, List<int> values, Color barColor, Vector2? pos = null)
        {
            this.title = title;
            this.labels = labels;
            this.values = values;
            this.barColor = barColor;
            
            Depth = -100;
            
            position = pos ?? new Vector2(
                (1920 - width) / 2,
                (1080 - height) / 2
            );
            
            Tag = Tags.HUD | Tags.Global;
            maxValue = values.Count > 0 ? values.Max() : 0;
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
            if (values.Count == 0 || maxValue == 0) return;
            
            float barWidth = w / values.Count;
            float barSpacing = barWidth * 0.15f;
            
            for (int i = 0; i < values.Count; i++)
            {
                int count = values[i];
                
                float barHeight = (float)count / maxValue * h;
                
                float barX = x + i * barWidth + barSpacing / 2;
                float barY = y + h - barHeight;
                float actualBarWidth = barWidth - barSpacing;
                
                // Draw bar
                Draw.Rect(barX, barY, actualBarWidth, barHeight, barColor);
                
                // Draw count on top of bar
                if (barHeight > 20)
                {
                    string countText = count.ToString();
                    Vector2 countSize = ActiveFont.Measure(countText) * 0.35f;
                    ActiveFont.DrawOutline(
                        countText,
                        new Vector2(barX + actualBarWidth / 2 - countSize.X / 2, barY - countSize.Y - 5),
                        new Vector2(0f, 0f),
                        Vector2.One * 0.35f,
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
            
            // Y axis label
            string yAxisLabel = "DNFs";
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
            int yLabelCount = Math.Min(5, maxValue);
            if (yLabelCount == 0) yLabelCount = 1;

            for (int i = 0; i <= yLabelCount; i++)
            {
                int countValue = (int)Math.Round((double)maxValue / yLabelCount * i);
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
            
            // X axis labels (room names)
            if (labels.Count > 0)
            {
                float barWidth = w / labels.Count;
                float baseLabelY = y + h + 10;
                
                for (int i = 0; i < labels.Count; i++)
                {
                    float labelX = x + i * barWidth + barWidth / 2;
                    
                    string label = labels[i];
                    Vector2 labelSize = ActiveFont.Measure(label) * 0.35f;
                    // Stagger Y positions to improve visibility when tracking large number of rooms
                    float labelY = values.Count > 25 ? i % 2 == 0 ? baseLabelY : baseLabelY + 20 : baseLabelY;

                    ActiveFont.DrawOutline(
                        label,
                        new Vector2(labelX - labelSize.X / 2, labelY),
                        new Vector2(0f, 0f),
                        Vector2.One * 0.35f,
                        barColor,
                        2f,
                        Color.Black
                    );
                }
            }
            
            // Total at bottom
            int total = values.Sum();
            string stats = $"Total DNFs: {total}";
            Vector2 statsSize = ActiveFont.Measure(stats) * 0.4f;
            ActiveFont.DrawOutline(
                stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + 58),
                new Vector2(0f, 0f),
                Vector2.One * 0.4f,
                Color.LightGray,
                2f,
                Color.Black
            );
        }
    }
}
