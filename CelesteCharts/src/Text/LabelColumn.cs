#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A label wanted beside a plot at a pixel: a line's end value on the right axis, say.
///     <paramref name="Hidden"/> keeps its place without drawing it, for a label a tooltip already shows,
///     so the others do not jump when the tooltip appears.
/// </summary>
internal readonly record struct SideLabel(float At, string Text, Rgba Colour, bool Hidden = false);

/// <summary>
///     A label where it will be drawn: its centre, its text, and the colours of every line it names
///     (more than one when lines ended on the same value).
/// </summary>
internal sealed record PlacedSideLabel(float At, string Text, Rgba[] Colours, bool Hidden);

/// <summary>
///     Places labels in a column beside a plot so that none overlap, each as near its line as the others
///     allow, in the order of their lines.
/// </summary>
// Crowded labels move as a group centred on where they wanted to be, so a pair moves half a spacing each
// rather than one staying put and the other moving a whole one: the least total distance, with the order
// kept. Labels with the same text at the same pixel say the same thing, so they become one label with
// every line's colour.
//
// ⚠️ Allocates: call it when the data or the size changes.
internal static class LabelColumn {
    /// <param name="labels">The labels, in any order.</param>
    /// <param name="spacing">The least distance between two labels' centres: a line of text and a gap.</param>
    /// <param name="top">The highest a label's centre may go (a plot's top).</param>
    /// <param name="bottom">The lowest (a plot's bottom).</param>
    /// <param name="margin">
    ///     How far past top or bottom a label's line may end and still be labelled; beyond, it is dropped.
    /// </param>
    public static PlacedSideLabel[] Place(IReadOnlyList<SideLabel> labels, float spacing, float top, float bottom, float margin = 0f) {
        List<(float At, string Text, List<Rgba> Colours, bool Hidden)> merged = new();
        foreach (SideLabel label in labels) {
            if (label.At < top - margin || label.At > bottom + margin) continue;
            int same = merged.FindIndex(m => m.Text == label.Text && Math.Abs(m.At - label.At) <= 1f);
            if (same < 0) {
                merged.Add((label.At, label.Text, [label.Colour], label.Hidden));
            } else {
                var m = merged[same];
                m.Colours.Add(label.Colour);
                merged[same] = m with { Hidden = m.Hidden && label.Hidden };   // shown if any of them is
            }
        }
        // Stable, so labels wanted at one pixel keep the caller's order.
        var sorted = new List<(float At, string Text, List<Rgba> Colours, bool Hidden)>(merged);
        for (int i = 1; i < sorted.Count; i++)
            for (int j = i; j > 0 && sorted[j].At < sorted[j - 1].At; j--) (sorted[j], sorted[j - 1]) = (sorted[j - 1], sorted[j]);

        float[] at = Spread(sorted.ConvertAll(l => l.At), spacing, Math.Min(top, bottom), Math.Max(top, bottom));
        PlacedSideLabel[] placed = new PlacedSideLabel[sorted.Count];
        for (int i = 0; i < placed.Length; i++) placed[i] = new PlacedSideLabel(at[i], sorted[i].Text, [.. sorted[i].Colours], sorted[i].Hidden);
        return placed;
    }

    /// <summary>
    ///     Ascending wanted positions moved apart to at least <paramref name="spacing"/>, keeping their
    ///     order, each group of crowded ones centred on the mean of where they wanted to be, then kept
    ///     between <paramref name="low"/> and <paramref name="high"/>. When they cannot all fit, they
    ///     start at <paramref name="low"/> and run past <paramref name="high"/>.
    /// </summary>
    public static float[] Spread(IReadOnlyList<float> wanted, float spacing, float low, float high) {
        int n = wanted.Count;
        float[] at = new float[n];
        if (n == 0) return at;

        // Groups as (first index, count, sum of wanted): a group's first centre is its mean minus half its span.
        List<(int First, int Count, double Sum)> groups = new();
        for (int i = 0; i < n; i++) {
            groups.Add((i, 1, wanted[i]));
            while (groups.Count > 1) {
                var last = groups[^1];
                var before = groups[^2];
                if (Start(last) >= Start(before) + before.Count * spacing) break;
                groups.RemoveAt(groups.Count - 1);
                groups[^1] = (before.First, before.Count + last.Count, before.Sum + last.Sum);
            }
        }
        foreach (var g in groups)
            for (int k = 0; k < g.Count; k++) at[g.First + k] = Start(g) + k * spacing;

        // Inside the bounds: pushed up from the bottom, then down from the top, so the top wins an overflow.
        for (int i = n - 1; i >= 0; i--) at[i] = Math.Min(at[i], high - (n - 1 - i) * spacing);
        for (int i = 0; i < n; i++) at[i] = Math.Max(at[i], low + i * spacing);
        return at;

        float Start((int First, int Count, double Sum) g) => (float) (g.Sum / g.Count - (g.Count - 1) * spacing / 2.0);
    }
}
