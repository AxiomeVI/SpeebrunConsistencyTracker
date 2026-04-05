using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

// Adapted from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/GraphOverlay.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class ScatterPlotOverlay : BaseChartOverlay
    {
        public class RoomData(string roomName, List<TimeTicks> times)
        {
            public string RoomName { get; set; } = roomName;
            public List<TimeTicks> Times { get; set; } = times;
        }

        private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

        private readonly List<RoomData> roomDataList;
        private readonly RoomData segmentData;
        private readonly TimeTicks? targetTime = null;

        // Cache computed values
        private List<(Vector2 pos, bool isSegment, float radius, int attemptIndex)> cachedDots = null;
        private int _hoveredDotIndex = -1;
        private long maxRoomTime;
        private long maxSegmentTime;
        private long minRoomTime;
        private long minSegmentTime;

        // Scatter-specific colors
        private Color gridColor = ChartConstants.Colors.GridLineColor;

        public ScatterPlotOverlay(List<List<TimeTicks>> rooms, List<TimeTicks> segment, Vector2? pos = null, TimeTicks? target = null)
            : base("Room and Segment Times", pos)
        {
            // Filter out rooms with no times
            roomDataList = [.. rooms.Select((room, index) => new RoomData("R" + (index + 1).ToString(), room)).Where(r => r.Times.Count > 0)];
            segmentData  = new RoomData("Segment", segment);
            targetTime   = target;
            ComputeMaxValues();
        }

        // Custom render order: grid → axes → data → target line → labels
        public override void Render()
        {
            Draw.Rect(position, width, height, backgroundColor);

            float graphX      = position.X + marginH;
            float graphY      = position.Y + margin;
            float graphWidth  = width  - marginH * 2;
            float graphHeight = height - margin  * 2;

            DrawGrid(graphX, graphY, graphWidth, graphHeight);
            DrawScatterAxes(graphX, graphY, graphWidth, graphHeight);
            DrawDataPoints(graphX, graphY, graphWidth, graphHeight);
            DrawTargetLine(graphX, graphY, graphWidth, graphHeight);
            DrawLabels(graphX, graphY, graphWidth, graphHeight);
        }

        // Required by BaseChartOverlay but unused — Render() is fully overridden above
        protected override void DrawBars(float x, float y, float w, float h) { }

        private void ComputeMaxValues()
        {
            long minRoomTimeRaw = long.MaxValue;
            long maxRoomTimeRaw = 0;

            foreach (var room in roomDataList)
            {
                if (room.Times.Count != 0)
                {
                    minRoomTimeRaw = Math.Min(minRoomTimeRaw, room.Times.Min(t => t.Ticks));
                    maxRoomTimeRaw = Math.Max(maxRoomTimeRaw, room.Times.Max(t => t.Ticks));
                }
            }

            long minSegmentTimeRaw = long.MaxValue;
            long maxSegmentTimeRaw = 0;

            if (segmentData.Times.Count != 0)
            {
                minSegmentTimeRaw = segmentData.Times.Min(t => t.Ticks);
                maxSegmentTimeRaw = segmentData.Times.Max(t => t.Ticks);
            }

            if (targetTime.HasValue && targetTime.Value.Ticks > 0)
            {
                maxSegmentTimeRaw = Math.Max(maxSegmentTimeRaw, targetTime.Value.Ticks);
                minSegmentTimeRaw = Math.Min(minSegmentTimeRaw, targetTime.Value.Ticks);
            }

            // Add 10% margin on each side, rounded to nearest frame
            long roomRange    = maxRoomTimeRaw - minRoomTimeRaw;
            long roomMargin   = Math.Max(ChartConstants.Time.OneFrameTicks, ChartConstants.Time.OneFrameTicks * (long)Math.Round(roomRange * 0.1 / ChartConstants.Time.OneFrameTicks, 0));
            minRoomTime = Math.Max(0, minRoomTimeRaw - roomMargin);
            maxRoomTime = maxRoomTimeRaw + roomMargin;

            long segmentRange  = maxSegmentTimeRaw - minSegmentTimeRaw;
            long segmentMargin = Math.Max(ChartConstants.Time.OneFrameTicks, ChartConstants.Time.OneFrameTicks * (long)Math.Round(segmentRange * 0.1 / ChartConstants.Time.OneFrameTicks, 0));
            minSegmentTime = Math.Max(0, minSegmentTimeRaw - segmentMargin);
            maxSegmentTime = maxSegmentTimeRaw + segmentMargin;
        }

        private void DrawScatterAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x - 1, y + h), new Vector2(x + w + 1, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x, y),     new Vector2(x, y + h),     axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x + w, y), new Vector2(x + w, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            int totalColumns  = roomDataList.Count + 1;
            float columnWidth = w / totalColumns;
            float roomAreaWidth = columnWidth * roomDataList.Count;

            // Vertical grid lines — separator between rooms and segment is thicker/more opaque
            for (int i = 0; i <= totalColumns; i++)
            {
                float xPos = x + columnWidth * i;
                bool isSeparator = i == roomDataList.Count;
                Draw.Line(new Vector2(xPos, y), new Vector2(xPos, y + h),
                    isSeparator ? Color.Gray * 0.85f : gridColor,
                    isSeparator ? 1.5f : 1f);
            }

            // Horizontal lines for rooms (left axis)
            long roomRange = maxRoomTime - minRoomTime;
            if (roomRange > 0)
            {
                GetFrameAxisSettings(roomRange, out long roomStep, out int yLeftLabelCount);
                for (int i = 0; i <= yLeftLabelCount; i++)
                {
                    float normalizedY = (float)(i * roomStep) / roomRange;
                    float yPos = y + h - (normalizedY * h);
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + roomAreaWidth, yPos), gridColor, 1f);
                }
            }

            // Horizontal lines for segment (right axis)
            long segmentRange = maxSegmentTime - minSegmentTime;
            if (segmentRange > 0)
            {
                GetFrameAxisSettings(segmentRange, out long segmentStep, out int yRightLabelCount);
                for (int i = 0; i <= yRightLabelCount; i++)
                {
                    float normalizedY = (float)(i * segmentStep) / segmentRange;
                    float yPos = y + h - (normalizedY * h);
                    Draw.Line(new Vector2(x + roomAreaWidth, yPos), new Vector2(x + w, yPos), gridColor, 1f);
                }
            }
        }

        private void DrawTargetLine(float x, float y, float w, float h)
        {
            if (!targetTime.HasValue || targetTime.Value <= 0) return;

            long segmentRange = maxSegmentTime - minSegmentTime;
            if (segmentRange == 0) return;

            int totalColumns  = roomDataList.Count + 1;
            float columnWidth = w / totalColumns;

            float normalizedY = (float)(targetTime.Value.Ticks - minSegmentTime) / segmentRange;
            normalizedY = MathHelper.Clamp(normalizedY, 0f, 1f);
            float targetY = y + h - (normalizedY * h);

            float segmentStartX = x + columnWidth * roomDataList.Count;
            float segmentEndX   = x + w;

            Draw.Line(new Vector2(segmentStartX, targetY), new Vector2(segmentEndX, targetY), Color.Red, ChartConstants.Stroke.OutlineSize);

            string targetLabel = $"Target: {targetTime.Value}";
            Vector2 labelSize  = ActiveFont.Measure(targetLabel) * ChartConstants.FontScale.AxisLabelMedium;
            ActiveFont.DrawOutline(
                targetLabel,
                new Vector2(segmentStartX + 5, targetY - labelSize.Y - 5),
                new Vector2(0f, 0f),
                Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                Color.Red, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        private void DrawDataPoints(float x, float y, float w, float h)
        {
            if (cachedDots == null)
            {
                cachedDots = [];

                int totalColumns  = roomDataList.Count + 1;
                float columnWidth = w / totalColumns;
                Random random     = new(42);
                float baseRadius  = ChartConstants.Scatter.DotRadius;

                long roomRange    = maxRoomTime - minRoomTime;
                long segmentRange = maxSegmentTime - minSegmentTime;

                for (int roomIndex = 0; roomIndex < roomDataList.Count; roomIndex++)
                {
                    var room    = roomDataList[roomIndex];
                    float centerX = x + columnWidth * (roomIndex + 0.5f);

                    for (int t = 0; t < room.Times.Count; t++)
                    {
                        float normalizedY = roomRange > 0 ? (float)(room.Times[t].Ticks - minRoomTime) / roomRange : 0.5f;
                        float dotY        = y + h - (normalizedY * h);
                        float jitterX     = centerX + (float)(random.NextDouble() - 0.5) * (columnWidth * ChartConstants.Scatter.JitterRatio);
                        cachedDots.Add((new Vector2(jitterX, dotY), false, baseRadius, t));
                    }
                }

                float segmentCenterX = x + columnWidth * (totalColumns - 0.5f);
                for (int t = 0; t < segmentData.Times.Count; t++)
                {
                    float normalizedY = segmentRange > 0 ? (float)(segmentData.Times[t].Ticks - minSegmentTime) / segmentRange : 0.5f;
                    float dotY        = y + h - (normalizedY * h);
                    float jitterX     = segmentCenterX + (float)(random.NextDouble() - 0.5) * (columnWidth * ChartConstants.Scatter.JitterRatio);
                    cachedDots.Add((new Vector2(jitterX, dotY), true, baseRadius, t));
                }
            }

            foreach (var (pos, isSegment, radius, _) in cachedDots)
                DrawDot(pos, isSegment ? _settings.SegmentColorFinal : _settings.RoomColorFinal, radius);
        }

        private static void DrawDot(Vector2 position, Color color, float radius)
        {
            int circleCount = (int)Math.Ceiling(radius * 2);
            for (int i = 0; i < circleCount; i++)
                Draw.Circle(position, radius - i * 0.5f, color, 4);
        }

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            if (cachedDots == null)
            {
                _hoveredDotIndex = -1;
                return null;
            }

            float snapSq  = ChartConstants.Interactivity.ScatterSnapRadius * ChartConstants.Interactivity.ScatterSnapRadius;
            float bestDist = float.MaxValue;
            int   bestIdx  = -1;

            for (int i = 0; i < cachedDots.Count; i++)
            {
                float dx   = cachedDots[i].pos.X - mouseHudPos.X;
                float dy   = cachedDots[i].pos.Y - mouseHudPos.Y;
                float dist = dx * dx + dy * dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx  = i;
                }
            }

            if (bestIdx < 0 || bestDist > snapSq)
            {
                _hoveredDotIndex = -1;
                return null;
            }

            _hoveredDotIndex = bestIdx;
            var (pos, isSegment, radius, attemptIndex) = cachedDots[bestIdx];

            string label;
            if (isSegment)
            {
                string timeStr = segmentData.Times[attemptIndex].ToString();
                label = $"Segment attempt #{attemptIndex + 1}: {timeStr}";
            }
            else
            {
                int dotCount = 0;
                int roomIdx  = -1;
                for (int r = 0; r < roomDataList.Count; r++)
                {
                    if (bestIdx < dotCount + roomDataList[r].Times.Count)
                    {
                        roomIdx = r;
                        break;
                    }
                    dotCount += roomDataList[r].Times.Count;
                }
                string timeStr  = roomIdx >= 0 ? roomDataList[roomIdx].Times[attemptIndex].ToString() : "?";
                label = $"Attempt #{attemptIndex + 1}: {timeStr}";
            }

            float lineHeight = ActiveFont.Measure("A").Y * ChartConstants.FontScale.AxisLabelMedium;
            float labelY = pos.Y - ChartConstants.Scatter.DotRadius - ChartConstants.Interactivity.TooltipPaddingY - lineHeight - ChartConstants.Interactivity.TooltipBgPadding * 2f;
            return new HoverInfo(label, new Vector2(pos.X, labelY));
        }

        public override void DrawHighlight()
        {
            if (_hoveredDotIndex < 0 || cachedDots == null) return;

            var (pos, isSegment, radius, _) = cachedDots[_hoveredDotIndex];
            float highlightRadius = ChartConstants.Interactivity.ScatterSnapRadius;
            int circleCount = (int)Math.Ceiling(highlightRadius * 2);
            for (int i = 0; i < circleCount; i++)
                Draw.Circle(pos, highlightRadius - i * 0.5f, Color.White * 0.9f, 4);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            int totalColumns  = roomDataList.Count + 1;
            float columnWidth = w / totalColumns;
            float baseLabelY  = y + h + ChartConstants.XAxisLabel.BaseOffsetY;

            // X axis labels (room names) — staggered
            for (int i = 0; i < roomDataList.Count; i++)
            {
                float centerX = x + columnWidth * (i + 0.5f);
                string label  = roomDataList[i].RoomName;
                if (label.Length > ChartConstants.Scatter.LabelTruncationLength)
                    label = string.Concat(label.AsSpan(0, ChartConstants.Scatter.LabelTruncationLength), "...");

                float labelY      = totalColumns > ChartConstants.XAxisLabel.StaggerThreshold
                    ? (i % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                    : baseLabelY;
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;
                ActiveFont.DrawOutline(label,
                    new Vector2(centerX - labelSize.X / 2, labelY),
                    new Vector2(0f, 0f),
                    Vector2.One * ChartConstants.FontScale.AxisLabel,
                    _settings.RoomColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            // Segment label — continues the stagger pattern
            float segmentX     = x + columnWidth * (totalColumns - 0.5f);
            float segmentLabelY = totalColumns >= ChartConstants.XAxisLabel.StaggerThreshold
                ? (roomDataList.Count % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                : baseLabelY;
            Vector2 segLabelSize = ActiveFont.Measure("Segment") * ChartConstants.FontScale.AxisLabel;
            ActiveFont.DrawOutline("Segment",
                new Vector2(segmentX - segLabelSize.X / 2, segmentLabelY),
                new Vector2(0f, 0f),
                Vector2.One * ChartConstants.FontScale.AxisLabel,
                _settings.SegmentColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);

            // Left Y axis labels (room times)
            long roomRange = maxRoomTime - minRoomTime;
            if (roomRange > 0)
            {
                GetFrameAxisSettings(roomRange, out long roomStep, out int yLeftLabelCount);
                for (int i = 0; i <= yLeftLabelCount; i++)
                {
                    long timeValue    = minRoomTime + i * roomStep;
                    float normalizedY = (float)(i * roomStep) / roomRange;
                    float yPos        = y + h - (normalizedY * h);
                    string timeLabel  = new TimeTicks(timeValue).ToString();
                    Vector2 labelSize = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelMedium;
                    ActiveFont.DrawOutline(timeLabel,
                        new Vector2(x - labelSize.X - ChartConstants.Axis.YLabelMarginX, yPos - labelSize.Y / 2),
                        new Vector2(0f, 0f),
                        Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                        _settings.RoomColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }

            // Right Y axis labels (segment times)
            long segmentRange = maxSegmentTime - minSegmentTime;
            if (segmentRange > 0)
            {
                GetFrameAxisSettings(segmentRange, out long segmentStep, out int yRightLabelCount);
                for (int i = 0; i <= yRightLabelCount; i++)
                {
                    long timeValue    = minSegmentTime + i * segmentStep;
                    float normalizedY = (float)(i * segmentStep) / segmentRange;
                    float yPos        = y + h - (normalizedY * h);
                    string timeLabel  = new TimeTicks(timeValue).ToString();
                    Vector2 labelSize = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelMedium;
                    ActiveFont.DrawOutline(timeLabel,
                        new Vector2(x + w + ChartConstants.Trajectory.RightLabelMarginX, yPos - labelSize.Y / 2),
                        new Vector2(0f, 0f),
                        Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                        _settings.SegmentColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }

            DrawTitle();
        }
    }
}
