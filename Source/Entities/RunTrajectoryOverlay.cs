using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    // The chart proper: the derived dataset it draws, the column geometry, the attempt lines,
    // the hit test and the pin contract. Everything drawn around the lines — axes, grid, axis
    // text, tooltips, comparison table — is in RunTrajectoryOverlayAnnotations.cs.
    public partial class RunTrajectoryOverlay : BaseChartOverlay
    {
        private readonly int _totalRooms;

        // The dataset is derived, not raw, and neither half touches XNA: TrajectoryModel builds
        // it from the session, TrajectoryScope holds everything that depends on which columns
        // are hidden. Both live under Source/Metrics/ and are unit-tested there.
        private readonly TrajectoryModel _model;
        private readonly TrajectoryScope _scope;

        // Plot rect. position/width/height/margin/marginH are readonly, so it never moves.
        private readonly float _gx, _gy, _gw, _gh;

        // Deviation-to-pixel mapping, refreshed by RecomputeCache(): the scope's
        // MaxUpwardDeviation and TotalRange move only there. Not a per-Render() local — HitTest
        // and ColumnHitTest read it from Update, outside Render.
        private float _baselineY;                 // Y of the zero-deviation line
        private float _devScale;                  // pixels per tick of deviation

        // Column geometry, rebuilt by RebuildColumnLayout() on every toggle.
        private readonly bool[]  _colHidden;
        private readonly float[] _colRightEdge;    // X of column r's right edge
        private readonly float[] _colCenterX;      // X of column r's centre
        private readonly int[]   _colPrevVisible;  // nearest visible column before r, -1 if none
        private float _colNormalWidth;             // width of a visible column

        private LineId _hoveredLine = LineId.None;
        // Main pin is the fixed reference line (None = comparison mode off); comp pin the
        // optional secondary line (None = compare vs Avg).
        private LineId _mainPin = LineId.None;
        private LineId _compPin = LineId.None;

        private LineId SobLineId      => LineId.Sob(_model.Attempts.Count);
        private LineId BaselineLineId => LineId.Baseline(_model.Attempts.Count);

        public RunTrajectoryOverlay(
            PracticeSession session,
            int totalRooms,
            Vector2? pos = null)
            : base(Dialog.Clean(DialogIds.ChartTrajectoryTitle), pos)
        {
            _totalRooms = totalRooms;
            _gx = position.X + marginH;
            _gy = position.Y + margin;
            _gw = width  - marginH * 2;
            _gh = height - margin  * 2;
            _colHidden      = new bool[_totalRooms];
            _colRightEdge   = new float[_totalRooms];
            _colCenterX     = new float[_totalRooms];
            _colPrevVisible = new int[_totalRooms];
            RebuildColumnLayout();

            _model = TrajectoryModel.Build(session, totalRooms);
            _scope = new TrajectoryScope(_model);
            RecomputeCache();
        }

        private void RecomputeCache()
        {
            _scope.Recompute(_colHidden);
            RefreshDeviationScale();
        }

        private void RefreshDeviationScale()
        {
            _baselineY = _gy + (float)_scope.MaxUpwardDeviation / _scope.TotalRange * _gh;
            _devScale  = _gh / _scope.TotalRange;
        }

        // Two invalidation keys, not one: hiding a middle room changes every column width and
        // leaves _scope.LastVisibleRoom alone, so this must not hang off RecomputeCache()'s guard.
        private void RebuildColumnLayout()
        {
            const float stub = ChartConstants.Interactivity.HiddenColumnStubWidth;
            int visibleCount = _totalRooms - _hiddenColumns.Count;
            _colNormalWidth = visibleCount <= 0
                ? _gw / Math.Max(_totalRooms, 1)
                : (_gw - _hiddenColumns.Count * stub) / visibleCount;

            float x = _gx;
            int prevVisible = -1;
            for (int r = 0; r < _totalRooms; r++)
            {
                bool  hidden = _hiddenColumns.Contains(r);
                float colW   = hidden ? stub : _colNormalWidth;
                _colHidden[r]      = hidden;
                _colCenterX[r]     = x + colW * 0.5f;
                _colPrevVisible[r] = prevVisible;
                x += colW;
                _colRightEdge[r] = x;
                if (!hidden) prevVisible = r;
            }
        }

        private float ColumnWidth(int r) =>
            _colHidden[r] ? ChartConstants.Interactivity.HiddenColumnStubWidth : _colNormalWidth;

        public override void ClearHiddenColumns()
        {
            base.ClearHiddenColumns();
            RebuildColumnLayout();
            int newLastVisible = _totalRooms - 1; // after clearing, last room is always visible
            if (newLastVisible != _scope.LastVisibleRoom)
                RecomputeCache();
        }

        public override void ToggleColumn(int columnIndex)
        {
            base.ToggleColumn(columnIndex);
            RebuildColumnLayout();
            int newLastVisible = -1;
            for (int r = _totalRooms - 1; r >= 0; r--)
                if (!_colHidden[r]) { newLastVisible = r; break; }
            if (newLastVisible != _scope.LastVisibleRoom)
                RecomputeCache();
        }

        public override void Render()
        {
            Draw.Rect(position, width, height, backgroundColor);

            DrawGrid(_gx, _gy, _gw, _gh);
            DrawAxesLines();
            DrawBars(_gx, _gy, _gw, _gh);
            DrawLabels(_gx, _gy, _gw, _gh);
            DrawPinnedHighlights();
            DrawComparisonTable();
        }

        private bool IsHovered(LineId id) => !_hoveredLine.IsNone && _hoveredLine == id;

        private bool IsLinePinned(LineId id) => id == _mainPin || id == _compPin;

        private bool IsLineDimmed(LineId id)
        {
            bool hovering       = !_hoveredLine.IsNone;
            bool comparisonMode = !_mainPin.IsNone;
            if (!hovering && !comparisonMode) return false;

            if (IsHovered(id)) return false;

            if (comparisonMode)
            {
                if (id == _mainPin) return false;  // main pin always lit
                if (id == _compPin) return false;  // comp pin always lit (or None = no comp pin)
                if (_compPin.IsNone && id.IsSob) return false; // default comp is SoB
            }

            return true;
        }

        // One line may carry two identities when SoB, Best and Last coincide. It thickens when
        // either is hovered or pinned, and dims only when *both* are dim — hence && here against
        // the || above.
        private void DrawSpecialLine(AttemptLine line, LineId a, LineId b, Color color)
        {
            bool lit    = IsHovered(a) || IsHovered(b) || IsLinePinned(a) || IsLinePinned(b);
            bool dimmed = IsLineDimmed(a) && IsLineDimmed(b);
            DrawAttemptLine(line,
                dimmed ? color * ChartConstants.Trajectory.DimFactor : color,
                lit ? ChartConstants.Trajectory.SpecialLineHitThickness
                    : ChartConstants.Trajectory.SpecialLineThickness);
        }

        protected override void DrawBars(float x, float y, float w, float h)
        {
            if (_model.Attempts.Count == 0) return;

            int   total     = _model.Attempts.Count;

            for (int i = 0; i < total; i++)
            {
                if (i == _scope.BestIdx || i == total - 1) continue;

                LineId id       = LineId.Attempt(i);
                bool  isHovered = IsHovered(id);
                bool  dimmed    = IsLineDimmed(id);
                float thickness;
                Color color;
                // With nothing hovered and no main pin, nothing is hovered, dimmed or pinned,
                // so the plain brightness ramp below is the only reachable case.
                if (isHovered)
                {
                    color     = Color.White;
                    thickness = 2.5f;
                }
                else if (IsLinePinned(id))
                {
                    color     = Color.White;
                    thickness = 2.5f;
                }
                else
                {
                    float brightness = total <= 1
                        ? ChartConstants.Trajectory.BrightnessMax
                        : MathHelper.Lerp(ChartConstants.Trajectory.BrightnessMin, ChartConstants.Trajectory.BrightnessMax, (float)i / (total - 1));
                    // Dimming scales the line's own place on the ramp, the way DrawSpecialLine
                    // does. A flat BrightnessMin put every dimmed line at exactly the brightness
                    // of the oldest undimmed one, so the two could not be told apart.
                    if (dimmed) brightness *= ChartConstants.Trajectory.DimFactor;
                    color     = Color.White * brightness;
                    thickness = dimmed ? 1f : 1.5f;
                }
                DrawAttemptLine(_model.Attempts[i], color, thickness);
            }

            // SoB, Best and Last draw on top of the regulars, in that order. Colours come from
            // LineColor so the line, its tooltip and the comparison table cannot disagree.
            LineId sob  = SobLineId;
            LineId best = LineId.Attempt(_scope.BestIdx);
            LineId last = LineId.Attempt(total - 1);

            switch (_scope.Coincidence)
            {
                case LineCoincidence.AllThree:
                    // One line for all three; colour precedence last > best > sob.
                    DrawSpecialLine(_model.Attempts[last.Value], sob, last, LineColor(last));
                    break;

                case LineCoincidence.SobIsBest:
                    // Shared line takes bestColor (best > sob); Last draws separately. Both ids,
                    // not sob twice: HitTest checks the attempt lines before the SoB line, so
                    // hovering this one resolves to Best and the line read as not-hovered and dimmed.
                    DrawSpecialLine(_model.SobLine, sob, best, LineColor(sob));
                    DrawSpecialLine(_model.Attempts[last.Value], last, last, LineColor(last));
                    break;

                case LineCoincidence.LastIsBest:
                    // Shared line takes lastColor (last > best); SoB draws separately.
                    DrawSpecialLine(_model.SobLine, sob, sob, LineColor(sob));
                    DrawSpecialLine(_model.Attempts[last.Value], last, last, LineColor(last));
                    break;

                default:
                    DrawSpecialLine(_model.SobLine, sob, sob, LineColor(sob));
                    DrawSpecialLine(_model.Attempts[best.Value], best, best, LineColor(best));
                    DrawSpecialLine(_model.Attempts[last.Value], last, last, LineColor(last));
                    break;
            }
        }

        // Where a line sits at the right edge of room r. The clamp is to baseline +- _gh, which is
        // wider than the plot: it guards a stale scale, it does not keep the point inside the plot
        // rectangle. Every deviation drawn is inside the scope's own range, which does.
        private float PointY(AttemptLine line, int room) =>
            MathHelper.Clamp(_baselineY + line.CumulativeDeviations[room] * _devScale,
                             _baselineY - _gh, _baselineY + _gh);

        // Edge-based: room r runs from the right edge of the previous *visible* room to its own
        // right edge, so hidden rooms are bridged; the first visible room starts on the Y axis at
        // the baseline. Drawing and the hit test share this, because a second copy that drifts
        // stops the hit test pointing at the line the player sees. Tooltips place a dot per room
        // and go straight to PointY.
        private (Vector2 From, Vector2 To) SegmentFor(AttemptLine line, int room)
        {
            int prev = _colPrevVisible[room];
            Vector2 from = prev < 0
                ? new Vector2(_gx, _baselineY)
                : new Vector2(_colRightEdge[prev], PointY(line, prev));
            return (from, new Vector2(_colRightEdge[room], PointY(line, room)));
        }

        private void DrawAttemptLine(AttemptLine attempt, Color color, float thickness)
        {
            int limit = Math.Min(attempt.RoomsCompleted - 1, _scope.LastVisibleRoom);
            for (int r = 0; r <= limit; r++)
            {
                if (_colHidden[r]) continue;
                var (from, to) = SegmentFor(attempt, r);
                Draw.Line(from, to, color, thickness);
            }
        }

        public override int? ColumnHitTest(Vector2 mousePos) =>
            HitTestColumnStrip(mousePos, _totalRooms, _colNormalWidth);

        public override HoverInfo? HitTest(Vector2 mouseHudPos)
        {
            _hoveredLine = LineId.None;

            if (_model.Attempts.Count == 0 || _totalRooms == 0) return null;
            if (mouseHudPos.X < _gx || mouseHudPos.X > _gx + _gw ||
                mouseHudPos.Y < _gy || mouseHudPos.Y > _gy + _gh)
                return null;

            // Columns have variable width (hidden ones are stubs), so find the one the mouse
            // falls in from the edge table.
            int col = _totalRooms - 1;
            for (int r = 0; r < _totalRooms; r++)
                if (mouseHudPos.X < _colRightEdge[r]) { col = r; break; }

            float mouseX  = mouseHudPos.X;
            float mouseY  = mouseHudPos.Y;
            float  nearestDist = float.MaxValue;
            LineId nearest     = LineId.None;

            void Check(AttemptLine line, LineId id)
            {
                if (col >= line.RoomsCompleted) return;
                if (_colHidden[col]) return;
                var (from, to) = SegmentFor(line, col);

                float segW    = to.X - from.X;
                float t       = segW > 0 ? (mouseX - from.X) / segW : 0f;
                float lerpedY = from.Y + t * (to.Y - from.Y);
                float dist    = Math.Abs(mouseY - lerpedY);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest     = id;
                }
            }

            for (int i = 0; i < _model.Attempts.Count; i++)
                Check(_model.Attempts[i], LineId.Attempt(i));
            Check(_model.SobLine, SobLineId);

            // Baseline is horizontal, so snap on Y distance alone.
            float baselineDist = Math.Abs(mouseY - _baselineY);
            if (baselineDist < nearestDist)
            {
                nearestDist = baselineDist;
                nearest     = BaselineLineId;
            }

            const float snapThreshold = 4f;
            if (nearest.IsNone || nearestDist > snapThreshold) return null;

            _hoveredLine = nearest;
            // Empty label: DrawHighlight draws the whole tooltip itself.
            return new HoverInfo("", Vector2.Zero, Key: _hoveredLine.ToKey());
        }

        public override bool ManagesPins => true;
        public override bool HasPins => !_mainPin.IsNone;

        public override bool HandleClick(HoverInfo hover)
        {
            LineId id = LineId.FromKey(hover.Key, _model.Attempts.Count);
            if (id.IsNone) return false;

            if (_mainPin.IsNone)
            {
                _mainPin = id;
                _compPin = LineId.None;
                return true;
            }

            if (id == _mainPin)
            {
                _mainPin = LineId.None;
                _compPin = LineId.None;
                return true;
            }

            _compPin = id == _compPin ? LineId.None : id;
            return true;
        }

        public override void ClearPins()
        {
            _mainPin     = LineId.None;
            _compPin     = LineId.None;
            _hoveredLine = LineId.None;
        }
    }
}
