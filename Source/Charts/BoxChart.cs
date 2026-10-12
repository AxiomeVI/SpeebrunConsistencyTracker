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

// One box per room, and the segment's.
//
// The five numbers of a box are MetricHelper.ComputePercentile's, the ones the text overlay and the
// CSV print; the library only places them.
internal sealed class BoxChart : RoomColumnsChart
{
    private readonly List<List<TimeTicks>> _roomTimes;
    private readonly List<TimeTicks> _segmentTimes;

    private readonly record struct Column(long Min, long Q1, long Median, long Q3, long Max, BoxMarks Marks);

    private Column?[] _columns = [];
    private int _hoverBuiltFor = -1;
    private float _hoverShift;
    private Sketch _stats;

    public BoxChart(List<List<TimeTicks>> roomTimes, List<TimeTicks> segmentTimes)
        : base(Dialog.Clean(DialogIds.ChartBoxPlotTitle), relative: ChartOptions.BoxPlotRelative)
    {
        _roomTimes    = [.. roomTimes.Select(r => (List<TimeTicks>)[.. r.OrderBy(t => t)])];
        _segmentTimes = [.. segmentTimes.OrderBy(t => t)];
    }

    internal override ChartHelp Help => new(DialogIds.HintSubBox, DialogIds.HintAboutBox, DialogIds.HintMouseBox);
    protected override int Rooms => _roomTimes.Count;
    protected override string RoomName(int room) => RoomLabels.For(room);
    protected override List<TimeTicks> SortedRoomTimes(int room) => _roomTimes[room];
    protected override List<TimeTicks> SortedSegmentTimes => _segmentTimes;
    protected override float ColumnFill => 0.4f;

    protected override void OnModeChosen(bool relative) => ChartOptions.BoxPlotRelative = relative;

    protected override void DrawData(ChartStyle style)
    {
        _hoverBuiltFor = -1;
        ChartPalette palette = ChartPalette.Current;
        ChartStyle roomStyle    = style with { Data = GameCanvas.ToRgba(palette.Room), BoxFill = GameCanvas.ToRgba(palette.RoomFill), Median = Rgba.White };
        ChartStyle segmentStyle = style with { Data = GameCanvas.ToRgba(palette.Segment), BoxFill = GameCanvas.ToRgba(palette.SegmentFill), Median = Rgba.White };

        _columns = new Column?[Rooms + 1];
        for (int r = 0; r < Rooms; r++)
        {
            if (!RoomDrawn(r)) continue;
            int room = r;
            _columns[r] = FiveOf(_roomTimes[r], ticks => RoomAt(room, ticks));
        }
        if (_segmentTimes.Count > 0) _columns[SegmentColumn] = FiveOf(_segmentTimes, SegmentY.At);

        for (int i = 0; i <= Rooms; i++)
            if (_columns[i] is { } column) Paint.Box(i == SegmentColumn ? Sketch : Scrolled, Bands, i, column.Marks, i == SegmentColumn ? segmentStyle : roomStyle);
    }

    // ComputePercentile is the single source of the quartiles: the drawing and the tooltip read
    // the same Column.
    private static Column FiveOf(List<TimeTicks> sorted, Func<long, float> at)
    {
        long min = sorted[0].Ticks, max = sorted[^1].Ticks;
        long q1  = MetricHelper.ComputePercentile(sorted, 25).Ticks;
        long med = MetricHelper.ComputePercentile(sorted, 50).Ticks;
        long q3  = MetricHelper.ComputePercentile(sorted, 75).Ticks;
        return new Column(min, q1, med, q3, max, new BoxMarks(at(min), at(q1), at(med), at(q3), at(max), []));
    }

    private const string BoxKey = "box:";

    public override HoverInfo? HitTest(Vector2 mouseHudPos)
    {
        EnsureBuilt();
        if (!Plot.Contains(mouseHudPos.X, mouseHudPos.Y)) return null;

        int i = Bands.IndexAt(mouseHudPos.X, Shift);
        if (i < 0 || _columns[i] is not { } column) return null;
        // On the box or its whisker, not beside it.
        float left = Bands.BarStart(i) - ShiftOf(i);
        if (mouseHudPos.X < left || mouseHudPos.X > left + Bands.BarWidthOf(i) ||
            mouseHudPos.Y < column.Marks.High || mouseHudPos.Y > column.Marks.Low)
            return null;
        return InfoOf(i);
    }

    public override HoverInfo? Locate(string key)
    {
        if (key == null || !key.StartsWith(BoxKey) || !int.TryParse(key.AsSpan(BoxKey.Length), out int i)) return null;
        EnsureBuilt();
        if (i < 0 || i >= _columns.Length || _columns[i] == null) return null;
        // A room's box scrolled out of the plot does not show.
        float left = Bands.BarStart(i) - ShiftOf(i);
        return i < Rooms && (left + Bands.BarWidthOf(i) <= RoomArea.X || left >= RoomArea.Right) ? null : InfoOf(i);
    }

    // Empty label: DrawHighlight draws the statistics itself. Keyed on the column, or every box
    // would share one pin.
    private HoverInfo InfoOf(int i) => new("", new Vector2(Bands.Centre(i) - ShiftOf(i), Plot.Y), BoxKey + i, i);

    public override void DrawHighlight(HoverInfo info)
    {
        int i = info.Mark;
        if (i < 0 || i >= _columns.Length || _columns[i] is not { } column) return;
        float shift = ShiftOf(i);
        if (i != _hoverBuiltFor || shift != _hoverShift) BuildHighlight(i, column, shift);
        GameCanvas.Draw(Hover, -shift, 0f, i < Rooms ? RoomArea : Plot);
        GameCanvas.Draw(_stats);
    }

    // The box redrawn in white where it was built, and its five numbers beside where it shows,
    // each level with its own line.
    private void BuildHighlight(int index, Column column, float shift)
    {
        (_hoverBuiltFor, _hoverShift) = (index, shift);
        _stats ??= new Sketch(Sketch.Metrics);
        Hover.Clear();
        _stats.Clear();
        Paint.Box(Hover, Bands, index, column.Marks, Style with { Data = Rgba.White.Times(0.85f), BoxFill = Rgba.Transparent, Median = Rgba.White });

        // Y grows downward, so the slowest sits highest: Max, Q3, Median, Q1, Min.
        (string Name, long Ticks, float At)[] stats =
        [
            (Dialog.Clean(DialogIds.ChartStatMax),    column.Max,    column.Marks.High),
            ("Q3",                                    column.Q3,     column.Marks.Q3),
            (Dialog.Clean(DialogIds.ChartStatMedian), column.Median, column.Marks.Median),
            ("Q1",                                    column.Q1,     column.Marks.Q1),
            (Dialog.Clean(DialogIds.ChartStatMin),    column.Min,    column.Marks.Low),
        ];
        // A tab makes a column: the names line up on the left, the times on the right.
        string[] texts = [.. stats.Select(s => $"{s.Name}\t{new TimeTicks(s.Ticks)}")];
        float boxWidth = 0f, boxHeight = 0f;
        foreach (string text in texts)
        {
            (float w, float h) = Paint.TooltipSize(_stats, text, Style);
            boxWidth  = Math.Max(boxWidth, w);
            boxHeight = h;
        }
        float[] centres = LabelColumn.Spread([.. stats.Select(s => s.At)], boxHeight, Plot.Y + boxHeight / 2f, Plot.Bottom - boxHeight / 2f);
        float x = Bands.BarStart(index) - shift + Bands.BarWidthOf(index) + ChartConstants.Interactivity.TooltipBgPadding;
        for (int i = 0; i < texts.Length; i++)
            Paint.TooltipBox(_stats, texts[i], new Area(x, centres[i] - boxHeight / 2f, boxWidth, boxHeight), Style);
    }
}
