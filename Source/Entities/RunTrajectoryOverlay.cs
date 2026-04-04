using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Line chart showing each attempt as a trajectory relative to the room average baseline.
    /// Deviation convention: deviation[r] = actualTime[r] - roomAverage[r].
    ///   Negative = faster than average → line goes UP. Positive = slower → line goes DOWN.
    /// Cumulative deviation is the running sum across rooms.
    /// Attempts are ordered: regulars (chronological) -> best (if not last) -> last.
    /// If best == last, it appears only once at the end in gold.
    /// </summary>
    public class RunTrajectoryOverlay : BaseChartOverlay
    {
        private readonly int _totalRooms;

        private record AttemptLine(
            long[] CumulativeDeviations,
            long[] RoomTimes,            // actual room ticks per room (length = RoomsCompleted)
            int    RoomsCompleted,
            int    ChronologicalIndex);  // 1-based

        // Ordered: regulars (chronological) -> best (if not last) -> last
        private readonly List<AttemptLine> _attempts;
        private readonly AttemptLine       _sobLine;
        private readonly bool              _lastIsBest;
        private readonly bool              _sobIsBest;
        private readonly long              _maxUpwardDeviation;    // magnitude of most-negative cumulative deviation (fastest, goes up)
        private readonly long              _maxDownwardDeviation;  // magnitude of most-positive cumulative deviation (slowest, goes down)
        private readonly long              _totalRange;
        private readonly long              _roomAveragesSum;
        private readonly long              _bestFinalDeviation;
        private readonly long              _lastFinalDeviation;
        private readonly bool              _anyCompleted;
        private readonly bool              _sobReachesEnd;
        private readonly bool              _lastReachesEnd;
        private readonly long[]            _roomAverages;   // per-room average ticks
        private readonly long[]            _sobRoomTimes;   // per-room SoB ticks (length = _totalRooms, 0 if no data)

        // Hover state — index into _attempts, or _attempts.Count for SoB, -1 for none
        private int _hoveredLineIdx = -1;

        public RunTrajectoryOverlay(
            IReadOnlyList<Attempt> attempts,
            List<List<TimeTicks>> roomTimes,
            int totalRooms,
            Vector2? pos = null)
            : base("Run Trajectory — deviation from average", pos)
        {
            _totalRooms = totalRooms;

            if (attempts.Count == 0 || totalRooms == 0)
            {
                _attempts            = [];
                _sobLine             = new AttemptLine([], [], 0, 0);
                _maxUpwardDeviation  = 1;
                _maxDownwardDeviation = 1;
                _totalRange          = 2;
                _roomAverages         = [];
                _sobRoomTimes         = [];
                return;
            }

            // Per-room averages from all attempts that reached each room
            _roomAverages = [.. Enumerable.Range(0, _totalRooms).Select(r =>
            {
                var times = attempts
                    .Where(a => a.Count > r)
                    .Select(a => a.GetRoomTime(r).Ticks)
                    .ToList();
                return times.Count == 0 ? 0L : (long)times.Average();
            })];

            // Build raw attempt lines in chronological order
            var raw = attempts.Select((a, chronoIdx) =>
            {
                long cumulative = 0;
                var deviations = new List<long>();
                var roomTks    = new List<long>();
                for (int r = 0; r < a.Count && r < _totalRooms; r++)
                {
                    long t = a.GetRoomTime(r).Ticks;
                    roomTks.Add(t);
                    cumulative += t - _roomAverages[r];
                    deviations.Add(cumulative);
                }
                return (
                    line: new AttemptLine([.. deviations], [.. roomTks], deviations.Count, chronoIdx + 1),
                    finalDeviation: deviations.Count > 0 ? deviations[^1] : 0L);
            }).ToList();

            // Find best: full runs first, then most negative final deviation (fastest)
            int bestIdx = Enumerable.Range(0, raw.Count)
                .Where(i => raw[i].line.RoomsCompleted == _totalRooms)
                .OrderBy(i => raw[i].finalDeviation)
                .FirstOrDefault(-1);

            if (bestIdx < 0)
                bestIdx = Enumerable.Range(0, raw.Count)
                    .OrderByDescending(i => raw[i].line.RoomsCompleted)
                    .ThenBy(i => raw[i].finalDeviation)
                    .First();

            int lastIdx  = raw.Count - 1;
            _lastIsBest  = bestIdx == lastIdx;

            var regulars = Enumerable.Range(0, raw.Count)
                .Where(i => i != bestIdx && i != lastIdx)
                .Select(i => raw[i].line);

            _attempts = [.. regulars];
            if (!_lastIsBest)
                _attempts.Add(raw[bestIdx].line);
            _attempts.Add(raw[lastIdx].line);

            // SoB line
            long sobCumulative  = 0;
            var sobDeviations   = new long[_totalRooms];
            _sobRoomTimes       = new long[_totalRooms];
            int sobRoomsCompleted = 0;
            for (int r = 0; r < _totalRooms; r++)
            {
                var times = r < roomTimes.Count ? roomTimes[r] : [];
                if (times.Count == 0) break;
                long best = times.Min(t => t.Ticks);
                _sobRoomTimes[r]  = best;
                sobCumulative    += best - _roomAverages[r];
                sobDeviations[r]  = sobCumulative;
                sobRoomsCompleted = r + 1;
            }
            _sobLine = new AttemptLine(sobDeviations, _sobRoomTimes[..sobRoomsCompleted], sobRoomsCompleted, 0);

            // SoB is always the topmost line (most negative cumulative deviation)
            _maxUpwardDeviation = Math.Max(sobRoomsCompleted > 0 ? -sobDeviations[sobRoomsCompleted - 1] : 0, 1);
            _maxDownwardDeviation = Math.Max(
                _attempts
                    .SelectMany(a => a.CumulativeDeviations)
                    .Where(d => d > 0)
                    .DefaultIfEmpty(1)
                    .Max(), 1);

            _totalRange          = _maxUpwardDeviation + _maxDownwardDeviation;
            _roomAveragesSum     = _roomAverages.Sum();
            _bestFinalDeviation  = raw[bestIdx].finalDeviation;
            _lastFinalDeviation  = raw[lastIdx].finalDeviation;
            long sobFinalDev     = sobRoomsCompleted > 0 ? sobDeviations[sobRoomsCompleted - 1] : 0;
            _sobIsBest           = sobFinalDev == _bestFinalDeviation;
            _anyCompleted        = _attempts.Any(a => a.RoomsCompleted == _totalRooms);
            _sobReachesEnd       = _sobLine.RoomsCompleted == _totalRooms;
            _lastReachesEnd      = _attempts.Count >= 1 && _attempts[^1].RoomsCompleted == _totalRooms;
        }

        protected override void DrawAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x, y + h), new Vector2(x + w, y + h), axisColor, 2f);
            Draw.Line(new Vector2(x, y),     new Vector2(x, y + h),     axisColor, 2f);
            float baselineY = y + (float)_maxUpwardDeviation / _totalRange * h;
            Draw.Line(new Vector2(x, baselineY), new Vector2(x + w, baselineY), ChartConstants.Colors.BaselineColor, ChartConstants.Stroke.OutlineSize);
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_attempts.Count == 0) return;

            float columnWidth = w / _totalRooms;
            float baselineY   = y + (float)_maxUpwardDeviation / _totalRange * h;
            float devScale    = h / _totalRange;
            int   total       = _attempts.Count;
            bool  hovering    = _hoveredLineIdx >= 0;
            var   s           = SpeebrunConsistencyTrackerModule.Settings;

            int regularCount = _lastIsBest ? total - 1 : total - 2;

            // Regular lines — dim when hovering (unless this one is hovered)
            for (int i = 0; i < regularCount; i++)
            {
                bool isHovered = hovering && _hoveredLineIdx == i;
                Color color;
                float thickness;
                if (!hovering)
                {
                    float brightness = total <= 1
                        ? ChartConstants.Trajectory.BrightnessMax
                        : MathHelper.Lerp(ChartConstants.Trajectory.BrightnessMin, ChartConstants.Trajectory.BrightnessMax, (float)i / (total - 1));
                    color     = Color.White * brightness;
                    thickness = 1.5f;
                }
                else if (isHovered)
                {
                    color     = Color.White;
                    thickness = 2.5f;
                }
                else
                {
                    color     = Color.Gray * 0.2f;
                    thickness = 1f;
                }
                DrawAttemptLine(_attempts[i], x, baselineY, columnWidth, devScale, h, color, thickness);
            }

            // SoB line
            Color sobColor     = _sobIsBest ? s.TrajectoryBestColorFinal : s.TrajectorySobColorFinal;
            bool  sobHovered   = hovering && _hoveredLineIdx == _attempts.Count;
            float sobThickness = sobHovered ? 3f : 2f;
            Color sobDrawColor = hovering && !sobHovered ? sobColor * 0.35f : sobColor;
            DrawAttemptLine(_sobLine, x, baselineY, columnWidth, devScale, h, sobDrawColor, sobThickness);

            // Best line (if separate from last)
            if (!_lastIsBest)
            {
                bool  bestHovered   = hovering && _hoveredLineIdx == total - 2;
                float bestThickness = bestHovered ? 3f : 2f;
                Color bestColor     = hovering && !bestHovered ? s.TrajectoryBestColorFinal * 0.35f : s.TrajectoryBestColorFinal;
                DrawAttemptLine(_attempts[^2], x, baselineY, columnWidth, devScale, h, bestColor, bestThickness);
            }

            // Last line
            bool  lastHovered   = hovering && _hoveredLineIdx == total - 1;
            float lastThickness = lastHovered ? 3f : 2f;
            Color lastBaseColor = _lastIsBest ? s.TrajectoryBestColorFinal : s.TrajectoryLastColorFinal;
            Color lastColor     = hovering && !lastHovered ? lastBaseColor * 0.35f : lastBaseColor;
            DrawAttemptLine(_attempts[^1], x, baselineY, columnWidth, devScale, h, lastColor, lastThickness);
        }

        private static void DrawAttemptLine(AttemptLine attempt, float x, float baselineY, float columnWidth, float devScale, float h, Color color, float thickness)
        {
            for (int r = 0; r < attempt.RoomsCompleted; r++)
            {
                float x1 = x + r * columnWidth;
                float y1 = r == 0
                    ? baselineY
                    : baselineY + attempt.CumulativeDeviations[r - 1] * devScale;
                float x2 = x + (r + 1) * columnWidth;
                float y2 = baselineY + attempt.CumulativeDeviations[r] * devScale;

                y1 = MathHelper.Clamp(y1, baselineY - h, baselineY + h);
                y2 = MathHelper.Clamp(y2, baselineY - h, baselineY + h);

                Draw.Line(new Vector2(x1, y1), new Vector2(x2, y2), color, thickness);
            }
        }

        // Returns the screen Y of the attempt line at the right edge of column r (x = graphX + (r+1)*colW)
        private float LineYAtColumnEnd(AttemptLine attempt, float baselineY, float devScale, float h, int r)
        {
            if (r >= attempt.RoomsCompleted) return float.MaxValue;
            float y = baselineY + attempt.CumulativeDeviations[r] * devScale;
            return MathHelper.Clamp(y, baselineY - h, baselineY + h);
        }

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            _hoveredLineIdx = -1;

            if (_attempts.Count == 0 || _totalRooms == 0) return null;
            if (mouseHudPos.X < gx || mouseHudPos.X > gx + gw ||
                mouseHudPos.Y < gy || mouseHudPos.Y > gy + gh)
                return null;

            float columnWidth = gw / _totalRooms;
            float baselineY   = gy + (float)_maxUpwardDeviation / _totalRange * gh;
            float devScale    = gh / _totalRange;

            // Which column is the mouse in?
            int col = (int)((mouseHudPos.X - gx) / columnWidth);
            col = Math.Clamp(col, 0, _totalRooms - 1);

            // For each line, lerp the Y at mouseX within this column's segment
            float mouseX  = mouseHudPos.X;
            float mouseY  = mouseHudPos.Y;
            float bestDist = float.MaxValue;
            int   bestIdx  = -1;

            void Check(AttemptLine line, int idx)
            {
                if (col >= line.RoomsCompleted) return;
                float x1 = gx + col * columnWidth;
                float y1 = col == 0
                    ? baselineY
                    : MathHelper.Clamp(baselineY + line.CumulativeDeviations[col - 1] * devScale, baselineY - gh, baselineY + gh);
                float x2 = gx + (col + 1) * columnWidth;
                float y2 = MathHelper.Clamp(baselineY + line.CumulativeDeviations[col] * devScale, baselineY - gh, baselineY + gh);

                float t       = (mouseX - x1) / (x2 - x1);
                float lerpedY = y1 + t * (y2 - y1);
                float dist    = Math.Abs(mouseY - lerpedY);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx  = idx;
                }
            }

            for (int i = 0; i < _attempts.Count; i++)
                Check(_attempts[i], i);
            Check(_sobLine, _attempts.Count);

            const float snapThreshold = 4f;
            if (bestIdx < 0 || bestDist > snapThreshold) return null;

            _hoveredLineIdx = bestIdx;
            // Return non-null to trigger DrawHighlight; label is empty (we draw everything in DrawHighlight)
            return new HoverInfo("", Vector2.Zero);
        }

        public override void DrawHighlight()
        {
            if (_hoveredLineIdx < 0) return;

            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            float columnWidth = gw / _totalRooms;
            float baselineY   = gy + (float)_maxUpwardDeviation / _totalRange * gh;
            float devScale    = gh / _totalRange;

            bool       isSob   = _hoveredLineIdx == _attempts.Count;
            AttemptLine line   = isSob ? _sobLine : _attempts[_hoveredLineIdx];

            var   s           = SpeebrunConsistencyTrackerModule.Settings;
            bool  isSpecial   = isSob || _hoveredLineIdx >= (_lastIsBest ? _attempts.Count - 1 : _attempts.Count - 2);
            Color labelColor  = isSob
                ? (_sobIsBest ? s.TrajectoryBestColorFinal : s.TrajectorySobColorFinal)
                : isSpecial
                    ? (_lastIsBest ? s.TrajectoryBestColorFinal : _hoveredLineIdx == _attempts.Count - 1 ? s.TrajectoryLastColorFinal : s.TrajectoryBestColorFinal)
                    : Color.White;

            // Right-axis label for completed runs
            if (line.RoomsCompleted == _totalRooms)
            {
                long   finalDev   = line.CumulativeDeviations[_totalRooms - 1];
                long   finalTime  = _roomAveragesSum + finalDev;
                float  lineEndY   = MathHelper.Clamp(baselineY + finalDev * devScale, gy, gy + gh);
                float  rightX     = gx + gw + ChartConstants.Trajectory.RightLabelMarginX;
                string timeStr    = new TimeTicks(finalTime).ToString();
                Vector2 sz        = ActiveFont.Measure(timeStr) * ChartConstants.FontScale.AxisLabelSmall;
                ActiveFont.DrawOutline(timeStr,
                    new Vector2(rightX, lineEndY - sz.Y / 2),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                    labelColor, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            // Per-column tooltips
            const float scale  = ChartConstants.FontScale.AxisLabelMedium;
            const float bgPad  = ChartConstants.Interactivity.TooltipBgPadding;
            const float colGap = 20f;
            float lineH = ActiveFont.Measure("A").Y * scale;

            // Prefix sums of room times (avoids O(N²) Take().Sum() in the loops below)
            long[] cumulTimes = new long[line.RoomsCompleted];
            long runningSum = 0;
            for (int r = 0; r < line.RoomsCompleted; r++)
            {
                runningSum += line.RoomTimes[r];
                cumulTimes[r] = runningSum;
            }

            // Row: null val = section header (full width, no value column); non-null = label+value pair
            // indent = true for sub-rows under a section header
            const float indentX = 10f;

            // Pre-compute max widths for alignment across all boxes
            float maxLabelW = 0f, maxValW = 0f;
            string[] labelNames = ["Run Time", "cumul", "room", "vs avg", "vs SoB"];
            foreach (var ln in labelNames)
                maxLabelW = Math.Max(maxLabelW, ActiveFont.Measure(ln).X * scale);

            for (int r = 0; r < line.RoomsCompleted; r++)
            {
                long cumul       = cumulTimes[r];
                long roomTime    = line.RoomTimes[r];
                long cumulDev    = line.CumulativeDeviations[r];
                long roomDev     = r == 0 ? (roomTime - _roomAverages[r]) : (cumulDev - line.CumulativeDeviations[r - 1]);
                long sobCumulDev = r < _sobLine.CumulativeDeviations.Length ? _sobLine.CumulativeDeviations[r] : 0;
                long sobRoom     = r < _sobRoomTimes.Length ? _sobRoomTimes[r] : 0;
                long sobRoomDev  = sobRoom > 0 ? (roomTime - sobRoom) : 0;

                string[] vals =
                [
                    new TimeTicks(cumul).ToString(),
                    new TimeTicks(roomTime).ToString(),
                    FormatDev(cumulDev),
                    FormatDev(roomDev),
                    FormatDev(cumulDev - sobCumulDev),
                    sobRoom > 0 ? FormatDev(sobRoomDev) : "n/a",
                ];
                foreach (var v in vals)
                    maxValW = Math.Max(maxValW, ActiveFont.Measure(v).X * scale);
            }

            float boxW   = indentX + maxLabelW + colGap + maxValW;
            float totalH = lineH * 9; // 3 headers + 6 sub-rows

            for (int r = 0; r < line.RoomsCompleted; r++)
            {
                long cumul       = cumulTimes[r];
                long roomTime    = line.RoomTimes[r];
                long cumulDev    = line.CumulativeDeviations[r];
                long roomDev     = r == 0 ? (roomTime - _roomAverages[r]) : (cumulDev - line.CumulativeDeviations[r - 1]);
                long sobCumulDev = r < _sobLine.CumulativeDeviations.Length ? _sobLine.CumulativeDeviations[r] : 0;
                long sobRoom     = r < _sobRoomTimes.Length ? _sobRoomTimes[r] : 0;
                long sobRoomDev  = sobRoom > 0 ? (roomTime - sobRoom) : 0;

                // (label, val, indent) — val "" means section header (no value column drawn)
                var rows = new (string label, string val, bool indent)[]
                {
                    ("Run Time", "",                                  false),
                    ("cumul",    new TimeTicks(cumul).ToString(),    true),
                    ("room",     new TimeTicks(roomTime).ToString(),  true),
                    ("vs avg",   "",                                  false),
                    ("cumul",    FormatDev(cumulDev),                 true),
                    ("room",     FormatDev(roomDev),                  true),
                    ("vs SoB",   "",                                  false),
                    ("cumul",    FormatDev(cumulDev - sobCumulDev),   true),
                    ("room",     sobRoom > 0 ? FormatDev(sobRoomDev) : "n/a", true),
                };

                // Box centered horizontally in column r; above the line if space, otherwise below
                float lineY   = LineYAtColumnEnd(line, baselineY, devScale, gh, r);
                float colMidX = gx + (r + 0.5f) * columnWidth;
                float boxX    = colMidX - boxW / 2f;
                float boxH    = totalH + bgPad * 2f;
                float boxY    = (lineY - bgPad - boxH >= gy)
                    ? lineY - bgPad - boxH   // above
                    : lineY + bgPad;          // below

                Draw.Rect(boxX - bgPad, boxY, boxW + bgPad * 2f, boxH, Color.Black * 0.92f);

                float textStartY = boxY + bgPad;
                for (int row = 0; row < rows.Length; row++)
                {
                    float ry     = textStartY + row * lineH;
                    float labelX = boxX + (rows[row].indent ? indentX : 0f);
                    ActiveFont.DrawOutline(rows[row].label, new Vector2(labelX, ry),
                        Vector2.Zero, Vector2.One * scale, Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
                    if (rows[row].val != "")
                        ActiveFont.DrawOutline(rows[row].val, new Vector2(boxX + indentX + maxLabelW + colGap, ry),
                            Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }
        }

        // Deviation convention: negative = faster than baseline (goes up), positive = slower (goes down).
        // Display directly: negative shows as "-1.000s" (gained time), positive as "+0.500s" (lost time).
        private static string FormatDev(long ticks)
        {
            if (ticks == 0) return "±0";
            string sign = ticks > 0 ? "+" : "-";
            return sign + new TimeTicks(Math.Abs(ticks)).ToString();
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            DrawTitle();

            if (_totalRooms == 0 || _attempts.Count == 0) return;

            float columnWidth = w / _totalRooms;
            float baselineY = y + (float)_maxUpwardDeviation / _totalRange * h;

            for (int r = 0; r < _totalRooms; r++)
                Draw.Line(
                    new Vector2(x + r * columnWidth, y),
                    new Vector2(x + r * columnWidth, y + h),
                    ChartConstants.Colors.GridLineColor, 1f);

            DrawXAxisStaggeredLabels(x, y, h, _totalRooms, columnWidth, r => $"R{r + 1}", Color.LightGray);

            float aboveHeight = (float)_maxUpwardDeviation / _totalRange * h;
            float belowHeight = (float)_maxDownwardDeviation / _totalRange * h;
            int totalTicks  = ChartConstants.Trajectory.TotalYTicks;
            int ticksAbove  = Math.Max(1, (int)Math.Round((double)aboveHeight / h * totalTicks));
            int ticksBelow  = Math.Max(1, totalTicks - ticksAbove);

            DrawYTicks(x, y, w, baselineY, aboveHeight, _maxUpwardDeviation, ticksAbove, true);
            DrawYBaseline(x, baselineY);
            DrawYTicks(x, y, w, baselineY, belowHeight, _maxDownwardDeviation, ticksBelow, false);

            DrawRightAxisLabels(x, y, w, h, baselineY);

            float legendY = y + h + ChartConstants.Legend.LegendOffsetY;
            float legendX = x + w;
            float offset  = 0;
            var s = SpeebrunConsistencyTrackerModule.Settings;
            if (!(_sobIsBest && _lastIsBest))
            {
                string lastLabel = _lastIsBest ? "Best & last run" : "Last run";
                Color lastColor  = _lastIsBest ? s.TrajectoryBestColorFinal : s.TrajectoryLastColorFinal;
                DrawLegendEntry(legendX, legendY, lastLabel, lastColor, ChartConstants.FontScale.AxisLabel, right: true);
                offset = ActiveFont.Measure(lastLabel).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;
            }
            if (!_lastIsBest && !_sobIsBest)
            {
                DrawLegendEntry(legendX - offset, legendY, "Best run", s.TrajectoryBestColorFinal, ChartConstants.FontScale.AxisLabel, right: true);
                offset += ActiveFont.Measure("Best run").X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;
            }
            string sobLabel      = _sobIsBest && _lastIsBest ? "SoB & Best & last run"
                                 : _sobIsBest               ? "SoB & Best run"
                                                            : "SoB";
            Color sobLegendColor = _sobIsBest ? s.TrajectoryBestColorFinal : s.TrajectorySobColorFinal;
            DrawLegendEntry(legendX - offset, legendY, sobLabel, sobLegendColor, ChartConstants.FontScale.AxisLabel, right: true);

            string stats = _attempts.Count == 1 ? "1 Attempt" : $"{_attempts.Count} Attempts";
            Vector2 statsSize = ActiveFont.Measure(stats) * ChartConstants.FontScale.AxisLabelMedium;
            ActiveFont.DrawOutline(stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + ChartConstants.Legend.LegendOffsetY),
                Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        private static void DrawYTicks(float x, float y, float w, float baselineY, float sideHeight, long maxDeviation, int tickCount, bool above)
        {
            string prefix    = above ? "-" : "+";
            float chartBottom = baselineY + (above ? 0 : sideHeight);
            float minSpacing  = ActiveFont.LineHeight * ChartConstants.FontScale.AxisLabelSmall * 1.1f;
            float lastDrawnY  = above ? float.MaxValue : float.MinValue;

            for (int i = 1; i <= tickCount; i++)
            {
                long  tickDeviation = maxDeviation / tickCount * i;
                float yPos = above
                    ? baselineY - (float)i / tickCount * sideHeight
                    : baselineY + (float)i / tickCount * sideHeight;

                if (above  && yPos < y)           continue;
                if (!above && yPos > chartBottom)  continue;
                if (above  && lastDrawnY - yPos < minSpacing) continue;
                if (!above && yPos - lastDrawnY  < minSpacing) continue;

                string timeLabel  = prefix + new TimeTicks(tickDeviation).ToString();
                Vector2 labelSize = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelSmall;
                ActiveFont.DrawOutline(timeLabel,
                    new Vector2(x - labelSize.X - ChartConstants.Axis.YLabelMarginX, yPos - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                    Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), ChartConstants.Colors.GridLineColor, 1f);
                lastDrawnY = yPos;
            }
        }

        private static void DrawYBaseline(float x, float baselineY)
        {
            string timeLabel  = "±0";
            Vector2 labelSize = ActiveFont.Measure(timeLabel) * ChartConstants.FontScale.AxisLabelSmall;
            ActiveFont.DrawOutline(timeLabel,
                new Vector2(x - labelSize.X - ChartConstants.Axis.YLabelMarginX, baselineY - labelSize.Y / 2),
                Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                Color.Gray, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        private void DrawRightAxisLabels(float x, float y, float w, float h, float baselineY)
        {
            float devScale   = h / _totalRange;
            float labelHeight = ActiveFont.Measure("0").Y * ChartConstants.FontScale.AxisLabelSmall;
            float minSpacing  = labelHeight + ChartConstants.Trajectory.LabelMinSpacingExtra;
            float rightX      = x + w + ChartConstants.Trajectory.RightLabelMarginX;
            var   s           = SpeebrunConsistencyTrackerModule.Settings;
            Color sobColor    = _sobIsBest ? s.TrajectoryBestColorFinal : s.TrajectorySobColorFinal;

            var labelList = new List<(float yPos, string text, Color color)>();
            if (_anyCompleted)
                labelList.Add((baselineY, new TimeTicks(_roomAveragesSum).ToString(), Color.Gray));
            if (!_sobIsBest && !_lastIsBest && _anyCompleted)
                labelList.Add((baselineY + _bestFinalDeviation * devScale, new TimeTicks(_roomAveragesSum + _bestFinalDeviation).ToString(), s.TrajectoryBestColorFinal));
            if (!_lastIsBest && _lastReachesEnd)
                labelList.Add((baselineY + _lastFinalDeviation * devScale, new TimeTicks(_roomAveragesSum + _lastFinalDeviation).ToString(), s.TrajectoryLastColorFinal));
            if (_sobReachesEnd)
                labelList.Add((baselineY - _maxUpwardDeviation * devScale, new TimeTicks(_roomAveragesSum - _maxUpwardDeviation).ToString(), sobColor));
            var labels = labelList.ToArray();

            float[] nudged = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++) nudged[i] = labels[i].yPos;
            for (int i = 1; i < nudged.Length; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    float diff = nudged[i] - nudged[j];
                    if (Math.Abs(diff) < minSpacing)
                        nudged[i] = nudged[j] + (diff >= 0 ? minSpacing : -minSpacing);
                }
                nudged[i] = MathHelper.Clamp(nudged[i], y, y + h);
            }

            for (int i = 0; i < labels.Length; i++)
            {
                Vector2 labelSize = ActiveFont.Measure(labels[i].text) * ChartConstants.FontScale.AxisLabelSmall;
                if (labels[i].yPos < y - labelSize.Y / 2 || labels[i].yPos > y + h + labelSize.Y / 2) continue;
                ActiveFont.DrawOutline(labels[i].text,
                    new Vector2(rightX, nudged[i] - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                    labels[i].color, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }
    }
}
