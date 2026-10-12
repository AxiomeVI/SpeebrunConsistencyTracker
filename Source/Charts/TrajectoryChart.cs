using System;
using System.Collections.Generic;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// Every run as a line of its running deviation from the rooms' averages: up is faster. The best
// run, the last and the sum of bests are highlighted; a click pins a line to compare it.
//
// The dataset is TrajectoryModel's and what depends on the hidden columns is TrajectoryScope's,
// both unit-tested under Metrics/. This places them and words nothing they did not compute.
//
// The whole chart is one sketch, rebuilt when the hovered line, a pin or a hidden column changes:
// which lines are dimmed depends on all three. The lines' pixels are kept between rebuilds.
//
// Three layers: the frame, then the lines, cut to the plot and moved while a zoomed axis is
// dragged, then the tooltips and the comparison table over them.
internal sealed class TrajectoryChart : SketchChart
{
    private const float Snap = 4f;

    private readonly int _rooms;
    private readonly PracticeSession _session;
    private TrajectoryModel _model;
    private TrajectoryScope _scope;
    // Average | Best run | SoB, at the foot of the panel: what the deviations are measured from.
    private readonly ButtonBar _foot = new(captureId: CaptureIds.TrajectoryFoot);
    private readonly BarSwitch _reference;
    // The widget's controls, in the panel's bottom left corner, on one row: "Widget:" On | Off, then
    // Edit mode, lit while the widget shows over the chart to be moved.
    private readonly ButtonBar _widgetBar = new(captureId: CaptureIds.WidgetButton);
    private readonly ButtonBar _editBar = new(captureId: CaptureIds.WidgetEdit);
    private readonly BarSwitch _widgetSwitch;
    private readonly BarToggle _editToggle;
    private Sketch _cornerBack;
    private Area _cornerArea;
    private static bool WidgetOn => ChartOptions.TrajectoryWidget;
    // Between a caption and the control it names.
    private const float CaptionGap = 4f;

    protected override float PlaceCorner()
    {
        _widgetBar.PlaceIn(Sketch.Metrics, Style, Panel, WidgetAnchor.BottomLeft, 6f, 6f);
        _editBar.Place(Sketch.Metrics, Style, _widgetBar.Bounds.Right + 8f, Panel.Bottom - 6f, 0f, 1f, Panel);
        // The piece of the panel under them, drawn alone while the chart is hidden for Edit mode:
        // over the game, the caption would be hard to read.
        _cornerBack ??= new Sketch(Sketch.Metrics);
        _cornerBack.Clear();
        _cornerArea = new Area(Panel.X, _widgetBar.Bounds.Y - 6f, _editBar.Bounds.Right + 6f - Panel.X, Panel.Bottom - _widgetBar.Bounds.Y + 6f);
        _cornerBack.Fill(_cornerArea, Style.Panel);
        return _editBar.Bounds.Right + 8f;
    }
    private TrajectoryBaseline Wanted => (TrajectoryBaseline)_reference.Choice;
    // The one in effect: the average while the best run is wanted and no run is complete.
    private TrajectoryBaseline Baseline => _model.Baseline;

    private HideableBands _bands;
    private Scale<long> _y;
    private float _baselineY;
    private bool[] _hidden;
    private bool _layoutStale = true;
    // One per attempt, then the sum of bests.
    private LinePoints[] _lines = [];
    // The wheel zooms both axes around the mouse, the deviations and the rooms; a drag then pans.
    // One capture id: a drag pans both at once.
    private AxisZoom _zoom = new(captureId: CaptureIds.TrajectoryZoom, vertical: true), _zoomX = new(captureId: CaptureIds.TrajectoryZoom);
    private Sketch _lineLayer, _labelLayer, _top, _tips, _cells;
    private bool Zoomed => _zoom.Zoomed || _zoomX.Zoomed;
    private bool Panning => _zoom.Panning || _zoomX.Panning;
    private bool InPlotX(float x) => x >= Plot.X - 0.5f && x <= Plot.Right + 0.5f;

    private LineId _hovered = LineId.None;
    // Where the lines are drawn: the plot, less a strip at its foot for the comparison table while
    // a line is pinned. The table never covers a line, at any zoom.
    private Area _linesArea;
    // Rooms too narrow for a time under each: the times and the comparison then show for one room
    // only, the one under the mouse. Zooming in widens the rooms and brings them all back.
    private bool _crowded;
    private int _focusRoom = -1;
    // Main pin is the fixed reference line (None = comparison mode off); comp pin the optional
    // secondary line (None = compare against the sum of bests).
    private LineId _mainPin = LineId.None;
    private LineId _compPin = LineId.None;

    private LineId SobId      => LineId.Sob(_model.Attempts.Count);
    private LineId BaselineId => LineId.Baseline(_model.Attempts.Count);
    private bool Empty => _rooms == 0 || _model.Attempts.Count == 0;

    public TrajectoryChart(PracticeSession session, int totalRooms)
        : base("")
    {
        _rooms   = totalRooms;
        _session = session;
        _hidden  = new bool[totalRooms];
        _foot.Caption(Dialog.Clean(DialogIds.ChartBaseline));
        _foot.Space(CaptionGap);
        _reference = _foot.Switch(
            [Dialog.Clean(DialogIds.ChartAverage), Dialog.Clean(DialogIds.ChartBestRun), Dialog.Clean(DialogIds.ChartSob)],
            [Dialog.Clean(DialogIds.ChartRefTipAverage), Dialog.Clean(DialogIds.ChartRefTipBestRun), Dialog.Clean(DialogIds.ChartRefTipSob)]);
        _reference.Choice = (int)ChartOptions.TrajectoryBaseline;
        _widgetBar.Caption(Dialog.Clean(DialogIds.ChartWidget));
        _widgetBar.Space(CaptionGap);
        _widgetSwitch = _widgetBar.Switch(
            [Dialog.Clean(DialogIds.ChartOn), Dialog.Clean(DialogIds.ChartOff)],
            [Dialog.Clean(DialogIds.ChartWidgetTip), null]);
        _widgetSwitch.Choice = WidgetOn ? 0 : 1;
        _editToggle = _editBar.Toggle(Dialog.Clean(DialogIds.ChartWidgetPlace), Dialog.Clean(DialogIds.ChartWidgetPlaceTip));
        Measure();
        // No closer than one room across the plot.
        _zoomX.MinSpan = 1.0 / Math.Max(1, totalRooms);
    }

    protected override string TitleNote =>
        Empty ? null : DialogText.Count(_model.Attempts.Count, DialogIds.ChartOneRun, DialogIds.ChartRunsFmt);

    // The lines, from the reference chosen. The runs keep their numbers, so a pin stays on its run.
    private void Measure()
    {
        _model = TrajectoryModel.Build(_session, _rooms, Wanted);
        title  = Baseline switch
        {
            TrajectoryBaseline.SumOfBest => Dialog.Clean(DialogIds.ChartTrajectoryTitleSob),
            TrajectoryBaseline.BestRun   => Dialog.Clean(DialogIds.ChartTrajectoryTitleBest),
            // The best run was asked for and there is none yet: say so, the switch still shows it.
            _ when Wanted == TrajectoryBaseline.BestRun
                => $"{Dialog.Clean(DialogIds.ChartTrajectoryTitle)} ({Dialog.Clean(DialogIds.ChartTrajectoryNoBestRun)})",
            _ => Dialog.Clean(DialogIds.ChartTrajectoryTitle),
        };
        _scope = new TrajectoryScope(_model);
        _scope.Recompute(_hidden);
        _layoutStale = true;
    }

    // The flat line: the average run, the best run, or the sum of bests.
    private string BaselineName => Dialog.Clean(Baseline switch
    {
        TrajectoryBaseline.SumOfBest => DialogIds.ChartSob,
        TrajectoryBaseline.BestRun   => DialogIds.ChartBest,
        _                            => DialogIds.ChartAvg,
    });

    public override void ToggleColumn(int columnIndex)
    {
        base.ToggleColumn(columnIndex);
        _layoutStale = true;
    }

    internal override void TakeViewFrom(BaseChartOverlay previous)
    {
        base.TakeViewFrom(previous);
        if (previous is not TrajectoryChart chart || chart._rooms != _rooms) return;
        (_zoom, _zoomX) = (chart._zoom, chart._zoomX);
        // A run keeps its number; the two lines that are not runs are numbered after the last one.
        LineId Here(LineId id) => id.IsNone ? id : id.IsSob ? SobId : id.IsBaseline ? BaselineId
                                : id.Value < _model.Attempts.Count ? id : LineId.None;
        _mainPin = Here(chart._mainPin);
        _compPin = _mainPin.IsNone ? LineId.None : Here(chart._compPin);
        _layoutStale = true;
    }

    public override void ClearHiddenColumns()
    {
        base.ClearHiddenColumns();
        _layoutStale = true;
    }

    private float RightEdge(int room) => _bands.BandStart(room) + _bands.WidthOf(room);

    // Where a line sits at the right edge of a room, kept within a plot's height of the baseline:
    // a guard against a stale scale, not a clip.
    // Zoomed, the lines run off the plot and are cut to it instead.
    private float PointY(AttemptLine line, int room)
    {
        float y = _y.At(line.CumulativeDeviations[room]);
        return Zoomed ? y : Math.Clamp(y, _baselineY - Plot.Height, _baselineY + Plot.Height);
    }

    // The comparison table's rows: the pinned line's three, and three more for a second comparison.
    private int TableRows => !_compPin.IsNone && _compPin != _mainPin || !_mainPin.IsSob ? 6 : 3;
    private float TableHeight(float lineHeight) => lineHeight * TableRows + 2f * ChartConstants.Interactivity.TooltipBgPadding;

    private void Layout()
    {
        _layoutStale = false;
        _hidden = HiddenFlags(_rooms);
        _scope.Recompute(_hidden);
        // Zoomed, the rooms are laid out wider than the plot and the part shown is cut out of them.
        float length = (float)(Plot.Width / (_zoomX.To - _zoomX.From)), start = Plot.X - (float)_zoomX.From * length;
        _bands = new HideableBands(_rooms, start, length, _hidden,
                                   ChartConstants.Interactivity.HiddenColumnStubWidth, fill: 1f, wholePixels: false);
        // Faster than average is a negative deviation, drawn up. The zoom counts from the bottom.
        _linesArea = Plot;
        if (!_mainPin.IsNone && _bands.Width >= ChartConstants.Trajectory.MinDetailWidth)
            _linesArea = Plot with { Height = Plot.Height - TableHeight(Sketch.LineHeight(Style.Label)) - 4f };
        (long bottom, long top) = _zoom.Range(Durations.Deltas, _scope.MaxDownwardDeviation, -_scope.MaxUpwardDeviation);
        _y = new Scale<long>(Durations.Deltas, top, bottom, _linesArea.Y, _linesArea.Bottom);
        _baselineY = _y.At(0);

        _lines = new LinePoints[_model.Attempts.Count + 1];
        for (int i = 0; i < _model.Attempts.Count; i++) _lines[i] = Place(_model.Attempts[i]);
        _lines[^1] = Place(_model.SobLine);
    }

    // A point at the right edge of each visible room a run finished, from the baseline at the axis.
    // A hidden room has no point, so the line runs straight across its stub.
    private LinePoints Place(AttemptLine line)
    {
        int limit = Math.Min(line.RoomsCompleted - 1, _scope.LastVisibleRoom), count = 1;
        for (int r = 0; r <= limit; r++)
            if (!_hidden[r]) count++;
        float[] x = new float[count], y = new float[count];
        (x[0], y[0]) = (_bands.BandStart(0), _baselineY);
        int at = 1;
        for (int r = 0; r <= limit; r++)
        {
            if (_hidden[r]) continue;
            (x[at], y[at]) = (RightEdge(r), PointY(line, r));
            at++;
        }
        return new LinePoints(x, y);
    }

    protected override void Build(ChartStyle style)
    {
        if (_layoutStale || _bands == null) Layout();
        _lineLayer ??= new Sketch(Sketch.Metrics);
        _top       ??= new Sketch(Sketch.Metrics);
        // The times along a line and the table's cells follow a pan, each in a layer of its own.
        _tips      ??= new Sketch(Sketch.Metrics);
        _cells     ??= new Sketch(Sketch.Metrics);
        _tips.Clear();
        _cells.Clear();
        _labelLayer ??= new Sketch(Sketch.Metrics);
        _lineLayer.Clear();
        _labelLayer.Clear();
        _top.Clear();
        if (Empty)
        {
            DrawAxisLines();
            return;
        }
        TextStyle small = SctChartStyle.Small(style);

        // Grid: a line where each room starts, and one per deviation tick.
        for (int r = 0; r < _rooms; r++)
            _lineLayer.Fill(new Area(MathF.Round(_bands.BandStart(r)), _linesArea.Y, 1f, _linesArea.Height), style.Grid);
        Paint.YAxis(Sketch, _linesArea, _y.Ticks(ChartConstants.Trajectory.TotalYTicks), style);
        Paint.HiddenStubs(_lineLayer, _bands, Plot, -1, style);
        DrawAxisLines();
        float bandsStart = _bands.BandStart(0), bandsEnd = RightEdge(_rooms - 1);
        _lineLayer.Fill(new Area(bandsStart, MathF.Round(_baselineY) - 1f, bandsEnd - bandsStart, 2f), GameCanvas.ToRgba(ChartConstants.Colors.BaselineColor));

        DrawLines();

        Paint.BandLabels(_labelLayer, _bands, RoomNames(), Plot.Bottom, style, LabelFit.Thin);
        DrawEndLabels(style, style.Label);
        DrawLegend(style);
        _foot.Place(Sketch.Metrics, style, Panel.X + Panel.Width / 2f, Panel.Bottom - 6f, 0.5f, 1f, Panel);

        _crowded = _bands.Width < ChartConstants.Trajectory.MinDetailWidth;
        if (!_mainPin.IsNone) DrawLineTooltips(_mainPin, style, small);
        if (_crowded) DrawRoomCard(style);
        else DrawComparisonTable(style);
        // The main pin already has its tooltips.
        if (!_hovered.IsNone && _hovered != _mainPin) DrawLineTooltips(_hovered, style, small);
    }

    private string[] RoomNames()
    {
        string[] names = new string[_rooms];
        for (int r = 0; r < _rooms; r++) names[r] = RoomLabels.For(r);
        return names;
    }

    // ---------------------------------------------------------------------------------- lines

    private bool IsHovered(LineId id) => !_hovered.IsNone && _hovered == id;
    private bool IsPinned(LineId id) => id == _mainPin || id == _compPin;

    private bool IsDimmed(LineId id)
    {
        bool comparing = !_mainPin.IsNone;
        if (_hovered.IsNone && !comparing) return false;
        if (IsHovered(id)) return false;
        if (comparing)
        {
            if (id == _mainPin || id == _compPin) return false;
            if (_compPin.IsNone && id.IsSob) return false;   // the default comparison
        }
        return true;
    }

    // One place for a line's colour, so the line, its tooltips and the comparison table agree.
    // Precedence last > best > sum of bests, the order they are drawn in.
    private Rgba LineColor(LineId id)
    {
        ChartPalette palette = ChartPalette.Current;
        if (id.IsBaseline) return GameCanvas.ToRgba(Color.Gray);
        if (id.IsSob)
            return GameCanvas.ToRgba(_scope.Coincidence == LineCoincidence.AllThree ? palette.Last
                                     : _scope.SobIsBest ? palette.Best : palette.Sob);
        if (!id.IsAttempt) return Rgba.White;
        if (id.Value == _model.Attempts.Count - 1) return GameCanvas.ToRgba(palette.Last);
        if (id.Value == _scope.BestIdx)            return GameCanvas.ToRgba(palette.Best);
        return Rgba.White;
    }

    private LinePoints PointsOf(LineId id) => id.IsSob ? _lines[^1] : _lines[id.Value];

    // One line may carry two identities when the sum of bests, the best and the last coincide. It
    // thickens when either is hovered or pinned, and dims only when both are dim.
    private void DrawSpecial(LineId drawn, LineId a, LineId b)
    {
        bool lit    = IsHovered(a) || IsHovered(b) || IsPinned(a) || IsPinned(b);
        bool dimmed = IsDimmed(a) && IsDimmed(b);
        Rgba colour = LineColor(drawn);
        Paint.Line(_lineLayer, PointsOf(drawn), dimmed ? colour.Times(ChartConstants.Trajectory.DimFactor) : colour,
                   lit ? ChartConstants.Trajectory.SpecialLineHitThickness : ChartConstants.Trajectory.SpecialLineThickness);
    }

    private void DrawLines()
    {
        int total = _model.Attempts.Count;
        for (int i = 0; i < total; i++)
        {
            if (i == _scope.BestIdx || i == total - 1) continue;
            LineId id = LineId.Attempt(i);
            if (IsHovered(id) || IsPinned(id))
            {
                Paint.Line(_lineLayer, _lines[i], Rgba.White, 2.5f);
                continue;
            }
            // Older runs are darker. Dimming scales a line's own place on that ramp, so a dimmed
            // line and the oldest undimmed one can still be told apart.
            float brightness = total <= 1
                ? ChartConstants.Trajectory.BrightnessMax
                : MathHelper.Lerp(ChartConstants.Trajectory.BrightnessMin, ChartConstants.Trajectory.BrightnessMax, (float)i / (total - 1));
            bool dimmed = IsDimmed(id);
            if (dimmed) brightness *= ChartConstants.Trajectory.DimFactor;
            Paint.Line(_lineLayer, _lines[i], Rgba.White.Times(brightness), dimmed ? 1f : 1.5f);
        }

        // Sum of bests, best and last go on top of the others, in that order.
        LineId sob = SobId, best = LineId.Attempt(_scope.BestIdx), last = LineId.Attempt(total - 1);
        switch (_scope.Coincidence)
        {
            case LineCoincidence.AllThree:
                DrawSpecial(last, sob, last);
                break;
            case LineCoincidence.SobIsBest:
                // Both ids: the hit test finds the attempt before the sum of bests, so hovering
                // this line names the best run.
                DrawSpecial(sob, sob, best);
                DrawSpecial(last, last, last);
                break;
            case LineCoincidence.LastIsBest:
                DrawSpecial(sob, sob, sob);
                DrawSpecial(last, last, last);
                break;
            default:
                DrawSpecial(sob, sob, sob);
                DrawSpecial(best, best, best);
                DrawSpecial(last, last, last);
                break;
        }
    }

    // ------------------------------------------------------------------------- around the lines

    // The final time each highlighted line ends on, right of the plot. Lines that end together
    // share one label; one whose tooltip already says it is left out.
    private void DrawEndLabels(ChartStyle style, TextStyle small)
    {
        int lastVisible = _scope.LastVisibleRoom;
        // The labels name where the lines end: nothing to name while the last room is out of view.
        if (lastVisible < 0 || !InPlotX(RightEdge(lastVisible))) return;
        LineId sob = SobId, best = LineId.Attempt(_scope.BestIdx), last = LineId.Attempt(_model.Attempts.Count - 1);
        bool Told(LineId id) => _hovered == id || _mainPin == id;

        List<SideLabel> labels = [];
        HashSet<long> ends = [];
        void Add(long deviation, LineId id, bool told)
        {
            ends.Add(deviation);
            float at = _y.At(deviation);
            // A line that ends off a zoomed plot has no label beside it.
            if (at < _linesArea.Y || at > _linesArea.Bottom) return;
            labels.Add(new SideLabel(at, new TimeTicks(_scope.RoomReferenceSum + deviation).ToString(), LineColor(id), told));
        }

        LineCoincidence together = _scope.Coincidence;
        if (together == LineCoincidence.None && _scope.AnyCompleted)
            Add(TrajectoryModel.DevAtRoom(_model.Attempts[_scope.BestIdx], lastVisible), best, Told(best));
        if (_scope.SobReachesEnd)
            Add(TrajectoryModel.DevAtRoom(_model.SobLine, lastVisible), sob, Told(sob) || (together == LineCoincidence.AllThree && Told(last)));
        if (_scope.LastReachesEnd && together != LineCoincidence.AllThree)
            Add(TrajectoryModel.DevAtRoom(_model.Attempts[^1], lastVisible), last, Told(last));
        // The flat line's own, unless a line of the legend ends on it: that one's says the same time,
        // and a second mark beside it would name a line the legend does not.
        if (_scope.AnyCompleted && !ends.Contains(0)) Add(0, BaselineId, Told(BaselineId));

        float lineHeight = Sketch.LineHeight(small);
        PlacedSideLabel[] placed = LabelColumn.Place(labels, lineHeight + ChartConstants.Trajectory.LabelMinSpacingExtra,
                                                     _linesArea.Y, _linesArea.Bottom, margin: lineHeight / 2f);
        Paint.SideLabels(Sketch, placed, Plot.Right + ChartConstants.Axis.RightLabelMarginX, style with { Label = small });
        // What the column holds: totals, beside an axis of gaps. Above the plot and clear of a label
        // centred on its top edge, and against the panel's side: the column is narrower than its heading.
        if (placed.Length > 0)
            Sketch.Text(Dialog.Clean(DialogIds.ChartSegmentTime), Panel.Right - 6f, Plot.Y - lineHeight / 2f - 4f,
                        small with { Colour = Rgba.LightGray }, alignX: 1f, alignY: 1f);
    }

    private void DrawLegend(ChartStyle style)
    {
        ChartPalette palette = ChartPalette.Current;
        Rgba sob = GameCanvas.ToRgba(palette.Sob), best = GameCanvas.ToRgba(palette.Best), last = GameCanvas.ToRgba(palette.Last);
        (string[] names, Rgba[] colours) = _scope.Coincidence switch
        {
            LineCoincidence.AllThree   => ((string[])[Dialog.Clean(DialogIds.ChartLegendAllThree)], (Rgba[])[last]),
            LineCoincidence.SobIsBest  => ([Dialog.Clean(DialogIds.ChartLegendSobBest), Dialog.Clean(DialogIds.ChartLegendLast)], [best, last]),
            LineCoincidence.LastIsBest => ([Dialog.Clean(DialogIds.ChartSob), Dialog.Clean(DialogIds.ChartLegendBestLast)], [sob, last]),
            _ => ([Dialog.Clean(DialogIds.ChartSob), Dialog.Clean(DialogIds.ChartLegendBest), Dialog.Clean(DialogIds.ChartLegendLast)], [sob, best, last]),
        };
        ChartStyle legend = style;
        Paint.Legend(Sketch, Plot.Right - Paint.LegendWidth(Sketch, names, legend),
                     Plot.Bottom + ChartConstants.Legend.LegendOffsetY, names, colours, legend);
    }

    // At each room a line finished: its running time and that room's, on a stem from a dot on the
    // line. The line's name goes on the middle one.
    private void DrawLineTooltips(LineId id, ChartStyle style, TextStyle small)
    {
        if (_scope.LastVisibleRoom < 0) return;
        AttemptLine line = id.IsSob ? _model.SobLine : id.IsBaseline ? null : _model.Attempts[id.Value];
        int count = Math.Min(line?.RoomsCompleted ?? _rooms, _scope.LastVisibleRoom + 1);
        string name = id.IsBaseline ? BaselineName : id.IsSob ? Dialog.Clean(DialogIds.ChartSob) : $"#{line.ChronologicalIndex}";

        int visible = 0, named = -1;
        for (int r = 0; r < count; r++)
            if (!_hidden[r]) visible++;
        for (int r = 0, seen = 0; r < count && named < 0; r++)
            if (!_hidden[r] && seen++ == (visible - 1) / 2) named = r;

        Rgba colour = LineColor(id);
        ChartStyle tip = style with { Tooltip = small with { Colour = colour }, TooltipAlign = 0f };
        const float gap = 4f, dot = 3f;

        long running = 0;
        float before = _baselineY;
        for (int r = 0; r < count; r++)
        {
            long roomTime = line == null ? _model.RoomReference[r] : r < line.RoomTimes.Length ? line.RoomTimes[r] : 0;
            running += roomTime;
            if (_hidden[r]) continue;
            float cameFrom = before;
            before = line == null ? _baselineY : PointY(line, r);

            string text = (r == named ? name + "\n" : "") + new TimeTicks(running) + "\n" + new TimeTicks(roomTime);
            (float w, float h) = Paint.TooltipSize(_tips, text, tip);
            float x = RightEdge(r), y = line == null ? _baselineY : PointY(line, r);
            if (y < _linesArea.Y || y > _linesArea.Bottom || !InPlotX(x)) continue;
            if (_crowded && r != _focusRoom)
            {
                _tips.Fill(new Area(x - dot + 1f, y - dot + 1f, dot * 2 - 2f, dot * 2 - 2f), colour);
                continue;
            }
            bool above = y - h - gap - dot * 2 >= _linesArea.Y;
            // Beside the stem, on the side the line leaves free, where the line comes down steeply
            // into the room or climbs steeply out of it: centred, the times would lie on the line.
            int next = r + 1;
            while (next < count && _hidden[next]) next++;
            bool last = next >= count;
            float goesTo = last || line == null ? y : PointY(line, next);
            bool falls = y - cameFrom > h / 2f && goesTo >= y - 1f, climbs = goesTo - y < -h / 2f && y - cameFrom <= 1f;
            int side = falls ? (above ? 1 : -1) : climbs ? (above ? -1 : 1) : 0;
            // The last room's stand at the plot's edge: never past it.
            if (last && side > 0) side = 0;
            float left = side > 0 ? x + 2f : side < 0 ? x - w - 2f : x - w / 2f;
            Area box = new(left, above ? y - h - gap - dot * 2 : y + gap + dot * 2, w, h);

            // Box, then stem, then dot: each over the last.
            Paint.TooltipBox(_tips, text, box, tip);
            float from = above ? box.Bottom : y + dot, to = above ? y - dot : box.Y;
            _tips.Fill(new Area(MathF.Round(x) - 1f, from, 2f, to - from), colour);
            _tips.Fill(new Area(x - dot, y - dot, dot * 2, dot * 2), colour);
        }
    }

    // ----------------------------------------------------------------------- comparison table

    private LineId _bestSoFarFor = LineId.None;
    private int[] _bestSoFar = [];

    // "vs Best Split" means the best split among every run but the pinned one, whenever it was
    // made. For the sum of bests and the average every run is a candidate.
    private int[] BestSoFarForMainPin()
    {
        if (_bestSoFarFor == _mainPin && _bestSoFar.Length == _rooms) return _bestSoFar;
        _bestSoFarFor = _mainPin;
        _bestSoFar    = _model.BestAmongOthers(_mainPin.IsAttempt ? _mainPin.Value : -1);
        return _bestSoFar;
    }

    // Signed as stored: negative is time gained, positive time lost.
    internal static string FormatDev(long ticks) =>
        ticks == 0 ? "±0" : (ticks > 0 ? "+" : "-") + new TimeTicks(Math.Abs(ticks));

    private static readonly Rgba Ahead = GameCanvas.ToRgba(ChartConstants.Colors.AheadGaining);
    private static readonly Rgba Behind = GameCanvas.ToRgba(ChartConstants.Colors.BehindLosing);
    private static readonly Rgba Gray = GameCanvas.ToRgba(Color.Gray);

    // LiveSplit's four delta colours: ahead or behind overall, and gained or lost in this room.
    private static Rgba DeviationColor(long cumulative, long room) => (cumulative <= 0, room <= 0) switch
    {
        (true, true)   => Ahead,
        (true, false)  => GameCanvas.ToRgba(ChartConstants.Colors.AheadLosing),
        (false, true)  => GameCanvas.ToRgba(ChartConstants.Colors.BehindGaining),
        (false, false) => Behind,
    };

    // The pinned line and the one it is read against: the second pin, or the sum of bests until
    // there is one. A null line is the reference the chart measures from.
    private readonly record struct Pair(AttemptLine Main, bool MainIsSob, AttemptLine Comp, bool HasComp, bool CompIsAvg, bool CompIsSob)
    {
        // The sum of bests pinned alone would be compared with itself: the section is dropped.
        public bool ShowComp => HasComp || !MainIsSob;
    }

    private AttemptLine LineOf(LineId id) => id.IsBaseline ? null : id.IsSob ? _model.SobLine : _model.Attempts[id.Value];

    private Pair Pinned()
    {
        bool hasComp   = !_compPin.IsNone && _compPin != _mainPin;
        bool compIsAvg = hasComp && _compPin.IsBaseline;
        bool compIsSob = !hasComp || _compPin.IsSob;
        return new Pair(LineOf(_mainPin), _mainPin.IsSob, compIsAvg ? null : compIsSob ? _model.SobLine : LineOf(_compPin), hasComp, compIsAvg, compIsSob);
    }

    private string NameOf(AttemptLine line, bool isSob) => line == null ? BaselineName : isSob ? Dialog.Clean(DialogIds.ChartSob)
                                                         : DialogText.Format(DialogIds.ChartRunFmt, line.ChronologicalIndex);

    // The pinned line against the best split so far, and against a second line (the sum of bests
    // until another is pinned): row names left of the plot, a column of deltas under each room.
    private void DrawComparisonTable(ChartStyle style)
    {
        if (_mainPin.IsNone) return;
        int[] bestSoFar = BestSoFarForMainPin();

        (AttemptLine main, bool mainIsSob, AttemptLine comp, _, bool compIsAvg, bool compIsSob) = Pinned();
        bool showComp = Pinned().ShowComp;
        int mainRooms = main?.RoomsCompleted ?? _rooms;

        string mainName = NameOf(main, mainIsSob);
        string compName = compIsAvg ? Dialog.Clean(Baseline switch { TrajectoryBaseline.SumOfBest => DialogIds.ChartVsSob, TrajectoryBaseline.BestRun => DialogIds.ChartVsBestRun, _ => DialogIds.ChartVsAvg }) : compIsSob ? Dialog.Clean(DialogIds.ChartVsSob)
                        : DialogText.Format(DialogIds.ChartVsRunFmt, comp.ChronologicalIndex);
        string cumulName = Dialog.Clean(DialogIds.ChartCumul), roomName = Dialog.Clean(DialogIds.ChartRoom);

        // Row names: the line, "vs Best Split", its running delta, then the second comparison's
        // name, running delta and room delta. "vs Best Split" has no room row: its reference is a
        // different run in each room.
        List<(string Text, Rgba Colour)> names =
        [
            (mainName, LineColor(_mainPin)), (Dialog.Clean(DialogIds.ChartVsBestSplit), Rgba.LightGray), (cumulName, Gray),
        ];
        if (showComp) names.AddRange([(compName, Rgba.LightGray), (cumulName, Gray), (roomName, Gray)]);

        // One column of values per room: the running delta against the best split, a blank row,
        // then the second comparison's running and room deltas.
        int limit = Math.Min(mainRooms, _scope.LastVisibleRoom + 1);
        var columns = new List<(int Room, (string Text, Rgba Colour)[] Rows)>();
        float valueWidth = 0f;
        TextStyle text = style.Label;
        for (int r = 0; r < limit; r++)
        {
            if (_hidden[r] || !InPlotX(_bands.Centre(r))) continue;
            TrajectoryComparison c = TrajectoryComparison.At(_model, main, comp, bestSoFar, r);
            var rows = new List<(string, Rgba)> { c.HasBest ? (FormatDev(c.VsBest), c.VsBest <= 0 ? Ahead : Behind) : ("n/a", Gray) };

            if (showComp)
            {
                rows.Add(("", Gray));
                rows.Add(c.CompReaches ? (FormatDev(c.CumulDelta), DeviationColor(c.CumulDelta, c.RoomKnown ? c.RoomDelta : 0)) : ("n/a", Gray));
                rows.Add(c.RoomKnown ? (FormatDev(c.RoomDelta), c.RoomDelta <= 0 ? Ahead : Behind) : ("n/a", Gray));
            }
            foreach ((string value, _) in rows) valueWidth = Math.Max(valueWidth, _top.Width(value, text));
            columns.Add((r, [.. rows]));
        }

        const float pad = ChartConstants.Interactivity.TooltipBgPadding;
        float lineHeight = _top.LineHeight(text), bottom = Plot.Bottom - 1f;

        float nameWidth = 0f;
        foreach ((string name, _) in names) nameWidth = Math.Max(nameWidth, _top.Width(name, text));
        // Between the panel's side and the plot's: a saved image is cut to the panel, and the first
        // room's values start at the plot. Names too wide for that are written smaller.
        float tableTop = bottom - lineHeight * names.Count - 2 * pad;
        // No smaller than four fifths: past that the box reaches into the plot, and the first room's
        // cells stand clear of it.
        const float namePad = 4f;
        float margin = Plot.X - Panel.X - 2f - 2 * namePad;
        float shrink = Math.Clamp(margin / Math.Max(1f, nameWidth), 0.8f, 1f);
        TextStyle nameText = text with { Scale = text.Scale * shrink };
        nameWidth *= shrink;
        Area nameBox = new(Panel.X + 2f, tableTop, nameWidth + 2 * namePad, lineHeight * names.Count + 2 * pad);
        // Solid: the deviation axis's numbers are under it, and would show through.
        _top.Fill(nameBox, Rgba.Black);
        for (int i = 0; i < names.Count; i++)
            _top.Text(names[i].Text, nameBox.X + namePad, nameBox.Y + pad + i * lineHeight + lineHeight / 2f, nameText with { Colour = names[i].Colour }, alignY: 0.5f);

        foreach ((int room, (string Text, Rgba Colour)[] rows) in columns)
        {
            // The first room's, unzoomed, would start under the row names.
            float cellLeft = _bands.Centre(room) - valueWidth / 2f - pad;
            if (!Zoomed) cellLeft = Math.Max(cellLeft, nameBox.Right + 2f);
            Area box = new(cellLeft, nameBox.Bottom - lineHeight * rows.Length - 2 * pad,
                           valueWidth + 2 * pad, lineHeight * rows.Length + 2 * pad);
            _cells.Fill(box, style.TooltipBack);
            for (int i = 0; i < rows.Length; i++)
                _cells.Text(rows[i].Text, box.X + pad, box.Y + pad + i * lineHeight, text with { Colour = rows[i].Colour });
        }
    }

    // The comparison table for one room, the one under the mouse: the pinned line's time there and
    // its running time, the same for the line it is compared with, the difference, and the running
    // delta against the best split.
    private void DrawRoomCard(ChartStyle style)
    {
        int r = _focusRoom;
        if (_mainPin.IsNone || r < 0 || r >= _rooms || _hidden[r] || r > _scope.LastVisibleRoom) return;

        (AttemptLine main, bool mainIsSob, AttemptLine comp, bool hasComp, _, bool compIsSob) = Pinned();
        if (main != null && r >= main.RoomsCompleted) return;
        TrajectoryComparison c = TrajectoryComparison.At(_model, main, comp, BestSoFarForMainPin(), r);

        Rgba white = Rgba.White;
        var rows = new List<(string Name, Rgba NameColour, string Room, Rgba RoomColour, string Cumul, Rgba CumulColour)>
        {
            (RoomLabels.For(r), white, Dialog.Clean(DialogIds.ChartRoom), Gray, Dialog.Clean(DialogIds.ChartCumul), Gray),
            (NameOf(main, mainIsSob), LineColor(_mainPin), new TimeTicks(c.MainRoom).ToString(), white, new TimeTicks(c.MainRunning).ToString(), white),
        };
        if (Pinned().ShowComp && c.CompReaches)
        {
            rows.Add((NameOf(comp, compIsSob), hasComp ? LineColor(_compPin) : LineColor(SobId),
                      new TimeTicks(c.CompRoom).ToString(), white, new TimeTicks(c.CompRunning).ToString(), white));
            rows.Add((Dialog.Clean(DialogIds.ChartDifference), Rgba.LightGray, FormatDev(c.RoomDelta), c.RoomDelta <= 0 ? Ahead : Behind,
                      FormatDev(c.CumulDelta), DeviationColor(c.CumulDelta, c.RoomDelta)));
        }
        if (c.HasBest)
            rows.Add((Dialog.Clean(DialogIds.ChartVsBestSplit), Rgba.LightGray, "", Gray, FormatDev(c.VsBest), c.VsBest <= 0 ? Ahead : Behind));

        TextStyle text = style.Label;
        const float pad = ChartConstants.Interactivity.TooltipBgPadding, between = 18f;
        float lineHeight = _top.LineHeight(text), nameWidth = 0f, roomWidth = 0f, cumulWidth = 0f;
        foreach (var row in rows)
        {
            nameWidth  = Math.Max(nameWidth, _top.Width(row.Name, text));
            roomWidth  = Math.Max(roomWidth, _top.Width(row.Room, text));
            cumulWidth = Math.Max(cumulWidth, _top.Width(row.Cumul, text));
        }
        float width = nameWidth + roomWidth + cumulWidth + 2f * between + 2f * pad, height = lineHeight * rows.Count + 2f * pad;
        // At the foot of the plot, under the room, kept inside it.
        float x = Math.Clamp(MathF.Round(_bands.Centre(r) - width / 2f), Plot.X + 2f, Math.Max(Plot.X + 2f, Plot.Right - width - 2f));
        // At the top instead when the line passes there: the card must not cover what it describes.
        float lineY = main == null ? _baselineY : PointY(main, r), foot = Plot.Bottom - 1f - height;
        Area box = new(x, lineY > foot - 3f * lineHeight ? Plot.Y + 1f : foot, width, height);
        _top.Fill(box, style.TooltipBack);
        _top.Outline(box, style.Axis);
        float roomRight = box.X + pad + nameWidth + between + roomWidth, cumulRight = box.Right - pad;
        for (int i = 0; i < rows.Count; i++)
        {
            float y = box.Y + pad + i * lineHeight;
            _top.Text(rows[i].Name, box.X + pad, y, text with { Colour = rows[i].NameColour });
            _top.Text(rows[i].Room, roomRight, y, text with { Colour = rows[i].RoomColour }, alignX: 1f);
            _top.Text(rows[i].Cumul, cumulRight, y, text with { Colour = rows[i].CumulColour }, alignX: 1f);
        }
    }

    // ------------------------------------------------------------------------- mouse and pins

    protected override void DrawLayers()
    {
        // Zoomed, cut to the plot. Not zoomed, a thick line on the plot's edge keeps its whole width,
        // but for the left one: there it would lie across the deviation axis.
        DrawLayer(_lineLayer, _zoomX.PanPixels, _zoom.PanPixels,
                  Zoomed ? _linesArea : new Area(Plot.X + 1f, Panel.Y, Panel.Right - Plot.X - 1f, Panel.Height));
        // A room's name shows while it is whole between the plot's sides.
        DrawLayer(_labelLayer, _zoomX.PanPixels, 0f, new Area(Plot.X - 2f, Plot.Bottom, Plot.Width + 4f, Panel.Bottom - Plot.Bottom));
        // Not cut to the panel: the comparison table's row names stand left of it.
        // The times move with the lines, the table's cells with the rooms; both a little past the
        // plot's sides, where the last room's stand half out of it.
        const float past = 40f;
        DrawLayer(_tips, _zoomX.PanPixels, _zoom.PanPixels, new Area(_linesArea.X - past, _linesArea.Y, _linesArea.Width + 2f * past, _linesArea.Height));
        DrawLayer(_cells, _zoomX.PanPixels, 0f, new Area(Plot.X - past, _linesArea.Bottom, Plot.Width + 2f * past, Plot.Bottom - _linesArea.Bottom));
        DrawLayer(_top, 0f, 0f, new Area(0f, 0f, ChartConstants.Screen.ScreenWidth, ChartConstants.Screen.ScreenHeight));
    }

    // The strip under a room's name, for the rooms in view.
    // Zoomed, the rooms run past the plot: a strip cut by its side is left out.
    protected override void DrawOver()
    {
        if (!Panning && !Empty) DrawStrips(_bands, 0f, Plot.X - 0.5f, Plot.Right + 0.5f, whole: true);
    }

    protected override bool UpdateOwnControls(Pointer pointer)
    {
        // In Edit mode the chart is hidden and told the mouse is nowhere. Its widget controls are
        // still drawn, and are what ends the mode: they read the real mouse, unless the widget holds it.
        Pointer corner = TrajectoryWidget.Editing && !TrajectoryWidget.Taken ? GameInput.Current() : pointer;
        _widgetBar.Update(corner);
        _editBar.Update(corner);
        bool onCorner = _widgetBar.Contains(corner.X, corner.Y) || _editBar.Contains(corner.X, corner.Y);
        // Mod Options has the same switch: follow it when it moved.
        if (_widgetSwitch.Changed) TrajectoryWidget.Switch(_widgetSwitch.Choice == 0);
        else if ((_widgetSwitch.Choice == 0) != WidgetOn) _widgetSwitch.Choice = WidgetOn ? 0 : 1;
        // The widget is hidden under this chart until it is to be edited.
        _editToggle.Enabled = WidgetOn;
        if (_editToggle.Clicked) TrajectoryWidget.Placing = _editToggle.On;
        else if (_editToggle.On != TrajectoryWidget.Placing) _editToggle.On = TrajectoryWidget.Placing;
        // Nothing to measure, no baseline switch: Build places it only with runs to show.
        if (Empty) return onCorner;
        _foot.Update(pointer);
        if (_reference.Changed)
        {
            ChartOptions.TrajectoryBaseline = Wanted;
            // A zoom on one reference's deviations shows the wrong part of the other's.
            _zoom.Reset();
            Measure();
            Invalidate();
        }
        // A pan carries on across the switch.
        if ((onCorner || _foot.Contains(pointer.X, pointer.Y)) && !Panning) return true;
        _zoom.Update(pointer, _linesArea);
        _zoomX.Update(pointer, _linesArea);
        if (_zoom.Changed || _zoomX.Changed)
        {
            _layoutStale = true;
            Invalidate();
        }
        return Panning;
    }

    protected override void DrawOwnControls()
    {
        // Drawn while the rest of the chart is hidden for Edit mode: they are what ends it.
        float fade = GameCanvas.Fade;
        GameCanvas.Fade = 1f;
        // It comes as the panel goes, so the corner looks the same throughout.
        if (_cornerBack != null) GameCanvas.Draw(_cornerBack, 0f, 0f, _cornerArea, 1f - TrajectoryWidget.ChartFade);
        GameCanvas.Draw(_widgetBar.Sketch);
        GameCanvas.Draw(_editBar.Sketch);
        GameCanvas.Fade = fade;
        if (!Empty) GameCanvas.Draw(_foot.Sketch);
    }

    protected override bool IsZoomed => Zoomed;

    protected override void ResetZoom()
    {
        _zoom.Reset();
        _zoomX.Reset();
        _layoutStale = true;
        Invalidate();
    }

    internal override CursorShape Cursor => Panning ? _zoom.Cursor : base.Cursor;
    // One help for the three baselines: it names them all, and says "the baseline" for whichever is chosen.
    internal override ChartHelp Help => new(DialogIds.HintSubTrajectory, DialogIds.HintAboutTrajectory, DialogIds.HintMouseTrajectory);

    public override int? ColumnHitTest(Vector2 mousePos)
    {
        EnsureBuilt();
        if (!Panning && !Empty) return StripAt(_bands, mousePos, 0f, Plot.X - 0.5f, Plot.Right + 0.5f, whole: true);
        NoStrip();
        return null;
    }

    public override HoverInfo? HitTest(Vector2 mouseHudPos)
    {
        EnsureBuilt();
        LineId found = Empty || Panning || !_linesArea.Contains(mouseHudPos.X, mouseHudPos.Y) ? LineId.None : LineAt(mouseHudPos.X, mouseHudPos.Y);
        if (found != _hovered)
        {
            _hovered = found;
            Invalidate();
        }
        int focus = Empty || Panning || !_linesArea.Contains(mouseHudPos.X, mouseHudPos.Y) ? -1 : _bands.IndexAt(mouseHudPos.X);
        if (focus != _focusRoom)
        {
            _focusRoom = focus;
            // Only a crowded chart draws anything for it.
            if (_crowded && !(_mainPin.IsNone && _hovered.IsNone)) Invalidate();
        }
        // Empty label: the sketch carries the tooltips.
        return found.IsNone ? null : new HoverInfo("", Vector2.Zero, Key: found.ToKey());
    }

    // The line passing nearest the mouse, measured straight up or down: attempts first, then the
    // sum of bests, then the baseline, so the earliest of equals wins. No line is picked over a
    // hidden room's stub, where the lines only bridge.
    private LineId LineAt(float x, float y)
    {
        float nearest = Math.Abs(y - _baselineY);
        LineId found = BaselineId;
        int column = _bands.IndexAt(x);
        if (column >= 0 && !_hidden[column])
        {
            int line = Polyline.NearestLine(_lines, x, y);
            if (line >= 0 && Math.Abs(_lines[line].YAt(x) - y) <= nearest)
            {
                nearest = Math.Abs(_lines[line].YAt(x) - y);
                found   = line == _lines.Length - 1 ? SobId : LineId.Attempt(line);
            }
        }
        return nearest > Snap ? LineId.None : found;
    }

    public override bool HasPins => !_mainPin.IsNone;

    public override bool HandleClick(HoverInfo hover)
    {
        LineId id = LineId.FromKey(hover.Key, _model.Attempts.Count);
        if (id.IsNone) return false;
        if (_mainPin.IsNone) (_mainPin, _compPin) = (id, LineId.None);
        else if (id == _mainPin) (_mainPin, _compPin) = (LineId.None, LineId.None);
        else _compPin = id == _compPin ? LineId.None : id;
        _layoutStale = true;
        Invalidate();
        return true;
    }

    public override void ClearPins()
    {
        (_mainPin, _compPin, _hovered) = (LineId.None, LineId.None, LineId.None);
        _layoutStale = true;
        Invalidate();
    }
}
