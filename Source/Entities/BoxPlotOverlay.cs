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
        private readonly long _minRoom, _maxRoom;
        private readonly long _minSeg, _maxSeg;

        private record BoxGeometry(
            float CenterX, float BoxHalfW, float CapHalfW,
            float PxMin, float PxMax, float PxQ1, float PxQ3, float PxMed,
            long TickMin, long TickMax, long TickQ1, long TickQ3, long TickMed);

        private BoxGeometry? _hoveredBox = null;

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
        }

        // Required by BaseChartOverlay — Render() is fully overridden above
        protected override void DrawBars(float x, float y, float w, float h) { }

        private void ComputeRanges(out long minRoom, out long maxRoom, out long minSeg, out long maxSeg)
        {
            long rMin = long.MaxValue, rMax = 0;
            foreach (var room in _roomTimes)
            {
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

        private void DrawSeparator(float x, float y, float w, float h)
        {
            float columnWidth = w / (_roomTimes.Count + 1);
            float sepX = x + columnWidth * _roomTimes.Count;
            Draw.Line(new Vector2(sepX, y), new Vector2(sepX, y + h), Color.Gray * 0.85f, 1.5f);
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            float columnWidth   = w / (_roomTimes.Count + 1);
            float roomAreaWidth = columnWidth * _roomTimes.Count;

            long roomRange = _maxRoom - _minRoom;
            if (roomRange > 0)
            {
                GetFrameAxisSettings(roomRange, out long step, out int count);
                for (int i = 0; i <= count; i++)
                {
                    float yPos = y + h - (float)(i * step) / roomRange * h;
                    Draw.Line(new Vector2(x, yPos), new Vector2(x + roomAreaWidth, yPos), ChartConstants.Colors.GridLineColor, 1f);
                }
            }

            long segRange = _maxSeg - _minSeg;
            if (segRange > 0)
            {
                GetFrameAxisSettings(segRange, out long step, out int count);
                for (int i = 0; i <= count; i++)
                {
                    float yPos = y + h - (float)(i * step) / segRange * h;
                    Draw.Line(new Vector2(x + roomAreaWidth, yPos), new Vector2(x + w, yPos), ChartConstants.Colors.GridLineColor, 1f);
                }
            }
        }

        private void DrawBoxes(float x, float y, float w, float h)
        {
            int roomCount    = _roomTimes.Count;
            float columnWidth = w / (roomCount + 1);

            for (int r = 0; r < roomCount; r++)
            {
                var times = _roomTimes[r];
                if (times.Count == 0) continue;
                float centerX = x + columnWidth * (r + 0.5f);
                DrawBox(times, centerX, columnWidth, y, h, _minRoom, _maxRoom, SpeebrunConsistencyTrackerModule.Settings.RoomColorFinal);
            }

            if (_segmentTimes.Count > 0)
            {
                float segCenterX = x + columnWidth * (roomCount + 0.5f);
                DrawBox(_segmentTimes, segCenterX, columnWidth, y, h, _minSeg, _maxSeg, SpeebrunConsistencyTrackerModule.Settings.SegmentColorFinal);
            }
        }

        private static void DrawBox(List<TimeTicks> times, float centerX, float columnWidth,
            float y, float h, long minTick, long maxTick, Color color)
        {
            long tMin   = times[0].Ticks;
            long tMax   = times[^1].Ticks;
            TimeTicks q1  = MetricHelper.ComputePercentile(times, 25);
            TimeTicks med = MetricHelper.ComputePercentile(times, 50);
            TimeTicks q3  = MetricHelper.ComputePercentile(times, 75);

            float pxBest  = ToPixelY(tMin,       minTick, maxTick, y, h);
            float pxWorst = ToPixelY(tMax,       minTick, maxTick, y, h);
            float pxQ1    = ToPixelY(q1.Ticks,   minTick, maxTick, y, h);
            float pxMed   = ToPixelY(med.Ticks,  minTick, maxTick, y, h);
            float pxQ3    = ToPixelY(q3.Ticks,   minTick, maxTick, y, h);

            float boxLeft  = MathF.Round(centerX - columnWidth * 0.2f);
            float boxRight = MathF.Round(centerX + columnWidth * 0.2f);
            float capLeft  = MathF.Round(centerX - columnWidth * 0.08f);
            float capRight = MathF.Round(centerX + columnWidth * 0.08f);

            // Whisker: vertical line spanning full range
            Draw.Line(new Vector2(centerX, pxWorst), new Vector2(centerX, pxBest), color, 1.5f);
            // End caps
            Draw.Line(new Vector2(capLeft, pxBest),  new Vector2(capRight, pxBest),  color, 1.5f);
            Draw.Line(new Vector2(capLeft, pxWorst), new Vector2(capRight, pxWorst), color, 1.5f);

            // Box Q1–Q3 (pxQ3 < pxQ1 in screen coords — slower time = higher on screen)
            float boxTop    = Math.Min(pxQ1, pxQ3);
            float boxBottom = Math.Max(pxQ1, pxQ3);
            float boxHeight = Math.Max(1f, boxBottom - boxTop);
            Draw.Rect(boxLeft, boxTop, boxRight - boxLeft, boxHeight, color);

            // Median line (thick white)
            Draw.Line(new Vector2(boxLeft, pxMed), new Vector2(boxRight, pxMed), Color.White, 2.5f);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            int roomCount    = _roomTimes.Count;
            int totalColumns = roomCount + 1;
            float columnWidth = w / totalColumns;
            float baseLabelY  = y + h + ChartConstants.XAxisLabel.BaseOffsetY;

            // X-axis room labels (staggered)
            for (int i = 0; i < roomCount; i++)
            {
                float centerX = x + columnWidth * (i + 0.5f);
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
            float segX      = x + columnWidth * (totalColumns - 0.5f);
            float segLabelY = totalColumns >= ChartConstants.XAxisLabel.StaggerThreshold
                ? (roomCount % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                : baseLabelY;
            Vector2 segLabelSize = ActiveFont.Measure("Segment") * ChartConstants.FontScale.AxisLabel;
            ActiveFont.DrawOutline("Segment",
                new Vector2(segX - segLabelSize.X / 2, segLabelY),
                Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                SpeebrunConsistencyTrackerModule.Settings.SegmentColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);

            // Left Y axis (room times)
            long roomRange = _maxRoom - _minRoom;
            GetFrameAxisSettings(roomRange, out long roomStep, out int yRoomCount);
            for (int i = 0; i <= yRoomCount; i++)
            {
                long timeValue    = _minRoom + i * roomStep;
                float normalizedY = (float)(i * roomStep) / roomRange;
                float yPos        = y + h - (normalizedY * h);
                string timeLabel  = new TimeTicks(timeValue).ToString();
                Vector2 lSize     = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelMedium;
                ActiveFont.DrawOutline(timeLabel,
                    new Vector2(x - lSize.X - ChartConstants.Axis.YLabelMarginX, yPos - lSize.Y / 2),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                    SpeebrunConsistencyTrackerModule.Settings.RoomColorFinal, ChartConstants.Stroke.OutlineSize, Color.Black);
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

        private static float ToPixelY(long ticks, long minTick, long maxTick, float y, float h)
        {
            if (maxTick == minTick) return y + h / 2;
            return y + h - (float)(ticks - minTick) / (maxTick - minTick) * h;
        }

        private BoxGeometry? ComputeBoxGeometry(int columnIndex, float gx, float gy, float gw, float gh)
        {
            int   totalColumns = _roomTimes.Count + 1;
            float columnWidth  = gw / totalColumns;
            bool  isSegment    = columnIndex == _roomTimes.Count;

            List<TimeTicks> times;
            long minTick, maxTick;
            if (isSegment)
            {
                times   = _segmentTimes;
                minTick = _minSeg;
                maxTick = _maxSeg;
            }
            else
            {
                times   = _roomTimes[columnIndex];
                minTick = _minRoom;
                maxTick = _maxRoom;
            }

            if (times.Count == 0) return null;

            var sorted = times.OrderBy(t => t).ToList();
            long tMin  = sorted[0].Ticks;
            long tMax  = sorted[^1].Ticks;
            var  q1    = MetricHelper.ComputePercentile(sorted, 25);
            var  med   = MetricHelper.ComputePercentile(sorted, 50);
            var  q3    = MetricHelper.ComputePercentile(sorted, 75);

            float centerX  = gx + columnWidth * (columnIndex + 0.5f);
            float boxHalfW = columnWidth * 0.2f;
            float capHalfW = columnWidth * 0.08f;

            return new BoxGeometry(
                CenterX:  centerX,
                BoxHalfW: boxHalfW,
                CapHalfW: capHalfW,
                PxMin:    ToPixelY(tMin,       minTick, maxTick, gy, gh),
                PxMax:    ToPixelY(tMax,       minTick, maxTick, gy, gh),
                PxQ1:     ToPixelY(q1.Ticks,  minTick, maxTick, gy, gh),
                PxQ3:     ToPixelY(q3.Ticks,  minTick, maxTick, gy, gh),
                PxMed:    ToPixelY(med.Ticks, minTick, maxTick, gy, gh),
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

            _hoveredBox  = null;
            _statLabels  = [];

            if (mouseHudPos.X < gx || mouseHudPos.X > gx + gw ||
                mouseHudPos.Y < gy || mouseHudPos.Y > gy + gh)
                return null;

            int   totalColumns = _roomTimes.Count + 1;
            float columnWidth  = gw / totalColumns;
            int   idx          = Math.Clamp((int)((mouseHudPos.X - gx) / columnWidth), 0, totalColumns - 1);

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
