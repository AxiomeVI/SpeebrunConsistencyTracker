using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public class BoxPlotOverlay : BaseChartOverlay
    {
        private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

        private readonly List<List<TimeTicks>> _roomTimes;
        private readonly List<TimeTicks> _segmentTimes;
        private long _minRoom, _maxRoom;
        private readonly long _minSeg, _maxSeg;
        private double _minRoomPct, _maxRoomPct;

        private readonly record struct BoxGeometry(
            float CenterX, float BoxHalfW, float CapHalfW,
            float PxMin, float PxMax, float PxQ1, float PxQ3, float PxMed,
            long TickMin, long TickMax, long TickQ1, long TickQ3, long TickMed);

        private BoxGeometry? _hoveredBox = null;

        private readonly NormalizationToggle _toggle = new(normalized: true);

        // A struct, and the list is reused rather than replaced: the hit-test path rewrites it
        // every frame while the box plot is on screen, and DrawHighlight reads it back.
        private readonly record struct StatLabel(string Left, string Right, float X, float Y);
        private readonly List<StatLabel> _statLabels = [];

        public BoxPlotOverlay(
            List<List<TimeTicks>> roomTimes,
            List<TimeTicks> segmentTimes,
            Vector2? pos = null)
            : base(Dialog.Clean(DialogIds.ChartBoxPlotTitle), pos)
        {
            _roomTimes    = [.. roomTimes.Select(r => (List<TimeTicks>)[.. r.OrderBy(t => t)])];
            _segmentTimes = [.. segmentTimes.OrderBy(t => t)];
            ComputeRanges(out _minRoom, out _maxRoom, out _minSeg, out _maxSeg);
            RecomputeRelativeRanges();
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
            _toggle.Render(position, width, height);
        }

        // Unused: Render() is fully overridden above.
        protected override void DrawBars(float x, float y, float w, float h) { }

        public override void ClearHiddenColumns()
        {
            base.ClearHiddenColumns();
            InvalidateColumnOffsets();
            ComputeRanges(out _minRoom, out _maxRoom, out _, out _);
            RecomputeRelativeRanges();
        }

        public override void ToggleColumn(int columnIndex)
        {
            base.ToggleColumn(columnIndex);
            InvalidateColumnOffsets();
            ComputeRanges(out _minRoom, out _maxRoom, out _, out _);
            RecomputeRelativeRanges();
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

        private void RecomputeRelativeRanges() =>
            ComputeRelativeRanges(_roomTimes.Count, i => _roomTimes[i], out _minRoomPct, out _maxRoomPct);

        private float ComputeNormalColumnWidth(float gw)
        {
            int visibleRooms = _roomTimes.Count - _hiddenColumns.Count;
            int visibleCols  = visibleRooms + 1; // +1 for segment (never hidden)
            if (visibleCols <= 0) return gw / Math.Max(_roomTimes.Count + 1, 1);
            float available = gw - _hiddenColumns.Count * ChartConstants.Interactivity.HiddenColumnStubWidth;
            return available / visibleCols;
        }

        // Left edge of each column as an offset from the plot's own left edge, plus one past the
        // end. Offsets and not absolute X, so a chart that moves does not invalidate them.
        // Everything that used to walk every previous column reads this: GetColumnCenterX per
        // column per frame was O(n^2) on a chart drawn every frame.
        private float[] _columnOffsets;
        private float _columnOffsetsFor = float.NaN;

        private float[] ColumnOffsets(float gw)
        {
            if (_columnOffsets != null && _columnOffsetsFor == gw) return _columnOffsets;

            int cols = _roomTimes.Count + 1; // + the segment column, which is never hidden
            float normalW = ComputeNormalColumnWidth(gw);
            float[] offsets = new float[cols + 1];
            for (int j = 0; j < cols; j++)
                offsets[j + 1] = offsets[j] + ColumnWidth(j, normalW);

            _columnOffsets = offsets;
            _columnOffsetsFor = gw;
            return offsets;
        }

        private float ColumnWidth(int i, float normalW)
            => i < _roomTimes.Count && _hiddenColumns.Contains(i)
                ? ChartConstants.Interactivity.HiddenColumnStubWidth
                : normalW;

        private void InvalidateColumnOffsets() => _columnOffsetsFor = float.NaN;

        // i == _roomTimes.Count is the segment column, which is never hidden.
        private float GetColumnCenterX(float gx, float gw, int i)
        {
            float[] offsets = ColumnOffsets(gw);
            return gx + (offsets[i] + offsets[i + 1]) * 0.5f;
        }

        // Width of the room half of the plot: where the separator goes, and where the room grid ends.
        private float RoomAreaWidth(float gw) => ColumnOffsets(gw)[_roomTimes.Count];

        public override bool HandleClick(HoverInfo hover) => _toggle.HandleClick();

        private void DrawSeparator(float x, float y, float w, float h)
        {
            float sepX = x + RoomAreaWidth(w);
            Draw.Line(new Vector2(sepX, y), new Vector2(sepX, y + h), Color.Gray * 0.85f, 1.5f);
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            float roomAreaWidth = RoomAreaWidth(w);

            if (_toggle.Normalized)
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
                // 100% is the median anchor.
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

        // ComputeBoxGeometry is the single source of the quartiles: the render pass and the
        // hit test must not derive them separately.
        private void DrawBoxes(float x, float y, float w, float h)
        {
            int roomCount = _roomTimes.Count;

            for (int r = 0; r < roomCount; r++)
            {
                if (_hiddenColumns.Contains(r)) continue;
                var box = ComputeBoxGeometry(r, x, y, w, h);
                if (box == null) continue;
                DrawBox(box.Value, ChartPalette.Current.Room, ChartPalette.Current.RoomFill);
            }

            if (_segmentTimes.Count > 0)
            {
                var segBox = ComputeBoxGeometry(roomCount, x, y, w, h);
                if (segBox != null)
                    DrawBox(segBox.Value, ChartPalette.Current.Segment, ChartPalette.Current.SegmentFill);
            }
        }

        private static void DrawBox(BoxGeometry box, Color color, Color fill)
        {
            float boxLeft  = MathF.Round(box.CenterX - box.BoxHalfW);
            float boxRight = MathF.Round(box.CenterX + box.BoxHalfW);
            float capLeft  = MathF.Round(box.CenterX - box.CapHalfW);
            float capRight = MathF.Round(box.CenterX + box.CapHalfW);

            Draw.Line(new Vector2(box.CenterX, box.PxMax), new Vector2(box.CenterX, box.PxMin), color, 1.5f);
            Draw.Line(new Vector2(capLeft,  box.PxMin), new Vector2(capRight, box.PxMin), color, 1.5f);
            Draw.Line(new Vector2(capLeft,  box.PxMax), new Vector2(capRight, box.PxMax), color, 1.5f);

            float boxTop    = Math.Min(box.PxQ1, box.PxQ3);
            float boxBottom = Math.Max(box.PxQ1, box.PxQ3);
            float boxHeight = Math.Max(1f, boxBottom - boxTop);
            Draw.Rect(boxLeft, boxTop, boxRight - boxLeft, boxHeight, fill);

            Draw.Line(new Vector2(boxLeft, box.PxMed), new Vector2(boxRight, box.PxMed), Color.White, 2.5f);
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            int roomCount    = _roomTimes.Count;
            int totalColumns = roomCount + 1;
            bool isStaggered = totalColumns > ChartConstants.XAxisLabel.StaggerThreshold;
            float baseLabelY  = y + h + (isStaggered ? ChartConstants.XAxisLabel.BaseOffsetY / 2f : ChartConstants.XAxisLabel.BaseOffsetY);

            float[] offsets = ColumnOffsets(w);
            for (int i = 0; i < roomCount; i++)
            {
                float colW    = offsets[i + 1] - offsets[i];
                float centerX = GetColumnCenterX(x, w, i);
                DrawColumnStrip(i, centerX - colW * 0.5f, colW, y + h);

                if (_hiddenColumns.Contains(i)) continue;
                string label  = RoomLabels.For(i);
                float labelY  = totalColumns > ChartConstants.XAxisLabel.StaggerThreshold
                    ? (i % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                    : baseLabelY;
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;
                ActiveFont.DrawOutline(label,
                    new Vector2(centerX - labelSize.X / 2, labelY),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                    ChartPalette.Current.Room, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            float segX      = GetColumnCenterX(x, w, roomCount);
            float segLabelY = totalColumns >= ChartConstants.XAxisLabel.StaggerThreshold
                ? (roomCount % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                : baseLabelY;
            Vector2 segLabelSize = ActiveFont.Measure(Dialog.Clean(DialogIds.ChartSegment)) * ChartConstants.FontScale.AxisLabel;
            ActiveFont.DrawOutline(Dialog.Clean(DialogIds.ChartSegment),
                new Vector2(segX - segLabelSize.X / 2, segLabelY),
                Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                ChartPalette.Current.Segment, ChartConstants.Stroke.OutlineSize, Color.Black);

            if (_toggle.Normalized)
                DrawPercentageAxisLabels(x, y, w, h, _minRoomPct, _maxRoomPct, ChartPalette.Current.Room);
            else
                DrawFrameAxisLabels(x, y, w, h, _minRoom, _maxRoom, YAxisSide.Left, ChartPalette.Current.Room);

            DrawFrameAxisLabels(x, y, w, h, _minSeg, _maxSeg, YAxisSide.Right, ChartPalette.Current.Segment);

            DrawTitle();
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
                if (_toggle.Normalized)
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

            _hoveredBox = null;
            _statLabels.Clear();

            if (_toggle.UpdateHover(mouseHudPos))
                return new HoverInfo("", mouseHudPos);

            if (mouseHudPos.X < gx || mouseHudPos.X > gx + gw ||
                mouseHudPos.Y < gy || mouseHudPos.Y > gy + gh)
                return null;

            int totalColumns = _roomTimes.Count + 1;
            float[] columnOffsets = ColumnOffsets(gw);
            int idx = totalColumns - 1; // default to segment column
            for (int i = 0; i < totalColumns; i++)
                if (mouseHudPos.X < gx + columnOffsets[i + 1]) { idx = i; break; }

            // A hidden column keeps a 6 px stub, and hovering it used to draw a ghost box and a
            // full stats tooltip for a room the player had just asked not to see.
            if (idx < _roomTimes.Count && _hiddenColumns.Contains(idx)) return null;

            var times = idx == _roomTimes.Count ? _segmentTimes : _roomTimes[idx];
            if (times.Count == 0) return null;

            _hoveredBox = ComputeBoxGeometry(idx, gx, gy, gw, gh);
            if (_hoveredBox == null) return null;

            var b = _hoveredBox.Value;

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

            const float scale   = ChartConstants.FontScale.AxisLabelMedium;
            const float bgPad   = ChartConstants.Interactivity.TooltipBgPadding;
            float lineH  = ActiveFont.Measure("A").Y * scale;
            float labelX = b.CenterX + b.BoxHalfW + bgPad * 2f;

            // Y grows downward, so the slowest stat sits highest: Max, Q3, Median, Q1, Min.
            var raw = new (string name, string val, float py)[]
            {
                (Dialog.Clean(DialogIds.ChartStatMax),    new TimeTicks(b.TickMax).ToString(), b.PxMax),
                ("Q3",     new TimeTicks(b.TickQ3).ToString(),  Math.Min(b.PxQ1, b.PxQ3)),
                (Dialog.Clean(DialogIds.ChartStatMedian), new TimeTicks(b.TickMed).ToString(), b.PxMed),
                ("Q1",     new TimeTicks(b.TickQ1).ToString(),  Math.Max(b.PxQ1, b.PxQ3)),
                (Dialog.Clean(DialogIds.ChartStatMin),    new TimeTicks(b.TickMin).ToString(), b.PxMin),
            };

            // Two passes to spread overlapping labels, down then back up.
            float minGap  = lineH + bgPad * 2f;
            float[] nudged = new float[raw.Length];
            for (int i = 0; i < raw.Length; i++) nudged[i] = raw[i].py;
            for (int i = 1; i < nudged.Length; i++)
                if (nudged[i] - nudged[i - 1] < minGap)
                    nudged[i] = nudged[i - 1] + minGap;

            // Spreading only ever pushes down, so the bottom label could end up under the plot.
            // The block slides back up as a whole, then is clamped at the top -- the pass that
            // used to sit here walked back up asking whether the gaps were big enough, which the
            // loop above has just guaranteed, so it never moved anything.
            float overshoot = nudged[^1] + lineH / 2f - (gy + gh);
            if (overshoot > 0f)
                for (int i = 0; i < nudged.Length; i++) nudged[i] -= overshoot;
            float undershoot = gy - (nudged[0] - lineH / 2f);
            if (undershoot > 0f)
                for (int i = 0; i < nudged.Length; i++) nudged[i] += undershoot;

            _statLabels.Clear();
            for (int i = 0; i < raw.Length; i++)
                _statLabels.Add(new StatLabel(raw[i].name, raw[i].val, labelX, nudged[i] - lineH / 2f));

            // Empty label skips DrawTooltip; this only triggers DrawHighlight. The key is the
            // column: pins are matched on the label when there is no key, and every box here has
            // the same empty one, so pinning a second box unpinned the first.
            return new HoverInfo("", new Vector2(labelX, nudged[0]), Key: $"box:{idx}");
        }

        // The segment column is never hideable, so it stays out of the count.
        public override int? ColumnHitTest(Vector2 mousePos) =>
            HitTestColumnStrip(mousePos, _roomTimes.Count, ComputeNormalColumnWidth(width - marginH * 2));

        public override void DrawHighlight()
        {
            if (_hoveredBox == null) return;

            var   b      = _hoveredBox.Value;
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
            const float colGap = ChartConstants.Interactivity.TooltipColumnGap;
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

                Draw.Rect(bgX, bgY, totalW + bgPad * 2f, lineH + bgPad * 2f, ChartConstants.Colors.PanelBackgroundColor);
                ActiveFont.DrawOutline(sl.Left,  new Vector2(sl.X, sl.Y), Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                float rw = ActiveFont.Measure(sl.Right).X * scale;
                ActiveFont.DrawOutline(sl.Right, new Vector2(sl.X + maxLeftW + colGap + maxRightW - rw, sl.Y), Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }
    }
}
