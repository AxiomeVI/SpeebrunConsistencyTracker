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
    /// <summary>No box: what <see cref="Box.Place{T}"/> gives for no five numbers. Nothing is under any pixel.</summary>
    public static readonly BoxMarks Empty = new(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, []);

    /// <summary>Whether there is nothing to draw: no box, or a part or an outlier not at a finite pixel (a NaN among the values).</summary>
    // Every part or none: a box missing its quartile would show a spread the data does not have.
    public bool IsEmpty {
        get {
            if (!(float.IsFinite(Low) && float.IsFinite(Q1) && float.IsFinite(Median) && float.IsFinite(Q3) && float.IsFinite(High)))
                return true;
            foreach (float outlier in Outliers)
                if (!float.IsFinite(outlier)) return true;
            return false;
        }
    }

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
    /// <summary>Places five numbers, and any outliers, on a scale. No five numbers (a room with no times) place <see cref="BoxMarks.Empty"/>.</summary>
    public static BoxMarks Place<T>(Scale<T> scale, FiveNumbers<T>? numbers, IReadOnlyList<T>? outliers = null) {
        if (numbers is not { } five) return BoxMarks.Empty;
        float[] placed = new float[outliers?.Count ?? 0];
        for (int i = 0; i < placed.Length; i++) placed[i] = scale.At(outliers![i]);
        return new BoxMarks(scale.At(five.Low), scale.At(five.Q1), scale.At(five.Median), scale.At(five.Q3),
                            scale.At(five.High), placed);
    }

    /// <summary>
    ///     Five numbers from sorted values, with whiskers at the minimum and maximum, using the
    ///     percentile function the mod passes — the one its own statistics use. Null for no values: there is no box.
    /// </summary>
    /// <param name="percentile">(sorted values, percent 0-100) to the value at that percentile.</param>
    // Whiskers at fences (1.5 × IQR) need arithmetic on the values, which is the mod's to do: it builds
    // the FiveNumbers itself and passes the values beyond the fences as outliers.
    public static FiveNumbers<T>? FromSorted<T>(IReadOnlyList<T> sorted, Func<IReadOnlyList<T>, int, T> percentile) {
        if (sorted.Count == 0) return null;
        return new FiveNumbers<T>(sorted[0], percentile(sorted, 25), percentile(sorted, 50),
                                  percentile(sorted, 75), sorted[^1]);
    }
}
