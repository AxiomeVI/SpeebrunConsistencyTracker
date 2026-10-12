#nullable enable

using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>One tick on an axis: where it goes, and what it reads.</summary>
internal readonly record struct Tick<T>(T Value, string Label);

/// <summary>
///     What a kind of value means to an axis: how far along a range a value sits, where the ticks
///     go, and how a value reads. Plain numbers, durations and dates each do their arithmetic
///     differently, so each is its own kind.
/// </summary>
// ⚠️ Generic over the value's own type rather than converting everything to double up front. A
// double holds 53 bits: a DateTime's ticks (about 6.4e17 today) land on a grid 128 ticks wide, and a
// tick computed as `min + i * step` in doubles reads 12.339999 instead of 12.34. Each kind computes
// its ticks in its own arithmetic and only turns a value into a double for Fraction, where that
// rounding is far below a pixel.
internal interface IValueKind<T> {
    /// <summary>Orders two values, as IComparer does.</summary>
    int Compare(T a, T b);

    /// <summary>
    ///     Where <paramref name="value"/> sits between <paramref name="min"/> (0) and
    ///     <paramref name="max"/> (1). Not clamped: a value outside the range lands outside [0, 1],
    ///     and the caller decides whether to clip it. An empty range (min equal to max) puts every
    ///     value at 0.5, the middle.
    /// </summary>
    double Fraction(T value, T min, T max);

    /// <summary>
    ///     The value at <paramref name="fraction"/> of the way from <paramref name="min"/> (0) to
    ///     <paramref name="max"/> (1): the inverse of <see cref="Fraction"/>, for reading a value back
    ///     from a pixel (a dragged line, a zoomed range). Not clamped to the range, only to what the type
    ///     can hold; not rounded to anything, so a mod snaps it to what it can use (a whole frame).
    /// </summary>
    T Between(T min, T max, double fraction);

    /// <summary>
    ///     The ticks for the range [min, max], ascending, all inside it, at most
    ///     <paramref name="maxTicks"/> of them and at least one whenever maxTicks is at least one.
    ///     The labels are written for the step the ticks were spaced at, so they carry exactly the
    ///     precision that step needs. The bounds may come in either order.
    /// </summary>
    // ⚠️ Allocates a list and a string per tick. It is meant to run when the data changes, from
    // behind a cache keyed on the data's version, never once per frame.
    IReadOnlyList<Tick<T>> Ticks(T min, T max, int maxTicks);

    /// <summary>One value at full precision, for a tooltip, where no step applies.</summary>
    string Format(T value);
}
