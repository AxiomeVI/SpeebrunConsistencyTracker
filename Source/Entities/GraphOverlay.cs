using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

// Adapted from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/GraphOverlay.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class GraphOverlay : Entity
    {
        public class RoomData(string roomName, List<TimeTicks> times)
        {
            public string RoomName { get; set; } = roomName;
            public List<TimeTicks> Times { get; set; } = times;
        }

        private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

        private List<(Vector2 pos, Color color, float radius)> cachedDots = null;
        private readonly List<RoomData> roomDataList;
        private readonly RoomData segmentData;
        private readonly TimeTicks? targetTime = null;

        // Cache computed values
        private long maxRoomTime;
        private long maxSegmentTime;
        
        // Graph settings
        private Vector2 position;
        private readonly float width = 1800f;
        private readonly float height = 900f;
        private readonly float margin = 80f;
        
        // Colors
        private Color backgroundColor = Color.Black * 0.8f;
        private Color gridColor = Color.Gray * 0.5f;
        private Color axisColor = Color.White;
        private Color dotColor = Color.Cyan;
        private Color segmentDotColor = Color.Orange;

        public static Color ToColor(ColorChoice choice)
        {
            return choice switch
            {
                ColorChoice.BadelineHair => new Color(197, 80, 128),
                ColorChoice.MadelineHair => new Color(255, 89, 99),
                ColorChoice.Blue => new Color(100, 149, 237),
                ColorChoice.Coral => new Color(255, 127, 80),
                ColorChoice.Cyan => new Color(0, 255, 255),
                ColorChoice.Gold => new Color(255, 215, 0),
                ColorChoice.Green => new Color(50, 205, 50),
                ColorChoice.Indigo => new Color(75, 0, 130),
                ColorChoice.LightGreen => new Color(124, 252, 0),
                ColorChoice.Orange => new Color(255, 165, 0),
                ColorChoice.Pink => new Color(255, 105, 180),
                ColorChoice.Purple => new Color(147, 112, 219),
                ColorChoice.Turquoise => new Color(72, 209, 204),
                ColorChoice.Yellow => new Color(240, 228, 66),
                _ => Color.White,
            };
        }
        
        public GraphOverlay(List<List<TimeTicks>> rooms, List<TimeTicks> segment, Vector2? pos = null, TimeTicks? target = null)
        {
            Depth = -100; // Render on top
            roomDataList = [.. rooms.Select((room, index) => new RoomData("R" + (index + 1).ToString(), room))];
            segmentData = new RoomData("Segment", segment);
            targetTime = target;
            position = pos ?? new Vector2(
                (1920 - width) / 2,
                (1080 - height) / 2
            );
            dotColor = ToColor(_settings.RoomColor);
            segmentDotColor = ToColor(_settings.SegmentColor);
            ComputeMaxValues();
            Tag = Tags.HUD | Tags.Global;
        }

        public override void Render()
        {
            base.Render();
            
            Draw.Rect(position, width, height, backgroundColor);
            
            // Calculate drawable area
            float graphX = position.X + margin;
            float graphY = position.Y + margin;
            float graphWidth = width - margin * 2;
            float graphHeight = height - margin * 2;
            
            DrawAxes(graphX, graphY, graphWidth, graphHeight);
            
            DrawGrid(graphX, graphY, graphWidth, graphHeight);
            
            DrawDataPoints(graphX, graphY, graphWidth, graphHeight);

            DrawTargetLine(graphX, graphY, graphWidth, graphHeight);
            
            DrawLabels(graphX, graphY, graphWidth, graphHeight);
        }

        private void ComputeMaxValues()
        {
            maxRoomTime = 0;
            foreach (var room in roomDataList)
            {
                if (room.Times.Count != 0)
                    maxRoomTime = Math.Max(maxRoomTime, room.Times.Max(t => t.Ticks));
            }
            
            maxSegmentTime = 0;
            if (segmentData.Times.Count != 0)
                maxSegmentTime = segmentData.Times.Max(t => t.Ticks);

            // Adjust segment scale to include target time if it's higher
            if (targetTime.HasValue)
            {
                maxSegmentTime = Math.Max(maxSegmentTime, targetTime.Value.Ticks);
            }
        }

        private void DrawAxes(float x, float y, float w, float h)
        {
            // X axis
            Draw.Line(new Vector2(x, y + h), new Vector2(x + w, y + h), axisColor, 2f);
            
            // Left Y axis (for rooms)
            Draw.Line(new Vector2(x, y), new Vector2(x, y + h), axisColor, 2f);

            // Right Y axis (for segment)
            Draw.Line(new Vector2(x + w, y), new Vector2(x + w, y + h), axisColor, 2f);
        }

        private void DrawGrid(float x, float y, float w, float h)
        {
            int totalColumns = roomDataList.Count + 1; // +1 for segment
            
            // Vertical grid lines (one per room + segment)
            for (int i = 0; i <= totalColumns; i++)
            {
                float xPos = x + w / totalColumns * i;
                Draw.Line(
                    new Vector2(xPos, y),
                    new Vector2(xPos, y + h),
                    gridColor,
                    1f
                );
            }
            
            // Horizontal grid lines
            int horizontalLines = 10;
            for (int i = 0; i <= horizontalLines; i++)
            {
                float yPos = y + h / horizontalLines * i;
                Draw.Line(
                    new Vector2(x, yPos),
                    new Vector2(x + w, yPos),
                    gridColor,
                    1f
                );
            }
        }

        private void DrawTargetLine(float x, float y, float w, float h)
        {
            if (!targetTime.HasValue) return;
            
            if (maxSegmentTime == 0) return;
            
            int totalColumns = roomDataList.Count + 1;
            float columnWidth = w / totalColumns;
            
            // Calculate Y position based on target time
            float normalizedY = (float)targetTime.Value.Ticks / maxSegmentTime;
            
            // Clamp to graph bounds (in case target is outside range)
            normalizedY = MathHelper.Clamp(normalizedY, 0f, 1f);
            
            float targetY = y + h - (normalizedY * h);
            
            // Calculate X range
            float segmentStartX = x + columnWidth * roomDataList.Count;
            float segmentEndX = x + w;
            
            // Draw the target line in the segment column only
            Color targetColor = Color.Red;
            Draw.Line(
                new Vector2(segmentStartX, targetY),
                new Vector2(segmentEndX, targetY),
                targetColor,
                2f
            );
            
            // Draw small label on the line
            string targetLabel = $"Target: {targetTime.Value}";
            Vector2 labelSize = ActiveFont.Measure(targetLabel) * 0.4f;
            
            ActiveFont.DrawOutline(
                targetLabel,
                new Vector2(segmentStartX + 5, targetY - labelSize.Y - 5), // 5px padding from left, 5px above line
                new Vector2(0f, 0f),
                Vector2.One * 0.4f,
                targetColor,
                2f,
                Color.Black
            );
        }

        private void DrawDataPoints(float x, float y, float w, float h)
        {            
            if (maxRoomTime == 0 && maxSegmentTime == 0) return;
            
            // Only compute positions once
            if (cachedDots == null)
            {
                cachedDots = [];
                
                int totalColumns = roomDataList.Count + 1;
                float columnWidth = w / totalColumns;
                
                Random random = new(42); // Fixed seed for consistent positions, we don't really need these anymore with cache...
                float baseRadius = 2f;
                
                // Draw room data
                for (int roomIndex = 0; roomIndex < roomDataList.Count; roomIndex++)
                {
                    var room = roomDataList[roomIndex];
                    float centerX = x + columnWidth * (roomIndex + 0.5f);
                    
                    foreach (var time in room.Times)
                    {
                        float normalizedY = (float)time.Ticks / maxRoomTime;
                        float dotY = y + h - (normalizedY * h);
                        
                        float jitterX = centerX + (float)(random.NextDouble() - 0.5) * (columnWidth * 0.4f);
                        
                        cachedDots.Add((new Vector2(jitterX, dotY), dotColor, baseRadius));
                    }
                }
                
                // Draw segment data
                float segmentCenterX = x + columnWidth * (totalColumns - 0.5f);
                foreach (var time in segmentData.Times)
                {
                    float normalizedY = (float)time.Ticks / maxSegmentTime;
                    float dotY = y + h - (normalizedY * h);
                    
                    float jitterX = segmentCenterX + (float)(random.NextDouble() - 0.5) * (columnWidth * 0.4f);
                    
                    cachedDots.Add((new Vector2(jitterX, dotY), segmentDotColor, baseRadius));
                }
            }
            
            // Draw the cached dots every frame
            foreach (var (pos, color, radius) in cachedDots)
            {
                DrawDot(pos, color, (int)radius);
            }
        }

        private static void DrawDot(Vector2 position, Color color, float radius)
        {
            // Draw a filled circle
            int circleCount = (int)Math.Ceiling(radius * 2);
            for (int i = 0; i < circleCount; i++)
            {
                Draw.Circle(position, radius - i * 0.5f, color, 4);
            }
        }

        private void DrawLabels(float x, float y, float w, float h)
        {
            int totalColumns = roomDataList.Count + 1;
            float columnWidth = w / totalColumns;
            
            // X axis labels (room names)
            for (int i = 0; i < roomDataList.Count; i++)
            {
                float centerX = x + columnWidth * (i + 0.5f);
                string label = roomDataList[i].RoomName;
                
                if (label.Length > 10)
                    label = string.Concat(label.AsSpan(0, 10), "...");
                
                Vector2 labelSize = ActiveFont.Measure(label) * 0.5f;
                ActiveFont.DrawOutline(
                    label,
                    new Vector2(centerX - labelSize.X / 2, y + h + 10),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.5f,
                    dotColor,
                    2f,
                    Color.Black
                );
            }
            
            // Segment label
            float segmentX = x + columnWidth * (totalColumns - 0.5f);
            Vector2 segmentLabelSize = ActiveFont.Measure("Segment") * 0.5f;
            ActiveFont.DrawOutline(
                "Segment",
                new Vector2(segmentX - segmentLabelSize.X / 2, y + h + 10),
                new Vector2(0f, 0f),
                Vector2.One * 0.5f,
                segmentDotColor,
                2f,
                Color.Black
            );
            
            // LEFT Y axis labels (room times)
            int yLabelCount = 5;
            for (int i = 0; i <= yLabelCount; i++)
            {
                double timeValue = (double)maxRoomTime / yLabelCount * i;
                float yPos = y + h - h / yLabelCount * i;
                
                string timeLabel = new TimeTicks((long)Math.Round(timeValue)).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.4f;
                
                ActiveFont.DrawOutline(
                    timeLabel,
                    new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.4f,
                    dotColor, // Use room color
                    2f,
                    Color.Black
                );
            }
            
            // RIGHT Y axis labels (segment times)
            for (int i = 0; i <= yLabelCount; i++)
            {
                double timeValue = (double)maxSegmentTime / yLabelCount * i;
                float yPos = y + h - h / yLabelCount * i;
                
                string timeLabel = new TimeTicks((long)Math.Round(timeValue)).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * 0.4f;
                
                ActiveFont.DrawOutline(
                    timeLabel,
                    new Vector2(x + w + 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * 0.4f,
                    segmentDotColor, // Use segment color
                    2f,
                    Color.Black
                );
            }
            
            // Title
            string title = "Room and Segment Times";
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
        }
    }
}