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

// The segment's histogram, then every room's, as tiles on one wall: two to a row, two rows in view,
// scrolled when there are more. A
// click on a tile opens that histogram full size; Back, a right click or a double-click returns to
// the wall, and Next and Previous move among the histograms while one is open.
//
// Open, the wall hands everything to the full-size HistogramChart: drawing, the mouse, its buttons.
internal sealed class HistogramWallChart : SketchChart
{
    private const float Gutter = 14f;
    private const int PerRow = 2, RowsInView = 2;

    private readonly string[] _names;
    // The segment's name on its full-size chart, with its room count.
    private readonly string _segmentTitle;
    private readonly TimeTicks[][] _sorted;
    private readonly Func<TimeTicks?> _target;
    private readonly HistogramChart[] _full;
    private int Tiles => _names.Length;
    private const int SegmentTile = 0;

    private ScrollView _view = new(id: CaptureIds.WallScroll);
    private FocusTransition _focus = new();
    // The histogram shown while one is open, once Next or Previous moved off the tile clicked; -1 before.
    private int _stepped = -1;
    private int OpenItem => _stepped >= 0 ? _stepped : _focus.Item;
    // Clicks made on the open histogram: a double-click counts once both of its clicks were.
    private int _openClicks;
    private readonly ButtonBar _backBar = new(captureId: CaptureIds.WallBack);
    private readonly BarButton _back;

    private Sketch _content, _over;
    private GridPlan _plan;
    private Area _window;
    private float _left;
    private int _hoveredTile = -1;

    public HistogramWallChart(List<string> roomNames, List<List<TimeTicks>> roomTimes, List<TimeTicks> segmentTimes, string segmentTitle,
                              Func<TimeTicks?> target)
        : base(Dialog.Clean(DialogIds.ChartWallTitle))
    {
        _names  = [Dialog.Clean(DialogIds.ChartSegment), .. roomNames];
        _sorted = [.. roomTimes.Prepend(segmentTimes).Select(times => (TimeTicks[])[.. times.OrderBy(t => t)])];
        _target = target;
        _segmentTitle = segmentTitle;
        _full   = new HistogramChart[Tiles];
        _back   = _backBar.Action(Dialog.Clean(DialogIds.ChartWallBack));
    }

    // An open histogram's own help while one is open.
    internal override ChartHelp Help =>
        _focus.ShowsContent ? Full(OpenItem).Help : new(DialogIds.HintSubWall, DialogIds.HintAboutWall, DialogIds.HintMouseWall);

    private HistogramChart Open => _focus.ShowsContent ? Full(OpenItem) : null;

    private HistogramChart Full(int tile) =>
        _full[tile] ??= new HistogramChart(tile == SegmentTile ? _segmentTitle : _names[tile], [.. _sorted[tile]], tile == SegmentTile, tile == SegmentTile ? _target : null);

    // ------------------------------------------------------------------------------ the wall

    protected override void Build(ChartStyle style)
    {
        _content ??= new Sketch(Sketch.Metrics);
        _over    ??= new Sketch(Sketch.Metrics);
        _content.Clear();
        _backBar.PlaceIn(Sketch.Metrics, style, Panel, WidgetAnchor.BottomLeft, 6f, 6f);

        // Under the title; the scrollbar takes the window's right edge.
        _window = new Area(Panel.X + 20f, Panel.Y + 68f, Panel.Width - 40f, Panel.Height - 88f);
        float width = _window.Width - _view.BarWidth - 8f;
        int rows = (Tiles + PerRow - 1) / PerRow;
        float tileWidth  = MathF.Floor((width - (PerRow - 1) * Gutter) / PerRow);
        float tileHeight = MathF.Floor((_window.Height - (RowsInView - 1) * Gutter) / RowsInView);
        float content    = rows * tileHeight + (rows - 1) * Gutter;
        _plan = new GridPlan(PerRow, rows, tileWidth, tileHeight, Gutter, content, content > _window.Height);
        _left = MathF.Round(_window.X + (width - _plan.ContentWidth) / 2f);
        _view.Resize(_window, _plan.ContentHeight);

        for (int i = 0; i < Tiles; i++) DrawTile(i, style);
    }

    // A tile in the content's own coordinates: y from 0 at the top of the wall.
    private Area TileArea(int tile)
    {
        (float x, float y) = _plan.TileAt(tile);
        return new Area(_left + MathF.Round(x), MathF.Round(y), MathF.Round(_plan.TileWidth), MathF.Round(_plan.TileHeight));
    }

    private Area TileOnScreen(int tile) => TileArea(tile).Offset(0f, _view.ScreenY(0f));

    private void DrawTile(int tile, ChartStyle style)
    {
        Area area = TileArea(tile);
        TimeTicks[] sorted = _sorted[tile];
        TextStyle small = SctChartStyle.Small(style);
        bool segment = tile == SegmentTile;
        ChartPalette palette = ChartPalette.Current;

        _content.Fill(area, Rgba.White.Times(0.05f));
        _content.Text(_names[tile], area.X + 8f, area.Y + 4f, style.Label with { Colour = GameCanvas.ToRgba(segment ? palette.Segment : palette.Room) });
        _content.Text(HistogramChart.RunCount(sorted.Length, segment), area.Right - 8f, area.Y + 4f, small, alignX: 1f);

        float head = _content.LineHeight(style.Label) + 10f, foot = _content.LineHeight(small) + 6f;
        Area plot = area.Inset(8f, head, 8f, foot);
        _content.Fill(new Area(plot.X, plot.Bottom, plot.Width, 1f), style.Axis);
        if (sorted.Length == 0 || plot.IsEmpty) return;

        (Bins<long> bins, BinCounts counts) = HistogramChart.BinsOf(sorted);
        double[] values = [.. counts.Counts.Select(c => (double)c)];
        Paint.SparkBars(_content, plot, Numbers.Integers, values, 0.0, Math.Max(1, counts.Tallest), GameCanvas.ToRgba(segment ? palette.SegmentFill : palette.RoomFill));

        // The median, as the full-size histogram marks it; the bins run evenly across the tile.
        Scale<long> x = new(Durations.Times, bins.All[0].Lower, bins.All[^1].Upper, plot.X, plot.Right);
        float median = MathF.Round(x.At(MetricHelper.ComputePercentile(sorted, 50).Ticks));
        _content.Fill(new Area(Math.Min(median, plot.Right - 1f), plot.Y, 1f, plot.Height), Rgba.White.Times(0.8f));

        _content.Text(new TimeTicks(bins.All[0].Lower).ToString(), plot.X, plot.Bottom + 3f, small);
        _content.Text(new TimeTicks(bins.All[^1].Upper).ToString(), plot.Right, plot.Bottom + 3f, small, alignX: 1f);
    }

    private int TileAt(float x, float y)
    {
        if (!_window.Contains(x, y) || _plan.Columns <= 0) return -1;
        for (int i = 0; i < Tiles; i++)
            if (TileArea(i).Contains(x, _view.ContentY(y))) return i;
        return -1;
    }

    protected override void DrawLayers() => DrawLayer(_content, 0f, _view.ScreenY(0f), _window);

    protected override void DrawOver()
    {
        _over.Clear();
        Paint.Scrollbar(_over, _view, Style);
        if (_focus.IsActive)
        {
            // The frame on its way between the tile and the full-size chart.
            _over.Fill(_focus.Frame, Rgba.Black.Times(0.6f * _focus.Amount));
            _over.Outline(_focus.Frame, Rgba.White.Times(0.8f));
        }
        else if (_hoveredTile >= 0)
        {
            _over.Outline(TileOnScreen(_hoveredTile).Intersect(_window), Rgba.White.Times(0.8f));
        }
        GameCanvas.Draw(_over);
    }

    protected override bool UpdateOwnControls(Pointer pointer)
    {
        _focus.Update(pointer.Input.Seconds);
        _hoveredTile = -1;
        if (_focus.IsActive) return true;

        _view.Update(pointer);
        if (!_view.DraggingContent && !_view.ThumbHot) _hoveredTile = TileAt(pointer.X, pointer.Y);
        _openClicks = 0;
        if (_hoveredTile >= 0 && pointer.Clicked())
        {
            _stepped = -1;
            _focus.Open(_hoveredTile, TileOnScreen(_hoveredTile).Intersect(_window), Panel);
        }
        // Nothing on the wall is pinned: the mouse is the wall's.
        return true;
    }

    // ------------------------------------------------------------------- one histogram, open

    public override void Render()
    {
        if (Open is { } open) open.Render();
        else base.Render();
    }

    internal override bool UpdateControls(Pointer pointer)
    {
        if (Open is not { } open) return base.UpdateControls(pointer);
        EnsureBuilt();
        _backBar.Update(pointer);
        if (pointer.Clicked()) _openClicks++;
        // Read before the histogram's own update: zoomed, a double-click is its Reset zoom.
        bool zoomed = open.ZoomedNow;
        bool onControl = open.UpdateControls(pointer) || _backBar.Contains(pointer.X, pointer.Y);
        // The double-click that opened the histogram is not one to close it: both clicks must be here.
        bool doubleClick = pointer.DoubleClicked() && _openClicks >= 2 && !zoomed && !onControl;
        if (_back.Clicked || pointer.Pressed(PointerButton.Right) || doubleClick)
        {
            _focus.Close();
            DropPins();
            return true;
        }
        return onControl;
    }

    internal override void DrawControls()
    {
        if (Open is not { } open)
        {
            base.DrawControls();
            return;
        }
        open.DrawControls();
        GameCanvas.Draw(_backBar.Sketch);
    }

    internal override void TakeViewFrom(BaseChartOverlay previous)
    {
        base.TakeViewFrom(previous);
        // Where the wall is scrolled, and the histogram that is open.
        if (previous is HistogramWallChart wall && wall.Tiles == Tiles) (_view, _focus, _stepped) = (wall._view, wall._focus, wall._stepped);
    }

    // Coming back shows the wall, not the histogram left open.
    internal override void OnLeft() => (_focus, _stepped) = (new FocusTransition(), -1);

    // Open, Next and Previous go round the histograms, the segment's first.
    internal override bool Step(int steps)
    {
        if (Open == null) return false;
        _stepped = ((OpenItem + steps) % Tiles + Tiles) % Tiles;
        _openClicks = 0;
        DropPins();
        return true;
    }

    internal override float ToolsLeft => Open?.ToolsLeft ?? base.ToolsLeft;

    public override HoverInfo? HitTest(Vector2 mouseHudPos) => Open?.HitTest(mouseHudPos);

    public override HoverInfo? Locate(string key) => Open?.Locate(key);

    public override void DrawHighlight(HoverInfo info) => Open?.DrawHighlight(info);

    internal override int Revision => Open is { } open ? HashCode.Combine(OpenItem, open.Revision) : base.Revision;

    // Back stands in the corner while a histogram is open: the pins' buttons go after it.
    internal override float FootLeft
    {
        get
        {
            if (Open == null) return base.FootLeft;
            EnsureBuilt();
            return _backBar.Bounds.Right + 8f;
        }
    }
}
