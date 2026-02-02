using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class GraphOverlay : Entity
    {
        public class RoomData
        {
            public string RoomName { get; set; }
            public List<TimeTicks> Times { get; set; }

            public RoomData(string roomName, List<TimeTicks> times)
            {
                RoomName = roomName;
                Times = times;
            }
        }

        private List<RoomData> roomDataList;
        private RoomData segmentData;
        private TimeTicks? targetTime = null;

        // Cache computed values
        private long maxRoomTime;
        private long maxSegmentTime;
        
        // Graph settings
        private Vector2 position;
        private float width = 1600f;
        private float height = 800f;
        private float margin = 60f;
        
        // Colors
        private Color backgroundColor = Color.Black * 0.8f;
        private Color gridColor = Color.Gray * 0.5f;
        private Color axisColor = Color.White;
        private Color dotColor = Color.Cyan;
        private Color segmentDotColor = Color.Orange;
        
        public GraphOverlay(List<List<TimeTicks>> rooms, List<TimeTicks> segment, Vector2 pos, TimeTicks? target = null)
        {
            Depth = -100; // Render on top
            this.roomDataList = rooms.Select((room, index) => new RoomData("R" + (index + 1).ToString(), room)).ToList();
            this.segmentData = new RoomData("Segment", segment);
            this.position = pos;
            this.targetTime = target;
            ComputeMaxValues();
            
            Tag = Tags.HUD | Tags.Global;
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
            
            DrawGrid(graphX, graphY, graphWidth, graphHeight);
            
            DrawDataPoints(graphX, graphY, graphWidth, graphHeight);

            DrawTargetLine(graphX, graphY, graphWidth, graphHeight);
            
            DrawLabels(graphX, graphY, graphWidth, graphHeight);
        }

        private void ComputeMaxValues()
        {
            // Find max time for rooms
            maxRoomTime = 0;
            foreach (var room in roomDataList)
            {
                if (room.Times.Any())
                    maxRoomTime = Math.Max(maxRoomTime, room.Times.Max(t => t.Ticks));
            }
            
            // Find max time for segment
            maxSegmentTime = 0;
            if (segmentData.Times.Any())
                maxSegmentTime = segmentData.Times.Max(t => t.Ticks);
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
            float targetY = y + h - (normalizedY * h);
            
            // Calculate X range (only in segment column)
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
            
            // Position label just to the left of the segment column
            ActiveFont.DrawOutline(
                targetLabel,
                new Vector2(segmentStartX - labelSize.X - 5, targetY - labelSize.Y / 2),
                new Vector2(0f, 0f),
                Vector2.One * 0.4f,
                targetColor,
                2f,
                Color.Black
            );
        }

        private void DrawDataPoints(float x, float y, float w, float h)
        {            
            if (maxRoomTime == 0 && maxSegmentTime == 0) return; // No data to display
            
            int totalColumns = roomDataList.Count + 1;
            float columnWidth = w / totalColumns;

            // Track dot positions and counts for size scaling
            Dictionary<Vector2, int> dotCounts = new Dictionary<Vector2, int>();
            float snapDistance = 2f; // Distance threshold to consider dots "same position"
            
            // Collect all dot positions first
            List<(Vector2 pos, Color color)> allDots = new List<(Vector2, Color)>();
            
            // Draw room data
            for (int roomIndex = 0; roomIndex < roomDataList.Count; roomIndex++)
            {
                var room = roomDataList[roomIndex];
                float centerX = x + columnWidth * (roomIndex + 0.5f);
                // Draw each attempt as a dot
                foreach (var time in room.Times)
                {
                    float normalizedY = (float)time.Ticks / maxRoomTime;
                    float dotY = y + h - (normalizedY * h); // Invert Y (higher time = higher on graph)
                    Vector2 pos = new Vector2(centerX, dotY);
                    allDots.Add((pos, dotColor));
                }
            }
            
            // Draw segment data (last column)
            float segmentCenterX = x + columnWidth * (totalColumns - 0.5f);
            foreach (var time in segmentData.Times)
            {
                float normalizedY = (float)time.Ticks / maxSegmentTime;
                float dotY = y + h - (normalizedY * h);
                Vector2 pos = new Vector2(segmentCenterX, dotY);      
                allDots.Add((pos, segmentDotColor));          
            }

            foreach (var dot in allDots)
            {
                Vector2 snappedPos = SnapToGrid(dot.pos, snapDistance);
                if (dotCounts.ContainsKey(snappedPos))
                    dotCounts[snappedPos]++;
                else
                    dotCounts[snappedPos] = 1;
            }

            Dictionary<Vector2, bool> drawnPositions = new Dictionary<Vector2, bool>();
            foreach (var dot in allDots)
            {
                Vector2 snappedPos = SnapToGrid(dot.pos, snapDistance);
                
                // Only draw once per position
                if (drawnPositions.ContainsKey(snappedPos))
                    continue;
                    
                drawnPositions[snappedPos] = true;
                
                int count = dotCounts[snappedPos];
                float radius = 3f + (count - 1) * 1.5f; // Base 3px, +1.5px per additional dot
                
                DrawDot(snappedPos, dot.color, radius);
            }
        }

        private Vector2 SnapToGrid(Vector2 position, float gridSize)
        {
            return new Vector2(
                (float)Math.Round(position.X / gridSize) * gridSize,
                (float)Math.Round(position.Y / gridSize) * gridSize
            );
        }

        private void DrawDot(Vector2 position, Color color, float radius)
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
                    label = label.Substring(0, 10) + "...";
                
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
            
            // Axis labels
            string leftAxisLabel = "Room";
            Vector2 leftAxisSize = ActiveFont.Measure(leftAxisLabel) * 0.5f;
            ActiveFont.DrawOutline(
                leftAxisLabel,
                new Vector2(x - leftAxisSize.X - 25, y + h / 2 - leftAxisSize.Y / 2),
                new Vector2(0f, 0f),
                Vector2.One * 0.5f,
                dotColor,
                2f,
                Color.Black
            );
            
            string rightAxisLabel = "Segment";
            Vector2 rightAxisSize = ActiveFont.Measure(rightAxisLabel) * 0.5f;
            ActiveFont.DrawOutline(
                rightAxisLabel,
                new Vector2(x + w + 70, y + h / 2 - rightAxisSize.Y / 2),
                new Vector2(0f, 0f),
                Vector2.One * 0.5f,
                segmentDotColor,
                2f,
                Color.Black
            );
        }
    }
}