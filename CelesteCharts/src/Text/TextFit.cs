#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Making text fit: shortening a title to a width, and thinning a row of labels so none overlap.
///     Every width comes from a measure function the caller passes, so this runs without the game's
///     font; in a mod, pass <c>s => ActiveFont.Measure(s).X * scale</c>.
/// </summary>
// ⚠️ Allocates a string per attempt while shortening. Call it when the text or the width changes and
// keep the result, never once per frame.
internal static class TextFit {
    /// <summary>The ellipsis a shortened text ends its shortened part with.</summary>
    public const string Ellipsis = "…";

    /// <summary>
    ///     The text itself when it fits in <paramref name="width"/>; otherwise its head shortened with an
    ///     ellipsis. When <paramref name="keepFrom"/> occurs in the text, everything from its first
    ///     occurrence is kept whole: a title's count (" · 38 reps") is the part a player needs.
    /// </summary>
    public static string Truncate(string text, float width, Func<string, float> measure, string keepFrom = " · ") {
        if (measure(text) <= width) return text;

        int split = keepFrom.Length == 0 ? -1 : text.IndexOf(keepFrom, StringComparison.Ordinal);
        string head = split < 0 ? text : text[..split];
        string tail = split < 0 ? "" : text[split..];

        // Longest head that fits, by bisection on its length: the measure is the costly part.
        int lo = 0, hi = head.Length - 1;
        while (lo < hi) {
            int mid = (lo + hi + 1) / 2;
            if (measure(head[..mid].TrimEnd() + Ellipsis + tail) <= width) lo = mid;
            else hi = mid - 1;
        }
        return lo > 0 ? head[..lo].TrimEnd() + Ellipsis + tail : Ellipsis + tail;
    }

    /// <summary>
    ///     The largest scale, up to <paramref name="most"/>, at which one line of text fits a box: a stat
    ///     tile's number as big as its tile allows. Width grows with the scale, so it is one division.
    /// </summary>
    /// <param name="widthAtOne">The text's width at scale 1.</param>
    /// <param name="lineHeightAtOne">A line's height at scale 1.</param>
    public static float LargestScale(float widthAtOne, float lineHeightAtOne, float width, float height, float most) {
        float scale = most;
        if (widthAtOne > 0f) scale = Math.Min(scale, width / widthAtOne);
        if (lineHeightAtOne > 0f) scale = Math.Min(scale, height / lineHeightAtOne);
        return Math.Max(0f, scale);
    }

    /// <summary>
    ///     Keep every Nth label of a row so that no two kept labels overlap: the smallest N, from 1. A
    ///     label is centred on its position; <paramref name="gap"/> pixels must separate neighbours.
    /// </summary>
    /// <param name="centres">Ascending positions of the labels' centres.</param>
    /// <param name="widths">Each label's width, in the same order.</param>
    public static int Stride(IReadOnlyList<float> centres, IReadOnlyList<float> widths, float gap = 6f) {
        if (centres.Count != widths.Count) throw new ArgumentException("one width per centre", nameof(widths));
        for (int stride = 1; stride < Math.Max(2, centres.Count); stride++)
            if (Fits(centres, widths, gap, stride)) return stride;
        return Math.Max(1, centres.Count);
    }

    private static bool Fits(IReadOnlyList<float> centres, IReadOnlyList<float> widths, float gap, int stride) {
        for (int i = stride; i < centres.Count; i += stride) {
            float previousRight = centres[i - stride] + widths[i - stride] / 2f;
            float left = centres[i] - widths[i] / 2f;
            if (left - previousRight < gap) return false;
        }
        return true;
    }
}
