#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Something that lasted, on a timeline: from one value to another, in one lane, in a colour. From and To
///     equal is an instant (a press), drawn as a narrow tick. A menu input timeline is lanes of these: the
///     frames a confirm was accepted, the press, the frames lost between.
/// </summary>
internal readonly record struct Interval<T>(T From, T To, int Lane, Rgba Colour, string? Label = null);

/// <summary>
///     Intervals placed on a value axis across and lanes down: each one's rectangle, cut to the axis, and which
///     is under the mouse. An instant, and any interval narrower than <c>minWidth</c>, is widened to it round
///     its middle so it can be seen and hovered.
/// </summary>
internal sealed class Timeline {
    private readonly Area[] _areas;
    private readonly bool[] _shown;

    private Timeline(Area[] areas, bool[] shown) {
        _areas = areas;
        _shown = shown;
    }

    public int Count => _areas.Length;

    /// <summary>Interval <paramref name="index"/>'s rectangle.</summary>
    public Area Area(int index) => _areas[index];

    /// <summary>Whether any of interval <paramref name="index"/> is on the axis's range.</summary>
    public bool Shown(int index) => _shown[index];

    /// <param name="lanes">The lanes down the plot: a band per lane (their labels are <c>Paint.RowLabels</c>).</param>
    public static Timeline Place<T, TBands>(Scale<T> x, TBands lanes, IReadOnlyList<Interval<T>> intervals, float minWidth = 2f)
        where TBands : IBandLayout {
        float left = Math.Min(x.Start, x.End), right = Math.Max(x.Start, x.End);
        Area[] areas = new Area[intervals.Count];
        bool[] shown = new bool[intervals.Count];
        for (int i = 0; i < intervals.Count; i++) {
            Interval<T> interval = intervals[i];
            if (interval.Lane < 0 || interval.Lane >= lanes.Count) continue;
            float a = x.At(interval.From), b = x.At(interval.To);
            float from = Math.Min(a, b), to = Math.Max(a, b);
            if (to - from < minWidth) (from, to) = ((from + to) / 2f - minWidth / 2f, (from + to) / 2f + minWidth / 2f);
            if (to < left || from > right) continue;
            from = MathF.Round(Math.Max(from, left));
            to = MathF.Round(Math.Min(to, right));
            areas[i] = new Area(from, lanes.BarStart(interval.Lane), Math.Max(1f, to - from), lanes.BarWidth);
            shown[i] = true;
        }
        return new Timeline(areas, shown);
    }

    /// <summary>
    ///     The interval under a point, or -1. Where several overlap, the narrowest wins — an instant on top of
    ///     the window it fell in — and among equals the last drawn.
    /// </summary>
    public int IndexAt(float x, float y) {
        int best = -1;
        for (int i = 0; i < _areas.Length; i++) {
            if (!_shown[i] || !_areas[i].Contains(x, y)) continue;
            if (best < 0 || _areas[i].Width <= _areas[best].Width) best = i;
        }
        return best;
    }
}
