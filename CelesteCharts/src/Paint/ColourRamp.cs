#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Colours for values: a fraction from 0 to 1 becomes a colour between stops, for heatmap cells and
///     calendars. Place a value on it with a scale onto [0, 1]:
///     <c>ramp.At(new Scale&lt;T&gt;(kind, min, max, 0, 1).At(value))</c>.
/// </summary>
// Stops are interpolated channel by channel. The built-in ramps are sampled densely enough from
// perceptual colour maps that straight lines between stops stay close to them.
internal sealed class ColourRamp {
    private readonly float[] _at;
    private readonly Rgba[] _colours;

    /// <param name="stops">Ascending fractions from 0 to 1, each with its colour; at least two.</param>
    public ColourRamp(params (float At, Rgba Colour)[] stops) {
        if (stops.Length < 2) throw new ArgumentException("a ramp needs at least two stops", nameof(stops));
        _at = new float[stops.Length];
        _colours = new Rgba[stops.Length];
        for (int i = 0; i < stops.Length; i++) {
            if (i > 0 && stops[i].At < stops[i - 1].At) throw new ArgumentException("stops must ascend", nameof(stops));
            _at[i] = stops[i].At;
            _colours[i] = stops[i].Colour;
        }
        if (_at[0] != 0f || _at[^1] != 1f) throw new ArgumentException("stops must run from 0 to 1", nameof(stops));
    }

    /// <summary>
    ///     Viridis: dark purple to yellow, brightness rising all the way, readable with every common colour
    ///     blindness and in greyscale. For "more is more": counts, time lost.
    /// </summary>
    // ⚠️ Its dark end is close to a dark panel's black: on the HUD, Slice(0.2f, 1f) keeps low cells visible.
    public static readonly ColourRamp Viridis = Even(
        0x440154, 0x472d7b, 0x3b528b, 0x2c728e, 0x21918c, 0x28ae80, 0x5ec962, 0xaddc30, 0xfde725);

    /// <summary>
    ///     Two sides of a middle, for signed values such as a change or a delta: light blue through a dark
    ///     grey to light orange, brighter the further from the middle, so on a dark panel no change is
    ///     quiet and a big change stands out. Blue against orange stays apart with every common colour
    ///     blindness. Give it a range symmetric around zero, so zero lands on the middle.
    /// </summary>
    public static readonly ColourRamp Diverging = Even(0x92c5de, 0x4393c3, 0x404040, 0xe08214, 0xfdb863);

    /// <summary>The colour at <paramref name="fraction"/>, clamped to [0, 1]; NaN gives the middle.</summary>
    public Rgba At(double fraction) {
        float t = double.IsNaN(fraction) ? 0.5f : (float) Math.Clamp(fraction, 0.0, 1.0);
        int upper = 1;
        while (upper < _at.Length - 1 && _at[upper] < t) upper++;
        float span = _at[upper] - _at[upper - 1];
        float local = span <= 0f ? 1f : (t - _at[upper - 1]) / span;
        return Mix(_colours[upper - 1], _colours[upper], local);
    }

    /// <summary>The part of this ramp from <paramref name="from"/> to <paramref name="to"/>, stretched to a whole ramp.</summary>
    public ColourRamp Slice(float from, float to, int stops = 9) {
        (float, Rgba)[] sampled = new (float, Rgba)[Math.Max(2, stops)];
        for (int i = 0; i < sampled.Length; i++) {
            float t = i / (float) (sampled.Length - 1);
            sampled[i] = (t, At(from + (to - from) * t));
        }
        return new ColourRamp(sampled);
    }

    /// <summary>The same colours from the other end.</summary>
    public ColourRamp Reversed() {
        (float, Rgba)[] stops = new (float, Rgba)[_at.Length];
        for (int i = 0; i < stops.Length; i++) stops[i] = (1f - _at[^(i + 1)], _colours[^(i + 1)]);
        return new ColourRamp(stops);
    }

    /// <summary>The stops, in order.</summary>
    public IReadOnlyList<(float At, Rgba Colour)> Stops {
        get {
            (float, Rgba)[] stops = new (float, Rgba)[_at.Length];
            for (int i = 0; i < stops.Length; i++) stops[i] = (_at[i], _colours[i]);
            return stops;
        }
    }

    private static Rgba Mix(Rgba a, Rgba b, float t) =>
        new((int) MathF.Round(a.R + (b.R - a.R) * t), (int) MathF.Round(a.G + (b.G - a.G) * t),
            (int) MathF.Round(a.B + (b.B - a.B) * t), (int) MathF.Round(a.A + (b.A - a.A) * t));

    private static ColourRamp Even(params int[] rgb) {
        (float, Rgba)[] stops = new (float, Rgba)[rgb.Length];
        for (int i = 0; i < rgb.Length; i++)
            stops[i] = (i / (float) (rgb.Length - 1), new Rgba((rgb[i] >> 16) & 0xff, (rgb[i] >> 8) & 0xff, rgb[i] & 0xff));
        return new ColourRamp(stops);
    }
}
