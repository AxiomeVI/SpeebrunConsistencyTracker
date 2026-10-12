#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Which values a bin holds.</summary>
internal enum BinShape {
    /// <summary>From Lower (included) to Upper (excluded).</summary>
    Range,
    /// <summary>Everything below Upper: "&lt;0".</summary>
    Underflow,
    /// <summary>Everything from Lower up: "10+".</summary>
    Overflow,
}

/// <summary>
///     One bin. For a Range, values v with Lower &lt;= v &lt; Upper. An Underflow reads only Upper and an
///     Overflow only Lower; the other bound repeats the edge so the struct needs no nullable T.
/// </summary>
internal readonly record struct Bin<T>(T Lower, T Upper, BinShape Shape, string Label);

/// <summary>How many values each bin of a <see cref="Bins{T}"/> holds.</summary>
/// <param name="Counts">One count per bin, in the bins' order.</param>
/// <param name="Below">Values under the first bin, when there is no underflow bin to hold them.</param>
/// <param name="Above">Values at or over the last bin's end, when there is no overflow bin to hold them.</param>
// ⚠️ Below and Above are COUNTED, not dropped. A histogram that silently loses the reps outside its
// range is a chart that lies about the session played; the caller decides whether to say so.
internal sealed record BinCounts(int[] Counts, int Below, int Above) {
    /// <summary>Every value offered, inside the bins or not.</summary>
    public int Total { get; } = Sum(Counts) + Below + Above;

    /// <summary>The largest single count, 0 when every bin is empty: the height a bar axis scales to.</summary>
    public int Tallest { get; } = Max(Counts);

    private static int Sum(int[] counts) {
        int sum = 0;
        foreach (int c in counts) sum += c;
        return sum;
    }

    private static int Max(int[] counts) {
        int max = 0;
        foreach (int c in counts) if (c > max) max = c;
        return max;
    }
}

/// <summary>
///     A set of bins over one kind of value: ascending edges, and optionally a bin for everything below
///     the first edge and one for everything from the last edge up. Immutable; every With* returns a
///     new set.
/// </summary>
/// <remarks>
///     Build one with the factories on <see cref="Bins"/>: a bin per whole number, groups of whole
///     numbers (1-3, 4-6, 7-9), whole-frame widths for durations, or edges of your choosing.
/// </remarks>
internal sealed class Bins<T> {
    private readonly IValueKind<T> _kind;
    private readonly T[] _edges;
    private readonly Func<Bin<T>, string> _label;

    /// <summary>Every bin, underflow first and overflow last when present.</summary>
    public IReadOnlyList<Bin<T>> All { get; }

    public bool HasUnderflow { get; }
    public bool HasOverflow { get; }

    /// <param name="edges">At least two, strictly ascending. n edges make n - 1 range bins.</param>
    /// <param name="label">Writes each bin's label. The factories on <see cref="Bins"/> pass one.</param>
    public Bins(IValueKind<T> kind, IReadOnlyList<T> edges, Func<Bin<T>, string> label,
                bool underflow = false, bool overflow = false) {
        if (edges.Count < 2) throw new ArgumentException("bins need at least two edges", nameof(edges));
        for (int i = 1; i < edges.Count; i++)
            if (kind.Compare(edges[i - 1], edges[i]) >= 0)
                throw new ArgumentException($"edges must be strictly ascending (edge {i})", nameof(edges));

        _kind = kind;
        _edges = [.. edges];
        _label = label;
        HasUnderflow = underflow;
        HasOverflow = overflow;

        List<Bin<T>> all = new(_edges.Length + 1);
        if (underflow) all.Add(Labelled(_edges[0], _edges[0], BinShape.Underflow));
        for (int i = 0; i + 1 < _edges.Length; i++) all.Add(Labelled(_edges[i], _edges[i + 1], BinShape.Range));
        if (overflow) all.Add(Labelled(_edges[^1], _edges[^1], BinShape.Overflow));
        All = all;
    }

    /// <summary>The same bins, with everything from the last edge up gathered in one more: "10+".</summary>
    public Bins<T> WithOverflow() => new(_kind, _edges, _label, HasUnderflow, overflow: true);

    /// <summary>The same bins, with everything below the first edge gathered in one more.</summary>
    public Bins<T> WithUnderflow() => new(_kind, _edges, _label, underflow: true, HasOverflow);

    /// <summary>The same bins with the labels a mod writes, for its own wording or language.</summary>
    public Bins<T> WithLabels(Func<Bin<T>, string> label) => new(_kind, _edges, label, HasUnderflow, HasOverflow);

    /// <summary>The index in <see cref="All"/> of the bin holding a value, or -1 when none does.</summary>
    public int IndexOf(T value) {
        int range = RangeIndex(value);
        if (range == -1) return HasUnderflow ? 0 : -1;
        if (range == _edges.Length - 1) return HasOverflow ? All.Count - 1 : -1;
        return range + (HasUnderflow ? 1 : 0);
    }

    /// <summary>Counts each value once.</summary>
    public BinCounts Count(IEnumerable<T> values) {
        int[] counts = new int[All.Count];
        int below = 0, above = 0;
        foreach (T value in values) Tally(value, 1, counts, ref below, ref above);
        return new BinCounts(counts, below, above);
    }

    /// <summary>Counts values already tallied, as (value, how many times) — a distribution's shape.</summary>
    public BinCounts Count(IEnumerable<KeyValuePair<T, int>> tallies) {
        int[] counts = new int[All.Count];
        int below = 0, above = 0;
        foreach (KeyValuePair<T, int> pair in tallies) Tally(pair.Key, pair.Value, counts, ref below, ref above);
        return new BinCounts(counts, below, above);
    }

    private void Tally(T value, int times, int[] counts, ref int below, ref int above) {
        int index = IndexOf(value);
        if (index >= 0) counts[index] += times;
        else if (RangeIndex(value) == -1) below += times;
        else above += times;
    }

    // -1 below the first edge, edges.Length - 1 at or past the last one, otherwise the range bin's own
    // index. A binary search, since a histogram of several hundred frame-wide bins is ordinary.
    private int RangeIndex(T value) {
        if (_kind.Compare(value, _edges[0]) < 0) return -1;
        if (_kind.Compare(value, _edges[^1]) >= 0) return _edges.Length - 1;
        int lo = 0, hi = _edges.Length - 1;   // edges[lo] <= value < edges[hi]
        while (hi - lo > 1) {
            int mid = (lo + hi) / 2;
            if (_kind.Compare(value, _edges[mid]) < 0) hi = mid;
            else lo = mid;
        }
        return lo;
    }

    private Bin<T> Labelled(T lower, T upper, BinShape shape) {
        Bin<T> bin = new(lower, upper, shape, "");
        return bin with { Label = _label(bin) };
    }
}

/// <summary>The ways to cut values into bins.</summary>
internal static class Bins {
    /// <summary>
    ///     Whole numbers from <paramref name="first"/> to <paramref name="last"/>, in groups of
    ///     <paramref name="width"/>: width 1 gives one bin per value ("0" ... "9"), width 3 from 1 gives
    ///     "1-3", "4-6", "7-9". A last group that would run past <paramref name="last"/> is cut there.
    ///     Chain <see cref="Bins{T}.WithOverflow"/> for "10+".
    /// </summary>
    public static Bins<double> Integers(int first, int last, int width = 1) {
        if (last < first) throw new ArgumentException("last must not be below first", nameof(last));
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width), width, "a group holds at least one value");
        List<double> edges = new();
        for (long at = first; at <= last; at += width) edges.Add(at);
        edges.Add(last + 1.0);
        return new Bins<double>(Numbers.Integers, edges, IntegerLabel);
    }

    /// <summary>
    ///     Durations in bins a whole number of frames wide, covering [min, max]. The width is the
    ///     preferred one rounded up to whole frames, widened further if the range would need more than
    ///     <paramref name="maxBins"/>. Edges are whole widths counted from <paramref name="origin"/>: from zero
    ///     (the default) they line up with the axis's ticks; from the smallest value, the first bin starts
    ///     on it and no bin is spent on the stretch before it.
    /// </summary>
    // Whole frames because Celeste's times are whole frames: a bin a third of a frame wide holds
    // nothing two times in three, and the histogram combs — the shape is the binning's, not the data's.
    public static Bins<long> Frames(Durations kind, long min, long max, long preferredWidth, int maxBins = 50, long origin = 0) {
        if (maxBins < 1) throw new ArgumentOutOfRangeException(nameof(maxBins), maxBins, "at least one bin");
        if (min > max) (min, max) = (max, min);
        long frame = kind.FrameTicks;
        long width = WholeFrames(Math.Max(preferredWidth, 1), frame);

        long first = origin + FloorTo(min - origin, width);
        long count = (max - first) / width + 1;
        if (count > maxBins) {
            // The estimate, then a frame at a time: flooring the first edge to the new width can leave
            // one bin too many, and the loop settles it in a step or two.
            width = Math.Max(width, WholeFrames((max - min) / maxBins + 1, frame));
            for (; ; width += frame) {
                first = origin + FloorTo(min - origin, width);
                count = (max - first) / width + 1;
                if (count <= maxBins) break;
            }
        }

        long[] edges = new long[count + 1];
        for (long i = 0; i <= count; i++) edges[i] = first + i * width;
        return new Bins<long>(kind, edges, bin => RangeLabel(kind, bin));
    }

    /// <summary>Bins between edges of your choosing, uneven ones included, labelled "a to b".</summary>
    public static Bins<T> Edges<T>(IValueKind<T> kind, params T[] edges) =>
        new(kind, edges, bin => RangeLabel(kind, bin));

    // "5" for a bin of one value, "1-3" for a group, "10+" and "<0" at the ends. A negative bound makes
    // the hyphen ambiguous ("-3--1"), so those groups read "-3 to -1".
    private static string IntegerLabel(Bin<double> bin) {
        CultureInfo c = CultureInfo.InvariantCulture;
        long lower = (long) bin.Lower, upperIncluded = (long) bin.Upper - 1;
        return bin.Shape switch {
            BinShape.Overflow => lower.ToString(c) + "+",
            BinShape.Underflow => "<" + lower.ToString(c),
            _ when upperIncluded == lower => lower.ToString(c),
            _ when lower < 0 => $"{lower.ToString(c)} to {upperIncluded.ToString(c)}",
            _ => $"{lower.ToString(c)}-{upperIncluded.ToString(c)}",
        };
    }

    private static string RangeLabel<T>(IValueKind<T> kind, Bin<T> bin) => bin.Shape switch {
        BinShape.Overflow => kind.Format(bin.Lower) + "+",
        BinShape.Underflow => "<" + kind.Format(bin.Upper),
        _ => $"{kind.Format(bin.Lower)} to {kind.Format(bin.Upper)}",
    };

    private static long WholeFrames(long ticks, long frame) => (ticks + frame - 1) / frame * frame;

    private static long FloorTo(long value, long step) => value / step * step - (value % step < 0 ? step : 0);
}
