using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
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

        // Parallel to roomDataList[i].Times: global attempt index for each time entry.
        private readonly List<List<int>> roomAttemptIndices;
        // Global attempt index for each segment time entry.
        private readonly List<int> segmentAttemptIndices;
        // Maps filtered roomDataList index → original visible room index (before empty-room filtering).
        private readonly List<int> _originalRoomIndices;

        // visibleRoomIndex is -1 for segment dots. The time travels with the dot: the tooltip used
        // to find it again by re-walking every column and an IndexOf over the segment list.
        private List<(Vector2 pos, bool isSegment, float radius, int globalAttemptIndex, int visibleRoomIndex, TimeTicks time)> cachedDots = null;
        private int _hoveredDotIndex = -1;
        private long maxRoomTime;
        private long maxSegmentTime;
        private long minRoomTime;
        private long minSegmentTime;

        private readonly NormalizationToggle _toggle = new(normalized: false);
        private double _minRoomPct, _maxRoomPct;

        private Color gridColor = ChartConstants.Colors.GridLineColor;

        public ScatterPlotOverlay(List<List<TimeTicks>> rooms, List<List<int>> roomIndices, List<TimeTicks> segment, List<int> segmentIndices, Vector2? pos = null, TimeTicks? target = null)
            : base("Room and Segment Times", pos)
        {
            // Drops rooms with no times; the index lists must stay in sync with it.
            var filtered = rooms
                .Select((room, i) => (room, indices: roomIndices[i], originalIndex: i))
                .Where(x => x.room.Count > 0)
                .ToList();
            _originalRoomIndices = filtered.Select(x => x.originalIndex).ToList();
            // Labelled by the room's own index, not its position after filtering.
            roomDataList         = filtered.Select(x => new RoomData(Utility.RoomLabels.For(x.originalIndex), x.room)).ToList();
            roomAttemptIndices   = filtered.Select(x => x.indices).ToList();
            segmentData          = new RoomData("Segment", segment);
            segmentAttemptIndices = segmentIndices;
            targetTime           = target;
            ComputeMaxValues();
            RecomputeRelativeRanges();
        }

        public override bool SupportsDeleteRuns => true;

        // Render order: grid → axes → data → target line → labels.
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
            _toggle.Render(position, width, height);
        }

        // Unused: Render() is fully overridden above.
        protected override void DrawBars(float x, float y, float w, float h) { }

        public override void ClearHiddenColumns()
        {
            base.ClearHiddenColumns();
            cachedDots = null;
            ComputeMaxValues();
            RecomputeRelativeRanges();
        }

        public override void ToggleColumn(int columnIndex)
        {
            base.ToggleColumn(columnIndex);
            cachedDots = null;
            ComputeMaxValues();
            RecomputeRelativeRanges();
        }

        private float ComputeNormalColumnWidth(float gw)
        {
            int visibleRooms = roomDataList.Count - _hiddenColumns.Count;
            int visibleCols  = visibleRooms + 1; // +1 for segment (never hidden)
            if (visibleCols <= 0) return gw / Math.Max(roomDataList.Count + 1, 1);
            float available = gw - _hiddenColumns.Count * ChartConstants.Interactivity.HiddenColumnStubWidth;
            return available / visibleCols;
        }

        private float GetColumnStartX(float gx, float gw, int i)
        {
            float normalW = ComputeNormalColumnWidth(gw);
            float x = gx;
            for (int j = 0; j < i; j++)
                x += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            return x;
        }

        private List<TimeTicks> SortedRoomTimes(int roomIndex) => [.. roomDataList[roomIndex].Times.OrderBy(t => t)];

        private void RecomputeRelativeRanges() =>
            ComputeRelativeRanges(roomDataList.Count, SortedRoomTimes, out _minRoomPct, out _maxRoomPct);

        private void ComputeMaxValues()
        {
            long minRoomTimeRaw = long.MaxValue;
            long maxRoomTimeRaw = 0;

            for (int i = 0; i < roomDataList.Count; i++)
            {
                if (_hiddenColumns.Contains(i)) continue;
                var room = roomDataList[i];
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

            // 10% margin each side, rounded to a whole frame.
            long roomRange    = maxRoomTimeRaw - minRoomTimeRaw;
            long roomMargin   = Math.Max(ChartConstants.Time.OneFrameTicks, ChartConstants.Time.OneFrameTicks * (long)Math.Round(roomRange * 0.1 / ChartConstants.Time.OneFrameTicks, 0));
            minRoomTime = Math.Max(0, minRoomTimeRaw - roomMargin);
            maxRoomTime = maxRoomTimeRaw + roomMargin;

            long segmentRange  = maxSegmentTimeRaw - minSegmentTimeRaw;
            long segmentMargin = Math.Max(ChartConstants.Time.OneFrameTicks, ChartConstants.Time.OneFrameTicks * (long)Math.Round(segmentRange * 0.1 / ChartConstants.Time.OneFrameTicks, 0));
            minSegmentTime = Math.Max(0, minSegmentTimeRaw - segmentMargin);
            maxSegmentTime = maxSegmentTimeRaw + segmentMargin;
        }

        public override bool HandleClick(HoverInfo hover)
        {
            if (!_toggle.HandleClick()) return false;
            cachedDots = null;
            return true;
        }

        private void DrawScatterAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x - 1, y + h), new Vector2(x + w + 1, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x, y),     new Vector2(x, y + h),     axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x + w, y), new Vector2(x + w, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            float normalW2 = ComputeNormalColumnWidth(w);
            float colX = x;
            for (int i = 0; i <= roomDataList.Count + 1; i++)
            {
                bool isSeparator = i == roomDataList.Count;
                Draw.Line(new Vector2(colX, y), new Vector2(colX, y + h),
                    isSeparator ? Color.Gray * 0.85f : gridColor,
                    isSeparator ? 1.5f : 1f);
                if (i < roomDataList.Count)
                    colX += _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW2;
                else if (i == roomDataList.Count)
                    colX += normalW2; // segment column
            }

            float roomAreaWidth = 0;
            for (int j = 0; j < roomDataList.Count; j++)
                roomAreaWidth += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW2;

            // Room lines read off the left axis.
            if (_toggle.Normalized)
            {
                double rangePct = _maxRoomPct - _minRoomPct;
                GetPercentageAxisSettings(rangePct, out double stepPct, out int count);
                for (int i = 0; i <= count; i++)
                {
                    double pctValue = _minRoomPct + i * stepPct;
                    if (pctValue > _maxRoomPct + 1e-9) break;
                    float yPos = ToPixelY(pctValue, _minRoomPct, _maxRoomPct, y, h);
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + roomAreaWidth, yPos), gridColor, 1f);
                }
                // 100% is the median anchor.
                float y100 = ToPixelY(100.0, _minRoomPct, _maxRoomPct, y, h);
                if (y100 >= y && y100 <= y + h)
                    Draw.Line(new Vector2(x, y100), new Vector2(x + roomAreaWidth, y100),
                        Color.Yellow * 0.5f, 1.5f);
            }
            else
            {
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
            }

            // Segment lines read off the right axis.
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

            float normalizedY = (float)(targetTime.Value.Ticks - minSegmentTime) / segmentRange;
            normalizedY = MathHelper.Clamp(normalizedY, 0f, 1f);
            float targetY = y + h - (normalizedY * h);

            float segmentStartX = GetColumnStartX(x, w, roomDataList.Count);
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

                float normalW     = ComputeNormalColumnWidth(w);
                float baseRadius  = ChartConstants.Scatter.DotRadius;

                long roomRange    = maxRoomTime - minRoomTime;
                long segmentRange = maxSegmentTime - minSegmentTime;

                for (int roomIndex = 0; roomIndex < roomDataList.Count; roomIndex++)
                {
                    if (_hiddenColumns.Contains(roomIndex)) continue;

                    var room    = roomDataList[roomIndex];
                    float startX  = GetColumnStartX(x, w, roomIndex);
                    float centerX = startX + normalW * 0.5f;

                    long medTicks = 0;
                    if (_toggle.Normalized)
                    {
                        medTicks = MetricHelper.ComputePercentile(SortedRoomTimes(roomIndex), 50).Ticks;
                        if (medTicks == 0) continue;
                    }

                    for (int t = 0; t < room.Times.Count; t++)
                    {
                        float dotY;
                        if (_toggle.Normalized)
                        {
                            double pct = (double)room.Times[t].Ticks / medTicks * 100.0;
                            dotY = ToPixelY(pct, _minRoomPct, _maxRoomPct, y, h);
                        }
                        else
                        {
                            float normalizedY = roomRange > 0 ? (float)(room.Times[t].Ticks - minRoomTime) / roomRange : 0.5f;
                            dotY = y + h - (normalizedY * h);
                        }
                        float dotX      = ChronologicalX(centerX, normalW, t, room.Times.Count);
                        int   globalIdx = roomAttemptIndices[roomIndex][t];
                        cachedDots.Add((new Vector2(dotX, dotY), false, baseRadius, globalIdx, _originalRoomIndices[roomIndex], room.Times[t]));
                    }
                }

                // The segment column is never hidden.
                float segStartX  = GetColumnStartX(x, w, roomDataList.Count);
                float segCenterX = segStartX + normalW * 0.5f;
                for (int t = 0; t < segmentData.Times.Count; t++)
                {
                    float normalizedY = segmentRange > 0 ? (float)(segmentData.Times[t].Ticks - minSegmentTime) / segmentRange : 0.5f;
                    float dotY        = y + h - (normalizedY * h);
                    float dotX        = ChronologicalX(segCenterX, normalW, t, segmentData.Times.Count);
                    int   globalIdx   = segmentAttemptIndices[t];
                    cachedDots.Add((new Vector2(dotX, dotY), true, baseRadius, globalIdx, -1, segmentData.Times[t]));
                }
            }

            foreach (var (pos, isSegment, radius, _, _, _) in cachedDots)
                DrawDot(pos, isSegment ? _settings.SegmentColorFinal : _settings.RoomColorFinal, radius);
        }

        // Dots spread chronologically inside their column: oldest left, newest right.
        private static float ChronologicalX(float centerX, float columnWidth, int index, int count)
        {
            if (count <= 1) return centerX;
            float t = (float)index / (count - 1);
            return centerX + (t - 0.5f) * columnWidth * ChartConstants.Scatter.SpreadRatio;
        }

        // One sprite. It used to be ceil(2r) nested Draw.Circle calls of four segments each --
        // sixteen sprites a dot, every frame, for a chart that can hold 500 runs x 20 rooms.
        // At this radius the square reads as a dot.
        private static void DrawDot(Vector2 position, Color color, float radius)
            => Draw.Rect(position.X - radius, position.Y - radius, radius * 2f, radius * 2f, color);

        // The segment column is never hideable, so it stays out of the count.
        public override int? ColumnHitTest(Vector2 mousePos) =>
            HitTestColumnStrip(mousePos, roomDataList.Count, ComputeNormalColumnWidth(width - marginH * 2));

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            if (_toggle.UpdateHover(mouseHudPos))
            {
                _hoveredDotIndex = -1;
                return new HoverInfo("", mouseHudPos);
            }

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
            var (pos, isSegment, _, globalAttemptIndex, visibleRoomIndex, time) = cachedDots[bestIdx];

            string label     = $"Run #{globalAttemptIndex + 1}: {time}";
            float lineHeight = ActiveFont.Measure("A").Y * ChartConstants.FontScale.AxisLabelMedium;
            float labelY     = pos.Y - ChartConstants.Scatter.DotRadius - ChartConstants.Interactivity.TooltipPaddingY - lineHeight - ChartConstants.Interactivity.TooltipBgPadding * 2f;
            string key = isSegment
                ? PinKey.ForSegment(globalAttemptIndex)
                : PinKey.ForRoom(globalAttemptIndex, visibleRoomIndex);
            return new HoverInfo(label, new Vector2(pos.X, labelY), Key: key);
        }

        public override void DrawHighlight()
        {
            if (_hoveredDotIndex < 0 || cachedDots == null) return;

            var (pos, _, _, _, _, _) = cachedDots[_hoveredDotIndex];
            float highlightRadius = ChartConstants.Interactivity.ScatterSnapRadius;
            int circleCount = (int)Math.Ceiling(highlightRadius * 2);
            for (int i = 0; i < circleCount; i++)
                Draw.Circle(pos, highlightRadius - i * 0.5f, Color.White * 0.9f, 4);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            float normalW3   = ComputeNormalColumnWidth(w);
            int totalVisible = roomDataList.Count - _hiddenColumns.Count + 1;
            bool isStaggered = totalVisible > ChartConstants.XAxisLabel.StaggerThreshold;
            float baseLabelY = y + h + (isStaggered ? ChartConstants.XAxisLabel.BaseOffsetY / 2f : ChartConstants.XAxisLabel.BaseOffsetY);

            for (int i = 0; i < roomDataList.Count; i++)
            {
                float colW   = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW3;
                float startX = GetColumnStartX(x, w, i);
                DrawColumnStrip(i, startX, colW, y + h);

                if (_hiddenColumns.Contains(i)) continue;
                float centerX = startX + normalW3 * 0.5f;
                string label  = roomDataList[i].RoomName;
                if (label.Length > ChartConstants.Scatter.LabelTruncationLength)
                    label = string.Concat(label.AsSpan(0, ChartConstants.Scatter.LabelTruncationLength), "...");
                float labelY  = isStaggered
                    ? (i % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                    : baseLabelY;
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;
                ActiveFont.DrawOutline(label,
                    new Vector2(centerX - labelSize.X / 2, labelY),
                    new Vector2(0f, 0f),
                    Vector2.One * ChartConstants.FontScale.AxisLabel,
                    _settings.RoomColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            float segStartX2  = GetColumnStartX(x, w, roomDataList.Count);
            float segCenterX2 = segStartX2 + normalW3 * 0.5f;

            // Continues the stagger pattern.
            float segmentLabelY = isStaggered
                ? (roomDataList.Count % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                : baseLabelY;
            Vector2 segLabelSize = ActiveFont.Measure("Segment") * ChartConstants.FontScale.AxisLabel;
            ActiveFont.DrawOutline("Segment",
                new Vector2(segCenterX2 - segLabelSize.X / 2, segmentLabelY),
                new Vector2(0f, 0f),
                Vector2.One * ChartConstants.FontScale.AxisLabel,
                _settings.SegmentColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);

            if (_toggle.Normalized)
                DrawPercentageAxisLabels(x, y, w, h, _minRoomPct, _maxRoomPct, _settings.RoomColorFinal);
            else
                DrawFrameAxisLabels(x, y, w, h, minRoomTime, maxRoomTime, YAxisSide.Left, _settings.RoomColorFinal);

            DrawFrameAxisLabels(x, y, w, h, minSegmentTime, maxSegmentTime, YAxisSide.Right, _settings.SegmentColorFinal);

            DrawTitle();
        }
    }
}
