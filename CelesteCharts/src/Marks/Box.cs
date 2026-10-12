#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     The five values a box plot draws: the whisker ends, the quartiles and the median. The mod
///     computes them — they are statistics a player also reads as text, so they must come from the
///     same percentile definition the mod's own overlay and exports use.
/// </summary>
/// <param name="Low">The lower whisker's end: the minimum, or a fence the mod chose.</param>
/// <param name="High">The upper whisker's end.</param>
internal readonly record struct FiveNumbers<T>(T Low, T Q1, T Median, T Q3, T High);

/// <summary>Which part of a box the pointer is on.</summary>
internal enum BoxPart { None, Low, Q1, Median, Q3, High, Box }

/// <summary>A box placed on a value axis: every part as a pixel along that axis.</summary>
/// <param name="Outliers">Values outside the whiskers, as pixels, in the order given.</param>
// Only the value axis. Across it, a box takes a band from Bands (BarStart, BarWidth), like a bar does.
internal sealed record BoxMarks(float Low, float Q1, float Median, float Q3, float High, float[] Outliers) {
    /// <summary>
    ///     What sits under a pixel on the value axis: the nearest of the five lines within
    ///     <paramref name="tolerance"/> pixels, else the box between the quartiles, else nothing.
    /// </summary>
    // A line wins over the box it bounds: hovering near the median names the median, not "the box".
    public BoxPart PartAt(float pixel, float tolerance = 4f) {
        BoxPart best = BoxPart.None;
        float bestDistance = tolerance;
        Consider(Low, BoxPart.Low);
        Consider(Q1, BoxPart.Q1);
        Consider(Median, BoxPart.Median);
        Consider(Q3, BoxPart.Q3);
        Consider(High, BoxPart.High);
        if (best != BoxPart.None) return best;
        return pixel >= Math.Min(Q1, Q3) && pixel <= Math.Max(Q1, Q3) ? BoxPart.Box : BoxPart.None;

        void Consider(float at, BoxPart part) {
            float distance = Math.Abs(pixel - at);
            if (distance <= bestDistance) {
                bestDistance = distance;
                best = part;
            }
        }
    }
}

internal static class Box {
    /// <summary>Places five numbers, and any outliers, on a scale.</summary>
    public static BoxMarks Place<T>(Scale<T> scale, FiveNumbers<T> five, IReadOnlyList<T>? outliers = null) {
        float[] placed = new float[outliers?.Count ?? 0];
        for (int i = 0; i < placed.Length; i++) placed[i] = scale.At(outliers![i]);
        return new BoxMarks(scale.At(five.Low), scale.At(five.Q1), scale.At(five.Median), scale.At(five.Q3),
                            scale.At(five.High), placed);
    }

    /// <summary>
    ///     Five numbers from sorted values, with whiskers at the minimum and maximum, using the
    ///     percentile function the mod passes — the one its own statistics use.
    /// </summary>
    /// <param name="percentile">(sorted values, percent 0-100) to the value at that percentile.</param>
    // Whiskers at fences (1.5 × IQR) need arithmetic on the values, which is the mod's to do: it builds
    // the FiveNumbers itself and passes the values beyond the fences as outliers.
    public static FiveNumbers<T> FromSorted<T>(IReadOnlyList<T> sorted, Func<IReadOnlyList<T>, int, T> percentile) {
        if (sorted.Count == 0) throw new ArgumentException("a box needs at least one value", nameof(sorted));
        return new FiveNumbers<T>(sorted[0], percentile(sorted, 25), percentile(sorted, 50),
                                  percentile(sorted, 75), sorted[^1]);
    }
}
