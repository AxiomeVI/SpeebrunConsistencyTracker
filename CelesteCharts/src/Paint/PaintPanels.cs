#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>A mark on a chart's x axis at one value: a personal best, a reset, a change of strategy.</summary>
internal readonly record struct ChartMarker<T>(T At, string Label, Rgba Colour);

/// <summary>
///     A value against a range, for a gauge: a stretch of the range in a colour, such as "under target" in
///     green. Bands are drawn in order, behind the gauge's bar.
/// </summary>
internal readonly record struct GaugeBand<T>(T From, T To, Rgba Colour);

// Components beyond plots: tables, gauges, stat tiles, lines of text.
internal static partial class Paint {
    /// <summary>
    ///     A table planned by <see cref="TableLayout"/>, its top-left corner at (x, y): the header with a line
    ///     under it, rows striped faintly so a long row is followed across. <paramref name="colour"/>, when
    ///     given, colours a cell's text (a slow time in orange); <see cref="Rgba.Transparent"/> keeps the
    ///     label colour.
    /// </summary>
    public static void Table(Sketch sketch, float x, float y, TablePlan plan, ChartStyle style, Func<int, int, Rgba>? colour = null) {
        for (int c = 0; c < plan.Columns.Count; c++)
            CellText(sketch, x, y, plan, c, 0f, plan.HeaderHeight - plan.Padding / 2f, plan.Headers[c], style.Header);
        sketch.Fill(new Area(x, y + plan.HeaderHeight - 1f, plan.TotalWidth, 1f), style.Axis);

        for (int r = 0; r < plan.RowCount; r++) {
            float top = plan.RowTop(r);
            if (r % 2 == 1) sketch.Fill(new Area(x, y + top, plan.TotalWidth, plan.RowHeight), style.Grid);
            for (int c = 0; c < plan.Columns.Count; c++) {
                Rgba text = colour?.Invoke(r, c) ?? Rgba.Transparent;
                TextStyle cell = text.IsInvisible ? style.Label : style.Label with { Colour = text };
                CellText(sketch, x, y, plan, c, top, plan.RowHeight, plan.Cells[r][c], cell);
            }
        }
    }

    /// <summary>A row's rectangle, for a hover highlight: the table's corner at (x, y).</summary>
    public static Area TableRow(float x, float y, TablePlan plan, int row) =>
        new(x, y + plan.RowTop(row), plan.TotalWidth, plan.RowHeight);

    private static void CellText(Sketch sketch, float x, float y, TablePlan plan, int column, float top, float height,
                                 string text, TextStyle style) {
        float left = x + plan.X[column], width = plan.Width[column];
        (float at, float align) = plan.Columns[column].Align switch {
            ColumnAlign.Right => (left + width - plan.Padding, 1f),
            ColumnAlign.Centre => (left + width / 2f, 0.5f),
            _ => (left + plan.Padding, 0f),
        };
        sketch.Text(text, MathF.Round(at), MathF.Round(y + top + height / 2f), style, alignX: align, alignY: 0.5f);
    }

    /// <summary>
    ///     A gauge across <paramref name="area"/>: its label on the left and its value's text on the right
    ///     above, then the track: the bands, and a bar from the scale's start to <paramref name="value"/>.
    ///     The scale runs across the track (<see cref="GaugeTrack"/>). A bullet chart, read by length.
    /// </summary>
    /// <returns>The bar, for a hover or a target mark.</returns>
    public static Area Gauge<T>(Sketch sketch, Area area, Scale<T> scale, T value, string label, string valueText, ChartStyle style,
                                IReadOnlyList<GaugeBand<T>>? bands = null, Rgba? colour = null) {
        sketch.Text(label, area.X, area.Y, style.Label);
        sketch.Text(valueText, area.Right, area.Y, style.Label with { Colour = style.Header.Colour }, alignX: 1f);
        Area track = GaugeTrack(sketch, area, style);
        sketch.Fill(track, style.Grid);
        if (bands != null)
            for (int i = 0; i < bands.Count; i++) {
                float from = Math.Clamp(scale.At(bands[i].From), track.X, track.Right), to = Math.Clamp(scale.At(bands[i].To), track.X, track.Right);
                sketch.Fill(new Area(MathF.Round(Math.Min(from, to)), track.Y, MathF.Round(Math.Abs(to - from)), track.Height), bands[i].Colour);
            }
        float end = MathF.Round(Math.Clamp(scale.At(value), track.X, track.Right));
        float barHeight = MathF.Round(track.Height / 3f);
        Area bar = new(track.X, track.Y + MathF.Round((track.Height - barHeight) / 2f), end - track.X, barHeight);
        sketch.Fill(bar, colour ?? style.Data);
        return bar;
    }

    /// <summary>Where <see cref="Gauge{T}"/> puts its track inside an area: build the gauge's scale over its width.</summary>
    public static Area GaugeTrack(Sketch sketch, Area area, ChartStyle style) {
        float top = MathF.Ceiling(sketch.LineHeight(style.Label)) + 2f;
        return new Area(area.X, area.Y + top, area.Width, Math.Max(0f, area.Height - top));
    }

    /// <summary>A target on a gauge: an upright line across its track, in the reference colour.</summary>
    public static void GaugeTarget<T>(Sketch sketch, Area area, Scale<T> scale, T target, ChartStyle style) {
        Area track = GaugeTrack(sketch, area, style);
        float at = MathF.Round(scale.At(target));
        if (at < track.X || at > track.Right) return;
        float inset = MathF.Round(track.Height / 6f);
        sketch.Fill(new Area(at - 1f, track.Y + inset, 2f, track.Height - 2 * inset), style.Reference);
    }

    /// <summary>
    ///     One number, as big as its tile allows: the caption at the top, the value under it scaled to fit
    ///     (at most <see cref="ChartStyle.Stat"/>'s scale), and an optional note at the bottom (a change,
    ///     "-0.120 on last session") in its own colour.
    /// </summary>
    public static void StatTile(Sketch sketch, Area area, string caption, string value, ChartStyle style, string? note = null,
                                Rgba? noteColour = null) {
        Area inner = area.Inset(style.LabelGap);
        sketch.Text(TextFit.Truncate(caption, inner.Width, s => sketch.Width(s, style.Label), keepFrom: ""), inner.X, inner.Y, style.Label);
        float top = inner.Y + sketch.LineHeight(style.Label);
        float bottom = inner.Bottom;
        if (!string.IsNullOrEmpty(note)) {
            TextStyle noteStyle = style.Label with { Colour = noteColour ?? style.Label.Colour };
            bottom -= sketch.LineHeight(noteStyle);
            sketch.Text(TextFit.Truncate(note!, inner.Width, s => sketch.Width(s, noteStyle), keepFrom: ""), inner.X, bottom, noteStyle);
        }
        float scale = TextFit.LargestScale(sketch.Metrics.WidthAt(value, 1f), sketch.Metrics.HeightAt(1f), inner.Width, bottom - top, style.Stat.Scale);
        if (scale <= 0f) return;
        sketch.Text(value, inner.X, MathF.Round((top + bottom) / 2f), style.Stat with { Scale = scale }, alignY: 0.5f);
    }

    /// <summary>
    ///     Text over several lines (split at '\n', each a mark of its own, since a text mark is one line),
    ///     from (x, y) down. Each line is shortened to <paramref name="width"/>. Returns the height it took.
    /// </summary>
    public static float TextLines(Sketch sketch, string text, float x, float y, float width, TextStyle style) {
        float line = sketch.LineHeight(style), top = y;
        foreach (string part in text.Split('\n')) {
            sketch.Text(TextFit.Truncate(part, width, s => sketch.Width(s, style), keepFrom: ""), x, top, style);
            top += line;
        }
        return top - y;
    }

    /// <summary>
    ///     A following window's end strips: at each end of <paramref name="plot"/>, a bar as wide as the share
    ///     of the route left out on that side (<see cref="FollowWindow.StripWidth"/>, at most
    ///     <paramref name="room"/>), with the count beside it. The window's bands go between the two rooms.
    /// </summary>
    public static void FollowStrips(Sketch sketch, Area plot, FollowWindow window, int total, float room, ChartStyle style) {
        float before = FollowWindow.StripWidth(window.HiddenBefore, total, room), after = FollowWindow.StripWidth(window.HiddenAfter, total, room);
        TextStyle count = style.Label with { Scale = style.Label.Scale * 0.85f };
        if (before > 0f) {
            sketch.Fill(new Area(plot.X, plot.Y, before, plot.Height), style.Axis);
            sketch.Text($"+{window.HiddenBefore}", plot.X, plot.Y - 2f, count, alignY: 1f);
        }
        if (after > 0f) {
            sketch.Fill(new Area(plot.Right - after, plot.Y, after, plot.Height), style.Axis);
            sketch.Text($"+{window.HiddenAfter}", plot.Right, plot.Y - 2f, count, alignX: 1f, alignY: 1f);
        }
    }

    /// <summary>
    ///     A section's header across a wall: "+" folded or "-" open, the name and a note (its tile count), in the
    ///     header colour over a line, lit while <paramref name="hot"/> (a click folds it).
    /// </summary>
    public static void SectionHeader(Sketch sketch, Area area, string name, string note, bool folded, bool hot, ChartStyle style) {
        if (hot) sketch.Fill(area, style.Hovered.Times(0.08f));
        TextStyle text = style.Header;
        string line = (folded ? "+ " : "- ") + name + (note.Length > 0 ? " · " + note : "");
        sketch.Text(TextFit.Truncate(line, area.Width - style.LabelGap, s => sketch.Width(s, text)), area.X + style.LabelGap / 2f,
                    area.Y + area.Height / 2f, text, alignY: 0.5f);
        sketch.Fill(new Area(area.X, area.Bottom - 1f, area.Width, 1f), hot ? style.Hovered : style.Axis);
    }

    /// <summary>
    ///     A tab strip: every tab's label, the chosen one filled and joined to a line under the strip, the one
    ///     under the mouse lit. Rebuild it when the choice or the hovered tab changes.
    /// </summary>
    public static void Tabs(Sketch sketch, TabStrip tabs, int selected, int hot, ChartStyle style) {
        Area bounds = tabs.Bounds;
        sketch.Fill(new Area(bounds.X, bounds.Bottom - 1f, bounds.Width, 1f), style.Axis);
        for (int i = 0; i < tabs.Count; i++) {
            Area tab = tabs.Tab(i);
            bool on = i == selected;
            if (on) sketch.Fill(tab, style.Data.Times(0.45f));
            else if (i == hot) sketch.Fill(tab, style.Hovered.Times(0.1f));
            if (on) sketch.Fill(new Area(tab.X, tab.Bottom - 2f, tab.Width, 2f), style.Data);
            TextStyle label = style.Label with { Colour = on || i == hot ? Rgba.White : style.Label.Colour };
            sketch.Text(tabs.Labels[i], tab.X + tab.Width / 2f, tab.Y + tab.Height / 2f, label, alignX: 0.5f, alignY: 0.5f);
        }
    }

    /// <summary>
    ///     The tiles of a scrolled wall on its mini-map, each a dot of the colour <paramref name="colour"/> gives
    ///     it (by the tile's index), over a faint backing. Built once with the wall; the frame is
    ///     <see cref="MiniMapFrame"/>.
    /// </summary>
    public static void MiniMapTiles(Sketch sketch, MiniMap map, System.Collections.Generic.IReadOnlyList<Area> tiles, Func<int, Rgba> colour,
                               ChartStyle style) {
        sketch.Fill(new Area(map.Bounds.X - 2f, map.Bounds.Y - 2f, map.ContentWidth * map.Factor + 4f, map.ContentHeight * map.Factor + 4f),
                    style.Grid);
        for (int i = 0; i < tiles.Count; i++) {
            Area dot = map.ToMap(tiles[i]);
            sketch.Fill(new Area(MathF.Round(dot.X), MathF.Round(dot.Y), MathF.Max(1f, MathF.Round(dot.Width)), MathF.Max(1f, MathF.Round(dot.Height))),
                        colour(i));
        }
    }

    /// <summary>The frame for the part of the wall on screen, lit while the map is hovered or dragged.</summary>
    public static void MiniMapFrame(Sketch sketch, MiniMap map, ScrollView view, ChartStyle style) {
        Area frame = map.Viewport(view);
        sketch.Outline(new Area(MathF.Round(frame.X) - 1f, MathF.Round(frame.Y) - 1f, MathF.Round(frame.Width) + 2f, MathF.Round(frame.Height) + 2f),
                       map.Hovered ? style.Hovered : style.Axis);
    }

    /// <summary>How much room <see cref="Markers{T}"/> takes above a plot.</summary>
    public static float MarkerHeight(Sketch sketch, ChartStyle style) => MathF.Ceiling(sketch.LineHeight(style.Label)) + 10f;

    /// <summary>
    ///     Markers along a plot's x axis: for each, a faint line down the plot, a small downward triangle on its
    ///     top edge and the label above it, in its colour. A label that would overlap the one before is left
    ///     out (its triangle stays; hover names it). Markers off the plot are skipped.
    /// </summary>
    public static void Markers<T>(Sketch sketch, Area plot, Scale<T> x, System.Collections.Generic.IReadOnlyList<ChartMarker<T>> markers,
                                  ChartStyle style) {
        float lastRight = float.NegativeInfinity;
        for (int i = 0; i < markers.Count; i++) {
            float at = MathF.Round(x.At(markers[i].At));
            if (at < plot.X || at > plot.Right) continue;
            Rgba colour = markers[i].Colour;
            sketch.Fill(new Area(at, plot.Y, 1f, plot.Height), colour.Times(0.35f));
            for (int row = 0; row < 5; row++) sketch.Fill(new Area(at - 4f + row, plot.Y - 6f + row, 9f - 2 * row, 1f), colour);
            TextStyle label = style.Label with { Colour = colour };
            float width = sketch.Width(markers[i].Label, label);
            if (at - width / 2f < lastRight + style.LabelSpacing) continue;
            sketch.Text(markers[i].Label, at, plot.Y - 8f, label, alignX: 0.5f, alignY: 1f);
            lastRight = at + width / 2f;
        }
    }

    /// <summary>The marker nearest a pixel along the axis, within <paramref name="within"/> pixels, or -1.</summary>
    public static int MarkerAt<T>(Scale<T> x, System.Collections.Generic.IReadOnlyList<ChartMarker<T>> markers, float pixel, float within = 5f) {
        int best = -1;
        float bestDistance = within;
        for (int i = 0; i < markers.Count; i++) {
            float distance = Math.Abs(x.At(markers[i].At) - pixel);
            if (distance <= bestDistance) (best, bestDistance) = (i, distance);
        }
        return best;
    }

    /// <summary>
    ///     A strip chart's points, each in its category's colour (<paramref name="colour"/> by band; one colour
    ///     for all is <c>_ => colour</c>).
    /// </summary>
    public static void Strips(Sketch sketch, StripPoints strips, Func<int, Rgba> colour, float size) {
        for (int i = 0; i < strips.Count; i++) Point(sketch, strips.Points.X[i], strips.Points.Y[i], colour(strips.BandOf(i)), size);
    }

    /// <summary>
    ///     A timeline's intervals in their colours, wider ones first so an instant is never hidden under the
    ///     window it fell in; a label is written inside its interval, in black or white by the colour, when it fits.
    /// </summary>
    public static void Intervals<T>(Sketch sketch, Timeline timeline, System.Collections.Generic.IReadOnlyList<Interval<T>> intervals,
                                    ChartStyle style) {
        // Widest first: a selection sort on indices, since timelines are short and this runs on a rebuild only.
        int n = Math.Min(timeline.Count, intervals.Count);
        bool[] drawn = new bool[n];
        for (int k = 0; k < n; k++) {
            int next = -1;
            for (int i = 0; i < n; i++)
                if (!drawn[i] && (next < 0 || timeline.Area(i).Width > timeline.Area(next).Width)) next = i;
            drawn[next] = true;
            if (!timeline.Shown(next)) continue;
            Area area = timeline.Area(next);
            sketch.Fill(area, intervals[next].Colour);
            string? label = intervals[next].Label;
            if (string.IsNullOrEmpty(label)) continue;
            TextStyle text = style.Label with { Colour = intervals[next].Colour.ContrastingText };
            if (sketch.Width(label!, text) + 6f > area.Width || sketch.LineHeight(text) > area.Height) continue;
            sketch.Text(label!, area.X + area.Width / 2f, area.Y + area.Height / 2f, text, alignX: 0.5f, alignY: 0.5f);
        }
    }

    /// <summary>
    ///     A button bar: each item labelled; faint while switched off, strongly lit while pressed, filled while on (a
    ///     toggle that is on, a switch's choice), lit while hovered. A switch is one outline with a line between its
    ///     choices.
    /// </summary>
    public static void ButtonBar(Sketch sketch, ButtonBar bar, ChartStyle style) {
        for (int i = 0; i < bar.Count; i++) {
            Area area = bar.AreaOf(i);
            if (bar.IsCaption(i)) {
                sketch.Text(bar.LabelOf(i), area.X, area.Y + area.Height / 2f, style.Label with { Colour = Rgba.LightGray }, alignY: 0.5f);
                continue;
            }
            if (bar.IsSlider(i)) {
                Slider(sketch, bar, i, style);
                continue;
            }
            bool on = bar.IsOn(i), hot = bar.IsHovered(i);
            // An item given a colour of its own (a destructive action) is lit, filled and written in it.
            Rgba? own = bar.IsEnabled(i) ? bar.ColourOf(i) : null;
            Rgba lit = own ?? style.Hovered, filled = own ?? style.Data;
            sketch.Fill(area, bar.IsPressed(i) ? lit.Times(own.HasValue ? 0.7f : 0.35f) : on ? filled.Times(0.6f) : hot ? lit.Times(own.HasValue ? 0.5f : 0.15f) : style.TooltipBack);
            Rgba text = !bar.IsEnabled(i) ? style.Axis.Times(0.6f) : on || hot ? Rgba.White : own ?? style.Label.Colour;
            sketch.Text(bar.LabelOf(i), area.X + area.Width / 2f, area.Y + area.Height / 2f, style.Label with { Colour = text }, alignX: 0.5f, alignY: 0.5f);
        }
        // Outlines after every fill, or the next item's fill would cover a shared edge.
        for (int i = 0; i < bar.Count; i++) {
            Area area = bar.AreaOf(i);
            if (bar.IsCaption(i) || bar.IsSlider(i)) continue;
            if (bar.JoinsPrevious(i)) {
                sketch.Fill(new Area(area.X, area.Y, 1f, area.Height), style.Axis);
                continue;
            }
            int last = i;
            bool hot = bar.IsHovered(i);
            while (last + 1 < bar.Count && bar.JoinsPrevious(last + 1)) hot |= bar.IsHovered(++last);
            Rgba? own = last == i && bar.IsEnabled(i) ? bar.ColourOf(i) : null;
            sketch.Outline(new Area(area.X, area.Y, bar.AreaOf(last).Right - area.X, area.Height), own ?? (hot ? style.Hovered : style.Axis));
        }
    }

    // A thin track through the item's middle, lit up to the knob's centre, and a knob that is lit while hovered or dragged.
    private static void Slider(Sketch sketch, ButtonBar bar, int i, ChartStyle style) {
        Area area = bar.AreaOf(i);
        bool enabled = bar.IsEnabled(i), lit = enabled && (bar.IsHovered(i) || bar.IsDragging(i));
        float knob = bar.SliderKnobWidth;
        float knobX = area.X + bar.SliderFraction(i) * (area.Width - knob), centre = knobX + knob / 2f;
        float mid = area.Y + area.Height / 2f;
        Rgba faint = style.Axis.Times(0.6f);
        sketch.Fill(new Area(area.X, mid - 1f, centre - area.X, 2f), enabled ? style.Data.Times(0.6f) : faint);
        sketch.Fill(new Area(centre, mid - 1f, area.Right - centre, 2f), enabled ? style.Axis : faint);
        sketch.Fill(new Area(knobX, area.Y + 3f, knob, area.Height - 6f), !enabled ? faint : lit ? style.Hovered : style.Label.Colour);
    }

    /// <summary>A hint card's box and text: a title, then one line per string. Its controls are a button bar, drawn after.</summary>
    public static void HintCard(Sketch sketch, Area card, string title, System.Collections.Generic.IReadOnlyList<string> lines,
                                TextStyle titleStyle, TextStyle bodyStyle, float pad, float between, ChartStyle style) {
        sketch.Fill(card, style.TooltipBack);
        sketch.Outline(card, style.Axis);
        float x = card.X + pad, y = card.Y + pad;
        sketch.Text(title, x, y, titleStyle);
        y += sketch.LineHeight(titleStyle) + between;
        for (int i = 0; i < lines.Count; i++) {
            sketch.Text(lines[i], x, y, bodyStyle);
            y += sketch.LineHeight(bodyStyle);
        }
    }

    /// <summary>How big <see cref="TooltipBox"/> draws a text: for <see cref="TooltipLayout"/> to place it first.</summary>
    public static (float Width, float Height) TooltipSize(Sketch sketch, string text, ChartStyle style) {
        float padX = 2f * style.TooltipPadX, padY = 2f * style.TooltipPadY, line = MathF.Ceiling(sketch.LineHeight(style.Tooltip));
        if (text.IndexOfAny(TooltipBreaks) < 0) return (MathF.Ceiling(sketch.Width(text, style.Tooltip)) + padX, line + padY);
        string[] lines = text.Split('\n');
        return (MathF.Ceiling(TooltipColumns(sketch, lines, style, null)) + padX, line * lines.Length + padY);
    }

    /// <summary>
    ///     A tooltip in an area <see cref="TooltipLayout"/> placed; <paramref name="pinned"/> draws it with an outline, as
    ///     one that stays. A line break in the text starts a line, and a tab a column: columns line up down the
    ///     lines, the first to the left and the others to the right, as a name beside its number. A line with no tab
    ///     sits across the columns, where <see cref="ChartStyle.TooltipAlign"/> says.
    /// </summary>
    // Splitting allocates, so a tooltip is built when what it names changes, never per frame.
    public static void TooltipBox(Sketch sketch, string text, Area area, ChartStyle style, bool pinned = false) {
        sketch.Fill(area, style.TooltipBack);
        if (pinned) sketch.Outline(area, style.Tooltip.Colour.Times(0.6f));
        float left = area.X + style.TooltipPadX, top = area.Y + style.TooltipPadY;
        if (text.IndexOfAny(TooltipBreaks) < 0) {
            sketch.Text(text, left, top, style.Tooltip);
            return;
        }
        string[] lines = text.Split('\n');
        float line = MathF.Ceiling(sketch.LineHeight(style.Tooltip)), inner = area.Width - 2f * style.TooltipPadX;
        float[] columns = new float[TooltipColumnCount(lines)];
        TooltipColumns(sketch, lines, style, columns);
        for (int i = 0; i < lines.Length; i++) {
            float y = top + i * line;
            if (lines[i].IndexOf('\t') < 0) {
                sketch.Text(lines[i], left + style.TooltipAlign * inner, y, style.Tooltip, alignX: style.TooltipAlign);
                continue;
            }
            string[] cells = lines[i].Split('\t');
            float x = left;
            for (int c = 0; c < cells.Length; c++) {
                // The last column ends at the box's edge, so a box wider than its text keeps numbers on its right.
                float right = c == columns.Length - 1 ? left + inner : x + columns[c];
                if (c == 0) sketch.Text(cells[c], x, y, style.Tooltip);
                else sketch.Text(cells[c], right, y, style.Tooltip, alignX: 1f);
                x += columns[c] + style.TooltipColumnGap;
            }
        }
    }

    private static readonly char[] TooltipBreaks = ['\n', '\t'];

    private static int TooltipColumnCount(string[] lines) {
        int count = 1;
        foreach (string line in lines) {
            int cells = 1;
            foreach (char c in line)
                if (c == '\t') cells++;
            count = Math.Max(count, cells);
        }
        return count;
    }

    // The text's width: its columns and the gaps between them, or its widest line with no tab if that is wider.
    // Fills <paramref name="columns"/> with each column's width when given.
    private static float TooltipColumns(Sketch sketch, string[] lines, ChartStyle style, float[]? columns) {
        columns ??= new float[TooltipColumnCount(lines)];
        float widest = 0f;
        foreach (string line in lines) {
            if (line.IndexOf('\t') < 0) {
                widest = Math.Max(widest, sketch.Width(line, style.Tooltip));
                continue;
            }
            string[] cells = line.Split('\t');
            for (int c = 0; c < cells.Length; c++) columns[c] = Math.Max(columns[c], sketch.Width(cells[c], style.Tooltip));
        }
        float total = 0f;
        int used = 0;
        for (int c = 0; c < columns.Length; c++) {
            total += columns[c];
            if (columns[c] > 0f) used = c + 1;
        }
        if (used > 1) total += (used - 1) * style.TooltipColumnGap;
        return Math.Max(total, widest);
    }

    /// <summary>A tooltip next to what it names: above the point at (x, y), or below when there is no room, kept inside <paramref name="within"/>.</summary>
    public static Area TooltipAt(Sketch sketch, string text, float x, float y, Area within, ChartStyle style, bool pinned = false) {
        (float width, float height) = TooltipSize(sketch, text, style);
        Area area = TooltipLayout.At(x, y, width, height, within);
        TooltipBox(sketch, text, area, style, pinned);
        return area;
    }
}
