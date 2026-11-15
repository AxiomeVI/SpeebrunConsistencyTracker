#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

// Components beyond plots: the button bar, the hint card, tooltips.
internal static partial class Paint {
    /// <summary>
    ///     A button bar: each item labelled; faint while switched off, strongly lit while pressed, filled while on (a
    ///     toggle that is on, a switch's choice), lit while hovered. A switch is one outline with a line between its
    ///     choices.
    /// </summary>
    public static void ButtonBar(Sketch sketch, ButtonBar bar, ChartStyle style) {
        for (int i = 0; i < bar.Count; i++) {
            Area area = bar.AreaOf(i);
            if (bar.IsCaption(i)) {
                sketch.Text(bar.LabelOf(i), area.X, area.Y + area.Height / 2f, style.Label with { Colour = style.CaptionText }, alignY: 0.5f);
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
            Rgba text = !bar.IsEnabled(i) ? style.Axis.Times(0.6f) : on || hot ? style.ActiveText : own ?? style.Label.Colour;
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
}
