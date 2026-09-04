using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    // Everything the trajectory chart draws around its lines: axes, grid, the X and Y labels,
    // the legend, the per-room tooltips and the comparison table. Split off RunTrajectoryOverlay
    // as a partial rather than as collaborator objects — the folder's precedent for one unit over
    // two files is GraphManager / GraphManagerFactories, and each of these methods reads a dozen
    // of the overlay's fields.
    //
    // DrawGrid and DrawLabels recompute the same Y-tick split. If the two diverge the grid lines
    // and the tick labels desynchronise, so they stay in one file, next to each other.
    public partial class RunTrajectoryOverlay
    {
        private void DrawAxesLines()
        {
            Draw.Line(new Vector2(_gx - 1, _gy + _gh), new Vector2(_gx + _gw + 1, _gy + _gh), axisColor, 2f);
            Draw.Line(new Vector2(_gx, _gy),           new Vector2(_gx, _gy + _gh),           axisColor, 2f);
            Draw.Line(new Vector2(_gx, _baselineY), new Vector2(_gx + _gw, _baselineY), ChartConstants.Colors.BaselineColor, ChartConstants.Stroke.OutlineSize);
        }

        protected override void DrawGrid(float x, float y, float w, float h)
        {
            if (_totalRooms == 0 || _model.Attempts.Count == 0) return;

            float colX = _gx;
            for (int r = 0; r < _totalRooms; r++)
            {
                Draw.Line(new Vector2(colX, y), new Vector2(colX, y + h), ChartConstants.Colors.GridLineColor, 1f);
                colX = _colRightEdge[r];
            }

            float aboveHeight = (float)_scope.MaxUpwardDeviation   / _scope.TotalRange * h;
            float belowHeight = (float)_scope.MaxDownwardDeviation / _scope.TotalRange * h;
            int totalTicks = ChartConstants.Trajectory.TotalYTicks;
            int ticksAbove = Math.Max(1, (int)Math.Round((double)aboveHeight / h * totalTicks));
            int ticksBelow = Math.Max(1, totalTicks - ticksAbove);

            DrawYTickGridLines(x, y, w, _baselineY, aboveHeight, ticksAbove, true);
            DrawYTickGridLines(x, y, w, _baselineY, belowHeight, ticksBelow, false);
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

        public override void DrawHighlight()
        {
            if (_hoveredLine.IsNone) return;
            // The main pin already has a persistent tooltip from DrawPinnedHighlights.
            if (_hoveredLine == _mainPin) return;
            DrawLineTooltips(_hoveredLine);
        }

        public override void DrawHighlight(HoverInfo info)
        {
            // Never called — RunTrajectory manages its own pins via HandleClick.
        }

        // Persistent tooltips for pinned lines, drawn from Render() regardless of hover.
        private void DrawPinnedHighlights()
        {
            if (_mainPin.IsNone) return;
            DrawLineTooltips(_mainPin);
        }

        private void DrawLineTooltips(LineId lineId)
        {
            bool isSob      = lineId.IsSob;
            bool isBaseline = lineId.IsBaseline;
            AttemptLine line = isSob ? _model.SobLine : isBaseline ? null! : _model.Attempts[lineId.Value];

            var   s         = SpeebrunConsistencyTrackerModule.Settings;
            int roomCount = isBaseline ? _totalRooms : line.RoomsCompleted;
            if (_scope.LastVisibleRoom < 0) return;
            int effectiveCount = Math.Min(roomCount, _scope.LastVisibleRoom + 1);

            bool  isLast    = lineId.IsAttempt && lineId.Value == _model.Attempts.Count - 1;
            bool  isBest    = lineId.IsAttempt && lineId.Value == _scope.BestIdx;
            Color lineColor = isBaseline ? Color.Gray
                : isSob
                    ? (_scope.SobIsBest ? s.TrajectoryBestColorFinal : s.TrajectorySobColorFinal)
                : isBest && isLast
                    ? s.TrajectoryLastColorFinal   // DrawBars and the legend both draw the merged line in lastColor
                : isBest
                    ? s.TrajectoryBestColorFinal
                : isLast
                    ? s.TrajectoryLastColorFinal
                : Color.White;

            string lineLabel = isBaseline ? "Avg" : isSob ? "SoB" : $"#{line.ChronologicalIndex}";
            // Label goes on the middle visible room.
            int visibleCount = 0;
            for (int r = 0; r < effectiveCount; r++)
                if (!_colHidden[r]) visibleCount++;
            int labelColR = -1;
            if (visibleCount > 0)
            {
                int target = (visibleCount - 1) / 2, seen = 0;
                for (int r = 0; r < effectiveCount; r++)
                {
                    if (_colHidden[r]) continue;
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
                if (_colHidden[r]) { cumul += isBaseline ? _model.RoomAverages[r] : (r < line.RoomTimes.Length ? line.RoomTimes[r] : 0); continue; }
                long roomTime = isBaseline ? _model.RoomAverages[r] : line.RoomTimes[r];
                cumul += roomTime;

                bool   showLabel = r == labelColR;
                string cumulStr  = new TimeTicks(cumul).ToString();
                string roomStr   = new TimeTicks(roomTime).ToString();
                float  dataW     = Math.Max(ActiveFont.Measure(cumulStr).X, ActiveFont.Measure(roomStr).X) * scale;
                float  textW     = showLabel ? Math.Max(dataW, ActiveFont.Measure(lineLabel).X * scale) : dataW;
                float  bgW       = textW + bgPad * 2f;
                float  bgH       = (showLabel ? lineH * 3f : lineH * 2f) + bgPad * 2f;

                float transitionX = _colRightEdge[r];
                float lineY = isBaseline ? _baselineY : PointY(line, r);

                float bgX   = transitionX - bgW / 2f;
                bool  above = lineY - bgH - gap - dotR * 2 >= _gy;
                float bgY   = above ? lineY - bgH - gap - dotR * 2 : lineY + gap + dotR * 2;

                // Box first, then stem, then dot: each draws over the last.
                Draw.Rect(bgX, bgY, bgW, bgH, ChartConstants.Colors.PanelBackgroundColor);
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

                float stemTop    = above ? bgY + bgH : lineY + dotR;
                float stemBottom = above ? lineY - dotR : bgY;
                Draw.Line(new Vector2(transitionX, stemTop), new Vector2(transitionX, stemBottom), lineColor, stemW);

                Draw.Rect(transitionX - dotR, lineY - dotR, dotR * 2, dotR * 2, lineColor);
            }
        }

        private void DrawComparisonTable()
        {
            if (_mainPin.IsNone) return;

            bool mainIsSob      = _mainPin.IsSob;
            bool mainIsBaseline = _mainPin.IsBaseline;

            AttemptLine? mainLine  = mainIsBaseline ? null : mainIsSob ? _model.SobLine : _model.Attempts[_mainPin.Value];
            int mainRoomCount     = mainIsBaseline ? _totalRooms : mainLine!.RoomsCompleted;

            var   sm        = SpeebrunConsistencyTrackerModule.Settings;
            Color mainColor = mainIsBaseline ? Color.Gray
                : mainIsSob
                    ? (_scope.SobIsBest ? sm.TrajectoryBestColorFinal : sm.TrajectorySobColorFinal)
                    : Color.White;

            const float scale = ChartConstants.FontScale.AxisLabelMedium;
            const float bgPad = ChartConstants.Interactivity.TooltipBgPadding;
            float lineH = ActiveFont.Measure("A").Y * scale;

            // Primary comparison is always vs Best; the secondary defaults to SoB until pinned.
            bool hasComp    = !_compPin.IsNone && _compPin != _mainPin;
            bool compIsAvg  = hasComp && _compPin.IsBaseline;
            bool compIsSob  = !hasComp || _compPin.IsSob;
            AttemptLine? compLine  = compIsAvg ? null : compIsSob ? _model.SobLine : _model.Attempts[_compPin.Value];
            string compLabel = compIsAvg ? "vs Avg" : compIsSob ? "vs SoB" : $"vs #{compLine!.ChronologicalIndex}";
            bool showComp   = true;

            // Header rows: 0=run label, 1="vs Best", 2=cumul, [3=comp label, 4=cumul, 5=room]
            int bestHeaderRow  = 1;
            int compHeaderRow  = 3;
            int totalHeaderRows = 3 + (showComp ? 3 : 0);

            // Value rows: 0=cumul dev vs Best, [1=empty, 2=cumul dev comp, 3=room dev comp].
            // "vs Best Split" has no per-room row: its reference switches attempts each room.
            int bestValRow  = 0;
            int compValRow  = 2;
            int totalValRows = 1 + (showComp ? 3 : 0);

            float maxLabelW = 0f, maxValW = 0f;
            string attemptHeader = mainIsBaseline ? "Avg" : mainIsSob ? "SoB" : $"Run #{mainLine!.ChronologicalIndex}";
            var sectionHeaders = new List<string> { attemptHeader, "vs Best Split", "cumul" };
            if (showComp) sectionHeaders.Add(compLabel);
            if (showComp) sectionHeaders.AddRange(["cumul", "room"]);
            foreach (var ln in sectionHeaders)
                maxLabelW = Math.Max(maxLabelW, ActiveFont.Measure(ln).X * scale);

            int widthLimit = Math.Min(mainRoomCount, _scope.LastVisibleRoom + 1);
            for (int r = 0; r < widthLimit; r++)
            {
                if (_colHidden[r]) continue;
                long roomTime     = mainIsBaseline ? _model.RoomAverages[r] : mainLine!.RoomTimes[r];
                long mainCumulDev = mainIsBaseline ? 0 : mainLine!.CumulativeDeviations[r];

                int  bIdx         = _model.BestSoFarIdx.Length > r ? _model.BestSoFarIdx[r] : -1;
                bool bestAvailable = bIdx >= 0;
                long bestCumulDev  = bestAvailable ? mainCumulDev - _model.Attempts[bIdx].CumulativeDeviations[r] : 0;
                maxValW = Math.Max(maxValW, ActiveFont.Measure(bestAvailable ? FormatDev(bestCumulDev) : "n/a").X * scale);

                if (showComp)
                {
                    long compCumulDev = compIsAvg
                        ? mainCumulDev
                        : mainCumulDev - (r < compLine!.CumulativeDeviations.Length ? compLine.CumulativeDeviations[r] : 0);
                    long compRoomTime = compIsAvg ? _model.RoomAverages[r] : r < compLine!.RoomsCompleted ? compLine.RoomTimes[r] : 0;
                    long compRoomDev  = roomTime - compRoomTime;
                    maxValW = Math.Max(maxValW, ActiveFont.Measure(FormatDev(compCumulDev)).X * scale);
                    maxValW = Math.Max(maxValW, ActiveFont.Measure(FormatDev(compRoomDev)).X * scale);
                }
            }

            float headerBoxW = maxLabelW;
            float valBoxW    = maxValW;
            float headerBoxH = lineH * totalHeaderRows + bgPad * 2f;
            float valBoxH    = lineH * totalValRows    + bgPad * 2f;
            float headerBoxY = _gy + _gh - 1f - headerBoxH;
            float valBoxY    = _gy + _gh - 1f - valBoxH;

            // Header column (left of chart)
            {
                float headerBoxX = _gx - headerBoxW - bgPad * 2f;
                Draw.Rect(headerBoxX - bgPad, headerBoxY, headerBoxW + bgPad * 2f, headerBoxH, ChartConstants.Colors.PanelBackgroundColor);
                float ty = headerBoxY + bgPad;
                ActiveFont.DrawOutline(attemptHeader, new Vector2(headerBoxX, ty),
                    Vector2.Zero, Vector2.One * scale, mainColor, ChartConstants.Stroke.OutlineSize, Color.Black);
                ActiveFont.DrawOutline("vs Best Split", new Vector2(headerBoxX, ty + bestHeaderRow * lineH),
                    Vector2.Zero, Vector2.One * scale, Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
                if (showComp)
                    ActiveFont.DrawOutline(compLabel, new Vector2(headerBoxX, ty + compHeaderRow * lineH),
                        Vector2.Zero, Vector2.One * scale, Color.LightGray, ChartConstants.Stroke.OutlineSize, Color.Black);
            }

            int compLimit = Math.Min(mainRoomCount, _scope.LastVisibleRoom + 1);
            for (int r = 0; r < compLimit; r++)
            {
                if (_colHidden[r]) continue;
                long roomTime2  = mainIsBaseline ? _model.RoomAverages[r] : mainLine!.RoomTimes[r];
                long mainCumulDev2 = mainIsBaseline ? 0 : mainLine!.CumulativeDeviations[r];
                float colMidX = _colCenterX[r];
                float boxX    = colMidX - valBoxW / 2f;
                Draw.Rect(boxX - bgPad, valBoxY, valBoxW + bgPad * 2f, valBoxH, ChartConstants.Colors.PanelBackgroundColor);

                float ty = valBoxY + bgPad;

                int  bIdx2         = _model.BestSoFarIdx.Length > r ? _model.BestSoFarIdx[r] : -1;
                bool bestAvail     = bIdx2 >= 0;
                long bestCumulDev2 = bestAvail ? mainCumulDev2 - _model.Attempts[bIdx2].CumulativeDeviations[r] : 0;
                Color cBestColor   = bestAvail ? (bestCumulDev2 <= 0 ? ChartConstants.Colors.AheadGaining : ChartConstants.Colors.BehindLosing) : Color.Gray;
                ActiveFont.DrawOutline(bestAvail ? FormatDev(bestCumulDev2) : "n/a",
                    new Vector2(boxX, ty + bestValRow * lineH),
                    Vector2.Zero, Vector2.One * scale, bestAvail ? cBestColor : Color.Gray, ChartConstants.Stroke.OutlineSize, Color.Black);

                if (showComp)
                {
                    long compCumulDev2 = compIsAvg
                        ? mainCumulDev2
                        : mainCumulDev2 - (r < compLine!.CumulativeDeviations.Length ? compLine.CumulativeDeviations[r] : 0);
                    long compRoomTime2 = compIsAvg ? _model.RoomAverages[r] : r < compLine!.RoomsCompleted ? compLine.RoomTimes[r] : 0;
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

        // LiveSplit's four delta colours, from cumulDev (<=0 ahead) and roomDev (<=0 gained).
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

        // Signed as stored: negative prints "-1.000s" (gained), positive "+0.500s" (lost).
        private static string FormatDev(long ticks)
        {
            if (ticks == 0) return "±0";
            string sign = ticks > 0 ? "+" : "-";
            return sign + new TimeTicks(Math.Abs(ticks)).ToString();
        }

        protected override void DrawLabels(float x, float y, float w, float h)
        {
            DrawTitle();

            if (_totalRooms == 0 || _model.Attempts.Count == 0) return;

            {
                float baseLabelY = y + h + ChartConstants.XAxisLabel.BaseOffsetY;
                for (int r = 0; r < _totalRooms; r++)
                {
                    float colW    = ColumnWidth(r);
                    float centerX = _colCenterX[r];
                    DrawColumnStrip(r, centerX - colW * 0.5f, colW, y + h);

                    if (_colHidden[r]) continue;
                    float labelX = centerX;
                    string label = RoomLabels.For(r);
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

            float aboveHeight = (float)_scope.MaxUpwardDeviation / _scope.TotalRange * h;
            float belowHeight = (float)_scope.MaxDownwardDeviation / _scope.TotalRange * h;
            int totalTicks  = ChartConstants.Trajectory.TotalYTicks;
            int ticksAbove  = Math.Max(1, (int)Math.Round((double)aboveHeight / h * totalTicks));
            int ticksBelow  = Math.Max(1, totalTicks - ticksAbove);

            DrawYTicks(x, y, _baselineY, aboveHeight, _scope.MaxUpwardDeviation, ticksAbove, true);
            DrawYBaseline(x, _baselineY);
            DrawYTicks(x, y, _baselineY, belowHeight, _scope.MaxDownwardDeviation, ticksBelow, false);

            DrawRightAxisLabels();

            var s3 = SpeebrunConsistencyTrackerModule.Settings;
            Color sobLegendColor  = s3.TrajectorySobColorFinal;
            Color bestLegendColor = s3.TrajectoryBestColorFinal;
            Color lastLegendColor = s3.TrajectoryLastColorFinal;

            float legendY2 = y + h + ChartConstants.Legend.LegendOffsetY;
            float legendX2 = x + w;
            float offset2;
            switch (_scope.Coincidence)
            {
                case LineCoincidence.AllThree:
                    DrawLegendEntry(legendX2, legendY2, "SoB, Best & Last run", lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    break;

                case LineCoincidence.SobIsBest:
                    string lastLabel2 = "Last run";
                    DrawLegendEntry(legendX2, legendY2, lastLabel2, lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    offset2 = ActiveFont.Measure(lastLabel2).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                    DrawLegendEntry(legendX2 - offset2, legendY2, "SoB & Best run", bestLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    break;

                case LineCoincidence.LastIsBest:
                    string bestLastLabel = "Best & Last run";
                    DrawLegendEntry(legendX2, legendY2, bestLastLabel, lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    offset2 = ActiveFont.Measure(bestLastLabel).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                    DrawLegendEntry(legendX2 - offset2, legendY2, "SoB", sobLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    break;

                default:
                    string lastLabel3 = "Last run";
                    DrawLegendEntry(legendX2, legendY2, lastLabel3, lastLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    offset2 = ActiveFont.Measure(lastLabel3).X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                    DrawLegendEntry(legendX2 - offset2, legendY2, "Best run", bestLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    offset2 += ActiveFont.Measure("Best run").X * ChartConstants.FontScale.AxisLabel + ChartConstants.Legend.LegendEntrySpacing;

                    DrawLegendEntry(legendX2 - offset2, legendY2, "SoB", sobLegendColor, ChartConstants.FontScale.AxisLabel, right: true);
                    break;
            }

            string stats = _model.Attempts.Count == 1 ? "1 Run" : $"{_model.Attempts.Count} Runs";
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

        private void DrawRightAxisLabels()
        {
            float labelHeight = ActiveFont.Measure("0").Y * ChartConstants.FontScale.AxisLabelSmall;
            float minSpacing  = labelHeight + ChartConstants.Trajectory.LabelMinSpacingExtra;
            float rightX      = _gx + _gw + ChartConstants.Axis.RightLabelMarginX;
            var   s4          = SpeebrunConsistencyTrackerModule.Settings;
            Color sobColor4   = s4.TrajectorySobColorFinal;
            Color bestColor4  = s4.TrajectoryBestColorFinal;
            Color lastColor4  = s4.TrajectoryLastColorFinal;

            int lastVis = _scope.LastVisibleRoom;
            if (lastVis < 0) return;

            var bestLine = _model.Attempts[_scope.BestIdx];

            long bestDevVis = TrajectoryModel.DevAtRoom(bestLine,      lastVis);
            long lastDevVis = TrajectoryModel.DevAtRoom(_model.Attempts[^1], lastVis);
            long sobDevVis  = TrajectoryModel.DevAtRoom(_model.SobLine,      lastVis);

            // Priority order Avg → Best → SoB → Last; coincident lines merge into one entry with
            // several colors, and skip=true when a tooltip already draws that right-axis label.
            LineId sobId  = SobLineId;
            LineId lastId  = LineId.Attempt(_model.Attempts.Count - 1);
            LineId bestId  = LineId.Attempt(_scope.BestIdx);

            bool pinnedBaseline = _mainPin.IsBaseline;
            bool pinnedSob      = _mainPin == sobId;
            bool pinnedLast     = _mainPin == lastId;
            bool pinnedBest     = !_scope.LastIsBest && _mainPin == bestId;
            bool hovBaseline = _hoveredLine.IsBaseline;
            bool hovSob      = _hoveredLine == sobId;
            bool hovLast     = _hoveredLine == lastId;
            bool hovBest     = !_scope.LastIsBest && _hoveredLine == bestId;

            var labelList = new List<(float yPos, string text, Color[] colors, bool skip)>();

            Color[] avgColors  = [Color.Gray];
            Color[] bestColors = [bestColor4];
            Color[] sobColors  = [sobColor4];
            Color[] lastColors = [lastColor4];

            if (_scope.AnyCompleted)
                labelList.Add((_baselineY, new TimeTicks(_scope.RoomAveragesSum).ToString(), avgColors, hovBaseline || pinnedBaseline));

            switch (_scope.Coincidence)
            {
                case LineCoincidence.AllThree:
                    if (_scope.SobReachesEnd)
                        labelList.Add((_baselineY + bestDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + bestDevVis).ToString(),
                                       [lastColor4], hovSob || hovLast || pinnedSob || pinnedLast));
                    break;

                case LineCoincidence.SobIsBest:
                    if (_scope.SobReachesEnd)
                        labelList.Add((_baselineY + bestDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + bestDevVis).ToString(),
                                       [bestColor4], hovSob || pinnedSob));
                    if (_scope.LastReachesEnd)
                        labelList.Add((_baselineY + lastDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + lastDevVis).ToString(),
                                       lastColors, hovLast || pinnedLast));
                    break;

                case LineCoincidence.LastIsBest:
                    if (_scope.LastReachesEnd)
                        labelList.Add((_baselineY + bestDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + bestDevVis).ToString(),
                                       [lastColor4], hovLast || pinnedLast));
                    if (_scope.SobReachesEnd)
                        labelList.Add((_baselineY + sobDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + sobDevVis).ToString(),
                                       sobColors, hovSob || pinnedSob));
                    break;

                default:
                    if (_scope.AnyCompleted)
                        labelList.Add((_baselineY + bestDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + bestDevVis).ToString(),
                                       bestColors, hovBest || pinnedBest));
                    if (_scope.SobReachesEnd)
                        labelList.Add((_baselineY + sobDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + sobDevVis).ToString(),
                                       sobColors, hovSob || pinnedSob));
                    if (_scope.LastReachesEnd)
                        labelList.Add((_baselineY + lastDevVis * _devScale,
                                       new TimeTicks(_scope.RoomAveragesSum + lastDevVis).ToString(),
                                       lastColors, hovLast || pinnedLast));
                    break;
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
                if (labelYPos < _gy - labelSize.Y / 2 || labelYPos > _gy + _gh + labelSize.Y / 2) continue;

                Color drawColor = labelColors[0];
                ActiveFont.DrawOutline(labelText,
                    new Vector2(rightX, nudged[i] - labelSize.Y / 2),
                    Vector2.Zero, Vector2.One * ChartConstants.FontScale.AxisLabelSmall,
                    drawColor, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }
    }
}
