using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// Every room time and every segment time as a dot, each column's runs spread oldest to newest.
// A pinned dot is a run that Delete runs removes, so each carries its attempt's own index.
//
// The dots are in two layers, the rooms' and the segment's, each cut to its own area: the wheel
// zooms the axis under the mouse. The target line is a third, redrawn as the player drags it.
internal sealed class ScatterChart : RoomColumnsChart
{
    private const float DotSize = ChartConstants.Scatter.DotRadius * 2f;

    // Rooms with no time have no column; these keep each column's own room index, which is what a
    // label and a pin must name.
    private readonly List<int> _roomIndex;
    private readonly List<List<TimeTicks>> _times, _sorted;
    private readonly List<List<int>> _attempts;
    private readonly List<TimeTicks> _segment, _segmentSorted;
    private readonly List<int> _segmentAttempts;
    // Read live: the line follows the setting, and the player's drag writes it.
    private readonly Func<TimeTicks?> _targetSetting;
    private readonly Action<TimeTicks> _setTarget;
    private TimeTicks? _target;
    private readonly DragLine _targetDrag = new(captureId: CaptureIds.TargetDrag, vertical: false);
    // Where the line shows: the target, or where a drag has it.
    private long _targetShown;
    private bool _targetHot;

    private readonly BarSwitch _spread;
    private bool SpreadOn => _spread.Choice == 0;
    private Sketch _roomLayer, _segmentLayer, _targetLayer;

    private float[] _x = [], _y = [];
    private (bool Segment, int Attempt, int Room, TimeTicks Time)[] _dots = [];
    private int _count;

    public ScatterChart(List<List<TimeTicks>> rooms, List<List<int>> roomIndices, List<TimeTicks> segment,
                        List<int> segmentIndices, Func<TimeTicks?> target, Action<TimeTicks> setTarget)
        : base(Dialog.Clean(DialogIds.ChartScatterTitle), relative: ChartOptions.ScatterRelative)
    {
        _roomIndex = [.. Enumerable.Range(0, rooms.Count).Where(i => rooms[i].Count > 0)];
        _times     = [.. _roomIndex.Select(i => rooms[i])];
        _sorted    = [.. _times.Select(t => (List<TimeTicks>)[.. t.OrderBy(v => v)])];
        _attempts  = [.. _roomIndex.Select(i => roomIndices[i])];
        _segment         = segment;
        _segmentSorted   = [.. segment.OrderBy(v => v)];
        _segmentAttempts = segmentIndices;
        _targetSetting   = target;
        _setTarget       = setTarget;
        Foot.Space(16f);
        Foot.Caption(Dialog.Clean(DialogIds.ChartSpread));
        Foot.Space(4f);
        _spread          = Foot.Switch([Dialog.Clean(DialogIds.ChartOn), Dialog.Clean(DialogIds.ChartOff)]);
        _spread.Choice   = ChartOptions.ScatterSpread ? 0 : 1;
    }

    protected override bool Zoomable => true;

    protected override int LiveKey
    {
        get
        {
            _target = _targetSetting();
            return _target?.Ticks.GetHashCode() ?? -1;
        }
    }

    public override bool SupportsDeleteRuns => true;

    // The target line is the player's to switch off: its line of help goes with it.
    internal override ChartHelp Help => new(DialogIds.HintSubScatter, DialogIds.HintAboutScatter,
        _target is { Ticks: > 0 } ? DialogIds.HintMouseScatterTarget : DialogIds.HintMouseScatter);
    protected override int Rooms => _times.Count;
    protected override string RoomName(int room) => RoomLabels.For(_roomIndex[room]);
    protected override List<TimeTicks> SortedRoomTimes(int room) => _sorted[room];
    protected override List<TimeTicks> SortedSegmentTimes => _segmentSorted;
    protected override long? AlsoOnSegmentAxis => _target?.Ticks;
    protected override float ColumnFill => 1f;

    protected override void OnModeChosen(bool relative) => ChartOptions.ScatterRelative = relative;

    protected override void DrawData(ChartStyle style)
    {
        ChartPalette palette = ChartPalette.Current;
        _roomLayer    ??= new Sketch(Sketch.Metrics);
        _segmentLayer ??= new Sketch(Sketch.Metrics);
        _targetLayer  ??= new Sketch(Sketch.Metrics);
        _roomLayer.Clear();
        _segmentLayer.Clear();
        if (!_targetDrag.Dragging) _targetShown = _target?.Ticks ?? 0;
        // A faint line where each column starts.
        for (int i = 0; i <= Rooms; i++)
            (i < Rooms ? Scrolled : Sketch).Fill(new Area(MathF.Round(Bands.BandStart(i)), Plot.Y, 1f, Plot.Height), style.Grid);

        int count = _segment.Count;
        for (int r = 0; r < Rooms; r++)
            if (RoomDrawn(r)) count += _times[r].Count;
        // Kept from one build to the next: at thirty rooms they are large enough to stutter on.
        if (_dots.Length < count)
        {
            _x    = new float[count];
            _y    = new float[count];
            _dots = new (bool, int, int, TimeTicks)[count];
        }
        _count = count;

        int at = 0;
        for (int r = 0; r < Rooms; r++)
        {
            if (!RoomDrawn(r)) continue;
            for (int i = 0; i < _times[r].Count; i++)
            {
                _x[at] = Along(r, i, _times[r].Count);
                _y[at] = RoomAt(r, _times[r][i].Ticks);
                _dots[at++] = (false, _attempts[r][i], _roomIndex[r], _times[r][i]);
            }
        }
        int firstSegment = at;
        for (int i = 0; i < _segment.Count; i++)
        {
            _x[at] = Along(SegmentColumn, i, _segment.Count);
            _y[at] = SegmentY.At(_segment[i].Ticks);
            _dots[at++] = (true, _segmentAttempts[i], -1, _segment[i]);
        }

        Rgba room = GameCanvas.ToRgba(palette.RoomFill), segment = GameCanvas.ToRgba(palette.SegmentFill);
        if (SpreadOn) DrawSpread(room, segment);
        for (int i = 0; i < count; i++)
            Paint.Point(i < firstSegment ? _roomLayer : _segmentLayer, _x[i], _y[i], i < firstSegment ? room : segment, DotSize);
        DrawTarget();
    }

    // Behind each column's dots: the middle half of its times as a band, and the median as a line.
    // The percentiles are MetricHelper's, the ones the box plot and the text overlay use.
    private void DrawSpread(Rgba room, Rgba segment)
    {
        for (int c = 0; c <= Rooms; c++)
        {
            bool isSegment = c == SegmentColumn;
            if (!isSegment && !RoomDrawn(c)) continue;
            List<TimeTicks> sorted = isSegment ? _segmentSorted : _sorted[c];
            if (sorted.Count < 2) continue;
            float At(int percentile)
            {
                long ticks = MetricHelper.ComputePercentile(sorted, percentile).Ticks;
                return MathF.Round(isSegment ? SegmentY.At(ticks) : RoomAt(c, ticks));
            }
            float q1 = At(25), median = At(50), q3 = At(75);
            float inset = Bands.WidthOf(c) * (1f - ChartConstants.Scatter.SpreadRatio) / 4f;
            float x = MathF.Round(Bands.BandStart(c) + inset), width = MathF.Round(Bands.WidthOf(c) - 2f * inset);
            Sketch layer = isSegment ? _segmentLayer : _roomLayer;
            Rgba colour = isSegment ? segment : room;
            layer.Fill(new Area(x, q3, width, q1 - q3), colour.Times(0.18f));
            layer.Fill(new Area(x, median - 1f, width, 2f), colour.Times(0.7f));
        }
    }

    private void DrawTarget()
    {
        _targetLayer.Clear();
        if (_targetShown <= 0) return;
        Rgba red = GameCanvas.ToRgba(Color.Red);
        // From the divider's right edge to the right axis, on whole pixels: the column's own edge is
        // not on one, and the line stuck out a pixel on the left and stopped short on the right.
        float left = MathF.Round(SegmentArea.X) + 2f;
        Area across = new(left, Plot.Y, Plot.Right - left, Plot.Height);
        Paint.ReferenceY(_targetLayer, across, SegmentY, _targetShown, DialogText.Format(DialogIds.ChartTargetFmt, new TimeTicks(_targetShown)),
                         Style with { Reference = red, Dash = 0f, Label = Style.Label with { Colour = red } }, _targetHot);
    }

    protected override void DrawLayers()
    {
        DrawLayer(_roomLayer, -Shift, RoomPan, RoomArea);
        DrawLayer(_segmentLayer, 0f, SegmentPan, SegmentArea);
        // Its label stands above the line and may be wider than the segment's column.
        DrawLayer(_targetLayer, 0f, SegmentPan, new Area(Panel.X, Plot.Y - 30f, Panel.Width, Plot.Height + 30f));
    }

    // The target line follows the mouse a frame at a time; letting go saves it as the target time,
    // and a right click during the drag gives it up.
    protected override bool UpdateMarks(Pointer pointer)
    {
        if (_spread.Changed)
        {
            ChartOptions.ScatterSpread = SpreadOn;
            Invalidate();
        }
        if (_target is not { Ticks: > 0 } target) return false;

        long before = _targetShown;
        bool wasHot = _targetHot;
        _targetDrag.Update(pointer, SegmentArea, MathF.Round(SegmentY.At(_targetShown)));
        if (_targetDrag.Dragging || _targetDrag.Released)
            _targetShown = Math.Max(Durations.CelesteFrameTicks, Durations.Times.NearestFrame(SegmentY.ValueAt(_targetDrag.DragPixel)));
        if (_targetDrag.Cancelled) _targetShown = target.Ticks;
        _targetHot = _targetDrag.Hovered;
        if (_targetShown != before || _targetHot != wasHot) DrawTarget();

        if (_targetDrag.Released && _targetShown != target.Ticks) _setTarget(new TimeTicks(_targetShown));
        return _targetHot;
    }

    internal override CursorShape Cursor => _targetHot ? _targetDrag.Cursor : base.Cursor;

    // Oldest at the left of its column, newest at the right.
    private float Along(int column, int index, int count)
    {
        float centre = Bands.BandStart(column) + Bands.WidthOf(column) / 2f;
        if (count <= 1) return centre;
        return centre + ((float)index / (count - 1) - 0.5f) * Bands.WidthOf(column) * ChartConstants.Scatter.SpreadRatio;
    }

    // Where a dot shows: a room's is scrolled with its column, and all follow a pan.
    private float ShownX(int dot) => _dots[dot].Segment ? _x[dot] : _x[dot] - Shift;
    private float ShownY(int dot) => _y[dot] + (_dots[dot].Segment ? SegmentPan : RoomPan);

    private bool Shows(int dot)
    {
        // A dot zoomed off the plot is not drawn, nor a room's scrolled out of it.
        float x = ShownX(dot), y = ShownY(dot);
        return y >= Plot.Y && y <= Plot.Bottom && (_dots[dot].Segment || (x >= RoomArea.X && x <= RoomArea.Right));
    }

    public override HoverInfo? HitTest(Vector2 mouseHudPos)
    {
        EnsureBuilt();
        if (Panning) return null;

        float best = ChartConstants.Interactivity.ScatterSnapRadius * ChartConstants.Interactivity.ScatterSnapRadius;
        int found = -1;
        for (int i = 0; i < _count; i++)
        {
            float dx = ShownX(i) - mouseHudPos.X, dy = ShownY(i) - mouseHudPos.Y, distance = dx * dx + dy * dy;
            if (distance > best || (distance == best && found >= 0) || !Shows(i)) continue;
            best  = distance;
            found = i;
        }
        return found < 0 ? null : InfoOf(found);
    }

    public override HoverInfo? Locate(string key)
    {
        EnsureBuilt();
        bool segment = PinKey.TryParseSegment(key, out int attempt);
        int room = -1;
        if (!segment && !PinKey.TryParseRoom(key, out attempt, out room)) return null;
        for (int i = 0; i < _count; i++)
            if (_dots[i].Segment == segment && _dots[i].Attempt == attempt && _dots[i].Room == room)
                return Shows(i) ? InfoOf(i) : null;
        return null;
    }

    private HoverInfo InfoOf(int i)
    {
        var dot = _dots[i];
        string label = DialogText.Format(DialogIds.ChartRunTimeFmt, dot.Attempt + 1, dot.Time);
        Vector2 at = new(ShownX(i), ShownY(i) - ChartConstants.Scatter.DotRadius - ChartConstants.Interactivity.TooltipPaddingY
                                    - ChartConstants.Interactivity.TooltipBgPadding);
        return new HoverInfo(label, at, dot.Segment ? PinKey.ForSegment(dot.Attempt) : PinKey.ForRoom(dot.Attempt, dot.Room), i);
    }

    public override void DrawHighlight(HoverInfo info)
    {
        int i = info.Mark;
        if (i < 0 || i >= _count) return;
        float r = ChartConstants.Interactivity.ScatterSnapRadius;
        Hover.Clear();
        Hover.Fill(new Area(MathF.Round(ShownX(i) - r), MathF.Round(ShownY(i) - r), 2f * r, 2f * r), Rgba.White.Times(0.9f));
        GameCanvas.Draw(Hover);
    }
}
