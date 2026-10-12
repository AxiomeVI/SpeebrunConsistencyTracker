#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Painters: each adds the marks of one part of a chart to a sketch — axes, bars, a box, a title,
///     a tooltip. They take the geometry the other layers computed (<see cref="Scale{T}"/>,
///     <see cref="Bands"/>, <see cref="BoxMarks"/>), so what is drawn and what the mouse hits come
///     from the same numbers.
/// </summary>
// A plot is an Area: its left edge is the value axis, its bottom edge the baseline. Labels go outside
// it, so a mod sizes the plot inside its tile to leave room for them: YLabelWidth on the left,
// XLabelHeight below.
//
// ⚠️ Painters measure text and build strings: call them when the data or the size changes, never per frame.
internal static partial class Paint {
    /// <summary>
    ///     A title at the top-left of <paramref name="tile"/>, shortened to its width with
    ///     <see cref="TextFit.Truncate"/> (keeping everything from " · "). Returns the height it took.
    /// </summary>
    public static float Title(Sketch sketch, Area tile, string text, ChartStyle style) {
        string fitted = TextFit.Truncate(text, tile.Width, s => sketch.Width(s, style.Title));
        return sketch.Text(fitted, tile.X, tile.Y, style.Title).Height;
    }

    /// <summary>
    ///     The widest of these tick labels, so a mod can leave that much room left of its plot. It takes
    ///     the kind's own ticks (<see cref="IValueKind{T}.Ticks"/>), which need no pixels: the plot's
    ///     size depends on this width, and the scale's pixels on the plot.
    /// </summary>
    public static float YLabelWidth<T>(Sketch sketch, IReadOnlyList<Tick<T>> ticks, ChartStyle style) {
        float widest = 0f;
        foreach (Tick<T> tick in ticks) widest = Math.Max(widest, sketch.Width(tick.Label, style.Label));
        return widest;
    }

    /// <summary>
    ///     A second value axis on the plot's right edge: the axis line, a tick mark out of the plot at
    ///     every tick, and labels left-aligned beyond them. No grid lines: those follow the left axis. Its
    ///     scale must span the same pixels as the left one's — <see cref="Scale{T}.Linked{TR}"/> for the
    ///     same values read another way, or a scale of its own over the plot's height.
    /// </summary>
    public static void YAxisRight<T>(Sketch sketch, Area plot, IReadOnlyList<PlacedTick<T>> ticks, ChartStyle style,
                                     TextStyle? text = null) {
        TextStyle label = text ?? style.Label;
        sketch.Fill(new Area(plot.Right, plot.Y, style.AxisThickness, plot.Height), style.Axis);
        foreach (PlacedTick<T> tick in ticks) {
            if (tick.At < plot.Y - 0.5f || tick.At > plot.Bottom + 0.5f) continue;
            sketch.Fill(new Area(plot.Right, MathF.Round(tick.At), style.TickLength, 1f), style.Axis);
            sketch.Text(tick.Label, plot.Right + style.TickLength + style.LabelGap / 2f, tick.At, label, alignY: 0.5f);
        }
    }

    /// <summary>How much room <see cref="YAxisRight{T}"/> needs right of a plot for these ticks.</summary>
    public static float YRightWidth<T>(Sketch sketch, IReadOnlyList<Tick<T>> ticks, ChartStyle style) =>
        style.TickLength + style.LabelGap / 2f + YLabelWidth(sketch, ticks, style);

    /// <summary>
    ///     Labels placed by <see cref="LabelColumn.Place"/>, left-aligned at <paramref name="x"/> (a plot's
    ///     right edge plus a gap): a square of each line's colour, then the text in the first colour.
    ///     Hidden ones take their place and draw nothing.
    /// </summary>
    public static void SideLabels(Sketch sketch, IReadOnlyList<PlacedSideLabel> labels, float x, ChartStyle style) {
        float chip = MathF.Round(sketch.LineHeight(style.Label) * 0.4f);
        foreach (PlacedSideLabel label in labels) {
            if (label.Hidden) continue;
            float left = x;
            foreach (Rgba colour in label.Colours) {
                sketch.Fill(new Area(left, MathF.Round(label.At - chip / 2f), chip, chip), colour);
                left += chip + 3f;
            }
            sketch.Text(label.Text, left + 3f, label.At, style.Label with { Colour = label.Colours[0] }, alignY: 0.5f);
        }
    }

    /// <summary>
    ///     How much room <see cref="XAxis{T}"/> or <see cref="BandLabels{TBands}"/> needs below a plot; two
    ///     <paramref name="rows"/> for labels that may be staggered.
    /// </summary>
    public static float XLabelHeight(Sketch sketch, ChartStyle style, int rows = 1) =>
        style.TickLength + style.LabelGap / 2f + rows * sketch.LineHeight(style.Label);

    /// <summary>
    ///     A vertical value axis on the plot's left edge: a grid line across the plot at every tick, and
    ///     its label right-aligned left of the axis, centred on the tick.
    /// </summary>
    public static void YAxis<T>(Sketch sketch, Area plot, IReadOnlyList<PlacedTick<T>> ticks, ChartStyle style) {
        foreach (PlacedTick<T> tick in ticks) {
            if (tick.At < plot.Y - 0.5f || tick.At > plot.Bottom + 0.5f) continue;
            sketch.Fill(new Area(plot.X, MathF.Round(tick.At), plot.Width, 1f), style.Grid);
            sketch.Text(tick.Label, plot.X - style.LabelGap, tick.At, style.Label, alignX: 1f, alignY: 0.5f);
        }
        // A thicker axis grows away from the plot, so the plot keeps its pixels.
        sketch.Fill(new Area(plot.X - (style.AxisThickness - 1f), plot.Y, style.AxisThickness, plot.Height), style.Axis);
    }

    /// <summary>
    ///     A horizontal value axis on the plot's bottom edge: the baseline, a tick mark at every tick, and
    ///     labels centred under them, thinned to every Nth when they would overlap.
    /// </summary>
    public static void XAxis<T>(Sketch sketch, Area plot, IReadOnlyList<PlacedTick<T>> ticks, ChartStyle style) {
        sketch.Fill(new Area(plot.X, plot.Bottom, plot.Width, style.AxisThickness), style.Axis);
        float[] centres = new float[ticks.Count], widths = new float[ticks.Count];
        for (int i = 0; i < ticks.Count; i++) {
            centres[i] = ticks[i].At;
            widths[i] = sketch.Width(ticks[i].Label, style.Label);
        }
        int stride = TextFit.Stride(centres, widths, style.LabelSpacing);
        float labelTop = plot.Bottom + style.TickLength + style.LabelGap / 2f;
        for (int i = 0; i < ticks.Count; i++) {
            if (ticks[i].At < plot.X - 0.5f || ticks[i].At > plot.Right + 0.5f) continue;
            sketch.Fill(new Area(MathF.Round(ticks[i].At), plot.Bottom, 1f, style.TickLength), style.Axis);
            if (i % stride == 0) sketch.Text(ticks[i].Label, ticks[i].At, labelTop, style.Label, alignX: 0.5f);
        }
    }

    /// <summary>
    ///     One label under each band, centred, below <paramref name="top"/> (a plot's bottom), fitted as
    ///     <paramref name="fit"/> says when they would not all fit on one row.
    /// </summary>
    public static void BandLabels<TBands>(Sketch sketch, TBands bands, IReadOnlyList<string> labels, float top, ChartStyle style,
                                          LabelFit fit = LabelFit.Thin) where TBands : IBandLayout {
        // Starts at the same height as XAxis's labels, so both kinds of chart line up side by side.
        top += style.TickLength + style.LabelGap / 2f;
        int count = Math.Min(bands.Count, labels.Count);
        string[] shown = new string[count];
        float[] centres = new float[count], widths = new float[count];
        for (int i = 0; i < count; i++) {
            // A hidden column (no bar) has no label; its stub is too narrow for one.
            shown[i] = bands.BarWidthOf(i) <= 0f ? ""
                : fit == LabelFit.Shorten
                    ? TextFit.Truncate(labels[i], bands.WidthOf(i) - style.LabelSpacing, s => sketch.Width(s, style.Label), keepFrom: "")
                    : labels[i];
            centres[i] = bands.Centre(i);
            widths[i] = sketch.Width(shown[i], style.Label);
        }
        int stride = TextFit.Stride(centres, widths, style.LabelSpacing);

        // Staggered: every other label one line lower, when each row then fits on its own.
        if (fit == LabelFit.Stagger && stride > 1 && RowFits(centres, widths, 0, style) && RowFits(centres, widths, 1, style)) {
            float lower = top + sketch.LineHeight(style.Label);
            for (int i = 0; i < count; i++) sketch.Text(shown[i], centres[i], i % 2 == 0 ? top : lower, style.Label, alignX: 0.5f);
            return;
        }
        for (int i = 0; i < count; i += stride) sketch.Text(shown[i], centres[i], top, style.Label, alignX: 0.5f);
    }

    private static bool RowFits(float[] centres, float[] widths, int first, ChartStyle style) {
        for (int i = first + 2; i < centres.Length; i += 2)
            if (centres[i] - widths[i] / 2f - (centres[i - 2] + widths[i - 2] / 2f) < style.LabelSpacing) return false;
        return true;
    }

    /// <summary>
    ///     Each group's name under its items' labels, from <paramref name="top"/>: a bracket line across the
    ///     group, then the name centred on it, shortened to the group's width.
    /// </summary>
    /// <param name="folded">
    ///     For groups that fold (<see cref="RouteFold"/>): whether each is folded. Each name then starts
    ///     with a "+" (folded: a click opens it) or a "-" (open), and reads as clickable.
    /// </param>
    public static void GroupLabels(Sketch sketch, RouteAxis route, float top, ChartStyle style, Func<int, bool>? folded = null) {
        for (int g = 0; g < route.GroupCount; g++) {
            (float start, float end) = route.GroupSpan(g);
            sketch.Fill(new Area(start, top, end - start, 1f), style.Axis);
            string text = folded == null ? route.Groups[g].Name : (folded(g) ? "+ " : "- ") + route.Groups[g].Name;
            TextStyle label = folded == null ? style.Label : style.Label with { Colour = style.Header.Colour };
            string name = TextFit.Truncate(text, end - start, s => sketch.Width(s, label), keepFrom: "");
            sketch.Text(name, (start + end) / 2f, top + 3f, label, alignX: 0.5f);
        }
    }

    /// <summary>How much room <see cref="GroupLabels"/> needs.</summary>
    public static float GroupLabelHeight(Sketch sketch, ChartStyle style) => 3f + sketch.LineHeight(style.Label);

    /// <summary>A faint line down the plot in the middle of each gap between groups.</summary>
    public static void GroupDividers(Sketch sketch, RouteAxis route, Area plot, ChartStyle style) {
        for (int g = 1; g < route.GroupCount; g++) {
            float x = MathF.Round(route.GroupSpan(g).Start - route.GroupGap / 2f);
            sketch.Fill(new Area(x, plot.Y, 1f, plot.Height), style.Grid);
        }
    }

    /// <summary>
    ///     A vertical bar in each band, from <paramref name="baseline"/> to its value on
    ///     <paramref name="y"/>. A value equal to the baseline draws nothing.
    /// </summary>
    public static void Bars<T, TBands>(Sketch sketch, TBands bands, Scale<T> y, IReadOnlyList<T> values, T baseline, Rgba colour)
        where TBands : IBandLayout {
        float bottom = y.At(baseline);
        for (int i = 0; i < Math.Min(bands.Count, values.Count); i++) sketch.Fill(Bar(bands, i, bottom, y.At(values[i])), colour);
    }

    /// <summary>
    ///     A text over each bar's end: above a bar that rises from <paramref name="baseline"/>, below one that
    ///     falls. The texts are the mod's (it formats its own numbers); an empty one draws nothing.
    /// </summary>
    /// <param name="shrinkTo">
    ///     How far a text too wide for its band may shrink, as a share of <paramref name="text"/>'s scale: 1 never
    ///     shrinks. A text that still does not fit is left out.
    /// </param>
    public static void BarValues<T, TBands>(Sketch sketch, TBands bands, Scale<T> y, IReadOnlyList<T> values, T baseline,
                                            IReadOnlyList<string> labels, TextStyle text, float gap = 3f, float shrinkTo = 1f)
        where TBands : IBandLayout {
        float bottom = y.At(baseline);
        for (int i = 0; i < Math.Min(bands.Count, Math.Min(values.Count, labels.Count)); i++) {
            if (bands.BarWidthOf(i) <= 0f) continue;
            float end = y.At(values[i]);
            BarValue(sketch, bands.BarStart(i) + bands.BarWidthOf(i) / 2f, end, end > bottom, bands.WidthOf(i), labels[i], text, gap, shrinkTo);
        }
    }

    /// <summary><see cref="BarValues{T,TBands}"/> for <see cref="GroupedBars{T,TBands}"/>: a text over each series' bar, kept to that bar's own band.</summary>
    /// <param name="labels">Texts per series, then per category.</param>
    public static void GroupedBarValues<T, TBands>(Sketch sketch, TBands bands, Scale<T> y, IReadOnlyList<IReadOnlyList<T>> series, T baseline,
                                                   IReadOnlyList<IReadOnlyList<string>> labels, TextStyle text, float innerFill = 0.9f,
                                                   float gap = 3f, float shrinkTo = 1f) where TBands : IBandLayout {
        float bottom = y.At(baseline);
        for (int c = 0; c < bands.Count; c++) {
            if (bands.BarWidthOf(c) <= 0f) continue;
            Bands inner = Bands.Within(bands, c, series.Count, innerFill);
            for (int s = 0; s < Math.Min(series.Count, labels.Count); s++) {
                if (c >= series[s].Count || c >= labels[s].Count) continue;
                float end = y.At(series[s][c]);
                BarValue(sketch, inner.BarStart(s) + inner.BarWidthOf(s) / 2f, end, end > bottom, inner.WidthOf(s), labels[s][c], text, gap, shrinkTo);
            }
        }
    }

    private static void BarValue(Sketch sketch, float centre, float end, bool below, float room, string label, TextStyle text, float gap, float shrinkTo) {
        if (label.Length == 0) return;
        float width = sketch.Width(label, text);
        if (width > room) {
            float scale = text.Scale * room / width;
            if (scale < text.Scale * Math.Clamp(shrinkTo, 0f, 1f) || scale <= 0f) return;
            text = text with { Scale = scale };
        }
        sketch.Text(label, centre, below ? end + gap : end - gap, text, alignX: 0.5f, alignY: below ? 0f : 1f);
    }

    /// <summary>
    ///     A horizontal bar in each row band (bands laid down the plot, from its top), from
    ///     <paramref name="baseline"/> to its value on <paramref name="x"/>. Label the rows with
    ///     <see cref="RowLabels{TBands}"/>; hit-test with the bands' IndexAt on the mouse's y.
    /// </summary>
    public static void BarsAcross<T, TBands>(Sketch sketch, TBands rows, Scale<T> x, IReadOnlyList<T> values, T baseline, Rgba colour)
        where TBands : IBandLayout {
        float from = x.At(baseline);
        for (int i = 0; i < Math.Min(rows.Count, values.Count); i++) sketch.Fill(BarAcross(rows, i, from, x.At(values[i])), colour);
    }

    /// <summary>The rectangle of the horizontal bar in row <paramref name="index"/> between two pixels.</summary>
    public static Area BarAcross<TBands>(TBands rows, int index, float from, float to) where TBands : IBandLayout =>
        new(Math.Min(from, to), rows.BarStart(index), Math.Abs(to - from), rows.BarWidthOf(index));

    /// <summary>
    ///     Grouped bars: in each band, one bar per series side by side (<see cref="Bands.Within{TBands}"/>),
    ///     series <c>s</c> in <paramref name="colours"/>[s].
    /// </summary>
    /// <param name="series">Values per series, then per category.</param>
    public static void GroupedBars<T, TBands>(Sketch sketch, TBands bands, Scale<T> y, IReadOnlyList<IReadOnlyList<T>> series, T baseline,
                                              IReadOnlyList<Rgba> colours, float innerFill = 0.9f) where TBands : IBandLayout {
        float bottom = y.At(baseline);
        for (int c = 0; c < bands.Count; c++) {
            Bands inner = Bands.Within(bands, c, series.Count, innerFill);
            for (int s = 0; s < series.Count; s++)
                if (c < series[s].Count) sketch.Fill(Bar(inner, s, bottom, y.At(series[s][c])), colours[s % colours.Count]);
        }
    }

    /// <summary>
    ///     Stacked bars: in each band, each series' segment on the one before, series <c>s</c> in
    ///     <paramref name="colours"/>[s]. The scale runs from 0 to <see cref="Stacks.Tallest"/> (100 for shares).
    /// </summary>
    public static void StackedBars<TBands>(Sketch sketch, TBands bands, Scale<double> y, Stacks stacks, IReadOnlyList<Rgba> colours)
        where TBands : IBandLayout {
        for (int c = 0; c < Math.Min(bands.Count, stacks.Categories); c++)
            for (int s = 0; s < stacks.Series; s++)
                sketch.Fill(Bar(bands, c, y.At(stacks.Start(s, c)), y.At(stacks.End(s, c))), colours[s % colours.Count]);
    }

    /// <summary>
    ///     The area between two lines over the same x, filled: a min-max or quartile band behind a line.
    ///     Drawn as vertical strips <paramref name="strip"/> pixels wide, each at the two lines' heights
    ///     in its middle; both lines need ascending x, and the band covers where both have points.
    /// </summary>
    // Strips because a mark is a rectangle, a line or a text: 2 pixels keeps the edges smooth and costs
    // one rectangle per 2 pixels of width, built once.
    public static void Band(Sketch sketch, LinePoints lower, LinePoints upper, Rgba colour, float strip = 2f) {
        if (lower.Count < 2 || upper.Count < 2) return;
        float from = MathF.Round(Math.Max(lower.X[0], upper.X[0]));
        float to = MathF.Round(Math.Min(lower.X[^1], upper.X[^1]));
        for (float x = from; x < to; x += strip) {
            float width = Math.Min(strip, to - x), middle = x + width / 2f;
            float a = HeightAt(lower, middle), b = HeightAt(upper, middle);
            sketch.Fill(new Area(x, MathF.Round(Math.Min(a, b)), width, MathF.Round(Math.Abs(b - a))), colour);
        }
    }

    // The line's height at x, straight between its points.
    private static float HeightAt(LinePoints line, float x) {
        int i = Math.Clamp(line.LastAtOrBefore(x), 0, line.Count - 2);
        float span = line.X[i + 1] - line.X[i];
        float t = span <= 0f ? 0f : Math.Clamp((x - line.X[i]) / span, 0f, 1f);
        return line.Y[i] + (line.Y[i + 1] - line.Y[i]) * t;
    }

    /// <summary>The rectangle of the bar in band <paramref name="index"/> between two pixels, for drawing a highlight over it.</summary>
    public static Area Bar<TBands>(TBands bands, int index, float from, float to) where TBands : IBandLayout =>
        new(bands.BarStart(index), Math.Min(from, to), bands.BarWidthOf(index), Math.Abs(to - from));

    /// <summary>
    ///     A box plot in band <paramref name="index"/>: whisker, caps, the box from Q1 to Q3, the median,
    ///     and a square per outlier. The marks are pixels on a vertical value axis (<see cref="Box.Place{T}"/>).
    /// </summary>
    public static void Box<TBands>(Sketch sketch, TBands bands, int index, BoxMarks marks, ChartStyle style) where TBands : IBandLayout {
        float x = bands.BarStart(index), width = bands.BarWidthOf(index), centre = MathF.Round(bands.Centre(index));
        if (width <= 0f) return;   // a hidden column
        float capLeft = MathF.Round(centre - width / 4f), capWidth = MathF.Round(width / 2f);
        // Pixel y grows downward: High is the top of the whisker, Q3 the top of the box.
        sketch.Fill(new Area(centre, marks.High, 1f, marks.Low - marks.High), style.Data);
        sketch.Fill(new Area(capLeft, marks.High, capWidth, 2f), style.Data);
        sketch.Fill(new Area(capLeft, marks.Low - 1f, capWidth, 2f), style.Data);
        Area box = new(x, marks.Q3, width, marks.Q1 - marks.Q3);
        sketch.Fill(box, style.BoxFill);
        sketch.Outline(box, style.Data);
        sketch.Fill(new Area(x, marks.Median - 1f, width, 3f), style.Median);
        foreach (float outlier in marks.Outliers) sketch.Fill(new Area(centre - 3f, outlier - 3f, 6f, 6f), style.Outlier);
    }

    /// <summary>
    ///     The line through <paramref name="line"/>'s points, skipping its breaks. Horizontal and
    ///     vertical segments (all of a step line's) are drawn as fills, which stay crisp; others as lines.
    /// </summary>
    public static void Line(Sketch sketch, LinePoints line, Rgba colour, float thickness) {
        for (int i = 1; i < line.Count; i++) {
            if (!line.JoinsPrevious(i)) continue;
            float x1 = line.X[i - 1], y1 = line.Y[i - 1], x2 = line.X[i], y2 = line.Y[i];
            if (y1 == y2 || x1 == x2) {
                // Reaching half a thickness past each end, so a corner has no notch.
                float half = thickness / 2f;
                sketch.Fill(new Area(MathF.Round(Math.Min(x1, x2) - half), MathF.Round(Math.Min(y1, y2) - half),
                                     MathF.Round(Math.Abs(x2 - x1) + thickness), MathF.Round(Math.Abs(y2 - y1) + thickness)), colour);
            } else {
                sketch.Line(x1, y1, x2, y2, colour, thickness);
            }
        }
    }

    /// <summary>A square of <paramref name="size"/> pixels centred on each point.</summary>
    public static void Points(Sketch sketch, LinePoints points, Rgba colour, float size) {
        for (int i = 0; i < points.Count; i++) Point(sketch, points.X[i], points.Y[i], colour, size);
    }

    /// <summary>A square of <paramref name="size"/> pixels centred on (x, y).</summary>
    public static void Point(Sketch sketch, float x, float y, Rgba colour, float size) =>
        sketch.Fill(new Area(MathF.Round(x - size / 2f), MathF.Round(y - size / 2f), size, size), colour);

    /// <summary>
    ///     A horizontal reference line across the plot at <paramref name="value"/> (a target, a best),
    ///     dashed as the style says, with its label above its right end. Nothing when the value is off
    ///     the plot.
    /// </summary>
    /// <param name="hot">
    ///     Drawn as a line that can be dragged is while the mouse is on it or dragging it (<see cref="DragLine"/>):
    ///     solid, twice as thick, with a grip at its end.
    /// </param>
    public static void ReferenceY<T>(Sketch sketch, Area plot, Scale<T> y, T value, string label, ChartStyle style, bool hot = false) {
        float at = MathF.Round(y.At(value));
        if (at < plot.Y || at > plot.Bottom) return;
        if (hot) {
            (float lo, float t) = ReferenceSpan(style, hot);
            sketch.Fill(new Area(plot.X, at - lo, plot.Width, t), style.Reference);
            sketch.Fill(new Area(plot.X - 4f, at - 5f, 4f, 10f), style.Reference);
        } else {
            Dashes(sketch, plot.X, plot.Right, at, horizontal: true, style);
        }
        ReferenceLabel(sketch, label, plot.Right - style.LabelGap / 2f, at - 2f - ReferenceSpan(style, false).Before, style, alignX: 1f, alignY: 1f, plot.X, plot.Right);
    }

    // A reference line's label on a dark backing, so a point or a bar under it does not run through the
    // letters. Draw reference lines after the data they cross, or the data covers the label instead. The
    // backing is cut at low and high: the plot's sides, and the label's own line when it stands beside one.
    private static void ReferenceLabel(Sketch sketch, string label, float x, float y, ChartStyle style, float alignX, float alignY, float low, float high) {
        if (label.Length == 0) return;
        TextStyle text = style.Label with { Colour = style.Reference };
        float width = sketch.Width(label, text), height = sketch.LineHeight(text);
        float left = Math.Max(low, MathF.Floor(x - alignX * width) - 2f), right = Math.Min(high, MathF.Floor(x - alignX * width) + MathF.Ceiling(width) + 3f);
        if (right > left) sketch.Fill(new Area(left, MathF.Floor(y - alignY * height), right - left, MathF.Ceiling(height) + 1f), style.TooltipBack);
        sketch.Text(label, x, y, text, alignX, alignY);
    }

    /// <summary>
    ///     A vertical reference line down the plot at <paramref name="value"/>, with its label at the top,
    ///     right of the line, or left of it when there is no room.
    /// </summary>
    /// <param name="hot">As <see cref="ReferenceY{T}"/>'s: solid, thicker, a grip at its foot.</param>
    public static void ReferenceX<T>(Sketch sketch, Area plot, Scale<T> x, T value, string label, ChartStyle style, bool hot = false) =>
        ReferenceAt(sketch, plot, x.At(value), label, style, hot);

    /// <summary>
    ///     A vertical line between two bands (<see cref="BandEdges"/>), with its label at the top: a
    ///     divider that sits between two bins rather than on a value.
    /// </summary>
    public static void ReferenceAt(Sketch sketch, Area plot, float x, string label, ChartStyle style, bool hot = false) {
        float at = MathF.Round(x);
        if (at < plot.X || at > plot.Right) return;
        ReferenceDown(sketch, plot, at, style, hot);
        TextStyle text = style.Label with { Colour = style.Reference };
        bool fitsRight = at + style.LabelGap / 2f + sketch.Width(label, text) <= plot.Right;
        // The label's backing stops short of the line at its hot thickness, the widest it gets.
        (float before, float thick) = ReferenceSpan(style, true);
        ReferenceLabel(sketch, label, fitsRight ? at + style.LabelGap / 2f : at - style.LabelGap / 2f, plot.Y, style, fitsRight ? 0f : 1f, 0f,
                       fitsRight ? at - before + thick : plot.X, fitsRight ? plot.Right : at - before);
    }

    private static void ReferenceDown(Sketch sketch, Area plot, float at, ChartStyle style, bool hot) {
        if (hot) {
            (float lo, float t) = ReferenceSpan(style, hot);
            sketch.Fill(new Area(at - lo, plot.Y, t, plot.Height), style.Reference);
            sketch.Fill(new Area(at - 5f, plot.Bottom, 10f, 4f), style.Reference);
        } else {
            Dashes(sketch, plot.Y, plot.Bottom, at, horizontal: false, style);
        }
    }

    // A reference line of thickness t covers [at - t/2, at - t/2 + t) in whole pixels (t/2 rounded down), so it
    // stays centred on at; at 1 it is the pixel at, and hot at 2 is one each side.
    private static (float Before, float Thickness) ReferenceSpan(ChartStyle style, bool hot) {
        float t = MathF.Max(1f, MathF.Round(style.ReferenceThickness)) + (hot ? 1f : 0f);
        return (MathF.Floor(t / 2f), t);
    }

    private static void Dashes(Sketch sketch, float from, float to, float at, bool horizontal, ChartStyle style) {
        (float lo, float t) = ReferenceSpan(style, false);
        float dash = style.Dash > 0f ? style.Dash : to - from;
        for (float start = from; start < to; start += 2 * dash) {
            float length = Math.Min(dash, to - start);
            sketch.Fill(horizontal ? new Area(start, at - lo, length, t) : new Area(at - lo, start, t, length), style.Reference);
        }
    }

    /// <summary>
    ///     Every cell of a grid in the colour <paramref name="colour"/> gives it; a cell given
    ///     <see cref="Rgba.Transparent"/> is drawn as an outline in the style's grid colour, so a cell with
    ///     no data never reads as a low value.
    /// </summary>
    public static void Cells(Sketch sketch, CellGrid grid, Func<int, int, Rgba> colour, ChartStyle style) {
        for (int row = 0; row < grid.RowCount; row++)
            for (int column = 0; column < grid.ColumnCount; column++) {
                Rgba c = colour(column, row);
                if (c.IsInvisible) sketch.Outline(grid.Cell(column, row), style.Grid);
                else sketch.Fill(grid.Cell(column, row), c);
            }
    }

    /// <summary>
    ///     One label per row band, right-aligned at <paramref name="right"/> and centred on its row,
    ///     shortened to <paramref name="maxWidth"/>; thinned to every Nth row when rows are shorter than a
    ///     line of text.
    /// </summary>
    public static void RowLabels<TBands>(Sketch sketch, TBands rows, IReadOnlyList<string> labels, float right, float maxWidth,
                                         ChartStyle style) where TBands : IBandLayout {
        int count = Math.Min(rows.Count, labels.Count);
        float height = sketch.LineHeight(style.Label);
        float[] centres = new float[count], heights = new float[count];
        for (int i = 0; i < count; i++) {
            centres[i] = rows.Centre(i);
            heights[i] = height;
        }
        int stride = TextFit.Stride(centres, heights, gap: 0f);
        for (int i = 0; i < count; i += stride) {
            string text = TextFit.Truncate(labels[i], maxWidth, s => sketch.Width(s, style.Label), keepFrom: "");
            sketch.Text(text, right, centres[i], style.Label, alignX: 1f, alignY: 0.5f);
        }
    }

    /// <summary>
    ///     A colour legend: <paramref name="ramp"/> drawn across <paramref name="bar"/>, and the ticks of
    ///     the scale that maps values onto it under the bar, as <see cref="XAxis{T}"/> draws them. The
    ///     scale must run from the bar's left edge to its right.
    /// </summary>
    public static void RampLegend<T>(Sketch sketch, Area bar, ColourRamp ramp, IReadOnlyList<PlacedTick<T>> ticks,
                                     ChartStyle style) {
        // Whole-pixel strips, at most 64: past that no step between neighbours is visible.
        int strips = (int) Math.Clamp(MathF.Floor(bar.Width), 1f, 64f);
        float left = MathF.Round(bar.X);
        for (int i = 0; i < strips; i++) {
            float right = MathF.Round(bar.X + bar.Width * (i + 1) / strips);
            sketch.Fill(new Area(left, bar.Y, right - left, bar.Height), ramp.At((i + 0.5) / strips));
            left = right;
        }
        XAxis(sketch, bar, ticks, style);
    }

    /// <summary>
    ///     A scroll view's scrollbar: the track, and the thumb brighter while hot (hovered or dragged).
    ///     Nothing when there is nothing to scroll. It changes every frame the view scrolls, so paint it
    ///     into a sketch cleared each frame (a few marks, no allocation).
    /// </summary>
    public static void Scrollbar(Sketch sketch, ScrollView view, ChartStyle style) {
        if (!view.Scrolls) return;
        sketch.Fill(view.Track, style.Grid);
        Area thumb = view.Thumb;
        Area snapped = view.Horizontal ? new Area(MathF.Round(thumb.X), thumb.Y, MathF.Round(thumb.Width), thumb.Height)
                                       : new Area(thumb.X, MathF.Round(thumb.Y), thumb.Width, MathF.Round(thumb.Height));
        sketch.Fill(snapped, view.ThumbHot ? style.Hovered : style.Axis);
    }

    /// <summary>
    ///     A close button: a square with a cross, brighter while <paramref name="hot"/>. Drawn from lines, as
    ///     the game's font may have no multiplication sign.
    /// </summary>
    /// <summary>A square with a bar along its foot: it sends the window to the dock.</summary>
    public static void MinimizeButton(Sketch sketch, Area button, bool hot, ChartStyle style) {
        Rgba colour = hot ? style.Hovered : style.Axis;
        sketch.Fill(button, style.TooltipBack);
        sketch.Outline(button, colour);
        float inset = MathF.Round(Math.Min(button.Width, button.Height) * 0.3f);
        sketch.Fill(new Area(button.X + inset, button.Bottom - inset - 2f, button.Width - 2 * inset, 2f), colour);
    }

    /// <summary>
    ///     The dock along the bottom of the windows' screen: a button per minimised window with its title,
    ///     lit under the mouse (<see cref="WindowManager.HoveredDock"/>). A click restores it. Rebuild it when a
    ///     window is minimised or restored, or the hovered button changes; nothing when no window is docked.
    /// </summary>
    public static void Dock(Sketch sketch, WindowManager windows, int hot, ChartStyle style) {
        for (int i = 0; i < windows.Docked.Count; i++) {
            Area button = windows.DockButton(i);
            sketch.Fill(button, Rgba.Black.Times(0.9f));
            sketch.Fill(button with { Height = 2f }, i == hot ? style.Hovered : style.Data);
            sketch.Outline(button, i == hot ? style.Hovered : style.Axis);
            TextStyle title = style.Label with { Colour = i == hot ? Rgba.White : style.Label.Colour };
            string text = TextFit.Truncate(windows.Docked[i].Title, button.Width - 2 * style.LabelGap, s => sketch.Width(s, title));
            sketch.Text(text, button.X + style.LabelGap, button.Y + button.Height / 2f, title, alignY: 0.5f);
        }
    }

    public static void CloseButton(Sketch sketch, Area button, bool hot, ChartStyle style) {
        Rgba colour = hot ? style.Hovered : style.Axis;
        sketch.Fill(button, style.TooltipBack);
        sketch.Outline(button, colour);
        float inset = MathF.Round(Math.Min(button.Width, button.Height) * 0.3f);
        Area cross = button.Inset(inset);
        sketch.Line(cross.X, cross.Y, cross.Right, cross.Bottom, colour, 2f);
        sketch.Line(cross.X, cross.Bottom, cross.Right, cross.Y, colour, 2f);
    }

    /// <summary>
    ///     A legend on one line from (x, y): a colour square and a name per series, in order. Returns the
    ///     area it covers, to place it (right-aligned, say, by painting it once to measure).
    /// </summary>
    public static Area Legend(Sketch sketch, float x, float y, IReadOnlyList<string> names, IReadOnlyList<Rgba> colours, ChartStyle style) {
        float height = sketch.LineHeight(style.Label), chip = MathF.Round(height * 0.5f), left = x;
        for (int i = 0; i < names.Count; i++) {
            if (i > 0) left += 3 * style.LabelGap;
            sketch.Fill(new Area(left, MathF.Round(y + (height - chip) / 2f), chip, chip), colours[i % colours.Count]);
            left += chip + style.LabelGap / 2f;
            left = sketch.Text(names[i], left, y, style.Label).Right;
        }
        return new Area(x, y, left - x, height);
    }

    /// <summary>How wide <see cref="Legend"/> would be, without painting it.</summary>
    public static float LegendWidth(Sketch sketch, IReadOnlyList<string> names, ChartStyle style) {
        float chip = MathF.Round(sketch.LineHeight(style.Label) * 0.5f), width = 0f;
        for (int i = 0; i < names.Count; i++)
            width += (i > 0 ? 3 * style.LabelGap : 0f) + chip + style.LabelGap / 2f + sketch.Width(names[i], style.Label);
        return width;
    }

    /// <summary>
    ///     A window's frame in its own coordinates, from (0, 0) to its size: the backing, the title bar with
    ///     its title (shortened to fit), the border, a resize grip, and the minimise and close buttons. Draw it moved to
    ///     the window's <see cref="ChartWindow.Bounds"/>; rebuild it when the window is resized, comes to
    ///     the front or goes back, or the part under the mouse changes — never because it moved.
    /// </summary>
    public static void Window(Sketch sketch, WindowManager windows, ChartWindow window, bool front, WindowZone hot, ChartStyle style) {
        Area local = new(0f, 0f, window.Bounds.Width, window.Bounds.Height);
        Area bar = local with { Height = windows.TitleHeight };
        sketch.Fill(local, Rgba.Black.Times(0.9f));
        sketch.Fill(bar, front ? Rgba.White.Times(0.14f) : Rgba.White.Times(0.06f));
        bool hotEdge = hot is not (WindowZone.None or WindowZone.Body or WindowZone.Close or WindowZone.Minimize);
        sketch.Outline(local, hotEdge ? style.Hovered : front ? style.Axis : style.Grid, hotEdge ? 2f : 1f);

        Area close = windows.CloseButton(window).Offset(-window.Bounds.X, -window.Bounds.Y);
        Area minimize = windows.MinimizeButton(window).Offset(-window.Bounds.X, -window.Bounds.Y);
        float titleRight = window.Minimizable ? minimize.X - style.LabelGap : window.Closable ? close.X - style.LabelGap : bar.Right - style.LabelGap;
        TextStyle title = style.Label with { Colour = front ? Rgba.White : style.Label.Colour };
        string text = TextFit.Truncate(window.Title, titleRight - style.LabelGap, s => sketch.Width(s, title));
        sketch.Text(text, style.LabelGap, bar.Height / 2f, title, alignY: 0.5f);
        if (window.Closable) CloseButton(sketch, close, hot == WindowZone.Close, style);
        if (window.Minimizable) MinimizeButton(sketch, minimize, hot == WindowZone.Minimize, style);

        // Three dots on a diagonal in the bottom-right corner: it resizes.
        if (window.Resizable)
            for (int i = 0; i < 3; i++)
                for (int j = 0; j <= i; j++)
                    sketch.Fill(new Area(local.Right - 6f - 4f * (i - j) - 2f, local.Bottom - 6f - 4f * j - 2f, 2f, 2f), style.Axis);
    }

    /// <summary>
    ///     A line of text on its own backing, anchored like <see cref="Sketch.Text"/> and moved inside
    ///     <paramref name="within"/> when it would leave it. Returns the backing's area.
    /// </summary>
    public static Area Tooltip(Sketch sketch, string text, float x, float y, Area within, ChartStyle style,
                               float alignX = 0f, float alignY = 0f) {
        // Several lines or columns: TooltipBox's layout, anchored the same way.
        if (text.Contains('\n') || text.Contains('\t')) {
            (float w, float h) = TooltipSize(sketch, text, style);
            Area box = new(Math.Clamp(x - alignX * w, within.X, Math.Max(within.X, within.Right - w)),
                           Math.Clamp(y - alignY * h, within.Y, Math.Max(within.Y, within.Bottom - h)), w, h);
            TooltipBox(sketch, text, box, style);
            return box;
        }
        float pad = style.TooltipPadX;
        float width = sketch.Width(text, style.Tooltip) + 2 * pad, height = sketch.LineHeight(style.Tooltip) + 2 * style.TooltipPadY;
        float left = Math.Clamp(x - alignX * width, within.X, Math.Max(within.X, within.Right - width));
        float top = Math.Clamp(y - alignY * height, within.Y, Math.Max(within.Y, within.Bottom - height));
        Area back = new(left, top, width, height);
        sketch.Fill(back, style.TooltipBack);
        sketch.Text(text, left + pad, top + style.TooltipPadY, style.Tooltip);
        return back;
    }
}
