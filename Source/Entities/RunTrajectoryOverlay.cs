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

        // All attempts in chronological order. _attempts[^1] is always the last attempt.
        private readonly List<AttemptLine> _attempts;
        private readonly AttemptLine       _sobLine;
        private readonly long[]            _roomAverages;   // per-room average ticks
        private readonly long[]            _sobRoomTimes;   // per-room SoB ticks (length = _totalRooms, 0 if no data)
        private readonly int[]             _bestSoFarIdx;   // per-room index into _attempts of best-so-far run

        // Cached fields — all recomputed by RecomputeCache() whenever _lastVisibleRoom changes.
        private int  _lastVisibleRoom     = -1;
        private int  _bestIdx             = -1;   // index into _attempts of best attempt up to _lastVisibleRoom
        private bool _lastIsBest;                 // _bestIdx == _attempts.Count - 1
        private bool _sobIsBest;                  // SoB dev == best dev at _lastVisibleRoom
        private bool _anyCompleted;               // any attempt reaches beyond _lastVisibleRoom
        private bool _sobReachesEnd;              // SoB reaches beyond _lastVisibleRoom
        private bool _lastReachesEnd;             // _attempts[^1] reaches beyond _lastVisibleRoom
        private long _roomAveragesSum;            // sum of _roomAverages[0.._lastVisibleRoom] inclusive
        private long _maxUpwardDeviation;         // max magnitude of negative cumulative dev up to _lastVisibleRoom (min 1)
        private long _maxDownwardDeviation;       // max magnitude of positive cumulative dev up to _lastVisibleRoom (min 1)
        private long _totalRange;                 // _maxUpwardDeviation + _maxDownwardDeviation

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
                _attempts     = [];
                _sobLine      = new AttemptLine([], [], 0, 0);
                _roomAverages = [];
                _sobRoomTimes = [];
                _bestSoFarIdx = [];
                RecomputeCache();
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

            // Build attempts in chronological order (flat — no reserved best slot)
            _attempts = [.. attempts.Select((a, chronoIdx) =>
            {
                long cumulative = 0;
                var deviations  = new List<long>();
                var roomTks     = new List<long>();
                for (int r = 0; r < a.Count && r < _totalRooms; r++)
                {
                    long t = a.GetRoomTime(r).Ticks;
                    roomTks.Add(t);
                    cumulative += t - _roomAverages[r];
                    deviations.Add(cumulative);
                }
                return new AttemptLine([.. deviations], [.. roomTks], deviations.Count, chronoIdx + 1);
            })];

            // Per-room best-so-far: index of attempt with lowest CumulativeDeviations[r] among those reaching r
            _bestSoFarIdx = new int[_totalRooms];
            for (int r = 0; r < _totalRooms; r++)
            {
                int bestI = -1;
                long bestDev = long.MaxValue;
                for (int i = 0; i < _attempts.Count - 1; i++)
                {
                    if (_attempts[i].RoomsCompleted <= r) continue;
                    if (_attempts[i].CumulativeDeviations[r] < bestDev)
                    {
                        bestDev = _attempts[i].CumulativeDeviations[r];
                        bestI   = i;
                    }
                }
                _bestSoFarIdx[r] = bestI; // -1 if no attempt reaches r
            }

            // SoB line
            long sobCumulative    = 0;
            var  sobDeviations    = new long[_totalRooms];
            _sobRoomTimes         = new long[_totalRooms];
            int  sobRoomsCompleted = 0;
            for (int r = 0; r < _totalRooms; r++)
            {
                var times = r < roomTimes.Count ? roomTimes[r] : [];
                if (times.Count == 0) break;
                long best         = times.Min(t => t.Ticks);
                _sobRoomTimes[r]  = best;
                sobCumulative    += best - _roomAverages[r];
                sobDeviations[r]  = sobCumulative;
                sobRoomsCompleted = r + 1;
            }
            _sobLine = new AttemptLine(sobDeviations, _sobRoomTimes[..sobRoomsCompleted], sobRoomsCompleted, 0);

            RecomputeCache();
        }

        private void RecomputeCache()
        {
            // Recompute _lastVisibleRoom
            int lastVis = -1;
            for (int r = _totalRooms - 1; r >= 0; r--)
                if (!_hiddenColumns.Contains(r)) { lastVis = r; break; }
            _lastVisibleRoom = lastVis;

            if (lastVis < 0 || _attempts.Count == 0)
            {
                _bestIdx              = _attempts.Count > 0 ? _attempts.Count - 1 : -1;
                _lastIsBest           = _bestIdx == _attempts.Count - 1;
                _sobIsBest            = false;
                _anyCompleted         = false;
                _sobReachesEnd        = false;
                _lastReachesEnd       = false;
                _roomAveragesSum      = 0;
                _maxUpwardDeviation   = 1;
                _maxDownwardDeviation = 1;
                _totalRange           = 2;
                return;
            }

            // --- Best attempt selection ---
            // Among attempts that reached lastVis, pick lowest cumulative deviation at lastVis.
            // If none reached lastVis, pick furthest reached, ties broken by lowest final deviation.
            int best = -1;
            {
                var reached = Enumerable.Range(0, _attempts.Count)
                    .Where(i => _attempts[i].RoomsCompleted > lastVis)
                    .ToList();
                if (reached.Count > 0)
                {
                    best = reached.OrderBy(i => _attempts[i].CumulativeDeviations[lastVis]).First();
                }
                else
                {
                    best = Enumerable.Range(0, _attempts.Count)
                        .OrderByDescending(i => _attempts[i].RoomsCompleted)
                        .ThenBy(i => _attempts[i].RoomsCompleted > 0 ? _attempts[i].CumulativeDeviations[_attempts[i].RoomsCompleted - 1] : 0)
                        .First();
                }
            }
            _bestIdx    = best;
            _lastIsBest = _bestIdx == _attempts.Count - 1;

            // --- Coincidence flags ---
            long bestDev = DevAtRoom(_attempts[_bestIdx], lastVis);
            long sobDev  = DevAtRoom(_sobLine,            lastVis);
            _sobIsBest  = sobDev == bestDev;

            // --- Reach flags ---
            _anyCompleted   = _attempts.Any(a => a.RoomsCompleted > lastVis);
            _sobReachesEnd  = _sobLine.RoomsCompleted > lastVis;
            _lastReachesEnd = _attempts[^1].RoomsCompleted > lastVis;

            // --- Room averages sum ---
            _roomAveragesSum = 0;
            for (int r = 0; r <= lastVis; r++) _roomAveragesSum += _roomAverages[r];

            // --- Axis range ---
            // Hidden rooms in the middle still contribute cumulative deviation;
            // rooms beyond lastVis are excluded entirely.
            long maxUp   = 1;
            long maxDown = 1;
            foreach (var attempt in _attempts)
            {
                int limit = Math.Min(attempt.RoomsCompleted - 1, lastVis);
                for (int r = 0; r <= limit; r++)
                {
                    long d = attempt.CumulativeDeviations[r];
                    if (d < 0) maxUp   = Math.Max(maxUp,  -d);
                    else       maxDown = Math.Max(maxDown,  d);
                }
            }
            {
                int limit = Math.Min(_sobLine.RoomsCompleted - 1, lastVis);
                for (int r = 0; r <= limit; r++)
                {
                    long d = _sobLine.CumulativeDeviations[r];
                    if (d < 0) maxUp   = Math.Max(maxUp,  -d);
                    else       maxDown = Math.Max(maxDown,  d);
                }
            }
            _maxUpwardDeviation   = maxUp;
            _maxDownwardDeviation = maxDown;
            _totalRange           = maxUp + maxDown;
        }

        public override void ClearHiddenColumns()
        {
            base.ClearHiddenColumns();
            int newLastVisible = _totalRooms - 1; // after clearing, last room is always visible
            if (newLastVisible != _lastVisibleRoom)
                RecomputeCache();
        }

        public override void ToggleColumn(int columnIndex)
        {
            base.ToggleColumn(columnIndex);
            // Recompute only if the last visible room changed
            int newLastVisible = -1;
            for (int r = _totalRooms - 1; r >= 0; r--)
                if (!_hiddenColumns.Contains(r)) { newLastVisible = r; break; }
            if (newLastVisible != _lastVisibleRoom)
                RecomputeCache();
        }

        // Returns the highest room index that is not hidden, capped at _totalRooms-1. -1 if all hidden.
        private int LastVisibleRoom()
        {
            for (int r = _totalRooms - 1; r >= 0; r--)
                if (!_hiddenColumns.Contains(r)) return r;
            return -1;
        }

        private float ComputeNormalColumnWidth(float gw)
        {
            int visibleCount = _totalRooms - _hiddenColumns.Count;
            if (visibleCount <= 0) return gw / Math.Max(_totalRooms, 1);
            float available = gw - _hiddenColumns.Count * ChartConstants.Interactivity.HiddenColumnStubWidth;
            return available / visibleCount;
        }

        private float GetRoomCenterX(float gx, float gw, int r)
        {
            float normalW = ComputeNormalColumnWidth(gw);
            float x = gx;
            for (int j = 0; j < r; j++)
                x += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            float thisW = _hiddenColumns.Contains(r) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            return x + thisW * 0.5f;
        }

        // Returns the X at the right edge of room r's column.
        private float GetRoomRightEdgeX(float gx, float gw, int r)
        {
            float normalW = ComputeNormalColumnWidth(gw);
            float x = gx;
            for (int j = 0; j <= r; j++)
                x += _hiddenColumns.Contains(j) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            return x;
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

            float normalW = ComputeNormalColumnWidth(w);
            float colX = x;
            for (int r = 0; r < _totalRooms; r++)
            {
                Draw.Line(new Vector2(colX, y), new Vector2(colX, y + h), ChartConstants.Colors.GridLineColor, 1f);
                colX += _hiddenColumns.Contains(r) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
            }

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
            float minSpacing  = ActiveFont.LineHeight * ChartConstants.FontScale.AxisLabelSmall * 1.1f;
            float chartBottom = baselineY + sideHeight; // only meaningful when !above
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
        /// is not one of the "active" lines (hovered, main pin, or comp pin).
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
                if (lineIdx == _mainPinIdx) return false; // main pin always lit
                if (lineIdx == _compPinIdx) return false; // comp pin always lit (or -1 = none)
                if (_compPinIdx < 0 && lineIdx == _attempts.Count) return false; // default comp is SoB
            }

            // If only hovering (no comparison mode), hovered line is handled above; all others dim.
            // If comparison mode, any line not matching the exemptions above is dimmed.
            return true;
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_attempts.Count == 0) return;

            float baselineY = y + (float)_maxUpwardDeviation / _totalRange * h;
            float devScale  = h / _totalRange;
            int   total     = _attempts.Count;
            bool  hovering  = _hoveredLineIdx >= 0;
            var   s         = SpeebrunConsistencyTrackerModule.Settings;

            // Regular lines: all attempts except _bestIdx and last (^1).
            // If _lastIsBest, there is only one special slot (^1); otherwise two (_bestIdx and ^1).
            for (int i = 0; i < total; i++)
            {
                if (i == _bestIdx || i == total - 1) continue;

                bool  isHovered = hovering && _hoveredLineIdx == i;
                bool  dimmed    = IsLineDimmed(i);
                float thickness;
                Color color;
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
                DrawAttemptLine(_attempts[i], x, w, baselineY, devScale, h, color, thickness);
            }

            // Special lines: SoB → Best → Last (drawn on top of regulars, in this order).
            Color sobColor  = s.TrajectorySobColorFinal;
            Color bestColor = s.TrajectoryBestColorFinal;
            Color lastColor = s.TrajectoryLastColorFinal;
            float specialThick = 2f;

            int sobIdx  = total;       // logical index for SoB in hover/pin system
            int bestIdx = _bestIdx;
            int lastIdx = total - 1;

            if (_sobIsBest && _lastIsBest)
            {
                // SoB == Best == Last: single dashed triple-color line
                bool  h3      = hovering && (_hoveredLineIdx == sobIdx || _hoveredLineIdx >= lastIdx);
                bool  pinned3 = IsLinePinned(sobIdx) || IsLinePinned(lastIdx);
                bool  dimmed3 = IsLineDimmed(sobIdx) && IsLineDimmed(lastIdx);
                float t3      = h3 || pinned3 ? 3f : specialThick;
                Color[] c3Draw = dimmed3
                    ? [sobColor * 0.35f, bestColor * 0.35f, lastColor * 0.35f]
                    : [sobColor, bestColor, lastColor];
                DrawDashedAttemptLine(_sobLine, c3Draw, x, w, baselineY, devScale, h, t3);
            }
            else if (_sobIsBest)
            {
                // SoB == Best: dashed SoB/Best line, separate Last line
                bool  hSB      = hovering && _hoveredLineIdx == sobIdx;
                bool  pinnedSB = IsLinePinned(sobIdx);
                bool  dimmedSB = IsLineDimmed(sobIdx);
                float tSB      = hSB || pinnedSB ? 3f : specialThick;
                Color[] cSBDraw = dimmedSB ? [sobColor * 0.35f, bestColor * 0.35f] : [sobColor, bestColor];
                DrawDashedAttemptLine(_sobLine, cSBDraw, x, w, baselineY, devScale, h, tSB);

                bool  hL      = hovering && _hoveredLineIdx == lastIdx;
                bool  pinnedL = IsLinePinned(lastIdx);
                bool  dimmedL = IsLineDimmed(lastIdx);
                float tL      = hL || pinnedL ? 3f : specialThick;
                Color cLDraw  = dimmedL ? lastColor * 0.35f : lastColor;
                DrawAttemptLine(_attempts[lastIdx], x, w, baselineY, devScale, h, cLDraw, tL);
            }
            else if (_lastIsBest)
            {
                // Best == Last: solid SoB, dashed Best/Last line
                bool  hSob      = hovering && _hoveredLineIdx == sobIdx;
                bool  pinnedSob = IsLinePinned(sobIdx);
                bool  dimmedSob = IsLineDimmed(sobIdx);
                float tSob      = hSob || pinnedSob ? 3f : specialThick;
                Color cSobDraw  = dimmedSob ? sobColor * 0.35f : sobColor;
                DrawAttemptLine(_sobLine, x, w, baselineY, devScale, h, cSobDraw, tSob);

                bool  hBL      = hovering && _hoveredLineIdx == lastIdx;
                bool  pinnedBL = IsLinePinned(lastIdx);
                bool  dimmedBL = IsLineDimmed(lastIdx);
                float tBL      = hBL || pinnedBL ? 3f : specialThick;
                Color[] cBLDraw = dimmedBL ? [bestColor * 0.35f, lastColor * 0.35f] : [bestColor, lastColor];
                DrawDashedAttemptLine(_attempts[lastIdx], cBLDraw, x, w, baselineY, devScale, h, tBL);
            }
            else
            {
                // No coincidence: solid SoB, solid Best, solid Last
                bool  hSob      = hovering && _hoveredLineIdx == sobIdx;
                bool  pinnedSob = IsLinePinned(sobIdx);
                bool  dimmedSob = IsLineDimmed(sobIdx);
                float tSob      = hSob || pinnedSob ? 3f : specialThick;
                Color cSobDraw  = dimmedSob ? sobColor * 0.35f : sobColor;
                DrawAttemptLine(_sobLine, x, w, baselineY, devScale, h, cSobDraw, tSob);

                bool  hBest      = hovering && _hoveredLineIdx == bestIdx;
                bool  pinnedBest = IsLinePinned(bestIdx);
                bool  dimmedBest = IsLineDimmed(bestIdx);
                float tBest      = hBest || pinnedBest ? 3f : specialThick;
                Color cBestDraw  = dimmedBest ? bestColor * 0.35f : bestColor;
                DrawAttemptLine(_attempts[bestIdx], x, w, baselineY, devScale, h, cBestDraw, tBest);

                bool  hLast      = hovering && _hoveredLineIdx == lastIdx;
                bool  pinnedLast = IsLinePinned(lastIdx);
                bool  dimmedLast = IsLineDimmed(lastIdx);
                float tLast      = hLast || pinnedLast ? 3f : specialThick;
                Color cLastDraw  = dimmedLast ? lastColor * 0.35f : lastColor;
                DrawAttemptLine(_attempts[lastIdx], x, w, baselineY, devScale, h, cLastDraw, tLast);
            }
        }

        private void DrawAttemptLine(AttemptLine attempt, float gx, float gw, float baselineY, float devScale, float h, Color color, float thickness)
        {
            // Edge-based model: each visible room r draws from its left edge (deviation[r-1]) to its right edge (deviation[r]).
            // When rooms are hidden, the next visible room connects from the right edge of prevVisible (deviation[prevVisible]).
            // Stop at the last visible room — rooms beyond it are not drawn.
            int lastVisible = LastVisibleRoom();
            int prevVisible = -1;
            int limit = Math.Min(attempt.RoomsCompleted - 1, lastVisible);
            for (int r = 0; r <= limit; r++)
            {
                if (_hiddenColumns.Contains(r)) continue;
                float x1 = prevVisible < 0 ? gx : GetRoomRightEdgeX(gx, gw, prevVisible);
                float y1 = prevVisible < 0
                    ? baselineY
                    : MathHelper.Clamp(baselineY + attempt.CumulativeDeviations[prevVisible] * devScale, baselineY - h, baselineY + h);
                float x2 = GetRoomRightEdgeX(gx, gw, r);
                float y2 = MathHelper.Clamp(baselineY + attempt.CumulativeDeviations[r] * devScale, baselineY - h, baselineY + h);
                Draw.Line(new Vector2(x1, y1), new Vector2(x2, y2), color, thickness);
                prevVisible = r;
            }
        }

        /// <summary>
        /// Draws an attempt line with a cycling dash pattern across all segments.
        /// colors.Length colors repeat in order; dash length is CoincidentDashLen pixels.
        /// Dash positions are computed in global path space so they never gap at segment boundaries.
        /// Hidden columns are skipped (the line connects adjacent visible rooms directly).
        /// </summary>
        private void DrawDashedAttemptLine(
            AttemptLine attempt, Color[] colors,
            float gx, float gw, float baselineY, float devScale, float h, float thickness)
        {
            float dashLen  = ChartConstants.Trajectory.CoincidentDashLen;
            float cycleLen = dashLen * colors.Length;
            float globalStart = 0f; // global path offset at the start of current segment

            int lastVisible2 = LastVisibleRoom();
            int prevVisible = -1;
            int limit2 = Math.Min(attempt.RoomsCompleted - 1, lastVisible2);
            for (int r = 0; r <= limit2; r++)
            {
                if (_hiddenColumns.Contains(r)) continue;

                float x1 = prevVisible < 0 ? gx : GetRoomRightEdgeX(gx, gw, prevVisible);
                float y1 = prevVisible < 0
                    ? baselineY
                    : MathHelper.Clamp(baselineY + attempt.CumulativeDeviations[prevVisible] * devScale, baselineY - h, baselineY + h);
                float x2 = GetRoomRightEdgeX(gx, gw, r);
                float y2 = MathHelper.Clamp(baselineY + attempt.CumulativeDeviations[r] * devScale, baselineY - h, baselineY + h);

                var   segStart = new Vector2(x1, y1);
                var   segEnd   = new Vector2(x2, y2);
                float segLen   = Vector2.Distance(segStart, segEnd);
                if (segLen < 0.5f) { globalStart += segLen; prevVisible = r; continue; }

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
                prevVisible = r;
            }
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
            for (int r = 0; r < _totalRooms; r++)
            {
                float colW = _hiddenColumns.Contains(r) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
                var (stripX, stripW) = ColumnStripRect(colX, colW);
                if (mousePos.X >= stripX && mousePos.X < stripX + stripW) { _hoveredColumnIndex = r; return r; }
                colX += colW;
            }
            _hoveredColumnIndex = -1;
            return null;
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

            float baselineY   = gy + (float)_maxUpwardDeviation / _totalRange * gh;
            float devScale    = gh / _totalRange;

            // Which column is the mouse in? Walk variable-width columns.
            int col = _totalRooms - 1;
            {
                float normalW = ComputeNormalColumnWidth(gw);
                float colX = gx;
                for (int r = 0; r < _totalRooms; r++)
                {
                    float colW = _hiddenColumns.Contains(r) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW;
                    if (mouseHudPos.X < colX + colW) { col = r; break; }
                    colX += colW;
                }
            }

            // For each line, lerp the Y at mouseX within this column's segment
            float mouseX  = mouseHudPos.X;
            float mouseY  = mouseHudPos.Y;
            float bestDist = float.MaxValue;
            int   bestIdx  = -1;

            void Check(AttemptLine line, int idx)
            {
                if (col >= line.RoomsCompleted) return;
                if (_hiddenColumns.Contains(col)) return;
                // Mirror DrawAttemptLine: edge-based model.
                // Find prev visible room (or use left grid edge).
                int prev = -1;
                for (int j = col - 1; j >= 0; j--)
                    if (!_hiddenColumns.Contains(j)) { prev = j; break; }
                float x1 = prev < 0 ? gx : GetRoomRightEdgeX(gx, gw, prev);
                float y1 = prev < 0
                    ? baselineY
                    : MathHelper.Clamp(baselineY + line.CumulativeDeviations[prev] * devScale, baselineY - gh, baselineY + gh);
                float x2 = GetRoomRightEdgeX(gx, gw, col);
                float y2 = MathHelper.Clamp(baselineY + line.CumulativeDeviations[col] * devScale, baselineY - gh, baselineY + gh);

                float segW = x2 - x1;
                float t       = segW > 0 ? (mouseX - x1) / segW : 0f;
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
            // Skip if the hovered line is the main pin — DrawPinnedHighlights already drew its tooltip.
            // Comp pin (secondary) does not get a persistent tooltip, so show it on hover.
            if (_hoveredLineIdx == _mainPinIdx) return;
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
        }

        private void DrawLineTooltips(int lineIdx, float gx, float gy, float gw, float gh)
        {
            float baselineY   = gy + (float)_maxUpwardDeviation / _totalRange * gh;
            float devScale    = gh / _totalRange;

            bool isSob      = lineIdx == _attempts.Count;
            bool isBaseline = lineIdx == _attempts.Count + 1;
            AttemptLine line = isSob ? _sobLine : isBaseline ? null! : _attempts[lineIdx];

            var   s         = SpeebrunConsistencyTrackerModule.Settings;
            int roomCount = isBaseline ? _totalRooms : line.RoomsCompleted;
            // Cap at last visible room so tooltips don't appear beyond it
            int lastVis = LastVisibleRoom();
            if (lastVis < 0) return;
            int effectiveCount = Math.Min(roomCount, lastVis + 1);

            bool  isLast    = lineIdx == _attempts.Count - 1;
            bool  isBest    = lineIdx == _bestIdx;
            bool  isSpecial = isSob || isBest || isLast;
            Color lineColor = isBaseline ? Color.Gray
                : isSob
                    ? (_sobIsBest ? s.TrajectoryBestColorFinal : s.TrajectorySobColorFinal)
                : isBest && isLast
                    ? s.TrajectoryBestColorFinal
                : isBest
                    ? s.TrajectoryBestColorFinal
                : isLast
                    ? s.TrajectoryLastColorFinal
                : Color.White;

            string lineLabel = isBaseline ? "Avg" : isSob ? "SoB" : $"#{line.ChronologicalIndex}";
            // Pick the middle visible room for the label — count visible rooms, then walk to the midpoint
            int visibleCount = 0;
            for (int r = 0; r < effectiveCount; r++)
                if (!_hiddenColumns.Contains(r)) visibleCount++;
            int labelColR = -1;
            if (visibleCount > 0)
            {
                int target = (visibleCount - 1) / 2, seen = 0;
                for (int r = 0; r < effectiveCount; r++)
                {
                    if (_hiddenColumns.Contains(r)) continue;
                    if (seen++ == target) { labelColR = r; break; }
                }
            }

            const float scale = ChartConstants.FontScale.AxisLabelSmall;
            const float bgPad = ChartConstants.Interactivity.TooltipBgPadding;
            float lineH = ActiveFont.Measure("A").Y * scale;
            const float gap   = 4f;
            const float dotR  = 3f;
            const float stemW = 1.5f;

            long cumul = 0;
            for (int r = 0; r < effectiveCount; r++)
            {
                if (_hiddenColumns.Contains(r)) { cumul += isBaseline ? _roomAverages[r] : (r < line.RoomTimes.Length ? line.RoomTimes[r] : 0); continue; }
                long roomTime = isBaseline ? _roomAverages[r] : line.RoomTimes[r];
                cumul += roomTime;

                bool   showLabel = r == labelColR;
                string cumulStr  = new TimeTicks(cumul).ToString();
                string roomStr   = new TimeTicks(roomTime).ToString();
                float  dataW     = Math.Max(ActiveFont.Measure(cumulStr).X, ActiveFont.Measure(roomStr).X) * scale;
                float  textW     = showLabel ? Math.Max(dataW, ActiveFont.Measure(lineLabel).X * scale) : dataW;
                float  bgW       = textW + bgPad * 2f;
                float  bgH       = (showLabel ? lineH * 3f : lineH * 2f) + bgPad * 2f;

                float transitionX = GetRoomRightEdgeX(gx, gw, r);
                float lineY = isBaseline
                    ? baselineY
                    : MathHelper.Clamp(baselineY + line.CumulativeDeviations[r] * devScale, gy, gy + gh);

                float bgX   = transitionX - bgW / 2f;
                bool  above = lineY - bgH - gap - dotR * 2 >= gy;
                float bgY   = above ? lineY - bgH - gap - dotR * 2 : lineY + gap + dotR * 2;

                // Tooltip box (drawn first so stem and dot appear on top)
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
                    Vector2.Zero, Vector2.One * scale, lineColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                ActiveFont.DrawOutline(roomStr,
                    new Vector2(bgX + bgPad, textY + lineH),
                    Vector2.Zero, Vector2.One * scale, lineColor, ChartConstants.Stroke.OutlineSize, Color.Black);

                // Stem
                float stemTop    = above ? bgY + bgH : lineY + dotR;
                float stemBottom = above ? lineY - dotR : bgY;
                Draw.Line(new Vector2(transitionX, stemTop), new Vector2(transitionX, stemBottom), lineColor, stemW);

                // Dot (drawn last so it's always on top)
                Draw.Rect(transitionX - dotR, lineY - dotR, dotR * 2, dotR * 2, lineColor);
            }
        }

        private void DrawComparisonTable()
        {
            if (_mainPinIdx < 0) return;

            bool mainIsSob      = _mainPinIdx == _attempts.Count;
            bool mainIsBaseline = _mainPinIdx == _attempts.Count + 1;

            AttemptLine? mainLine  = mainIsBaseline ? null : mainIsSob ? _sobLine : _attempts[_mainPinIdx];
            int mainRoomCount     = mainIsBaseline ? _totalRooms : mainLine!.RoomsCompleted;

            // "Run #n" is always white; SoB uses its line color; Avg uses gray
            var   sm        = SpeebrunConsistencyTrackerModule.Settings;
            Color mainColor = mainIsBaseline ? Color.Gray
                : mainIsSob
                    ? (_sobIsBest ? sm.TrajectoryBestColorFinal : sm.TrajectorySobColorFinal)
                    : Color.White;

            float gx = position.X + marginH;
            float gy = position.Y + margin;
            float gw = width  - marginH * 2;
            float gh = height - margin  * 2;
            const float scale = ChartConstants.FontScale.AxisLabelMedium;
            const float bgPad = ChartConstants.Interactivity.TooltipBgPadding;
            float lineH = ActiveFont.Measure("A").Y * scale;

            // Primary comparison: always vs Best (best-so-far per room)
            // Secondary comparison: user-clicked line via _compPinIdx; defaults to SoB when no pin set
            bool hasComp    = _compPinIdx >= 0 && _compPinIdx != _mainPinIdx;
            bool compIsAvg  = hasComp && _compPinIdx == _attempts.Count + 1;
            bool compIsSob  = !hasComp || _compPinIdx == _attempts.Count;
            AttemptLine? compLine  = compIsAvg ? null : compIsSob ? _sobLine : _attempts[_compPinIdx];
            string compLabel = compIsAvg ? "vs Avg" : compIsSob ? "vs SoB" : $"vs #{compLine!.ChronologicalIndex}";
            bool showComp   = true;

            // Header rows: 0=run label, 1="vs Best", 2=cumul, [3=comp label, 4=cumul, 5=room]
            int bestHeaderRow  = 1;
            int compHeaderRow  = 3;
            int totalHeaderRows = 3 + (showComp ? 3 : 0);

            // Value rows: 0=cumul dev vs Best, [1=empty, 2=cumul dev comp, 3=room dev comp]
            // Per-room deviation is omitted for "vs Best Split" — the reference switches attempts
            // per room, so a per-room delta would be meaningless.
            int bestValRow  = 0;
            int compValRow  = 2;
            int totalValRows = 1 + (showComp ? 3 : 0);

            // Pre-compute widths
            float maxLabelW = 0f, maxValW = 0f;
            string attemptHeader = mainIsBaseline ? "Avg" : mainIsSob ? "SoB" : $"Run #{mainLine!.ChronologicalIndex}";
            var sectionHeaders = new List<string> { attemptHeader, "vs Best Split", "cumul" };
            if (showComp) sectionHeaders.Add(compLabel);
            if (showComp) sectionHeaders.AddRange(["cumul", "room"]);
            foreach (var ln in sectionHeaders)
                maxLabelW = Math.Max(maxLabelW, ActiveFont.Measure(ln).X * scale);

            int lastVisWidth = LastVisibleRoom();
            int widthLimit   = Math.Min(mainRoomCount, lastVisWidth + 1);
            for (int r = 0; r < widthLimit; r++)
            {
                if (_hiddenColumns.Contains(r)) continue;
                long roomTime     = mainIsBaseline ? _roomAverages[r] : mainLine!.RoomTimes[r];
                long mainCumulDev = mainIsBaseline ? 0 : mainLine!.CumulativeDeviations[r];

                // vs Best (cumul only — no per-room row)
                int  bIdx         = _bestSoFarIdx.Length > r ? _bestSoFarIdx[r] : -1;
                bool bestAvailable = bIdx >= 0;
                long bestCumulDev  = bestAvailable ? mainCumulDev - _attempts[bIdx].CumulativeDeviations[r] : 0;
                maxValW = Math.Max(maxValW, ActiveFont.Measure(bestAvailable ? FormatDev(bestCumulDev) : "n/a").X * scale);

                if (showComp)
                {
                    long compCumulDev = compIsAvg
                        ? mainCumulDev
                        : mainCumulDev - (r < compLine!.CumulativeDeviations.Length ? compLine.CumulativeDeviations[r] : 0);
                    long compRoomTime = compIsAvg ? _roomAverages[r] : r < compLine!.RoomsCompleted ? compLine.RoomTimes[r] : 0;
                    long compRoomDev  = roomTime - compRoomTime;
                    maxValW = Math.Max(maxValW, ActiveFont.Measure(FormatDev(compCumulDev)).X * scale);
                    maxValW = Math.Max(maxValW, ActiveFont.Measure(FormatDev(compRoomDev)).X * scale);
                }
            }

            float headerBoxW = maxLabelW;
            float valBoxW    = maxValW;
            float headerBoxH = lineH * totalHeaderRows + bgPad * 2f;
            float valBoxH    = lineH * totalValRows    + bgPad * 2f;
            float headerBoxY = gy + gh - 1f - headerBoxH;
            float valBoxY    = gy + gh - 1f - valBoxH;

            // Header column (left of chart)
            {
                float headerBoxX = gx - headerBoxW - bgPad * 2f;
                Draw.Rect(headerBoxX - bgPad, headerBoxY, headerBoxW + bgPad * 2f, headerBoxH, Color.Black * 0.92f);
                float ty = headerBoxY + bgPad;
                ActiveFont.DrawOutline(attemptHeader, new Vector2(headerBoxX, ty),
                    Vector2.Zero, Vector2.One * scale, mainColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                ActiveFont.DrawOutline("vs Best Split", new Vector2(headerBoxX, ty + bestHeaderRow * lineH),
                    Vector2.Zero, Vector2.One * scale, Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
                if (showComp)
                    ActiveFont.DrawOutline(compLabel, new Vector2(headerBoxX, ty + compHeaderRow * lineH),
                        Vector2.Zero, Vector2.One * scale, Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            // Per-column value boxes — skip hidden columns and rooms beyond last visible
            int lastVisComp = LastVisibleRoom();
            int compLimit   = Math.Min(mainRoomCount, lastVisComp + 1);
            for (int r = 0; r < compLimit; r++)
            {
                if (_hiddenColumns.Contains(r)) continue;
                long roomTime2  = mainIsBaseline ? _roomAverages[r] : mainLine!.RoomTimes[r];
                long mainCumulDev2 = mainIsBaseline ? 0 : mainLine!.CumulativeDeviations[r];
                float colMidX = GetRoomCenterX(gx, gw, r);
                float boxX    = colMidX - valBoxW / 2f;
                Draw.Rect(boxX - bgPad, valBoxY, valBoxW + bgPad * 2f, valBoxH, Color.Black * 0.92f);

                float ty = valBoxY + bgPad;

                // vs Best (cumul only — per-room omitted: reference switches attempts per room, making it meaningless)
                int  bIdx2         = _bestSoFarIdx.Length > r ? _bestSoFarIdx[r] : -1;
                bool bestAvail     = bIdx2 >= 0;
                long bestCumulDev2 = bestAvail ? mainCumulDev2 - _attempts[bIdx2].CumulativeDeviations[r] : 0;
                Color cBestColor   = bestAvail ? (bestCumulDev2 <= 0 ? ChartConstants.Colors.AheadGaining : ChartConstants.Colors.BehindLosing) : Color.Gray;
                ActiveFont.DrawOutline(bestAvail ? FormatDev(bestCumulDev2) : "n/a",
                    new Vector2(boxX, ty + bestValRow * lineH),
                    Vector2.Zero, Vector2.One * scale, bestAvail ? cBestColor : Color.Gray, ChartConstants.Stroke.OutlineSize, Color.Black);

                if (showComp)
                {
                    long compCumulDev2 = compIsAvg
                        ? mainCumulDev2
                        : mainCumulDev2 - (r < compLine!.CumulativeDeviations.Length ? compLine.CumulativeDeviations[r] : 0);
                    long compRoomTime2 = compIsAvg ? _roomAverages[r] : r < compLine!.RoomsCompleted ? compLine.RoomTimes[r] : 0;
                    long compRoomDev2  = roomTime2 - compRoomTime2;
                    bool compRoomAvail = compIsAvg || compRoomTime2 > 0;
                    Color cCompColor   = DeviationColor(compCumulDev2, compRoomAvail ? compRoomDev2 : 0);
                    Color rCompColor   = compRoomDev2 <= 0 ? ChartConstants.Colors.AheadGaining : ChartConstants.Colors.BehindLosing;
                    ActiveFont.DrawOutline(FormatDev(compCumulDev2),
                        new Vector2(boxX, ty + compValRow * lineH),
                        Vector2.Zero, Vector2.One * scale, cCompColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                    ActiveFont.DrawOutline(compRoomAvail ? FormatDev(compRoomDev2) : "n/a",
                        new Vector2(boxX, ty + (compValRow + 1) * lineH),
                        Vector2.Zero, Vector2.One * scale, compRoomAvail ? rCompColor : Color.Gray, ChartConstants.Stroke.OutlineSize, Color.Black);
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

            float baselineY = y + (float)_maxUpwardDeviation / _totalRange * h;

            // X-axis room labels — skip hidden columns
            {
                float baseLabelY = y + h + ChartConstants.XAxisLabel.BaseOffsetY;
                float normalW2   = ComputeNormalColumnWidth(w);
                for (int r = 0; r < _totalRooms; r++)
                {
                    float colW   = _hiddenColumns.Contains(r) ? ChartConstants.Interactivity.HiddenColumnStubWidth : normalW2;
                    float centerX = GetRoomCenterX(x, w, r);
                    DrawColumnStrip(r, centerX - colW * 0.5f, colW, y + h);

                    if (_hiddenColumns.Contains(r)) continue;
                    float labelX = centerX;
                    string label = $"R{r + 1}";
                    Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;
                    float labelY = _totalRooms > ChartConstants.XAxisLabel.StaggerThreshold
                        ? (r % 2 == 0 ? baseLabelY : baseLabelY + ChartConstants.XAxisLabel.StaggerOffsetY)
                        : baseLabelY;
                    ActiveFont.DrawOutline(label,
                        new Vector2(labelX - labelSize.X / 2, labelY),
                        Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabel,
                        Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
                }
            }

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
                string label3 = "SoB, Best & Last run";
                Color[] cols3 = [sobLegendColor, bestLegendColor, lastLegendColor];
                DrawStripedLegendEntry(legendX2, legendY2, label3, cols3, ChartConstants.FontScale.AxisLabel, right: true);
            }
            else if (_sobIsBest)
            {
                string lastLabel2 = "Last run";
                DrawLegendEntry(legendX2, legendY2, lastLabel2, lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 = ActiveFont.Measure(lastLabel2).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                string sobBestLabel = "SoB & Best run";
                Color[] colsSB = [sobLegendColor, bestLegendColor];
                DrawStripedLegendEntry(legendX2 - offset2, legendY2, sobBestLabel, colsSB, ChartConstants.FontScale.AxisLabel, right: true);
            }
            else if (_lastIsBest)
            {
                string bestLastLabel = "Best & Last run";
                Color[] colsBL = [bestLegendColor, lastLegendColor];
                DrawStripedLegendEntry(legendX2, legendY2, bestLastLabel, colsBL, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 = ActiveFont.Measure(bestLastLabel).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                DrawLegendEntry(legendX2 - offset2, legendY2, "SoB", sobLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
            }
            else
            {
                string lastLabel3 = "Last run";
                DrawLegendEntry(legendX2, legendY2, lastLabel3, lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 = ActiveFont.Measure(lastLabel3).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                DrawLegendEntry(legendX2 - offset2, legendY2, "Best run", bestLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                offset2 += ActiveFont.Measure("Best run").X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                DrawLegendEntry(legendX2 - offset2, legendY2, "SoB", sobLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
            }

            string stats = _attempts.Count == 1 ? "1 Run" : $"{_attempts.Count} Runs";
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

            int lastVis = LastVisibleRoom();
            if (lastVis < 0) return;

            int n = _attempts.Count;
            var bestLine = _attempts[_bestIdx];

            long bestDevVis = DevAtRoom(bestLine,      lastVis);
            long lastDevVis = DevAtRoom(_attempts[^1], lastVis);
            long sobDevVis  = DevAtRoom(_sobLine,      lastVis);

            // Build label list in priority order: Avg → Best → SoB → Last
            // Coincident cases merge entries; colors[] has >1 element when lines coincide.
            // skip=true when the line already draws its own right-axis label via tooltip (hovered or main pin).
            bool pinnedBaseline = _mainPinIdx == n + 1;
            bool pinnedSob      = _mainPinIdx == n;
            bool pinnedLast     = _mainPinIdx == n - 1;
            bool pinnedBest     = !_lastIsBest && _mainPinIdx == _bestIdx;
            bool hovBaseline = _hoveredLineIdx == n + 1;
            bool hovSob      = _hoveredLineIdx == n;
            bool hovLast     = _hoveredLineIdx == n - 1;
            bool hovBest     = !_lastIsBest && _hoveredLineIdx == _bestIdx;

            var labelList = new List<(float yPos, string text, Color[] colors, bool skip)>();

            Color[] avgColors  = [Color.Gray];
            Color[] bestColors = [bestColor4];
            Color[] sobColors  = [sobColor4];
            Color[] lastColors = [lastColor4];

            if (_anyCompleted)
                labelList.Add((baselineY, new TimeTicks(_roomAveragesSum).ToString(), avgColors, hovBaseline || pinnedBaseline));

            if (_sobIsBest && _lastIsBest)
            {
                if (_sobReachesEnd)
                    labelList.Add((baselineY + bestDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + bestDevVis).ToString(),
                                   [sobColor4, bestColor4, lastColor4], hovSob || hovLast || pinnedSob || pinnedLast));
            }
            else if (_sobIsBest)
            {
                if (_sobReachesEnd)
                    labelList.Add((baselineY + bestDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + bestDevVis).ToString(),
                                   [sobColor4, bestColor4], hovSob || pinnedSob));
                if (_lastReachesEnd)
                    labelList.Add((baselineY + lastDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + lastDevVis).ToString(),
                                   lastColors, hovLast || pinnedLast));
            }
            else if (_lastIsBest)
            {
                if (_anyCompleted)
                    labelList.Add((baselineY + bestDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + bestDevVis).ToString(),
                                   [bestColor4, lastColor4], hovLast || pinnedLast));
                if (_sobReachesEnd)
                    labelList.Add((baselineY + sobDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + sobDevVis).ToString(),
                                   sobColors, hovSob || pinnedSob));
            }
            else
            {
                if (_anyCompleted)
                    labelList.Add((baselineY + bestDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + bestDevVis).ToString(),
                                   bestColors, hovBest || pinnedBest));
                if (_sobReachesEnd)
                    labelList.Add((baselineY + sobDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + sobDevVis).ToString(),
                                   sobColors, hovSob || pinnedSob));
                if (_lastReachesEnd)
                    labelList.Add((baselineY + lastDevVis * devScale,
                                   new TimeTicks(_roomAveragesSum + lastDevVis).ToString(),
                                   lastColors, hovLast || pinnedLast));
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

        private static long DevAtRoom(AttemptLine line, int room)
        {
            if (line.RoomsCompleted == 0) return 0;
            int idx = Math.Min(line.RoomsCompleted - 1, room);
            return line.CumulativeDeviations[idx];
        }
    }
}
