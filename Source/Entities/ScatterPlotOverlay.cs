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

        // Cache computed values
        private List<(Vector2 pos, bool isSegment, float radius, int globalAttemptIndex)> cachedDots = null;
        private int _hoveredDotIndex = -1;
        private long maxRoomTime;
        private long maxSegmentTime;
        private long minRoomTime;
        private long minSegmentTime;

        private bool _normalized = false;
        private bool _toggleButtonHovered = false;
        private Microsoft.Xna.Framework.Rectangle _toggleButtonRect = new(-9999, -9999, 0, 0);
        private double _minRoomPct, _maxRoomPct;

        // Scatter-specific colors
        private Color gridColor = ChartConstants.Colors.GridLineColor;

        public ScatterPlotOverlay(List<List<TimeTicks>> rooms, List<List<int>> roomIndices, List<TimeTicks> segment, List<int> segmentIndices, Vector2? pos = null, TimeTicks? target = null)
            : base("Room and Segment Times", pos)
        {
            // Filter out rooms with no times, keeping index lists in sync
            var filtered = rooms
                .Select((room, i) => (room, indices: roomIndices[i]))
                .Where(x => x.room.Count > 0)
                .ToList();
            roomDataList         = filtered.Select((x, i) => new RoomData("R" + (i + 1).ToString(), x.room)).ToList();
            roomAttemptIndices   = filtered.Select(x => x.indices).ToList();
            segmentData          = new RoomData("Segment", segment);
            segmentAttemptIndices = segmentIndices;
            targetTime           = target;
            ComputeMaxValues();
            ComputeRelativeRanges(out _minRoomPct, out _maxRoomPct);
        }

        public override bool SupportsDeleteRuns => true;

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
            DrawToggleButton();
        }

        // Required by BaseChartOverlay but unused — Render() is fully overridden above
        protected override void DrawBars(float x, float y, float w, float h) { }

        public override void ClearHiddenColumns()
        {
            base.ClearHiddenColumns();
            cachedDots = null;
            ComputeMaxValues();
            ComputeRelativeRanges(out _minRoomPct, out _maxRoomPct);
        }

        public override void ToggleColumn(int columnIndex)
        {
            base.ToggleColumn(columnIndex);
            cachedDots = null;
            ComputeMaxValues();
            ComputeRelativeRanges(out _minRoomPct, out _maxRoomPct);
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

        private void ComputeRelativeRanges(out double minRoomPct, out double maxRoomPct)
        {
            double pMin = double.MaxValue, pMax = double.MinValue;
            for (int i = 0; i < roomDataList.Count; i++)
            {
                if (_hiddenColumns.Contains(i)) continue;
                var times = roomDataList[i].Times;
                if (times.Count == 0) continue;
                var sorted = times.OrderBy(t => t).ToList();
                long medTicks = MetricHelper.ComputePercentile(sorted, 50).Ticks;
                if (medTicks == 0) continue;
                double roomMinPct = (double)times.Min(t => t.Ticks) / medTicks * 100.0;
                double roomMaxPct = (double)times.Max(t => t.Ticks) / medTicks * 100.0;
                pMin = Math.Min(pMin, roomMinPct);
                pMax = Math.Max(pMax, roomMaxPct);
            }
            if (pMin == double.MaxValue) { pMin = 90.0; pMax = 110.0; }
            double pRange  = pMax - pMin;
            double pMargin = Math.Max(1.0, pRange * 0.1);
            minRoomPct = Math.Max(0, pMin - pMargin);
            maxRoomPct = pMax + pMargin;
        }

        private static void GetPercentageAxisSettings(double rangePct, out double stepPct, out int count)
        {
            if (rangePct <= 0) { stepPct = 5.0; count = 1; return; }
            double[] candidates = [1, 2, 5, 10, 20, 25, 50];
            stepPct = candidates[^1];
            foreach (double c in candidates)
            {
                if (rangePct / c <= ChartConstants.Axis.MaxTickMarks) { stepPct = c; break; }
            }
            count = Math.Min((int)Math.Ceiling(rangePct / stepPct), ChartConstants.Axis.MaxTickMarks);
        }

        private static float ToPixelY(double value, double minVal, double maxVal, float y, float h)
        {
            if (maxVal == minVal) return y + h / 2;
            return y + h - (float)((value - minVal) / (maxVal - minVal)) * h;
        }

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

        private void DrawToggleButton()
        {
            const float scale  = ChartConstants.FontScale.AxisLabelSmall;
            const float pad    = ChartConstants.Interactivity.TooltipBgPadding;
            const float divW   = 2f;

            Vector2 sizeAbs = ActiveFont.Measure("Absolute") * scale;
            Vector2 sizeRel = ActiveFont.Measure("Relative")  * scale;
            float colW   = Math.Max(sizeAbs.X, sizeRel.X) + pad * 2f;
            float btnH   = Math.Max(sizeAbs.Y, sizeRel.Y) + pad * 2f;
            float totalW = colW * 2 + divW;

            float bgX = MathF.Round(position.X + width / 2f - totalW / 2f);
            float bgY = MathF.Round(position.Y + height - btnH - 8f);

            _toggleButtonRect = new Microsoft.Xna.Framework.Rectangle((int)bgX, (int)bgY, (int)totalW, (int)btnH);

            // Outer border
            Draw.Rect(bgX - 1f, bgY - 1f, totalW + 2f, btnH + 2f, Color.White * 0.6f);

            // "Absolute" segment (left)
            bool absHovered = _toggleButtonHovered && !_normalized;
            Draw.Rect(bgX, bgY, colW, btnH,
                !_normalized ? Color.White * 0.35f
                : absHovered  ? Color.White * 0.15f
                              : Color.Black * 0.92f);
            ActiveFont.DrawOutline("Absolute",
                new Vector2(bgX + colW / 2f - sizeAbs.X / 2f, bgY + pad),
                Vector2.Zero, Vector2.One * scale,
                !_normalized ? Color.White : Color.Gray * 0.8f,
                ChartConstants.Stroke.OutlineSize, Color.Black);

            // Divider
            Draw.Rect(bgX + colW, bgY, divW, btnH, Color.White * 0.6f);

            // "Relative" segment (right)
            bool relHovered = _toggleButtonHovered && _normalized;
            Draw.Rect(bgX + colW + divW, bgY, colW, btnH,
                _normalized  ? Color.White * 0.35f
                : relHovered ? Color.White * 0.15f
                             : Color.Black * 0.92f);
            ActiveFont.DrawOutline("Relative",
                new Vector2(bgX + colW + divW + colW / 2f - sizeRel.X / 2f, bgY + pad),
                Vector2.Zero, Vector2.One * scale,
                _normalized ? Color.White : Color.Gray * 0.8f,
                ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        public override bool HandleClick(HoverInfo hover)
        {
            if (_toggleButtonHovered)
            {
                _normalized = !_normalized;
                cachedDots  = null;
                return true;
            }
            return false;
        }

        private void DrawScatterAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x - 1, y + h), new Vector2(x + w + 1, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x, y),     new Vector2(x, y + h),     axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x + w, y), new Vector2(x + w, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            // Vertical grid lines
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

            // Horizontal lines for rooms (left axis)
            if (_normalized)
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
                // 100% reference line (median anchor)
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
                Random random     = new(42);
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
                    if (_normalized)
                    {
                        var sorted = room.Times.OrderBy(t => t).ToList();
                        medTicks = MetricHelper.ComputePercentile(sorted, 50).Ticks;
                        if (medTicks == 0) continue;
                    }

                    for (int t = 0; t < room.Times.Count; t++)
                    {
                        float dotY;
                        if (_normalized)
                        {
                            double pct = (double)room.Times[t].Ticks / medTicks * 100.0;
                            dotY = ToPixelY(pct, _minRoomPct, _maxRoomPct, y, h);
                        }
                        else
                        {
                            float normalizedY = roomRange > 0 ? (float)(room.Times[t].Ticks - minRoomTime) / roomRange : 0.5f;
                            dotY = y + h - (normalizedY * h);
                        }
                        float jitterX   = centerX + (float)(random.NextDouble() - 0.5) * (normalW * ChartConstants.Scatter.JitterRatio);
                        int   globalIdx = roomAttemptIndices[roomIndex][t];
                        cachedDots.Add((new Vector2(jitterX, dotY), false, baseRadius, globalIdx));
                    }
                }

                // Segment column — always visible
                float segStartX  = GetColumnStartX(x, w, roomDataList.Count);
                float segCenterX = segStartX + normalW * 0.5f;
                for (int t = 0; t < segmentData.Times.Count; t++)
                {
                    float normalizedY = segmentRange > 0 ? (float)(segmentData.Times[t].Ticks - minSegmentTime) / segmentRange : 0.5f;
                    float dotY        = y + h - (normalizedY * h);
                    float jitterX     = segCenterX + (float)(random.NextDouble() - 0.5) * (normalW * ChartConstants.Scatter.JitterRatio);
                    int   globalIdx   = segmentAttemptIndices[t];
                    cachedDots.Add((new Vector2(jitterX, dotY), true, baseRadius, globalIdx));
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

        public override int? ColumnHitTest(Vector2 mousePos)
        {
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            float hitZoneTop    = gy + gh + ChartConstants.XAxisLabel.BaseOffsetY;
            float hitZoneBottom = hitZoneTop + ChartConstants.Interactivity.ColumnLabelHitZoneH;

            if (mousePos.Y < hitZoneTop || mousePos.Y > hitZoneBottom)
            {
                _hoveredColumnIndex = -1;
                return null;
            }

            float normalW = ComputeNormalColumnWidth(gw);
            float colX = gx;
            for (int i = 0; i < roomDataList.Count; i++) // segment not hideable
            {
                float colW = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
                var (stripX, stripW) = ColumnStripRect(colX, colW);
                if (mousePos.X >= stripX && mousePos.X < stripX + stripW) { _hoveredColumnIndex = i; return i; }
                colX += colW;
            }
            _hoveredColumnIndex = -1;
            return null;
        }

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            _toggleButtonHovered = _toggleButtonRect.Contains((int)mouseHudPos.X, (int)mouseHudPos.Y);
            if (_toggleButtonHovered)
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
            var (pos, _, _, globalAttemptIndex) = cachedDots[bestIdx];

            // Find time string by scanning dot list for this dot's position (isSegment drives which list)
            var (_, isSegment, _, _) = cachedDots[bestIdx];
            string timeStr;
            if (isSegment)
            {
                int localIdx = segmentAttemptIndices.IndexOf(globalAttemptIndex);
                timeStr = localIdx >= 0 ? segmentData.Times[localIdx].ToString() : "?";
            }
            else
            {
                timeStr = "?";
                int dotCount = 0;
                for (int r = 0; r < roomDataList.Count; r++)
                {
                    if (_hiddenColumns.Contains(r)) continue;
                    if (bestIdx < dotCount + roomDataList[r].Times.Count)
                    {
                        int localIdx = bestIdx - dotCount;
                        timeStr = roomDataList[r].Times[localIdx].ToString();
                        break;
                    }
                    dotCount += roomDataList[r].Times.Count;
                }
            }

            string label     = $"Run #{globalAttemptIndex + 1}: {timeStr}";
            float lineHeight = ActiveFont.Measure("A").Y * ChartConstants.FontScale.AxisLabelMedium;
            float labelY     = pos.Y - ChartConstants.Scatter.DotRadius - ChartConstants.Interactivity.TooltipPaddingY - lineHeight - ChartConstants.Interactivity.TooltipBgPadding * 2f;
            return new HoverInfo(label, new Vector2(pos.X, labelY), Key: $"{globalAttemptIndex}:{bestIdx}");
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
            float normalW3   = ComputeNormalColumnWidth(w);
            float baseLabelY = y + h + ChartConstants.XAxisLabel.BaseOffsetY;

            // X axis labels (room names) — staggered, with strip highlights
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
                int totalVisible = roomDataList.Count - _hiddenColumns.Count + 1;
                float labelY  = totalVisible > ChartConstants.XAxisLabel.StaggerThreshold
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
            float roomAreaWidth = 0;
            for (int j = 0; j < roomDataList.Count; j++)
                roomAreaWidth += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW3;

            float segStartX2  = GetColumnStartX(x, w, roomDataList.Count);
            float segCenterX2 = segStartX2 + normalW3 * 0.5f;
            int totalVisible2 = roomDataList.Count - _hiddenColumns.Count + 1;
            float segmentLabelY = totalVisible2 >= ChartConstants.XAxisLabel.StaggerThreshold
                ? (roomDataList.Count % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                : baseLabelY;
            Vector2 segLabelSize = ActiveFont.Measure("Segment") * ChartConstants.FontScale.AxisLabel;
            ActiveFont.DrawOutline("Segment",
                new Vector2(segCenterX2 - segLabelSize.X / 2, segmentLabelY),
                new Vector2(0f, 0f),
                Vector2.One * ChartConstants.FontScale.AxisLabel,
                _settings.SegmentColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);

            // Left Y axis labels (room times or percentages)
            if (_normalized)
            {
                double rangePct = _maxRoomPct - _minRoomPct;
                GetPercentageAxisSettings(rangePct, out double stepPct, out int yCount);
                for (int i = 0; i <= yCount; i++)
                {
                    double pctValue = _minRoomPct + i * stepPct;
                    if (pctValue > _maxRoomPct + 1e-9) break;
                    float yPos       = ToPixelY(pctValue, _minRoomPct, _maxRoomPct, y, h);
                    string timeLabel = $"{pctValue:F0}%";
                    Vector2 labelSize = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelMedium;
                    ActiveFont.DrawOutline(timeLabel,
                        new Vector2(x - labelSize.X - ChartConstants.Axis.YLabelMarginX, yPos - labelSize.Y / 2),
                        new Vector2(0f, 0f),
                        Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                        _settings.RoomColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }
            else
            {
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
