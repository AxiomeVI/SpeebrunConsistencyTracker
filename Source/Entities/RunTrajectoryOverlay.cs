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

        // Hover state — index into _attempts, or _attempts.Count for SoB, _attempts.Count+1 for baseline, -1 for none
        private int _hoveredLineIdx = -1;
        // Pin state: _mainPinIdx is the fixed reference line (-1 = no pin / comparison mode off)
        // _compPinIdx is the optional secondary comparison line (-1 = compare vs Avg)
        private int _mainPinIdx = -1;
        private int _compPinIdx = -1;

        public RunTrajectoryOverlay(
            IReadOnlyList<Attempt> attempts,
            List<List<TimeTicks>> roomTimes,
            int totalRooms,
            Vector2? pos = null)
            : base("Run Trajectory — Deviation from average", pos)
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

        private void DrawAxesLines(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x - 1, y + h), new Vector2(x + w + 1, y + h), axisColor, 2f);
            Draw.Line(new Vector2(x, y),     new Vector2(x, y + h),     axisColor, 2f);
            float baselineY = y + (float)_maxUpwardDeviation / _totalRange * h;
            Draw.Line(new Vector2(x, baselineY), new Vector2(x + w, baselineY), ChartConstants.Colors.BaselineColor, ChartConstants.Stroke.OutlineSize);
        }

        public override void Render()
        {
            Draw.Rect(position, width, height, backgroundColor);
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;

            DrawGrid(gx, gy, gw, gh);
            DrawAxesLines(gx, gy, gw, gh);
            DrawBars(gx, gy, gw, gh);
            DrawLabels(gx, gy, gw, gh);
            DrawPinnedHighlights();
            DrawComparisonTable();
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            if (_totalRooms == 0 || _attempts.Count == 0) return;

            float columnWidth = w / _totalRooms;

            for (int r = 0; r < _totalRooms; r++)
                Draw.Line(
                    new Vector2(x + r * columnWidth, y),
                    new Vector2(x + r * columnWidth, y + h),
                    ChartConstants.Colors.GridLineColor, 1f);

            float baselineY   = y + (float)_maxUpwardDeviation / _totalRange * h;
            float aboveHeight = (float)_maxUpwardDeviation   / _totalRange * h;
            float belowHeight = (float)_maxDownwardDeviation / _totalRange * h;
            int totalTicks = ChartConstants.Trajectory.TotalYTicks;
            int ticksAbove = Math.Max(1, (int)Math.Round((double)aboveHeight / h * totalTicks));
            int ticksBelow = Math.Max(1, totalTicks - ticksAbove);

            DrawYTickGridLines(x, y, w, baselineY, aboveHeight, ticksAbove, true);
            DrawYTickGridLines(x, y, w, baselineY, belowHeight, ticksBelow, false);
        }

        private static void DrawYTickGridLines(float x, float y, float w, float baselineY, float sideHeight, int tickCount, bool above)
        {
            float chartBottom = baselineY + (above ? 0 : sideHeight);
            float minSpacing  = ActiveFont.LineHeight * ChartConstants.FontScale.AxisLabelSmall * 1.1f;
            float lastDrawnY  = above ? float.MaxValue : float.MinValue;

            for (int i = 1; i <= tickCount; i++)
            {
                float yPos = above
                    ? baselineY - (float)i / tickCount * sideHeight
                    : baselineY + (float)i / tickCount * sideHeight;

                if (above  && yPos < y)                       continue;
                if (!above && yPos > chartBottom)              continue;
                if (above  && lastDrawnY - yPos < minSpacing) continue;
                if (!above && yPos - lastDrawnY  < minSpacing) continue;

                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos), ChartConstants.Colors.GridLineColor, 1f);
                lastDrawnY = yPos;
            }
        }

        private bool IsLinePinned(int lineIdx) =>
            lineIdx == _mainPinIdx || lineIdx == _compPinIdx;

        /// <summary>
        /// Returns true if the line at <paramref name="lineIdx"/> should be drawn dimmed.
        /// Dimming applies when either hovering or comparison mode is active, and the line
        /// is not one of the "active" lines (hovered, main pin, comp pin, or SoB in comparison mode).
        /// </summary>
        private bool IsLineDimmed(int lineIdx)
        {
            bool hovering       = _hoveredLineIdx >= 0;
            bool comparisonMode = _mainPinIdx >= 0;
            if (!hovering && !comparisonMode) return false;

            bool isHovered = hovering && _hoveredLineIdx == lineIdx;
            if (isHovered) return false;

            if (comparisonMode)
            {
                if (lineIdx == _mainPinIdx)  return false; // main pin always lit
                if (lineIdx == _compPinIdx)  return false; // comp pin always lit
                // SoB is always lit in comparison mode (index = _attempts.Count)
                if (lineIdx == _attempts.Count) return false;
            }

            // If only hovering (no comparison mode), hovered line is handled above; all others dim.
            // If comparison mode, any line not matching the exemptions above is dimmed.
            return true;
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

            // Regular lines
            for (int i = 0; i < regularCount; i++)
            {
                bool isHovered = hovering && _hoveredLineIdx == i;
                bool dimmed    = IsLineDimmed(i);
                Color color;
                float thickness;
                if (!hovering && _mainPinIdx < 0)
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
                else if (dimmed)
                {
                    color     = Color.White * ChartConstants.Trajectory.BrightnessMin;
                    thickness = 1f;
                }
                else if (IsLinePinned(i))
                {
                    color     = Color.White;
                    thickness = 2.5f;
                }
                else
                {
                    float brightness = total <= 1
                        ? ChartConstants.Trajectory.BrightnessMax
                        : MathHelper.Lerp(ChartConstants.Trajectory.BrightnessMin, ChartConstants.Trajectory.BrightnessMax, (float)i / (total - 1));
                    color     = Color.White * brightness;
                    thickness = 1.5f;
                }
                DrawAttemptLine(_attempts[i], x, baselineY, columnWidth, devScale, h, color, thickness);
            }

            // --- Special lines: SoB, Best, Last ---
            Color sobColor2  = s.TrajectorySobColorFinal;
            Color bestColor2 = s.TrajectoryBestColorFinal;
            Color lastColor2 = s.TrajectoryLastColorFinal;
            float specialThick = 2f;

            if (_sobIsBest && _lastIsBest)
            {
                int     idx3    = _attempts.Count;
                bool    h3      = hovering && (_hoveredLineIdx == idx3 || _hoveredLineIdx >= total - 1);
                bool    pinned3 = IsLinePinned(idx3) || IsLinePinned(total - 1);
                bool    dimmed3 = IsLineDimmed(idx3) && IsLineDimmed(total - 1);
                float   t3      = h3 || pinned3 ? 3f : specialThick;
                Color[] c3      = [sobColor2, bestColor2, lastColor2];
                Color[] c3Draw  = dimmed3 ? [sobColor2 * 0.35f, bestColor2 * 0.35f, lastColor2 * 0.35f] : c3;
                DrawDashedAttemptLine(_sobLine, c3Draw, x, baselineY, columnWidth, devScale, h, t3);
            }
            else if (_sobIsBest)
            {
                bool    hSB      = hovering && _hoveredLineIdx == _attempts.Count;
                bool    pinnedSB = IsLinePinned(_attempts.Count);
                bool    dimmedSB = IsLineDimmed(_attempts.Count);
                float   tSB      = hSB || pinnedSB ? 3f : specialThick;
                Color[] cSB      = [sobColor2, bestColor2];
                Color[] cSBDraw  = dimmedSB ? [sobColor2 * 0.35f, bestColor2 * 0.35f] : cSB;
                DrawDashedAttemptLine(_sobLine, cSBDraw, x, baselineY, columnWidth, devScale, h, tSB);

                bool  lastHovered2   = hovering && _hoveredLineIdx == total - 1;
                bool  lastPinned2    = IsLinePinned(total - 1);
                bool  dimmedLast2    = IsLineDimmed(total - 1);
                float lastThickness2 = lastHovered2 || lastPinned2 ? 3f : specialThick;
                Color lastDrawColor2 = dimmedLast2 ? lastColor2 * 0.35f : lastColor2;
                DrawAttemptLine(_attempts[^1], x, baselineY, columnWidth, devScale, h, lastDrawColor2, lastThickness2);
            }
            else if (_lastIsBest)
            {
                bool    hBL      = hovering && _hoveredLineIdx >= total - 1;
                bool    pinnedBL = IsLinePinned(total - 1);
                bool    dimmedBL = IsLineDimmed(total - 1);
                float   tBL      = hBL || pinnedBL ? 3f : specialThick;
                Color[] cBL      = [bestColor2, lastColor2];
                Color[] cBLDraw  = dimmedBL ? [bestColor2 * 0.35f, lastColor2 * 0.35f] : cBL;
                DrawDashedAttemptLine(_attempts[^1], cBLDraw, x, baselineY, columnWidth, devScale, h, tBL);

                bool  sobHovered2   = hovering && _hoveredLineIdx == _attempts.Count;
                bool  sobPinned2    = IsLinePinned(_attempts.Count);
                bool  dimmedSob2    = IsLineDimmed(_attempts.Count);
                float sobThickness2 = sobHovered2 || sobPinned2 ? 3f : specialThick;
                Color sobDrawColor2 = dimmedSob2 ? sobColor2 * 0.35f : sobColor2;
                DrawAttemptLine(_sobLine, x, baselineY, columnWidth, devScale, h, sobDrawColor2, sobThickness2);
            }
            else
            {
                bool  dimmedSob3    = IsLineDimmed(_attempts.Count);
                bool  sobPinned3    = IsLinePinned(_attempts.Count);
                float sobThickness3 = (hovering && _hoveredLineIdx == _attempts.Count) || sobPinned3 ? 3f : specialThick;
                Color sobDrawColor3 = dimmedSob3 ? sobColor2 * 0.35f : sobColor2;
                DrawAttemptLine(_sobLine, x, baselineY, columnWidth, devScale, h, sobDrawColor3, sobThickness3);

                if (!_lastIsBest)
                {
                    bool  bestHovered3   = hovering && _hoveredLineIdx == total - 2;
                    bool  bestPinned3    = IsLinePinned(total - 2);
                    bool  dimmedBest3    = IsLineDimmed(total - 2);
                    float bestThickness3 = bestHovered3 || bestPinned3 ? 3f : specialThick;
                    Color bestDrawColor3 = dimmedBest3 ? bestColor2 * 0.35f : bestColor2;
                    DrawAttemptLine(_attempts[^2], x, baselineY, columnWidth, devScale, h, bestDrawColor3, bestThickness3);
                }

                bool  lastHovered3   = hovering && _hoveredLineIdx == total - 1;
                bool  lastPinned3    = IsLinePinned(total - 1);
                bool  dimmedLast3    = IsLineDimmed(total - 1);
                float lastThickness3 = lastHovered3 || lastPinned3 ? 3f : specialThick;
                Color lastBaseColor3 = _lastIsBest ? bestColor2 : lastColor2;
                Color lastDrawColor3 = dimmedLast3 ? lastBaseColor3 * 0.35f : lastBaseColor3;
                DrawAttemptLine(_attempts[^1], x, baselineY, columnWidth, devScale, h, lastDrawColor3, lastThickness3);
            }
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

        /// <summary>
        /// Draws an attempt line with a cycling dash pattern across all segments.
        /// colors.Length colors repeat in order; dash length is CoincidentDashLen pixels.
        /// Dash positions are computed in global path space so they never gap at segment boundaries.
        /// </summary>
        private static void DrawDashedAttemptLine(
            AttemptLine attempt, Color[] colors,
            float x, float baselineY, float columnWidth, float devScale, float h, float thickness)
        {
            float dashLen  = ChartConstants.Trajectory.CoincidentDashLen;
            float cycleLen = dashLen * colors.Length;
            float globalStart = 0f; // global path offset at the start of current segment

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

                var   segStart = new Vector2(x1, y1);
                var   segEnd   = new Vector2(x2, y2);
                float segLen   = Vector2.Distance(segStart, segEnd);
                if (segLen < 0.5f) { globalStart += segLen; continue; }

                Vector2 dir      = (segEnd - segStart) / segLen;
                float   segEndG  = globalStart + segLen; // global offset at end of this segment

                for (int k = 0; k < colors.Length; k++)
                {
                    // In global space, color k occupies [n*cycleLen + k*dashLen, n*cycleLen + (k+1)*dashLen) for all n.
                    // Find the first dash start (global) that overlaps [globalStart, segEndG).
                    float firstDashG = (float)Math.Floor((globalStart - k * dashLen) / cycleLen) * cycleLen + k * dashLen;
                    if (firstDashG + dashLen <= globalStart)
                        firstDashG += cycleLen; // advance to first overlapping dash

                    for (float dg = firstDashG; dg < segEndG; dg += cycleLen)
                    {
                        // Clip dash to this segment's global range
                        float clipStart = Math.Max(dg,           globalStart);
                        float clipEnd   = Math.Min(dg + dashLen, segEndG);
                        if (clipEnd <= clipStart) continue;

                        // Convert global offsets to local segment offsets, then to positions
                        float t1 = (clipStart - globalStart) / segLen;
                        float t2 = (clipEnd   - globalStart) / segLen;
                        Draw.Line(segStart + dir * (t1 * segLen), segStart + dir * (t2 * segLen), colors[k], thickness);
                    }
                }

                globalStart = segEndG;
            }
        }

        // Returns the screen Y of the attempt line at the right edge of column r (x = graphX + (r+1)*colW)
        private static float LineYAtColumnEnd(AttemptLine attempt, float baselineY, float devScale, float h, int r)
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

            // Baseline — horizontal line, snap purely on Y distance
            float baselineDist = Math.Abs(mouseY - baselineY);
            if (baselineDist < bestDist)
            {
                bestDist = baselineDist;
                bestIdx  = _attempts.Count + 1;
            }

            const float snapThreshold = 4f;
            if (bestIdx < 0 || bestDist > snapThreshold) return null;

            _hoveredLineIdx = bestIdx;
            // Return non-null to trigger DrawHighlight; label is empty (we draw everything in DrawHighlight)
            return new HoverInfo("", Vector2.Zero, Key: _hoveredLineIdx.ToString(), PinGroup: "trajectory");
        }

        public override bool ManagesPins => true;
        public override bool HasPins => _mainPinIdx >= 0;

        public override bool HandleClick(HoverInfo hover)
        {
            if (!int.TryParse(hover.Key, out int idx)) return false;

            if (_mainPinIdx < 0)
            {
                // No main pin yet — first click sets the main pin
                _mainPinIdx = idx;
                _compPinIdx = -1;
                return true;
            }

            if (idx == _mainPinIdx)
            {
                // Clicking the main line — exit comparison mode entirely
                _mainPinIdx = -1;
                _compPinIdx = -1;
                return true;
            }

            // Clicking any other line — replace (or set) the comparison line
            _compPinIdx = idx == _compPinIdx ? -1 : idx;
            return true;
        }

        public override void ClearPins()
        {
            _mainPinIdx     = -1;
            _compPinIdx     = -1;
            _hoveredLineIdx = -1;
        }

        // Called by GraphInteractivity when the mouse is hovering a line.
        public override void DrawHighlight()
        {
            if (_hoveredLineIdx < 0) return;
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;
            DrawLineTooltips(_hoveredLineIdx, gx, gy, gw, gh);
        }

        public override void DrawHighlight(HoverInfo info)
        {
            // Never called — RunTrajectory manages its own pins via HandleClick.
        }

        // Called from Render() — draws persistent tooltips for pinned lines, independent of hover.
        private void DrawPinnedHighlights()
        {
            if (_mainPinIdx < 0) return;
            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;
            DrawLineTooltips(_mainPinIdx, gx, gy, gw, gh);
            if (_compPinIdx >= 0 && _compPinIdx != _mainPinIdx)
                DrawLineTooltips(_compPinIdx, gx, gy, gw, gh);
        }

        private void DrawLineTooltips(int lineIdx, float gx, float gy, float gw, float gh)
        {
            float columnWidth = gw / _totalRooms;
            float baselineY   = gy + (float)_maxUpwardDeviation / _totalRange * gh;
            float devScale    = gh / _totalRange;

            bool isSob      = lineIdx == _attempts.Count;
            bool isBaseline = lineIdx == _attempts.Count + 1;
            AttemptLine line = isSob ? _sobLine : isBaseline ? null! : _attempts[lineIdx];

            var   s         = SpeebrunConsistencyTrackerModule.Settings;
            bool  isSpecial = isSob || lineIdx >= (_lastIsBest ? _attempts.Count - 1 : _attempts.Count - 2);
            Color lineColor = isBaseline ? Color.Gray
                : isSob
                    ? (_sobIsBest ? s.TrajectoryBestColorFinal : s.TrajectorySobColorFinal)
                    : isSpecial
                        ? (_lastIsBest ? s.TrajectoryBestColorFinal : lineIdx == _attempts.Count - 1 ? s.TrajectoryLastColorFinal : s.TrajectoryBestColorFinal)
                        : Color.White;

            int roomCount = isBaseline ? _totalRooms : line.RoomsCompleted;

            string lineLabel  = isBaseline ? "Avg" : isSob ? "SoB" : $"#{line.ChronologicalIndex}";
            int    labelColR  = (roomCount - 1) / 2; // middle column (0-based): even→3rd, odd→4th

            const float scale = ChartConstants.FontScale.AxisLabelSmall;
            const float bgPad = ChartConstants.Interactivity.TooltipBgPadding;
            float lineH = ActiveFont.Measure("A").Y * scale;
            const float gap   = 4f;
            const float dotR  = 3f;
            const float stemW = 1.5f;

            long cumul = 0;
            for (int r = 0; r < roomCount; r++)
            {
                long roomTime = isBaseline ? _roomAverages[r] : line.RoomTimes[r];
                cumul += roomTime;

                bool   showLabel = r == labelColR;
                string cumulStr  = new TimeTicks(cumul).ToString();
                string roomStr   = new TimeTicks(roomTime).ToString();
                float  dataW     = Math.Max(ActiveFont.Measure(cumulStr).X, ActiveFont.Measure(roomStr).X) * scale;
                float  textW     = showLabel ? Math.Max(dataW, ActiveFont.Measure(lineLabel).X * scale) : dataW;
                float  bgW       = textW + bgPad * 2f;
                float  bgH       = (showLabel ? lineH * 3f : lineH * 2f) + bgPad * 2f;

                float transitionX = gx + (r + 1) * columnWidth;
                float lineY = isBaseline
                    ? baselineY
                    : MathHelper.Clamp(baselineY + line.CumulativeDeviations[r] * devScale, gy, gy + gh);

                float bgX   = transitionX - bgW / 2f;
                bool  above = lineY - bgH - gap - dotR * 2 >= gy;
                float bgY   = above ? lineY - bgH - gap - dotR * 2 : lineY + gap + dotR * 2;

                // Dot
                Draw.Rect(transitionX - dotR, lineY - dotR, dotR * 2, dotR * 2, lineColor);

                // Stem
                float stemTop    = above ? bgY + bgH : lineY + dotR;
                float stemBottom = above ? lineY - dotR : bgY;
                Draw.Line(new Vector2(transitionX, stemTop), new Vector2(transitionX, stemBottom), lineColor, stemW);

                // Tooltip box
                Draw.Rect(bgX, bgY, bgW, bgH, Color.Black * 0.92f);
                float textY = bgY + bgPad;
                if (showLabel)
                {
                    ActiveFont.DrawOutline(lineLabel,
                        new Vector2(bgX + bgPad, textY),
                        Vector2.Zero, Vector2.One * scale, lineColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                    textY += lineH;
                }
                ActiveFont.DrawOutline(cumulStr,
                    new Vector2(bgX + bgPad, textY),
                    Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                ActiveFont.DrawOutline(roomStr,
                    new Vector2(bgX + bgPad, textY + lineH),
                    Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }

        private void DrawComparisonTable()
        {
            if (_mainPinIdx < 0) return;

            bool mainIsSob      = _mainPinIdx == _attempts.Count;
            bool mainIsBaseline = _mainPinIdx == _attempts.Count + 1;
            if (mainIsBaseline) return;

            AttemptLine mainLine  = mainIsSob ? _sobLine : _attempts[_mainPinIdx];
            int mainRoomCount     = mainLine.RoomsCompleted;

            // "Run #n" is always white; SoB uses its line color
            var   sm        = SpeebrunConsistencyTrackerModule.Settings;
            Color mainColor = mainIsSob
                ? (_sobIsBest ? sm.TrajectoryBestColorFinal : sm.TrajectorySobColorFinal)
                : Color.White;

            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;
            float columnWidth = gw / _totalRooms;

            const float scale = ChartConstants.FontScale.AxisLabelMedium;
            const float bgPad = ChartConstants.Interactivity.TooltipBgPadding;
            float lineH = ActiveFont.Measure("A").Y * scale;

            bool refIsAvg = _compPinIdx < 0 || _compPinIdx == _attempts.Count + 1;
            bool refIsSob = _compPinIdx == _attempts.Count;
            AttemptLine refLine = refIsAvg ? null : refIsSob ? _sobLine : _attempts[_compPinIdx];

            string refLabel = refIsAvg ? "vs Avg"
                : refIsSob ? "vs SoB"
                : $"vs #{refLine!.ChronologicalIndex}";

            bool showVsSob = !mainIsSob && !refIsSob;

            // Header rows: 0=run label, 1=ref label, 2=cumul, 3=room, [4=vs SoB label, 5=cumul, 6=room]
            int refHeaderRow    = 1;
            int vsSobHeaderRow  = 4;
            int totalHeaderRows = 4 + (showVsSob ? 3 : 0);

            // Value rows: 0=cumul dev ref, 1=room dev ref, [2=empty, 3=cumul dev SoB, 4=room dev SoB]
            int refValRow    = 0;
            int vsSobValRow  = 3;
            int totalValRows = 2 + (showVsSob ? 3 : 0);

            // Pre-compute widths
            float maxLabelW = 0f, maxValW = 0f;
            string attemptHeader = mainIsSob ? "SoB" : $"Run #{mainLine.ChronologicalIndex}";
            var sectionHeaders = new List<string> { attemptHeader, refLabel, "cumul", "room" };
            if (showVsSob) sectionHeaders.AddRange(["vs SoB", "cumul", "room"]);
            foreach (var ln in sectionHeaders)
                maxLabelW = Math.Max(maxLabelW, ActiveFont.Measure(ln).X * scale);

            for (int r = 0; r < mainRoomCount; r++)
            {
                long roomTime      = mainLine.RoomTimes[r];
                long[] refCumulDevs = refIsAvg ? null! : refLine!.CumulativeDeviations;
                long   refRoomTime  = refIsAvg ? _roomAverages[r]
                    : r < refLine!.RoomsCompleted ? refLine.RoomTimes[r] : 0;
                long refCumulDev = refIsAvg
                    ? mainLine.CumulativeDeviations[r]
                    : mainLine.CumulativeDeviations[r] - (r < refCumulDevs.Length ? refCumulDevs[r] : 0);
                long refRoomDev = refRoomTime > 0 ? roomTime - refRoomTime : 0;
                maxValW = Math.Max(maxValW, ActiveFont.Measure(FormatDev(refCumulDev)).X * scale);
                maxValW = Math.Max(maxValW, ActiveFont.Measure(refRoomTime > 0 || refIsAvg ? FormatDev(refRoomDev) : "n/a").X * scale);

                if (showVsSob)
                {
                    long sobCumulDev = r < _sobLine.CumulativeDeviations.Length ? _sobLine.CumulativeDeviations[r] : 0;
                    long sobRoom     = r < _sobRoomTimes.Length ? _sobRoomTimes[r] : 0;
                    long sobRoomDev  = sobRoom > 0 ? roomTime - sobRoom : 0;
                    maxValW = Math.Max(maxValW, ActiveFont.Measure(FormatDev(mainLine.CumulativeDeviations[r] - sobCumulDev)).X * scale);
                    maxValW = Math.Max(maxValW, ActiveFont.Measure(sobRoom > 0 ? FormatDev(sobRoomDev) : "n/a").X * scale);
                }
            }

            float headerBoxW = maxLabelW;
            float valBoxW    = maxValW;
            float headerBoxH = lineH * totalHeaderRows + bgPad * 2f;
            float valBoxH    = lineH * totalValRows    + bgPad * 2f;
            float headerBoxY = gy + gh - headerBoxH;
            float valBoxY    = gy + gh - valBoxH;

            // Header column (left of chart)
            {
                float headerBoxX = gx - headerBoxW - bgPad * 2f;
                Draw.Rect(headerBoxX - bgPad, headerBoxY, headerBoxW + bgPad * 2f, headerBoxH, Color.Black * 0.92f);
                float ty = headerBoxY + bgPad;
                ActiveFont.DrawOutline(attemptHeader, new Vector2(headerBoxX, ty),
                    Vector2.Zero, Vector2.One * scale, mainColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                ActiveFont.DrawOutline(refLabel, new Vector2(headerBoxX, ty + refHeaderRow * lineH),
                    Vector2.Zero, Vector2.One * scale, Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
                if (showVsSob)
                    ActiveFont.DrawOutline("vs SoB", new Vector2(headerBoxX, ty + vsSobHeaderRow * lineH),
                        Vector2.Zero, Vector2.One * scale, Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            // Per-column value boxes
            for (int r = 0; r < mainRoomCount; r++)
            {
                long roomTime  = mainLine.RoomTimes[r];
                float colMidX = gx + (r + 0.5f) * columnWidth;
                float boxX    = colMidX - valBoxW / 2f;
                Draw.Rect(boxX - bgPad, valBoxY, valBoxW + bgPad * 2f, valBoxH, Color.Black * 0.92f);

                float ty = valBoxY + bgPad;

                long[] refCumulDevs = refIsAvg ? null! : refLine!.CumulativeDeviations;
                long   refRoomTime  = refIsAvg ? _roomAverages[r]
                    : r < refLine!.RoomsCompleted ? refLine.RoomTimes[r] : 0;
                long refCumulDev = refIsAvg
                    ? mainLine.CumulativeDeviations[r]
                    : mainLine.CumulativeDeviations[r] - (r < refCumulDevs.Length ? refCumulDevs[r] : 0);
                long refRoomDev      = refRoomTime > 0 ? roomTime - refRoomTime : 0;
                bool refRoomAvailable = refIsAvg || refRoomTime > 0;
                Color cRefColor = DeviationColor(refCumulDev, refRoomAvailable ? refRoomDev : 0);
                Color rRefColor = refRoomDev <= 0 ? ChartConstants.Colors.AheadGaining : ChartConstants.Colors.BehindLosing;
                ActiveFont.DrawOutline(FormatDev(refCumulDev),
                    new Vector2(boxX, ty + refValRow * lineH),
                    Vector2.Zero, Vector2.One * scale, cRefColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                ActiveFont.DrawOutline(refRoomAvailable ? FormatDev(refRoomDev) : "n/a",
                    new Vector2(boxX, ty + (refValRow + 1) * lineH),
                    Vector2.Zero, Vector2.One * scale, refRoomAvailable ? rRefColor : Color.Gray, ChartConstants.Stroke.OutlineSize, Color.Black);

                if (showVsSob)
                {
                    long cumulDev    = mainLine.CumulativeDeviations[r];
                    long sobCumulDev = r < _sobLine.CumulativeDeviations.Length ? _sobLine.CumulativeDeviations[r] : 0;
                    long sobRoom     = r < _sobRoomTimes.Length ? _sobRoomTimes[r] : 0;
                    long sobRoomDev  = sobRoom > 0 ? roomTime - sobRoom : 0;
                    long vsCumul     = cumulDev - sobCumulDev;
                    Color cColor = DeviationColor(vsCumul, sobRoom > 0 ? sobRoomDev : 0);
                    Color rColor = sobRoomDev <= 0 ? ChartConstants.Colors.AheadGaining : ChartConstants.Colors.BehindLosing;
                    ActiveFont.DrawOutline(FormatDev(vsCumul),
                        new Vector2(boxX, ty + vsSobValRow * lineH),
                        Vector2.Zero, Vector2.One * scale, cColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                    ActiveFont.DrawOutline(sobRoom > 0 ? FormatDev(sobRoomDev) : "n/a",
                        new Vector2(boxX, ty + (vsSobValRow + 1) * lineH),
                        Vector2.Zero, Vector2.One * scale, sobRoom > 0 ? rColor : Color.Gray, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }
        }

        // Returns the LiveSplit delta color for a deviation value.
        // cumulDev: cumulative deviation (negative = ahead, positive = behind)
        // roomDev:  per-room deviation  (negative = gained time, positive = lost time)
        private static Color DeviationColor(long cumulDev, long roomDev)
        {
            bool ahead     = cumulDev <= 0;
            bool gainedRoom = roomDev <= 0;
            return (ahead, gainedRoom) switch
            {
                (true,  true)  => ChartConstants.Colors.AheadGaining,
                (true,  false) => ChartConstants.Colors.AheadLosing,
                (false, true)  => ChartConstants.Colors.BehindGaining,
                (false, false) => ChartConstants.Colors.BehindLosing,
            };
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

            DrawXAxisStaggeredLabels(x, y, h, _totalRooms, columnWidth, r => $"R{r + 1}", Color.LightGray);

            float aboveHeight = (float)_maxUpwardDeviation / _totalRange * h;
            float belowHeight = (float)_maxDownwardDeviation / _totalRange * h;
            int totalTicks  = ChartConstants.Trajectory.TotalYTicks;
            int ticksAbove  = Math.Max(1, (int)Math.Round((double)aboveHeight / h * totalTicks));
            int ticksBelow  = Math.Max(1, totalTicks - ticksAbove);

            DrawYTicks(x, y, baselineY, aboveHeight, _maxUpwardDeviation, ticksAbove, true);
            DrawYBaseline(x, baselineY);
            DrawYTicks(x, y, baselineY, belowHeight, _maxDownwardDeviation, ticksBelow, false);

            DrawRightAxisLabels(x, y, w, h, baselineY);

            var s3 = SpeebrunConsistencyTrackerModule.Settings;
            Color sobLegendColor  = s3.TrajectorySobColorFinal;
            Color bestLegendColor = s3.TrajectoryBestColorFinal;
            Color lastLegendColor = s3.TrajectoryLastColorFinal;

            float legendY2 = y + h + ChartConstants.Legend.LegendOffsetY;
            float legendX2 = x + w;
            float offset2  = 0f;

            if (_sobIsBest && _lastIsBest)
            {
                // Single entry for all three
                string label3 = "SoB, Best & Last run";
                Color[] cols3 = [sobLegendColor, bestLegendColor, lastLegendColor];
                DrawStripedLegendEntry(legendX2, legendY2, label3, cols3, ChartConstants.FontScale.AxisLabel, right: true);
            }
            else if (_sobIsBest)
            {
                // SoB+Best share one entry; Last is separate
                string lastLabel2 = "Last run";
                DrawLegendEntry(legendX2, legendY2, lastLabel2, lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 = ActiveFont.Measure(lastLabel2).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                string sobBestLabel = "SoB & Best run";
                Color[] colsSB = [sobLegendColor, bestLegendColor];
                DrawStripedLegendEntry(legendX2 - offset2, legendY2, sobBestLabel, colsSB, ChartConstants.FontScale.AxisLabel, right: true);
            }
            else if (_lastIsBest)
            {
                // Best+Last share one entry; SoB is separate
                string bestLastLabel = "Best & Last run";
                Color[] colsBL = [bestLegendColor, lastLegendColor];
                DrawStripedLegendEntry(legendX2, legendY2, bestLastLabel, colsBL, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 = ActiveFont.Measure(bestLastLabel).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                DrawLegendEntry(legendX2 - offset2, legendY2, "SoB", sobLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
            }
            else
            {
                // No coincidence — original single-color entries
                string lastLabel3 = "Last run";
                DrawLegendEntry(legendX2, legendY2, lastLabel3, lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 = ActiveFont.Measure(lastLabel3).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                DrawLegendEntry(legendX2 - offset2, legendY2, "Best run", bestLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 += ActiveFont.Measure("Best run").X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                DrawLegendEntry(legendX2 - offset2, legendY2, "SoB", sobLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
            }

            string stats = _attempts.Count == 1 ? "1 Attempt" : $"{_attempts.Count} Attempts";
            Vector2 statsSize = ActiveFont.Measure(stats) * ChartConstants.FontScale.AxisLabelMedium;
            ActiveFont.DrawOutline(stats,
                new Vector2(position.X + width / 2 - statsSize.X / 2, y + h + ChartConstants.Legend.LegendOffsetY),
                Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelMedium,
                Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        private static void DrawYTicks(float x, float y, float baselineY, float sideHeight, long maxDeviation, int tickCount, bool above)
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
            float devScale    = h / _totalRange;
            float labelHeight = ActiveFont.Measure("0").Y * ChartConstants.FontScale.AxisLabelSmall;
            float minSpacing  = labelHeight + ChartConstants.Trajectory.LabelMinSpacingExtra;
            float rightX      = x + w + ChartConstants.Trajectory.RightLabelMarginX;
            var   s4          = SpeebrunConsistencyTrackerModule.Settings;
            Color sobColor4   = s4.TrajectorySobColorFinal;
            Color bestColor4  = s4.TrajectoryBestColorFinal;
            Color lastColor4  = s4.TrajectoryLastColorFinal;


            // Build label list in priority order: Avg → Best → SoB → Last
            // Coincident cases merge entries; colors[] has >1 element when lines coincide.
            // skip=true when the hovered line already draws its own right-axis label in DrawHighlight.
            int n = _attempts.Count;
            bool hovBaseline = _hoveredLineIdx == n + 1;
            bool hovSob      = _hoveredLineIdx == n;
            bool hovLast     = _hoveredLineIdx == n - 1;
            bool hovBest     = !_lastIsBest && _hoveredLineIdx == n - 2;

            var labelList = new List<(float yPos, string text, Color[] colors, bool skip)>();

            Color[] avgColors  = [Color.Gray];
            Color[] bestColors = [bestColor4];
            Color[] sobColors  = [sobColor4];
            Color[] lastColors = [lastColor4];

            if (_anyCompleted)
                labelList.Add((baselineY, new TimeTicks(_roomAveragesSum).ToString(), avgColors, hovBaseline));

            if (_sobIsBest && _lastIsBest)
            {
                if (_sobReachesEnd)
                    labelList.Add((baselineY + _bestFinalDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum + _bestFinalDeviation).ToString(),
                                   [sobColor4, bestColor4, lastColor4], hovSob || hovLast));
            }
            else if (_sobIsBest)
            {
                if (_sobReachesEnd)
                    labelList.Add((baselineY + _bestFinalDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum + _bestFinalDeviation).ToString(),
                                   [sobColor4, bestColor4], hovSob));
                if (_lastReachesEnd)
                    labelList.Add((baselineY + _lastFinalDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum + _lastFinalDeviation).ToString(),
                                   lastColors, hovLast));
            }
            else if (_lastIsBest)
            {
                if (_anyCompleted)
                    labelList.Add((baselineY + _bestFinalDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum + _bestFinalDeviation).ToString(),
                                   [bestColor4, lastColor4], hovLast));
                if (_sobReachesEnd)
                    labelList.Add((baselineY - _maxUpwardDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum - _maxUpwardDeviation).ToString(),
                                   sobColors, hovSob));
            }
            else
            {
                if (_anyCompleted)
                    labelList.Add((baselineY + _bestFinalDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum + _bestFinalDeviation).ToString(),
                                   bestColors, hovBest));
                if (_sobReachesEnd)
                    labelList.Add((baselineY - _maxUpwardDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum - _maxUpwardDeviation).ToString(),
                                   sobColors, hovSob));
                if (_lastReachesEnd)
                    labelList.Add((baselineY + _lastFinalDeviation * devScale,
                                   new TimeTicks(_roomAveragesSum + _lastFinalDeviation).ToString(),
                                   lastColors, hovLast));
            }

            var labels = labelList.ToArray();
            float[] nudged = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++) nudged[i] = labels[i].yPos;

            for (int pass = 0; pass < ChartConstants.Trajectory.MaxNudgePasses; pass++)
            {
                bool anyNudged = false;
                for (int i = 1; i < nudged.Length; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        float diff = nudged[i] - nudged[j];
                        if (Math.Abs(diff) < minSpacing)
                        {
                            nudged[i] = nudged[j] + (diff >= 0 ? minSpacing : -minSpacing);
                            anyNudged = true;
                        }
                    }
                }
                if (!anyNudged) break;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                var (labelYPos, labelText, labelColors, skip) = labels[i];
                if (skip) continue;
                Vector2 labelSize = ActiveFont.Measure(labelText) * ChartConstants.FontScale.AxisLabelSmall;
                if (labelYPos < y - labelSize.Y / 2 || labelYPos > y + h + labelSize.Y / 2) continue;

                // Coincident lines use white; single-color entries use their own color
                Color drawColor = labelColors.Length > 1 ? Color.White : labelColors[0];
                ActiveFont.DrawOutline(labelText,
                    new Vector2(rightX, nudged[i] - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                    drawColor, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }
    }
}
