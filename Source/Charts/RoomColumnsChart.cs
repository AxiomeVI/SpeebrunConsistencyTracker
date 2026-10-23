using System;
using System.Collections.Generic;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// A column per room read on the left axis, then the segment's column read on an axis of its own on
// the right: the frame the scatter and the box plot share. Absolute reads the rooms as times;
// Relative reads each as a percent of its own median, so rooms of different lengths compare.
//
// The medians are MetricHelper.ComputePercentile's, the ones the text overlay prints.
internal abstract class RoomColumnsChart : SketchChart
{
    // Absolute | Relative, at the foot of the panel.
    private readonly ButtonBar _bar = new(captureId: CaptureIds.ModeBar);
    protected ButtonBar Foot => _bar;
    private readonly BarSwitch _mode;
    private Scale<long> _roomTimes;
    private Scale<double> _roomPercents;
    private long[] _medians = [];
    private bool[] _hidden = [];
    // The wheel zooms the axis under the mouse and a drag then pans it: the rooms' or the segment's.
    private AxisZoom _roomZoom = new(captureId: CaptureIds.RoomZoom, vertical: true), _segmentZoom = new(captureId: CaptureIds.SegmentZoom, vertical: true);

    protected RoomColumnsChart(string title, bool relative) : base(title)
    {
        _bar.Caption(Dialog.Clean(DialogIds.ChartScale));
        _bar.Space(4f);
        _mode = _bar.Switch([Dialog.Clean(DialogIds.ChartAbsolute), Dialog.Clean(DialogIds.ChartRelative)]);
        _mode.Choice = relative ? 1 : 0;
    }

    // What a subclass supplies: its rooms, each with its times ascending, and the segment's.
    protected abstract int Rooms { get; }
    protected abstract string RoomName(int room);
    protected abstract List<TimeTicks> SortedRoomTimes(int room);
    protected abstract List<TimeTicks> SortedSegmentTimes { get; }
    // A value the right axis must also reach: the target time.
    protected virtual long? AlsoOnSegmentAxis => null;
    // How much of a column its mark takes.
    protected abstract float ColumnFill { get; }
    protected abstract void DrawData(ChartStyle style);
    // Told when the player switches between Absolute and Relative: the chart keeps the choice.
    protected abstract void OnModeChosen(bool relative);
    // Whether the two axes zoom. A chart that says yes keeps its data in layers cut to RoomArea and
    // SegmentArea and drawn moved by RoomPan and SegmentPan.
    protected virtual bool Zoomable => false;
    // The chart's own marks that take the mouse before a zoom does; true while the mouse is on one.
    protected virtual bool UpdateMarks(Pointer pointer) => false;

    protected float RoomPan => _roomZoom.PanPixels;
    protected float SegmentPan => _segmentZoom.PanPixels;
    protected bool Panning => _roomZoom.Panning || _segmentZoom.Panning;

    protected PinnedEndBands Bands { get; private set; }
    protected Area RoomArea { get; private set; }
    protected Area SegmentArea { get; private set; }
    protected Scale<long> SegmentY { get; private set; }
    protected bool Relative => _mode.Choice == 1;
    protected int SegmentColumn => Rooms;
    // How far left of where it was built a column shows: the rooms scroll, the segment does not.
    protected float ShiftOf(int column) => column < Rooms ? Shift : 0f;
    // A pan moves the marks before the chart is rebuilt, so what is pinned moves with it.
    internal override int Revision => HashCode.Combine(base.Revision, RoomPan, SegmentPan);

    // Whether a room has a column to draw in: shown, and with a median to be a percent of.
    protected bool RoomDrawn(int room) => !_hidden[room] && SortedRoomTimes(room).Count > 0 && (!Relative || _medians[room] != 0);

    protected float RoomAt(int room, long ticks) =>
        Relative ? _roomPercents.At(100.0 * ticks / _medians[room]) : _roomTimes.At(ticks);

    // The range of the times shown, widened by a tenth in whole frames.
    private static (long Min, long Max) Widened(long min, long max)
    {
        long frame = Durations.CelesteFrameTicks;
        if (min > max) (min, max) = (0, frame);
        long margin = Math.Max(frame, frame * (long)Math.Round((max - min) * 0.1 / frame, 0));
        return (Math.Max(0, min - margin), max + margin);
    }

    // Every column an even share of the plot; or, with too many rooms for that, the segment in a
    // place of its own on the right and the rooms scrolling beside it.
    private PinnedEndBands Columned()
    {
        const float stub = ChartConstants.Interactivity.HiddenColumnStubWidth;
        float least = ChartConstants.Layout.MinColumnWidth, segment = ChartConstants.Layout.PinnedSegmentWidth;
        HideableBands even = new(Rooms + 1, Plot.X, Plot.Width, _hidden, stub, ColumnFill, wholePixels: false);
        if (Rooms == 0 || even.Width >= least)
        {
            PlaceColumns(Plot.X, Plot.Width, 0f, wholePlot: false);
            return new PinnedEndBands(even, Rooms, Plot.X, even.BandStart(Rooms), even.Width, ColumnFill);
        }

        float width = Plot.Width - segment;
        bool[] hidden = _hidden[..Rooms];
        HideableBands rooms = new(Rooms, Plot.X, width, hidden, stub, ColumnFill, wholePixels: false, minWidth: least);
        PlaceColumns(Plot.X, width, rooms.ContentLength, wholePlot: !Zoomable);
        return new PinnedEndBands(rooms, Rooms, Plot.X, Plot.Right - segment, segment, ColumnFill);
    }

    private (long Min, long Max) SegmentRange()
    {
        List<TimeTicks> segmentTimes = SortedSegmentTimes;
        long low = long.MaxValue, high = long.MinValue;
        if (segmentTimes.Count > 0) (low, high) = (segmentTimes[0].Ticks, segmentTimes[^1].Ticks);
        if (AlsoOnSegmentAxis is long also && also > 0)
        {
            low  = Math.Min(low, also);
            high = Math.Max(high, also);
        }
        return Widened(low, high);
    }

    // Long times outgrow the default margins: the rooms' axis on the left, the segment's on the right.
    // Measured over every room in absolute times, so hiding a room or switching the scale never moves
    // the plot; a zoom only shortens the labels.
    protected override (float Left, float Right) SideMargins(ChartStyle style)
    {
        (float left, float right) = base.SideMargins(style);
        int maxTicks = ChartConstants.Axis.MaxTickMarks;
        float gap = ChartConstants.Axis.PanelEdgeGap;
        long low = long.MaxValue, high = long.MinValue;
        for (int r = 0; r < Rooms; r++)
        {
            List<TimeTicks> times = SortedRoomTimes(r);
            if (times.Count == 0) continue;
            low  = Math.Min(low, times[0].Ticks);
            high = Math.Max(high, times[^1].Ticks);
        }
        if (low <= high)
        {
            (long min, long max) = Widened(low, high);
            left = Math.Max(left, Paint.YLabelWidth(Sketch, Durations.Times.Ticks(min, max, maxTicks), style) + style.LabelGap + gap);
        }
        (long segMin, long segMax) = SegmentRange();
        right = Math.Max(right, Paint.YRightWidth(Sketch, Durations.Times.Ticks(segMin, segMax, maxTicks), style) + gap);
        return (left, right);
    }

    protected sealed override void Build(ChartStyle style)
    {
        ChartPalette palette = ChartPalette.Current;
        Rgba room = GameCanvas.ToRgba(palette.Room), segment = GameCanvas.ToRgba(palette.Segment);

        _hidden = HiddenFlags(Rooms + 1);
        _hidden[Rooms] = false;   // the segment is never hidden
        Bands = Columned();
        float split = Bands.BandStart(Rooms);
        RoomArea    = Plot with { Width = split - Plot.X };
        SegmentArea = new Area(split, Plot.Y, Plot.Right - split, Plot.Height);

        int maxTicks = ChartConstants.Axis.MaxTickMarks;
        ChartStyle roomAxis = style with { Label = style.Label with { Colour = room } };

        _medians = new long[Rooms];
        long low = long.MaxValue, high = long.MinValue;
        for (int r = 0; r < Rooms; r++)
        {
            List<TimeTicks> times = SortedRoomTimes(r);
            if (times.Count == 0) continue;
            _medians[r] = MetricHelper.ComputePercentile(times, 50).Ticks;
            if (_hidden[r]) continue;
            low  = Math.Min(low, times[0].Ticks);
            high = Math.Max(high, times[^1].Ticks);
        }

        if (Relative)
        {
            List<List<TimeTicks>> shown = [];
            for (int r = 0; r < Rooms; r++)
                if (!_hidden[r]) shown.Add(SortedRoomTimes(r));
            (double lowPct, double highPct) = MetricHelper.RelativeRange(shown);
            (lowPct, highPct) = _roomZoom.Range(Numbers.Percent, lowPct, highPct);
            _roomPercents = new Scale<double>(Numbers.Percent, lowPct, highPct, Plot.Bottom, Plot.Y);
            Paint.YAxis(Sketch, RoomArea, _roomPercents.Ticks(maxTicks), roomAxis);
            // 100% is every room's own median.
            float median = MathF.Round(_roomPercents.At(100.0));
            if (median >= Plot.Y && median <= Plot.Bottom)
                Sketch.Fill(new Area(RoomArea.X, median - 1f, RoomArea.Width, 2f), Rgba.Yellow.Times(0.5f));
        }
        else
        {
            (long min, long max) = Widened(low, high);
            (min, max) = _roomZoom.Range(Durations.Times, min, max);
            _roomTimes = new Scale<long>(Durations.Times, min, max, Plot.Bottom, Plot.Y);
            Paint.YAxis(Sketch, RoomArea, _roomTimes.Ticks(maxTicks), roomAxis);
        }

        // The segment, on the right axis, with grid lines over its own column only.
        (long segMin, long segMax) = SegmentRange();
        (segMin, segMax) = _segmentZoom.Range(Durations.Times, segMin, segMax);
        SegmentY = new Scale<long>(Durations.Times, segMin, segMax, Plot.Bottom, Plot.Y);
        IReadOnlyList<PlacedTick<long>> segmentTicks = SegmentY.Ticks(maxTicks);
        foreach (PlacedTick<long> tick in segmentTicks)
            Sketch.Fill(new Area(SegmentArea.X, MathF.Round(tick.At), SegmentArea.Width, 1f), style.Grid);
        Paint.YAxisRight(Sketch, Plot, segmentTicks, style, style.Label with { Colour = segment });

        _bar.Place(Sketch.Metrics, style, Panel.X + Panel.Width / 2f, Panel.Bottom - 6f, 0.5f, 1f, Panel);

        DrawData(style);

        Sketch.Fill(new Area(MathF.Round(split), Plot.Y, 2f, Plot.Height), GameCanvas.ToRgba(Color.Gray * 0.85f));
        DrawHiddenStubs(Bands.Scrolling);
        DrawAxisLines(rightAxis: true);

        // Room names in the room colour, the segment's in its own.
        string[] roomNames = new string[Bands.Scrolling.Count], segmentName = new string[Rooms + 1];
        for (int i = 0; i < roomNames.Length; i++) roomNames[i] = i < Rooms ? RoomName(i) : "";
        for (int i = 0; i <= Rooms; i++) segmentName[i] = i < Rooms ? "" : Dialog.Clean(DialogIds.ChartSegment);
        // One row while the rooms scroll: the bar is under the names.
        Paint.BandLabels(Scrolled, Bands.Scrolling, roomNames, Plot.Bottom, style, Columns.Scrolls ? LabelFit.Shorten : LabelFit.Stagger);
        if (Columns.Scrolls)
            Paint.BandLabels(Sketch, new Bands(1, SegmentArea.X, SegmentArea.Width), [segmentName[Rooms]], Plot.Bottom, style, LabelFit.Shorten);
        else
            Paint.BandLabels(Sketch, Bands, segmentName, Plot.Bottom, style, LabelFit.Stagger);
    }

    protected override void DrawOver()
    {
        // Cut to the rooms: the segment has no strip, it cannot be hidden.
        DrawStrips(Bands.Scrolling, Shift, RoomArea.X, RoomArea.Right);
    }

    protected override bool UpdateOwnControls(Pointer pointer)
    {
        _bar.Update(pointer);
        if (_mode.Changed)
        {
            OnModeChosen(Relative);
            // A zoom on times means nothing on percents.
            _roomZoom.Reset();
            Invalidate();
        }
        // Not short-circuited: a subclass reads its own buttons of this bar there.
        bool held = UpdateMarks(pointer) | _bar.Contains(pointer.X, pointer.Y) | UpdateColumns(pointer);
        if (!Zoomable) return held;

        _roomZoom.Update(pointer, RoomArea);
        _segmentZoom.Update(pointer, SegmentArea);
        if (_roomZoom.Changed || _segmentZoom.Changed) Invalidate();
        return held || Panning;
    }

    protected override bool IsZoomed => _roomZoom.Zoomed || _segmentZoom.Zoomed;

    internal override void TakeViewFrom(BaseChartOverlay previous)
    {
        base.TakeViewFrom(previous);
        if (previous is RoomColumnsChart chart) (_roomZoom, _segmentZoom) = (chart._roomZoom, chart._segmentZoom);
    }

    protected override void ResetZoom()
    {
        _roomZoom.Reset();
        _segmentZoom.Reset();
        Invalidate();
    }

    internal override CursorShape Cursor => Panning ? _roomZoom.Cursor : base.Cursor;

    protected override void DrawOwnControls() => GameCanvas.Draw(_bar.Sketch);

    public override int? ColumnHitTest(Vector2 mousePos)
    {
        EnsureBuilt();
        return StripAt(Bands.Scrolling, mousePos, Shift, RoomArea.X, RoomArea.Right);
    }
}
