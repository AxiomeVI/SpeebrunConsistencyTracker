#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>How the points of one category spread across its band, so they do not all sit on one line.</summary>
internal enum StripSpread {
    /// <summary>In the order given, first on the left, last on the right: SCT's per-room scatter, oldest rep to newest.</summary>
    InOrder,

    /// <summary>Scattered by each point's index, the same way every time: a plain jitter with no meaning in x.</summary>
    Jitter,
}

/// <summary>
///     Points along a value axis, one strip per category: every rep of every room, as SCT's per-room scatter
///     shows them. The points are laid out once; <see cref="Points"/> draws them (<c>Paint.Points</c>) and
///     hover finds the nearest (<see cref="Nearest"/>), which names its category and its place there.
/// </summary>
internal sealed class StripPoints {
    private readonly int[] _band, _index;

    private StripPoints(LinePoints points, int[] band, int[] index) {
        Points = points;
        _band = band;
        _index = index;
    }

    /// <summary>Every point, category after category, never joined.</summary>
    public LinePoints Points { get; }

    public int Count => Points.Count;

    /// <summary>The category point <paramref name="point"/> belongs to.</summary>
    public int BandOf(int point) => _band[point];

    /// <summary>Where point <paramref name="point"/> is in its category's values: the rep, for SCT.</summary>
    public int IndexOf(int point) => _index[point];

    /// <summary>The point nearest (x, y) within <paramref name="within"/> pixels, or -1.</summary>
    public int Nearest(float x, float y, float within) => Points.Nearest(x, y, within);

    /// <summary>
    ///     Each category's values on <paramref name="y"/>, spread across <paramref name="spread"/> of its band's bar
    ///     (0.7 is SCT's). A category with one value sits in the middle. Values the mod left out (a death, a
    ///     hidden room) are simply not in its list.
    /// </summary>
    public static StripPoints Place<T, TBands>(TBands bands, Scale<T> y, IReadOnlyList<IReadOnlyList<T>> values, float spread = 0.7f,
                                               StripSpread mode = StripSpread.InOrder) where TBands : IBandLayout {
        int count = 0;
        for (int b = 0; b < Math.Min(bands.Count, values.Count); b++)
            if (bands.BarWidthOf(b) > 0f) count += values[b].Count;
        float[] xs = new float[count], ys = new float[count];
        int[] band = new int[count], index = new int[count];
        int at = 0;
        for (int b = 0; b < Math.Min(bands.Count, values.Count); b++) {
            // A band with no bar (a hidden column) places none of its points.
            float width = bands.BarWidthOf(b) * Math.Clamp(spread, 0f, 1f), centre = bands.BarStart(b) + bands.BarWidthOf(b) / 2f;
            int n = bands.BarWidthOf(b) > 0f ? values[b].Count : 0;
            for (int i = 0; i < n; i++) {
                float along = mode == StripSpread.Jitter ? Scatter(b, i) : n <= 1 ? 0.5f : (float) i / (n - 1);
                xs[at] = centre + (along - 0.5f) * width;
                ys[at] = y.At(values[b][i]);
                (band[at], index[at]) = (b, i);
                at++;
            }
        }
        // Every point starts a stretch of its own: a strip is never drawn as a line.
        int[] breaks = new int[count];
        for (int i = 0; i < count; i++) breaks[i] = i;
        return new StripPoints(new LinePoints(xs, ys, breaks), band, index);
    }

    // A fraction in [0, 1) from the category and the index alone, so a strip looks the same every time it is
    // built; a golden-ratio step keeps neighbours apart.
    private static float Scatter(int band, int index) {
        double v = (index * 0.6180339887498949 + band * 0.7548776662466927) % 1.0;
        return (float) v;
    }
}
