using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class BoxPlotOverlay : BaseChartOverlay
    {
        private readonly List<List<TimeTicks>> _roomTimes;
        private readonly List<TimeTicks> _segmentTimes;
        private long _minRoom, _maxRoom;
        private readonly long _minSeg, _maxSeg;
        private double _minRoomPct, _maxRoomPct;

        private record BoxGeometry(
            float CenterX, float BoxHalfW, float CapHalfW,
            float PxMin, float PxMax, float PxQ1, float PxQ3, float PxMed,
            long TickMin, long TickMax, long TickQ1, long TickQ3, long TickMed);

        private BoxGeometry? _hoveredBox = null;

        private bool _normalized = true;
        private bool _toggleButtonHovered = false;
        private Microsoft.Xna.Framework.Rectangle _toggleButtonRect = new(-9999, -9999, 0, 0);

        private record StatLabel(string Left, string Right, float X, float Y);
        private List<StatLabel> _statLabels = [];

        public BoxPlotOverlay(
            List<List<TimeTicks>> roomTimes,
            List<TimeTicks> segmentTimes,
            Vector2? pos = null)
            : base("Room and Segment Box Plot", pos)
        {
            _roomTimes    = [.. roomTimes.Select(r => (List<TimeTicks>)[.. r.OrderBy(t => t)])];
            _segmentTimes = [.. segmentTimes.OrderBy(t => t)];
            ComputeRanges(out _minRoom, out _maxRoom, out _minSeg, out _maxSeg);
            ComputeRelativeRanges(out _minRoomPct, out _maxRoomPct);
        }

        public override void Render()
        {
            Draw.Rect(position, width, height, backgroundColor);
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            DrawGrid(gx, gy, gw, gh);
            DrawYAxisLine(gx, gy, gw, gh);
            DrawXAxisLine(gx, gy, gw, gh);
            Draw.Line(new Vector2(gx + gw, gy), new Vector2(gx + gw, gy + gh), axisColor, ChartConstants.Stroke.OutlineSize);
            DrawSeparator(gx, gy, gw, gh);
            DrawBoxes(gx, gy, gw, gh);
            DrawLabels(gx, gy, gw, gh);
            DrawToggleButton();
        }

        // Required by BaseChartOverlay — Render() is fully overridden above
        protected override void DrawBars(float x, float y, float w, float h) { }

        public override void ClearHiddenColumns()
        {
            base.ClearHiddenColumns();
            ComputeRanges(out _minRoom, out _maxRoom, out _, out _);
            ComputeRelativeRanges(out _minRoomPct, out _maxRoomPct);
        }

        public override void ToggleColumn(int columnIndex)
        {
            base.ToggleColumn(columnIndex);
            ComputeRanges(out _minRoom, out _maxRoom, out _, out _);
            ComputeRelativeRanges(out _minRoomPct, out _maxRoomPct);
        }

        private void ComputeRanges(out long minRoom, out long maxRoom, out long minSeg, out long maxSeg)
        {
            long rMin = long.MaxValue, rMax = 0;
            for (int i = 0; i < _roomTimes.Count; i++)
            {
                if (_hiddenColumns.Contains(i)) continue;
                var room = _roomTimes[i];
                if (room.Count == 0) continue;
                rMin = Math.Min(rMin, room.Min(t => t.Ticks));
                rMax = Math.Max(rMax, room.Max(t => t.Ticks));
            }
            if (rMin == long.MaxValue) { rMin = 0; rMax = ChartConstants.Time.OneFrameTicks; }
            long rRange  = rMax - rMin;
            long rMargin = Math.Max(ChartConstants.Time.OneFrameTicks,
                ChartConstants.Time.OneFrameTicks * (long)Math.Round(rRange * 0.1 / ChartConstants.Time.OneFrameTicks, 0));
            minRoom = Math.Max(0, rMin - rMargin);
            maxRoom = rMax + rMargin;

            long sMin = long.MaxValue, sMax = 0;
            if (_segmentTimes.Count > 0)
            {
                sMin = _segmentTimes.Min(t => t.Ticks);
                sMax = _segmentTimes.Max(t => t.Ticks);
            }
            if (sMin == long.MaxValue) { sMin = 0; sMax = ChartConstants.Time.OneFrameTicks; }
            long sRange  = sMax - sMin;
            long sMargin = Math.Max(ChartConstants.Time.OneFrameTicks,
                ChartConstants.Time.OneFrameTicks * (long)Math.Round(sRange * 0.1 / ChartConstants.Time.OneFrameTicks, 0));
            minSeg = Math.Max(0, sMin - sMargin);
            maxSeg = sMax + sMargin;
        }

        private void ComputeRelativeRanges(out double minRoomPct, out double maxRoomPct)
        {
            double pMin = double.MaxValue, pMax = double.MinValue;
            for (int i = 0; i < _roomTimes.Count; i++)
            {
                if (_hiddenColumns.Contains(i)) continue;
                var room = _roomTimes[i];
                if (room.Count == 0) continue;
                long medTicks = MetricHelper.ComputePercentile(room, 50).Ticks;
                if (medTicks == 0) continue;
                double roomMinPct = (double)room[0].Ticks  / medTicks * 100.0;
                double roomMaxPct = (double)room[^1].Ticks / medTicks * 100.0;
                pMin = Math.Min(pMin, roomMinPct);
                pMax = Math.Max(pMax, roomMaxPct);
            }
            if (pMin == double.MaxValue) { pMin = 90.0; pMax = 110.0; }
            double pRange  = pMax - pMin;
            double pMargin = Math.Max(1.0, pRange * 0.1);
            minRoomPct = Math.Max(0, pMin - pMargin);
            maxRoomPct = pMax + pMargin;
        }

        private float ComputeNormalColumnWidth(float gw)
        {
            int visibleRooms = _roomTimes.Count - _hiddenColumns.Count;
            int visibleCols  = visibleRooms + 1; // +1 for segment (never hidden)
            if (visibleCols <= 0) return gw / Math.Max(_roomTimes.Count + 1, 1);
            float available = gw - _hiddenColumns.Count * ChartConstants.Interactivity.HiddenColumnStubWidth;
            return available / visibleCols;
        }

        // Returns center X for column i (0-based room index, or _roomTimes.Count for segment).
        // Segment column (i == _roomTimes.Count) is never hidden.
        private float GetColumnCenterX(float gx, float gw, int i)
        {
            float normalW = ComputeNormalColumnWidth(gw);
            float x = gx;
            for (int j = 0; j < i; j++)
                x += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            float thisW = (i < _roomTimes.Count && _hiddenColumns.Contains(i))
                ? ChartConstants.Interactivity.HiddenColumnStubWidth
                : normalW;
            return x + thisW * 0.5f;
        }

        private void DrawToggleButton()
        {
            const float scale  = ChartConstants.FontScale.AxisLabelSmall;
            const float pad    = ChartConstants.Interactivity.TooltipBgPadding;
            const float divW   = 2f; // divider between segments

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
                return true;
            }
            return false;
        }

        private void DrawSeparator(float x, float y, float w, float h)
        {
            float normalW = ComputeNormalColumnWidth(w);
            float sepX = x;
            for (int j = 0; j < _roomTimes.Count; j++)
                sepX += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            Draw.Line(new Vector2(sepX, y), new Vector2(sepX, y + h), Color.Gray * 0.85f, 1.5f);
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

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            float normalW = ComputeNormalColumnWidth(w);
            float roomAreaWidth = 0;
            for (int j = 0; j < _roomTimes.Count; j++)
                roomAreaWidth += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;

            if (_normalized)
            {
                double rangePct = _maxRoomPct - _minRoomPct;
                GetPercentageAxisSettings(rangePct, out double stepPct, out int count);
                for (int i = 0; i <= count; i++)
                {
                    double pctValue = _minRoomPct + i * stepPct;
                    if (pctValue > _maxRoomPct + 1e-9) break;
                    float yPos = ToPixelY(pctValue, _minRoomPct, _maxRoomPct, y, h);
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + roomAreaWidth, yPos),
                        ChartConstants.Colors.GridLineColor, 1f);
                }
                // 100% reference line (median anchor)
                float y100 = ToPixelY(100.0, _minRoomPct, _maxRoomPct, y, h);
                if (y100 >= y && y100 <= y + h)
                    Draw.Line(new Vector2(x, y100), new Vector2(x + roomAreaWidth, y100),
                        Color.Yellow * 0.5f, 1.5f);
            }
            else
            {
                long roomRange = _maxRoom - _minRoom;
                if (roomRange > 0)
                {
                    GetFrameAxisSettings(roomRange, out long step, out int count);
                    for (int i = 0; i <= count; i++)
                    {
                        float yPos = y + h - (float)(i * step) / roomRange * h;
                        Draw.Line(new Vector2(x, yPos), new Vector2(x + roomAreaWidth, yPos),
                            ChartConstants.Colors.GridLineColor, 1f);
                    }
                }
            }

            long segRange = _maxSeg - _minSeg;
            if (segRange > 0)
            {
                GetFrameAxisSettings(segRange, out long step, out int count);
                for (int i = 0; i <= count; i++)
                {
                    float yPos = y + h - (float)(i * step) / segRange * h;
                    Draw.Line(new Vector2(x + roomAreaWidth, yPos), new Vector2(x + w, yPos),
                        ChartConstants.Colors.GridLineColor, 1f);
                }
            }
        }

        private void DrawBoxes(float x, float y, float w, float h)
        {
            int   roomCount = _roomTimes.Count;
            float normalW   = ComputeNormalColumnWidth(w);

            for (int r = 0; r < roomCount; r++)
            {
                if (_hiddenColumns.Contains(r)) continue;
                var times = _roomTimes[r];
                if (times.Count == 0) continue;
                float centerX = GetColumnCenterX(x, w, r);
                double? normalizeBy = null;
                double minVal, maxVal;
                if (_normalized)
                {
                    long medTicks = MetricHelper.ComputePercentile(times, 50).Ticks;
                    if (medTicks == 0) continue;
                    normalizeBy = (double)medTicks;
                    minVal = _minRoomPct;
                    maxVal = _maxRoomPct;
                }
                else
                {
                    minVal = (double)_minRoom;
                    maxVal = (double)_maxRoom;
                }
                DrawBox(times, centerX, normalW, y, h, minVal, maxVal, normalizeBy,
                    SpeebrunConsistencyTrackerModule.Settings.RoomColorFinal);
            }

            if (_segmentTimes.Count > 0)
            {
                float segCenterX = GetColumnCenterX(x, w, roomCount);
                DrawBox(_segmentTimes, segCenterX, normalW, y, h,
                    (double)_minSeg, (double)_maxSeg, null,
                    SpeebrunConsistencyTrackerModule.Settings.SegmentColorFinal);
            }
        }

        private static void DrawBox(List<TimeTicks> times, float centerX, float columnWidth,
            float y, float h, double minVal, double maxVal, double? normalizeByTicks, Color color)
        {
            long tMin  = times[0].Ticks;
            long tMax  = times[^1].Ticks;
            TimeTicks q1  = MetricHelper.ComputePercentile(times, 25);
            TimeTicks med = MetricHelper.ComputePercentile(times, 50);
            TimeTicks q3  = MetricHelper.ComputePercentile(times, 75);

            double ToVal(long t) => normalizeByTicks.HasValue
                ? (double)t / normalizeByTicks.Value * 100.0
                : (double)t;

            float pxBest  = ToPixelY(ToVal(tMin),      minVal, maxVal, y, h);
            float pxWorst = ToPixelY(ToVal(tMax),      minVal, maxVal, y, h);
            float pxQ1    = ToPixelY(ToVal(q1.Ticks),  minVal, maxVal, y, h);
            float pxMed   = ToPixelY(ToVal(med.Ticks), minVal, maxVal, y, h);
            float pxQ3    = ToPixelY(ToVal(q3.Ticks),  minVal, maxVal, y, h);

            float boxLeft  = MathF.Round(centerX - columnWidth * 0.2f);
            float boxRight = MathF.Round(centerX + columnWidth * 0.2f);
            float capLeft  = MathF.Round(centerX - columnWidth * 0.08f);
            float capRight = MathF.Round(centerX + columnWidth * 0.08f);

            Draw.Line(new Vector2(centerX, pxWorst), new Vector2(centerX, pxBest), color, 1.5f);
            Draw.Line(new Vector2(capLeft,  pxBest),  new Vector2(capRight, pxBest),  color, 1.5f);
            Draw.Line(new Vector2(capLeft,  pxWorst), new Vector2(capRight, pxWorst), color, 1.5f);

            float boxTop    = Math.Min(pxQ1, pxQ3);
            float boxBottom = Math.Max(pxQ1, pxQ3);
            float boxHeight = Math.Max(1f, boxBottom - boxTop);
            Draw.Rect(boxLeft, boxTop, boxRight - boxLeft, boxHeight, color);

            Draw.Line(new Vector2(boxLeft, pxMed), new Vector2(boxRight, pxMed), Color.White, 2.5f);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            int roomCount    = _roomTimes.Count;
            int totalColumns = roomCount + 1;
            float baseLabelY  = y + h + ChartConstants.XAxisLabel.BaseOffsetY;

            // X-axis room labels (staggered), with strip highlights
            float normalW2 = ComputeNormalColumnWidth(w);
            for (int i = 0; i < roomCount; i++)
            {
                float colW    = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW2;
                float centerX = GetColumnCenterX(x, w, i);
                DrawColumnStrip(i, centerX - colW * 0.5f, colW, y + h);

                if (_hiddenColumns.Contains(i)) continue;
                string label  = $"R{i + 1}";
                float labelY  = totalColumns > ChartConstants.XAxisLabel.StaggerThreshold
                    ? (i % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                    : baseLabelY;
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;
                ActiveFont.DrawOutline(label,
                    new Vector2(centerX - labelSize.X / 2, labelY),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                    SpeebrunConsistencyTrackerModule.Settings.RoomColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            // Segment label
            float segX      = GetColumnCenterX(x, w, roomCount);
            float segLabelY = totalColumns >= ChartConstants.XAxisLabel.StaggerThreshold
                ? (roomCount % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                : baseLabelY;
            Vector2 segLabelSize = ActiveFont.Measure("Segment") * ChartConstants.FontScale.AxisLabel;
            ActiveFont.DrawOutline("Segment",
                new Vector2(segX - segLabelSize.X / 2, segLabelY),
                Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                SpeebrunConsistencyTrackerModule.Settings.SegmentColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);

            // Left Y axis (room times)
            if (_normalized)
            {
                double rangePct = _maxRoomPct - _minRoomPct;
                GetPercentageAxisSettings(rangePct, out double stepPct, out int yCount);
                for (int i = 0; i <= yCount; i++)
                {
                    double pctValue = _minRoomPct + i * stepPct;
                    if (pctValue > _maxRoomPct + 1e-9) break;
                    float  yPos      = ToPixelY(pctValue, _minRoomPct, _maxRoomPct, y, h);
                    string timeLabel = $"{pctValue:F0}%";
                    Vector2 lSize    = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelMedium;
                    ActiveFont.DrawOutline(timeLabel,
                        new Vector2(x - lSize.X - ChartConstants.Axis.YLabelMarginX, yPos - lSize.Y / 2),
                        Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                        SpeebrunConsistencyTrackerModule.Settings.RoomColorFinal,
                        ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }
            else
            {
                long roomRange = _maxRoom - _minRoom;
                GetFrameAxisSettings(roomRange, out long roomStep, out int yRoomCount);
                for (int i = 0; i <= yRoomCount; i++)
                {
                    long    timeValue   = _minRoom + i * roomStep;
                    float   normalizedY = (float)(i * roomStep) / roomRange;
                    float   yPos        = y + h - (normalizedY * h);
                    string  timeLabel   = new TimeTicks(timeValue).ToString();
                    Vector2 lSize       = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelMedium;
                    ActiveFont.DrawOutline(timeLabel,
                        new Vector2(x - lSize.X - ChartConstants.Axis.YLabelMarginX, yPos - lSize.Y / 2),
                        Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                        SpeebrunConsistencyTrackerModule.Settings.RoomColorFinal,
                        ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }

            // Right Y axis (segment times)
            long segRange = _maxSeg - _minSeg;
            GetFrameAxisSettings(segRange, out long segStep, out int ySegCount);
            for (int i = 0; i <= ySegCount; i++)
            {
                long timeValue    = _minSeg + i * segStep;
                float normalizedY = (float)(i * segStep) / segRange;
                float yPos        = y + h - (normalizedY * h);
                string timeLabel  = new TimeTicks(timeValue).ToString();
                Vector2 lSize     = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelMedium;
                ActiveFont.DrawOutline(timeLabel,
                    new Vector2(x + w + ChartConstants.Trajectory.RightLabelMarginX, yPos - lSize.Y / 2),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                    SpeebrunConsistencyTrackerModule.Settings.SegmentColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            DrawTitle();
        }

        private static float ToPixelY(double value, double minVal, double maxVal, float y, float h)
        {
            if (maxVal == minVal) return y + h / 2;
            return y + h - (float)((value - minVal) / (maxVal - minVal)) * h;
        }

        private BoxGeometry? ComputeBoxGeometry(int columnIndex, float gx, float gy, float gw, float gh)
        {
            float columnWidth = ComputeNormalColumnWidth(gw);
            bool  isSegment   = columnIndex == _roomTimes.Count;

            List<TimeTicks> times;
            double minVal, maxVal;
            double? normalizeByTicks = null;

            if (isSegment)
            {
                times  = _segmentTimes;
                minVal = (double)_minSeg;
                maxVal = (double)_maxSeg;
            }
            else
            {
                times = _roomTimes[columnIndex];
                if (_normalized)
                {
                    long medTicks = MetricHelper.ComputePercentile(times, 50).Ticks;
                    if (medTicks == 0) return null;
                    normalizeByTicks = (double)medTicks;
                    minVal = _minRoomPct;
                    maxVal = _maxRoomPct;
                }
                else
                {
                    minVal = (double)_minRoom;
                    maxVal = (double)_maxRoom;
                }
            }

            if (times.Count == 0) return null;

            long tMin = times[0].Ticks;
            long tMax = times[^1].Ticks;
            var  q1   = MetricHelper.ComputePercentile(times, 25);
            var  med  = MetricHelper.ComputePercentile(times, 50);
            var  q3   = MetricHelper.ComputePercentile(times, 75);

            double ToVal(long t) => normalizeByTicks.HasValue
                ? (double)t / normalizeByTicks.Value * 100.0
                : (double)t;

            float centerX  = GetColumnCenterX(gx, gw, columnIndex);
            float boxHalfW = columnWidth * 0.2f;
            float capHalfW = columnWidth * 0.08f;

            return new BoxGeometry(
                CenterX:  centerX,
                BoxHalfW: boxHalfW,
                CapHalfW: capHalfW,
                PxMin:    ToPixelY(ToVal(tMin),      minVal, maxVal, gy, gh),
                PxMax:    ToPixelY(ToVal(tMax),      minVal, maxVal, gy, gh),
                PxQ1:     ToPixelY(ToVal(q1.Ticks),  minVal, maxVal, gy, gh),
                PxQ3:     ToPixelY(ToVal(q3.Ticks),  minVal, maxVal, gy, gh),
                PxMed:    ToPixelY(ToVal(med.Ticks), minVal, maxVal, gy, gh),
                TickMin:  tMin,
                TickMax:  tMax,
                TickQ1:   q1.Ticks,
                TickQ3:   q3.Ticks,
                TickMed:  med.Ticks);
        }

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            _hoveredBox          = null;
            _statLabels          = [];
            _toggleButtonHovered = _toggleButtonRect.Contains((int)mouseHudPos.X, (int)mouseHudPos.Y);

            if (_toggleButtonHovered)
                return new HoverInfo("", mouseHudPos);

            if (mouseHudPos.X < gx || mouseHudPos.X > gx + gw ||
                mouseHudPos.Y < gy || mouseHudPos.Y > gy + gh)
                return null;

            int   totalColumns = _roomTimes.Count + 1;
            float normalW      = ComputeNormalColumnWidth(gw);
            int   idx          = totalColumns - 1; // default to segment column
            float colX         = gx;
            for (int i = 0; i < totalColumns; i++)
            {
                float colW = (i < _roomTimes.Count && _hiddenColumns.Contains(i))
                    ? ChartConstants.Interactivity.HiddenColumnStubWidth
                    : normalW;
                if (mouseHudPos.X < colX + colW) { idx = i; break; }
                colX += colW;
            }

            var times = idx == _roomTimes.Count ? _segmentTimes : _roomTimes[idx];
            if (times.Count == 0) return null;

            _hoveredBox = ComputeBoxGeometry(idx, gx, gy, gw, gh);
            if (_hoveredBox == null) return null;

            var b = _hoveredBox;

            // Hit test: must be within whisker X range and Y range
            float hitXMin = b.CenterX - b.BoxHalfW;
            float hitXMax = b.CenterX + b.BoxHalfW;
            float hitYMin = Math.Min(b.PxMin, b.PxMax); // PxMax (slowest) = low Y = screen top
            float hitYMax = Math.Max(b.PxMin, b.PxMax);
            if (mouseHudPos.X < hitXMin || mouseHudPos.X > hitXMax ||
                mouseHudPos.Y < hitYMin || mouseHudPos.Y > hitYMax)
            {
                _hoveredBox = null;
                return null;
            }

            // Build per-stat labels: place each to the right of the box, at the stat's Y position
            const float scale   = ChartConstants.FontScale.AxisLabelMedium;
            const float bgPad   = ChartConstants.Interactivity.TooltipBgPadding;
            float lineH  = ActiveFont.Measure("A").Y * scale;
            float labelX = b.CenterX + b.BoxHalfW + bgPad * 2f;

            // Screen Y: PxMax (slowest) = low Y = top of screen; PxMin (fastest) = high Y = bottom of screen
            // Stat order top→bottom on screen: Max, Q3, Median, Q1, Min
            var raw = new (string name, string val, float py)[]
            {
                ("Max",    new TimeTicks(b.TickMax).ToString(), b.PxMax),
                ("Q3",     new TimeTicks(b.TickQ3).ToString(),  Math.Min(b.PxQ1, b.PxQ3)),
                ("Median", new TimeTicks(b.TickMed).ToString(), b.PxMed),
                ("Q1",     new TimeTicks(b.TickQ1).ToString(),  Math.Max(b.PxQ1, b.PxQ3)),
                ("Min",    new TimeTicks(b.TickMin).ToString(), b.PxMin),
            };

            // Nudge labels apart so they don't overlap (min gap = lineH + 2*bgPad)
            float minGap  = lineH + bgPad * 2f;
            float[] nudged = new float[raw.Length];
            for (int i = 0; i < raw.Length; i++) nudged[i] = raw[i].py;
            // Pass 1: push down
            for (int i = 1; i < nudged.Length; i++)
                if (nudged[i] - nudged[i - 1] < minGap)
                    nudged[i] = nudged[i - 1] + minGap;
            // Pass 2: push up from bottom
            for (int i = nudged.Length - 2; i >= 0; i--)
                if (nudged[i + 1] - nudged[i] < minGap)
                    nudged[i] = nudged[i + 1] - minGap;

            _statLabels = [];
            for (int i = 0; i < raw.Length; i++)
                _statLabels.Add(new StatLabel(raw[i].name, raw[i].val, labelX, nudged[i] - lineH / 2f));

            // Return a sentinel HoverInfo (empty label = DrawTooltip skipped; just triggers DrawHighlight)
            return new HoverInfo("", new Vector2(labelX, nudged[0]));
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
            for (int i = 0; i < _roomTimes.Count; i++) // segment not hideable
            {
                float colW = _hiddenColumns.Contains(i) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
                var (stripX, stripW) = ColumnStripRect(colX, colW);
                if (mousePos.X >= stripX && mousePos.X < stripX + stripW) { _hoveredColumnIndex = i; return i; }
                colX += colW;
            }
            _hoveredColumnIndex = -1;
            return null;
        }

        public override void DrawHighlight()
        {
            if (_hoveredBox == null) return;

            var   b      = _hoveredBox;
            Color c      = Color.White * 0.85f;
            float boxTop = Math.Min(b.PxQ1, b.PxQ3);
            float boxBot = Math.Max(b.PxQ1, b.PxQ3);
            float boxH   = Math.Max(1f, boxBot - boxTop);

            float hBoxLeft  = MathF.Round(b.CenterX - b.BoxHalfW);
            float hBoxRight = MathF.Round(b.CenterX + b.BoxHalfW);
            float hCapLeft  = MathF.Round(b.CenterX - b.CapHalfW);
            float hCapRight = MathF.Round(b.CenterX + b.CapHalfW);

            Draw.HollowRect(hBoxLeft, boxTop, hBoxRight - hBoxLeft, boxH, c);
            Draw.Line(new Vector2(b.CenterX, b.PxMax), new Vector2(b.CenterX, b.PxMin), c, 1.5f);
            Draw.Line(new Vector2(hCapLeft, b.PxMin), new Vector2(hCapRight, b.PxMin), c, 1.5f);
            Draw.Line(new Vector2(hCapLeft, b.PxMax), new Vector2(hCapRight, b.PxMax), c, 1.5f);
            Draw.Line(new Vector2(hBoxLeft, b.PxMed), new Vector2(hBoxRight, b.PxMed), Color.White, 2.5f);

            const float scale  = ChartConstants.FontScale.AxisLabelMedium;
            const float bgPad  = ChartConstants.Interactivity.TooltipBgPadding;
            const float colGap = 8f;
            float lineH = ActiveFont.Measure("A").Y * scale;

            float maxLeftW = 0f, maxRightW = 0f;
            foreach (var sl in _statLabels)
            {
                maxLeftW  = Math.Max(maxLeftW,  ActiveFont.Measure(sl.Left).X  * scale);
                maxRightW = Math.Max(maxRightW, ActiveFont.Measure(sl.Right).X * scale);
            }
            float totalW = maxLeftW + colGap + maxRightW;

            foreach (var sl in _statLabels)
            {
                float bgX = sl.X - bgPad;
                float bgY = sl.Y - bgPad;

                Draw.Rect(bgX, bgY, totalW + bgPad * 2f, lineH + bgPad * 2f, Color.Black * 0.92f);
                ActiveFont.DrawOutline(sl.Left,  new Vector2(sl.X, sl.Y), Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                float rw = ActiveFont.Measure(sl.Right).X * scale;
                ActiveFont.DrawOutline(sl.Right, new Vector2(sl.X + maxLeftW + colGap + maxRightW - rw, sl.Y), Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }
    }
}
