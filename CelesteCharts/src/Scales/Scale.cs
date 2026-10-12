#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>A tick with its place on screen.</summary>
internal readonly record struct PlacedTick<T>(T Value, string Label, float At);

/// <summary>
///     Maps one kind of value onto a stretch of pixels: min lands on <c>start</c>, max on <c>end</c>.
///     End may be smaller than start — a vertical axis grows upward, towards smaller y.
/// </summary>
// Padding is done in pixels, not in values: shrink [start, end] and the data sits inside the same
// way, without the value arithmetic a date or a duration would need to widen its own range.
internal sealed class Scale<T> {
    public IValueKind<T> Kind { get; }
    public T Min { get; }
    public T Max { get; }
    public float Start { get; }
    public float End { get; }

    public Scale(IValueKind<T> kind, T min, T max, float start, float end) {
        Kind = kind;
        if (kind.Compare(min, max) > 0) (min, max) = (max, min);
        Min = min;
        Max = max;
        Start = start;
        End = end;
    }

    /// <summary>The pixel for a value. Not clamped: a value outside [Min, Max] lands outside [Start, End].</summary>
    public float At(T value) => Start + (float) Kind.Fraction(value, Min, Max) * (End - Start);

    /// <summary>
    ///     The value at a pixel, the inverse of <see cref="At"/>: where a dragged line was let go, the ends
    ///     of a zoomed range. Not clamped and not snapped; the caller does both. An axis of no length reads
    ///     its middle.
    /// </summary>
    public T ValueAt(float pixel) => Kind.Between(Min, Max, End == Start ? 0.5 : (pixel - Start) / (double) (End - Start));

    /// <summary>
    ///     The same pixels read in other values: a second axis tied to this one, such as absolute times
    ///     beside deviations from the average (<c>d => average + d</c>). The linked scale has its own
    ///     round ticks in its own kind. <paramref name="convert"/> must be linear (an offset, a factor),
    ///     or the two axes disagree between their ends; one that runs backwards is fine.
    /// </summary>
    public Scale<TR> Linked<TR>(IValueKind<TR> kind, Func<T, TR> convert) {
        TR min = convert(Min), max = convert(Max);
        // The new scale would swap a reversed range, so swap the pixels with it: Min still lands on Start.
        return kind.Compare(min, max) <= 0 ? new Scale<TR>(kind, min, max, Start, End) : new Scale<TR>(kind, max, min, End, Start);
    }

    /// <summary>The kind's ticks for this range, each with its pixel.</summary>
    // Allocates: call it when the data or the size changes, and keep the result.
    public IReadOnlyList<PlacedTick<T>> Ticks(int maxTicks) {
        IReadOnlyList<Tick<T>> ticks = Kind.Ticks(Min, Max, maxTicks);
        PlacedTick<T>[] placed = new PlacedTick<T>[ticks.Count];
        for (int i = 0; i < ticks.Count; i++) placed[i] = new PlacedTick<T>(ticks[i].Value, ticks[i].Label, At(ticks[i].Value));
        return placed;
    }
}
