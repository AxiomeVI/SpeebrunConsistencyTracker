#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>A value with a span round it: a median and its middle half, a best and a worst. The mod computes all three.</summary>
internal readonly record struct ValueRange<T>(T Low, T Value, T High);

/// <summary>Two values of one category joined: last session and this one, a room's median before and after practice.</summary>
internal readonly record struct Dumbbell<T>(T From, T To);

// Error bars, dumbbells and lollipops: a category's values as dots and lines rather than bars, read by
// position. Each comes upright (categories across, values up) and across (categories as rows, values along x),
// as bars do; drawing is fills only, so every line is crisp.
internal static partial class Paint {
    /// <summary>
    ///     Error bars, upright: in each band a line from Low to High with a cap at each end and a dot at Value,
    ///     all in <paramref name="colour"/> (the dot in <paramref name="dot"/> when given).
    /// </summary>
    public static void Ranges<T, TBands>(Sketch sketch, TBands bands, Scale<T> y, IReadOnlyList<ValueRange<T>> ranges, Rgba colour,
                                         ChartStyle style, Rgba? dot = null) where TBands : IBandLayout {
        for (int i = 0; i < Math.Min(bands.Count, ranges.Count); i++)
            RangeMark(sketch, bands.BarStart(i) + bands.BarWidthOf(i) / 2f, bands.BarWidthOf(i), y.At(ranges[i].Low), y.At(ranges[i].Value),
                      y.At(ranges[i].High), upright: true, colour, dot ?? colour, style);
    }

    /// <summary>Error bars across: in each row a line from Low to High along x, capped, with a dot at Value.</summary>
    public static void RangesAcross<T, TBands>(Sketch sketch, TBands rows, Scale<T> x, IReadOnlyList<ValueRange<T>> ranges, Rgba colour,
                                               ChartStyle style, Rgba? dot = null) where TBands : IBandLayout {
        for (int i = 0; i < Math.Min(rows.Count, ranges.Count); i++)
            RangeMark(sketch, rows.BarStart(i) + rows.BarWidthOf(i) / 2f, rows.BarWidthOf(i), x.At(ranges[i].Low), x.At(ranges[i].Value),
                      x.At(ranges[i].High), upright: false, colour, dot ?? colour, style);
    }

    /// <summary>
    ///     Dumbbells across, the usual way: in each row a dot at From in <paramref name="fromColour"/>, a dot at To in
    ///     <paramref name="toColour"/>, and a line between in the colour <paramref name="line"/> gives the row —
    ///     the mod says what a change means (faster in green, slower in orange).
    /// </summary>
    public static void DumbbellsAcross<T, TBands>(Sketch sketch, TBands rows, Scale<T> x, IReadOnlyList<Dumbbell<T>> dumbbells,
                                                  Rgba fromColour, Rgba toColour, Func<int, Rgba> line, ChartStyle style)
        where TBands : IBandLayout {
        for (int i = 0; i < Math.Min(rows.Count, dumbbells.Count); i++)
            DumbbellMark(sketch, rows.BarStart(i) + rows.BarWidthOf(i) / 2f, rows.BarWidthOf(i), x.At(dumbbells[i].From), x.At(dumbbells[i].To), upright: false,
                         fromColour, toColour, line(i), style, fromDot: true);
    }

    /// <summary>Dumbbells upright: in each band, From and To up the value axis, joined.</summary>
    public static void Dumbbells<T, TBands>(Sketch sketch, TBands bands, Scale<T> y, IReadOnlyList<Dumbbell<T>> dumbbells,
                                            Rgba fromColour, Rgba toColour, Func<int, Rgba> line, ChartStyle style)
        where TBands : IBandLayout {
        for (int i = 0; i < Math.Min(bands.Count, dumbbells.Count); i++)
            DumbbellMark(sketch, bands.BarStart(i) + bands.BarWidthOf(i) / 2f, bands.BarWidthOf(i), y.At(dumbbells[i].From), y.At(dumbbells[i].To), upright: true,
                         fromColour, toColour, line(i), style, fromDot: true);
    }

    /// <summary>
    ///     Lollipops upright: a thin line from the baseline to each value and a dot on its end — a bar chart's
    ///     reading with less ink, for many categories or values close together.
    /// </summary>
    public static void Lollipops<T, TBands>(Sketch sketch, TBands bands, Scale<T> y, IReadOnlyList<T> values, T baseline, Rgba colour,
                                            ChartStyle style) where TBands : IBandLayout {
        float from = y.At(baseline);
        for (int i = 0; i < Math.Min(bands.Count, values.Count); i++)
            DumbbellMark(sketch, bands.BarStart(i) + bands.BarWidthOf(i) / 2f, bands.BarWidthOf(i), from, y.At(values[i]), upright: true, colour, colour, colour, style,
                         fromDot: false);
    }

    /// <summary>Lollipops across: from the baseline along x in each row.</summary>
    public static void LollipopsAcross<T, TBands>(Sketch sketch, TBands rows, Scale<T> x, IReadOnlyList<T> values, T baseline, Rgba colour,
                                                  ChartStyle style) where TBands : IBandLayout {
        float from = x.At(baseline);
        for (int i = 0; i < Math.Min(rows.Count, values.Count); i++)
            DumbbellMark(sketch, rows.BarStart(i) + rows.BarWidthOf(i) / 2f, rows.BarWidthOf(i), from, x.At(values[i]), upright: false, colour, colour, colour, style,
                         fromDot: false);
    }

    // A line along the value axis from low to high at `centre` across, a cap at each end half the bar wide (at
    // most 16 px), and a dot at the value.
    private static void RangeMark(Sketch sketch, float centre, float barWidth, float low, float value, float high, bool upright,
                                  Rgba colour, Rgba dot, ChartStyle style) {
        if (barWidth <= 0f) return;   // a hidden column
        float c = MathF.Round(centre), cap = MathF.Round(Math.Min(barWidth / 2f, 16f)), t = Math.Max(1f, style.LineThickness / 2f);
        AlongLine(sketch, c, low, high, upright, t, colour);
        Cap(sketch, c, low, cap, t, upright, colour);
        Cap(sketch, c, high, cap, t, upright, colour);
        Dot(sketch, c, value, upright, style.PointSize + 2f, dot);
    }

    private static void Cap(Sketch sketch, float centre, float at, float length, float thickness, bool upright, Rgba colour) {
        float e = MathF.Round(at - thickness / 2f), from = MathF.Round(centre - length / 2f);
        sketch.Fill(upright ? new Area(from, e, length, thickness) : new Area(e, from, thickness, length), colour);
    }

    private static void DumbbellMark(Sketch sketch, float centre, float barWidth, float from, float to, bool upright, Rgba fromColour,
                                     Rgba toColour, Rgba line, ChartStyle style, bool fromDot) {
        if (barWidth <= 0f) return;   // a hidden column
        float c = MathF.Round(centre);
        AlongLine(sketch, c, from, to, upright, Math.Max(1f, style.LineThickness), line);
        if (fromDot) Dot(sketch, c, from, upright, style.PointSize + 2f, fromColour);
        Dot(sketch, c, to, upright, style.PointSize + 2f, toColour);
    }

    private static void AlongLine(Sketch sketch, float centre, float a, float b, bool upright, float thickness, Rgba colour) {
        float from = MathF.Round(Math.Min(a, b)), to = MathF.Round(Math.Max(a, b));
        if (to <= from) return;
        float half = MathF.Floor(thickness / 2f);
        sketch.Fill(upright ? new Area(centre - half, from, thickness, to - from) : new Area(from, centre - half, to - from, thickness), colour);
    }

    private static void Dot(Sketch sketch, float centre, float at, bool upright, float size, Rgba colour) {
        if (upright) Point(sketch, centre, at, colour, size);
        else Point(sketch, at, centre, colour, size);
    }

    /// <summary>
    ///     A calendar's days in the colours <paramref name="colour"/> gives them: <see cref="Rgba.Transparent"/> is a
    ///     day with no data, drawn as an outline (as a heatmap's missing cell); days outside the calendar have no
    ///     cell. Month names above the column each month starts in, and Mon, Wed and Fri on the left (both in
    ///     English unless the mod passes its own).
    /// </summary>
    public static void Calendar(Sketch sketch, CalendarGrid calendar, Func<DateTime, Rgba> colour, ChartStyle style,
                                Func<DateTime, string>? month = null, IReadOnlyList<string>? weekdays = null) {
        CellGrid grid = calendar.Grid;
        for (int column = 0; column < grid.ColumnCount; column++)
            for (int row = 0; row < 7; row++) {
                DateTime day = calendar.DayOf(column, row);
                if (!calendar.Holds(day)) continue;
                Rgba c = colour(day);
                if (c.IsInvisible) sketch.Outline(grid.Cell(column, row), style.Grid);
                else sketch.Fill(grid.Cell(column, row), c);
            }

        month ??= CalendarGrid.EnglishMonth;
        float lastRight = float.NegativeInfinity, top = grid.Bounds.Y - 4f;
        foreach ((int column, DateTime start) in calendar.MonthStarts()) {
            float x = grid.Columns.BandStart(column);
            string name = month(start);
            if (x < lastRight + style.LabelSpacing) continue;
            sketch.Text(name, x, top, style.Label, alignY: 1f);
            lastRight = x + sketch.Width(name, style.Label);
        }
        IReadOnlyList<string> names = weekdays ?? DefaultWeekdays;
        for (int row = 0; row < Math.Min(7, names.Count); row += 2)
            if (names[row].Length > 0)
                sketch.Text(names[row], grid.Bounds.X - style.LabelGap, grid.Rows.Centre(row), style.Label, alignX: 1f, alignY: 0.5f);
    }

    // Every other weekday, from Monday: enough to read the rows by, and they fit small cells.
    private static readonly string[] DefaultWeekdays = ["Mon", "", "Wed", "", "Fri", "", "Sun"];

    /// <summary>
    ///     A shaded band across the plot between two values (a target range, "within 3 frames of the target"),
    ///     in the reference colour, faint, with a line at each edge and its label at the right, above.
    /// </summary>
    public static void ReferenceBandY<T>(Sketch sketch, Area plot, Scale<T> y, T from, T to, string label, ChartStyle style) {
        float a = MathF.Round(Math.Clamp(y.At(from), plot.Y, plot.Bottom)), b = MathF.Round(Math.Clamp(y.At(to), plot.Y, plot.Bottom));
        float top = Math.Min(a, b), bottom = Math.Max(a, b);
        if (bottom <= top) return;
        sketch.Fill(new Area(plot.X, top, plot.Width, bottom - top), style.Reference.Times(0.14f));
        float edge = Math.Min(MathF.Max(1f, MathF.Round(style.ReferenceThickness)), (bottom - top) / 2f);
        sketch.Fill(new Area(plot.X, top, plot.Width, edge), style.Reference.Times(0.6f));
        sketch.Fill(new Area(plot.X, bottom - edge, plot.Width, edge), style.Reference.Times(0.6f));
        sketch.Text(label, plot.Right - style.LabelGap / 2f, top - 2f, style.Label with { Colour = style.Reference }, alignX: 1f, alignY: 1f);
    }

    /// <summary>A shaded band down the plot between two values on the x axis, its label at the top inside it.</summary>
    public static void ReferenceBandX<T>(Sketch sketch, Area plot, Scale<T> x, T from, T to, string label, ChartStyle style) {
        float a = MathF.Round(Math.Clamp(x.At(from), plot.X, plot.Right)), b = MathF.Round(Math.Clamp(x.At(to), plot.X, plot.Right));
        float left = Math.Min(a, b), right = Math.Max(a, b);
        if (right <= left) return;
        sketch.Fill(new Area(left, plot.Y, right - left, plot.Height), style.Reference.Times(0.14f));
        float edge = Math.Min(MathF.Max(1f, MathF.Round(style.ReferenceThickness)), (right - left) / 2f);
        sketch.Fill(new Area(left, plot.Y, edge, plot.Height), style.Reference.Times(0.6f));
        sketch.Fill(new Area(right - edge, plot.Y, edge, plot.Height), style.Reference.Times(0.6f));
        sketch.Text(label, left + style.LabelGap / 2f, plot.Y + 2f, style.Label with { Colour = style.Reference });
    }

    /// <summary>
    ///     The area under a line, down to <paramref name="baseline"/> (a pixel: the plot's bottom, or zero's), as
    ///     <see cref="Band"/> fills between two lines. Draw the line itself over it.
    /// </summary>
    public static void AreaUnder(Sketch sketch, LinePoints line, float baseline, Rgba colour, float strip = 2f) {
        if (line.Count < 2) return;
        float[] flat = new float[line.Count];
        Array.Fill(flat, baseline);
        Band(sketch, new LinePoints(line.X, flat), line, colour, strip);
    }

    /// <summary>
    ///     A sparkline: values in order as a line across <paramref name="area"/>, no axes, scaled from
    ///     <paramref name="min"/> to <paramref name="max"/> (pass one range to several sparklines so they compare),
    ///     with the last value as a dot. Returns the points, for hover.
    /// </summary>
    public static LinePoints Sparkline<T>(Sketch sketch, Area area, IValueKind<T> kind, IReadOnlyList<T> values, T min, T max, Rgba colour,
                                          ChartStyle style, bool lastDot = true) {
        Scale<T> y = new(kind, min, max, area.Bottom - 2f, area.Y + 2f);
        float[] xs = new float[values.Count], ys = new float[values.Count];
        for (int i = 0; i < values.Count; i++) {
            xs[i] = values.Count == 1 ? area.X + area.Width / 2f : area.X + 2f + (area.Width - 4f) * i / (values.Count - 1);
            ys[i] = y.At(values[i]);
        }
        LinePoints points = new(xs, ys);
        Line(sketch, points, colour, Math.Max(1f, style.LineThickness / 2f));
        if (lastDot && values.Count > 0) Point(sketch, xs[^1], ys[^1], colour, style.PointSize - 1f);
        return points;
    }

    /// <summary>Sparkline bars: a thin bar per value up from <paramref name="min"/>, across <paramref name="area"/>, no axes.</summary>
    public static Bands SparkBars<T>(Sketch sketch, Area area, IValueKind<T> kind, IReadOnlyList<T> values, T min, T max, Rgba colour) {
        Scale<T> y = new(kind, min, max, area.Bottom, area.Y);
        Bands bands = new(values.Count, area.X, area.Width, fill: 0.7f, wholePixels: true);
        for (int i = 0; i < values.Count; i++) sketch.Fill(Bar(bands, i, area.Bottom, y.At(values[i])), colour);
        return bands;
    }

    /// <summary>
    ///     Each hidden band as a faint stub down the plot, lit while <paramref name="hot"/> names it (the mouse is
    ///     on it: a click shows it again).
    /// </summary>
    public static void HiddenStubs(Sketch sketch, HideableBands bands, Area plot, int hot, ChartStyle style) {
        for (int i = 0; i < bands.Count; i++) {
            if (!bands.IsHidden(i)) continue;
            Area stub = new(MathF.Round(bands.BandStart(i)) + 1f, plot.Y, Math.Max(1f, bands.Stub - 2f), plot.Height);
            sketch.Fill(stub, i == hot ? style.Hovered.Times(0.35f) : style.Axis.Times(0.35f));
        }
    }

    /// <summary>
    ///     The strip under each band that a click hides or shows (<see cref="HideableBands.StripOf"/>), from
    ///     <paramref name="top"/> down <paramref name="height"/>, in the bands' own coordinates. A hidden band's strip is
    ///     always drawn, faint and brighter while <paramref name="hot"/> names it; a shown band's only while hot. A strip not
    ///     wholly inside [<paramref name="from"/>, <paramref name="to"/>] is left out. The tint is <c>style.Hovered</c>.
    /// </summary>
    public static void HideStrips(Sketch sketch, HideableBands bands, float top, float height, int hot, ChartStyle style,
                                  float maxWidth = 60f, float from = float.NegativeInfinity, float to = float.PositiveInfinity) {
        for (int i = 0; i < bands.Count; i++) {
            bool isHot = i == hot;
            float alpha = bands.IsHidden(i) ? (isHot ? 0.35f : 0.15f) : (isHot ? 0.25f : 0f);
            if (alpha <= 0f) continue;
            (float start, float width) = bands.StripOf(i, maxWidth);
            if (start < from || start + width > to) continue;
            sketch.Fill(new Area(start, top, width, height), style.Hovered.Times(alpha));
        }
    }
}
