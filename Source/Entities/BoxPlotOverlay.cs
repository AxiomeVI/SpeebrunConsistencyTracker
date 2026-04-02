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

            DrawAxes(gx, gy, gw, gh);
            DrawSeparator(gx, gy, gw, gh);
            DrawGrid(gx, gy, gw, gh);
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

        protected override void DrawAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x,     y + h), new Vector2(x + w, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x,     y),     new Vector2(x,     y + h), axisColor, ChartConstants.Stroke.OutlineSize);
            Draw.Line(new Vector2(x + w, y),     new Vector2(x + w, y + h), axisColor, ChartConstants.Stroke.OutlineSize);
        }

        private void DrawSeparator(float x, float y, float w, float h)
        {
            float columnWidth = w / (_roomTimes.Count + 1);
            float sepX = x + columnWidth * _roomTimes.Count;
            Draw.Line(new Vector2(sepX, y), new Vector2(sepX, y + h), Color.Gray * 0.85f, 1.5f);
        }

        private void DrawGrid(float x, float y, float w, float h)
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

        private void DrawBox(List<TimeTicks> times, float centerX, float columnWidth,
            float y, float h, long minTick, long maxTick, Color color)
        {
            var sorted  = times.OrderBy(t => t).ToList();
            long tMin   = sorted[0].Ticks;
            long tMax   = sorted[^1].Ticks;
            TimeTicks q1  = MetricHelper.ComputePercentile(sorted, 25);
            TimeTicks med = MetricHelper.ComputePercentile(sorted, 50);
            TimeTicks q3  = MetricHelper.ComputePercentile(sorted, 75);

            float pxBest  = ToPixelY(tMin,       minTick, maxTick, y, h);
            float pxWorst = ToPixelY(tMax,       minTick, maxTick, y, h);
            float pxQ1    = ToPixelY(q1.Ticks,   minTick, maxTick, y, h);
            float pxMed   = ToPixelY(med.Ticks,  minTick, maxTick, y, h);
            float pxQ3    = ToPixelY(q3.Ticks,   minTick, maxTick, y, h);

            float boxHalfW = columnWidth * 0.2f;
            float capHalfW = columnWidth * 0.08f;
            Color fillColor = color;

            // Whisker: vertical line spanning full range
            Draw.Line(new Vector2(centerX, pxWorst), new Vector2(centerX, pxBest), color, 1.5f);
            // End caps
            Draw.Line(new Vector2(centerX - capHalfW, pxBest),  new Vector2(centerX + capHalfW, pxBest),  color, 1.5f);
            Draw.Line(new Vector2(centerX - capHalfW, pxWorst), new Vector2(centerX + capHalfW, pxWorst), color, 1.5f);

            // Box Q1–Q3 (pxQ3 < pxQ1 in screen coords — slower time = higher on screen)
            float boxTop    = Math.Min(pxQ1, pxQ3);
            float boxBottom = Math.Max(pxQ1, pxQ3);
            float boxHeight = Math.Max(1f, boxBottom - boxTop);
            Draw.Rect(centerX - boxHalfW, boxTop, boxHalfW * 2, boxHeight, fillColor);

            // Median line (thick white)
            Draw.Line(new Vector2(centerX - boxHalfW, pxMed), new Vector2(centerX + boxHalfW, pxMed), Color.White, 2.5f);
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
    }
}
