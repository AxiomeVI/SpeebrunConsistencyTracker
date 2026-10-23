using System;
using System.IO;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// A chart drawn through CelesteCharts: built into a sketch when its look or its hidden columns
// change, and drawn from the sketch every frame. A new session version gives a new chart, so the
// data never changes under one.
internal abstract class SketchChart : BaseChartOverlay
{
    protected Sketch Sketch { get; private set; }
    protected Sketch Hover { get; private set; }
    protected ChartStyle Style { get; private set; }
    protected Area Panel { get; private set; }
    protected Area Plot { get; private set; }

    // The buttons in the panel's top right corner: Save PNG, then the chart's own.
    protected ButtonBar Tools { get; } = new(captureId: CaptureIds.Tools);
    private readonly BarButton _savePng;
    // In the bottom left corner while the chart is zoomed.
    private readonly ButtonBar _zoomBar = new(captureId: CaptureIds.ResetZoom);
    private readonly BarButton _resetZoom;

    // Columns that are too many for the plot keep a least width and scroll sideways. A chart with
    // columns sizes this in Build and draws what moves with them into Scrolled, where the first
    // column starts at the plot's left. Scrolled is drawn moved by -Shift and cut to ColumnsClip, so
    // a scroll rebuilds nothing; the mouse is on a mark at its x plus Shift.
    protected ScrollView Columns { get; private set; } = new(id: CaptureIds.ColumnScroll, horizontal: true);
    protected Sketch Scrolled { get; private set; }
    protected Area ColumnsClip { get; set; }
    private Sketch _scrollbar;
    protected float Shift => Columns.Scrolls ? Columns.DrawOffset : 0f;
    private int _builds;
    internal override int Revision => HashCode.Combine(_builds, Shift);

    private bool _stale = true;
    // Set while the chart is being drawn into an image instead of onto the HUD.
    private ChartPng _png;
    // True while the chart is built for a saved image, which has no buttons: what stands beside one
    // on the HUD is laid out alone.
    protected bool ForImage { get; private set; }

    protected SketchChart(string title) : base(title)
    {
        _savePng   = Tools.Action(Dialog.Clean(DialogIds.ChartSavePng));
        _resetZoom = _zoomBar.Action(Dialog.Clean(DialogIds.ChartResetZoom));
    }

    // A chart that zooms says when, and how to show everything again.
    // A chart's own controls in the panel's bottom left corner: it places them here, updates and
    // draws them itself, and answers where the next bar may start. Reset zoom and the pins'
    // buttons stand from there.
    protected virtual float PlaceCorner() => Panel.X + 6f;
    private float _footX;
    // What follows the title on its line, a count of what the chart holds: "7 runs". Not part of
    // the title, which also names the saved image.
    protected virtual string TitleNote => null;
    protected virtual bool IsZoomed => false;
    internal bool ZoomedNow => IsZoomed;
    protected virtual void ResetZoom() { }

    protected abstract void Build(ChartStyle style);

    // The chart's layers over its frame, each through DrawLayer: data kept in a sketch of its own
    // so it can be cut to the plot or moved. They are in a saved image too.
    protected virtual void DrawLayers() { }

    // Immediate drawing over the chart: what follows the mouse. Not in a saved image.
    protected virtual void DrawOver() { }

    protected void Invalidate() => _stale = true;

    // How far the plot sits from the panel's left and right sides. A chart whose labels outgrow the
    // default widens its own; Sketch is ready to measure with.
    protected virtual (float Left, float Right) SideMargins(ChartStyle style) => (marginH, marginH);

    // A setting the chart reads live (the target time): a change rebuilds the sketch, and the chart
    // keeps what the player did to it, which a new chart would not.
    protected virtual int LiveKey => 0;
    private int _liveKey;

    public override void Render()
    {
        EnsureBuilt();
        GameCanvas.Draw(Sketch);
        DrawLayer(Scrolled, -Shift, 0f, ColumnsClip);
        DrawLayers();
        if (Columns.Scrolls)
        {
            _scrollbar.Clear();
            Paint.Scrollbar(_scrollbar, Columns, Style);
            GameCanvas.Draw(_scrollbar);
        }
        DrawOver();
    }

    protected void DrawLayer(Sketch sketch) => DrawLayer(sketch, 0f, 0f, Panel);

    protected void DrawLayer(Sketch sketch, float dx, float dy, Area window)
    {
        if (_png != null) _png.Draw(sketch, dx, dy, window);
        else GameCanvas.Draw(sketch, dx, dy, window);
    }

    // ⚠️ From an Update only: ChartPng opens a sprite batch of its own, which a Render is already inside.
    private void SavePng() => Export.DataExporter.SaveChartImage(title, path =>
    {
        try
        {
            ForImage = true;
            Invalidate();
            EnsureBuilt();
            using ChartPng png = new(Panel, Style.Panel);
            _png = png;
            png.Draw(Sketch);
            DrawLayer(Scrolled, -Shift, 0f, ColumnsClip);
            DrawLayers();
            png.Save(path);
        }
        finally
        {
            _png = null;
            ForImage = false;
            Invalidate();
        }
    });

    internal override bool UpdateControls(Pointer pointer)
    {
        EnsureBuilt();
        Tools.Update(pointer);
        if (_savePng.Clicked) SavePng();
        bool onZoomBar = false;
        if (IsZoomed)
        {
            _zoomBar.Update(pointer);
            if (_resetZoom.Clicked) ResetZoom();
            onZoomBar = _zoomBar.Contains(pointer.X, pointer.Y);
        }
        else
        {
            _zoomBar.Rest();
        }
        bool own = UpdateOwnControls(pointer);
        return own || onZoomBar || Tools.Contains(pointer.X, pointer.Y);
    }

    internal override void DrawControls()
    {
        GameCanvas.Draw(Tools.Sketch);
        if (IsZoomed) GameCanvas.Draw(_zoomBar.Sketch);
        DrawOwnControls();
    }

    internal override float FootLeft
    {
        get
        {
            EnsureBuilt();
            return IsZoomed ? _zoomBar.Bounds.Right + 8f : _footX;
        }
    }

    internal override float ToolsLeft
    {
        get
        {
            EnsureBuilt();
            return Tools.Bounds.X;
        }
    }

    // The chart's own controls. Update answers whether the mouse is on one, or held by one.
    protected virtual bool UpdateOwnControls(Pointer pointer) => false;
    protected virtual void DrawOwnControls() { }

    // Also from HitTest: the mouse is read in Update, which can run before the first Render.
    protected void EnsureBuilt()
    {
        ChartStyle current = SctChartStyle.Current;
        int live = LiveKey;
        if (live != _liveKey) (_liveKey, _stale) = (live, true);
        if (!_stale && ReferenceEquals(current, Style)) return;
        _stale = false;
        _builds++;
        Style  = current;
        // The game's fonts load after mods do, so the metrics are taken here and not in a constructor.
        Sketch ??= new Sketch(GameCanvas.FontMetrics());
        Hover  ??= new Sketch(Sketch.Metrics);
        Scrolled   ??= new Sketch(Sketch.Metrics);
        _scrollbar ??= new Sketch(Sketch.Metrics);
        Sketch.Clear();
        Scrolled.Clear();

        Panel = new Area(position.X, position.Y, width, height);
        (float leftMargin, float rightMargin) = SideMargins(Style);
        float left = System.MathF.Round(position.X + leftMargin), top = System.MathF.Round(position.Y + margin);
        Plot = new Area(left, top, System.MathF.Round(position.X + width - rightMargin) - left,
                        System.MathF.Round(position.Y + height - margin) - top);
        ColumnsClip = Panel;
        Sketch.Fill(Panel, Style.Panel);
        Sketch.Text(TitleNote is { Length: > 0 } note ? $"{title}  ·  {note}" : title, Panel.X + Panel.Width / 2f, Panel.Y + 10f, Style.Title, alignX: 0.5f);
        Sketch.Text(Dialog.Clean(Help.Subtitle), Panel.X + Panel.Width / 2f, Panel.Y + 42f,
                    Style.Label with { Colour = Rgba.White.Times(0.6f) }, alignX: 0.5f);
        Tools.PlaceIn(Sketch.Metrics, Style, Panel, WidgetAnchor.TopRight, 6f, 6f);
        _footX = PlaceCorner();
        _zoomBar.Place(Sketch.Metrics, Style, _footX, Panel.Bottom - 6f, 0f, 1f, Panel);
        Build(Style);
    }

    public override void ToggleColumn(int columnIndex)
    {
        base.ToggleColumn(columnIndex);
        Invalidate();
    }

    internal override void TakeViewFrom(BaseChartOverlay previous)
    {
        base.TakeViewFrom(previous);
        // The scroll itself: it holds how far it is, and the next build sizes it again.
        if (previous is SketchChart chart) Columns = chart.Columns;
        Invalidate();
    }

    public override void ClearHiddenColumns()
    {
        base.ClearHiddenColumns();
        Invalidate();
    }

    protected bool[] HiddenFlags(int count)
    {
        bool[] hidden = new bool[count];
        foreach (int column in _hiddenColumns)
            if (column >= 0 && column < count) hidden[column] = true;
        return hidden;
    }

    // The left axis line and the baseline, as wide as the style says, meeting square at the corner:
    // the baseline starts under the axis line and ends with the plot, or under a right axis.
    protected void DrawAxisLines(bool rightAxis = false)
    {
        float t = Style.AxisThickness, left = Plot.X - (t - 1f);
        Sketch.Fill(new Area(left, Plot.Y, t, Plot.Height), Style.Axis);
        Sketch.Fill(new Area(left, Plot.Bottom, Plot.Right - left + (rightAxis ? t : 0f), t), Style.Axis);
    }

    // A hidden column's stub, faint down the plot, so the strip under the axis that brings it back
    // reads as the foot of something and not as a stray tick.
    protected void DrawHiddenStubs(HideableBands bands) => Paint.HiddenStubs(Scrolled, bands, Plot, -1, Style);

    // Sizes the sideways scroll to columns laid out from `left` over `width`.
    // wholePlot: the wheel and a drag scroll from anywhere in the plot, and not
    // only from the names under it (a chart whose plot zooms keeps the wheel for that).
    // The bar lies just under the names, clear of the buttons at the foot of the panel.
    private const float ScrollStrip = ChartConstants.XAxisLabel.BaseOffsetY + 24f;
    // How far down a column's strip reaches: short of the bar while the columns scroll.
    protected float? StripHeight => Columns.Scrolls ? ScrollStrip - 2f : null;

    protected void PlaceColumns(float left, float width, float contentLength, bool wholePlot)
    {
        const float strip = ScrollStrip;
        float top = wholePlot ? Plot.Y : Plot.Bottom;
        Columns.Resize(new Area(left, top, width, Plot.Bottom + strip + Columns.BarWidth - top), contentLength);
        if (Columns.Scrolls) ColumnsClip = new Area(left, Panel.Y, width, Panel.Height);
    }

    // Reads the scroll; true while it holds the mouse.
    protected bool UpdateColumns(Pointer pointer)
    {
        if (!Columns.Scrolls) return false;
        Columns.Update(pointer);
        return pointer.CapturedBy == Columns.Id || Columns.Track.Contains(pointer.X, pointer.Y);
    }

    // The strip under each column that a click hides it from, tinted under the mouse: CelesteCharts'
    // own. The bands are where they were built and show moved left by `shift`, between left and
    // right. whole: a strip cut by either side is left out, as the trajectory's are when zoomed.
    private Sketch _strips;
    private int _hotStrip = -1;
    private const float StripMaxWidth = ChartConstants.Interactivity.ColumnStripMaxWidth;

    protected int? StripAt(HideableBands bands, Vector2 mouse, float shift, float left, float right, bool whole = false)
    {
        // The strip as drawn, from the axis down: nowhere it shows is dead to a click.
        bool inZone = mouse.Y >= Plot.Bottom && mouse.Y <= Plot.Bottom + StripsHeight && mouse.X >= left && mouse.X < right;
        _hotStrip = !inZone ? -1 : whole ? bands.StripAt(mouse.X + shift, StripMaxWidth, left + shift, right + shift)
                                         : bands.StripAt(mouse.X + shift, StripMaxWidth);
        return _hotStrip < 0 ? null : _hotStrip;
    }

    protected void NoStrip() => _hotStrip = -1;

    private float StripsHeight => StripHeight ?? ChartConstants.XAxisLabel.BaseOffsetY + ChartConstants.Interactivity.ColumnLabelHitZoneH;

    protected void DrawStrips(HideableBands bands, float shift, float left, float right, bool whole = false)
    {
        _strips ??= new Sketch(Sketch.Metrics);
        _strips.Clear();
        float height = StripsHeight;
        if (whole) Paint.HideStrips(_strips, bands, Plot.Bottom, height, _hotStrip, Style, StripMaxWidth, left + shift, right + shift);
        else Paint.HideStrips(_strips, bands, Plot.Bottom, height, _hotStrip, Style, StripMaxWidth);
        GameCanvas.Draw(_strips, -shift, 0f, new Area(left, Plot.Bottom, right - left, height));
    }

    // Around a mark of the Scrolled layer: moved and cut as it is.
    protected void DrawOutline(Area area)
    {
        Hover.Clear();
        Hover.Outline(area, Rgba.White.Times(0.8f));
        GameCanvas.Draw(Hover, -Shift, 0f, ColumnsClip);
    }
}
