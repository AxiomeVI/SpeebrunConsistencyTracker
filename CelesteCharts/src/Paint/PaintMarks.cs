#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

// The area under a line, sparkline bars, and the marks of hidden columns.
internal static partial class Paint {
    /// <summary>
    ///     The area under a line, down to <paramref name="baseline"/> (a pixel: the plot's bottom, or zero's), as
    ///     <see cref="Band"/> fills between two lines. Draw the line itself over it.
    /// </summary>
    public static void AreaUnder(Sketch sketch, LinePoints line, float baseline, Rgba colour, float strip = 2f,
                                 float visibleFrom = float.NegativeInfinity, float visibleTo = float.PositiveInfinity) {
        if (line.Count < 2) return;
        float[] flat = new float[line.Count];
        Array.Fill(flat, baseline);
        Band(sketch, new LinePoints(line.X, flat), line, colour, strip, visibleFrom, visibleTo);
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
