#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Where each segment of a stacked bar starts and ends: series on top of one another, per category,
///     in the values' own units or as shares of each category's total (a 100% stack).
/// </summary>
// Shares are layout, as bar heights are: they are where the segments go. A number a player reads in a
// tooltip (a count, a success rate) still comes from the mod, which has the values it passed in.
internal sealed class Stacks {
    private readonly double[][] _ends;   // [series][category]: where each segment ends
    private readonly double[] _totals;

    /// <param name="series">Values per series, then per category: every series the same length. No negatives.</param>
    /// <param name="percent">Each category scaled to 100, for a stack of shares.</param>
    public Stacks(IReadOnlyList<IReadOnlyList<double>> series, bool percent = false) {
        int categories = series.Count == 0 ? 0 : series[0].Count;
        _ends = new double[series.Count][];
        _totals = new double[categories];
        for (int s = 0; s < series.Count; s++) {
            if (series[s].Count != categories) throw new ArgumentException("every series needs one value per category", nameof(series));
            for (int c = 0; c < categories; c++) {
                if (series[s][c] < 0) throw new ArgumentException("a stack cannot hold a negative value", nameof(series));
                _totals[c] += series[s][c];
            }
        }
        for (int s = 0; s < series.Count; s++) {
            _ends[s] = new double[categories];
            for (int c = 0; c < categories; c++) {
                double below = s == 0 ? 0 : _ends[s - 1][c];
                double value = percent ? (_totals[c] == 0 ? 0 : 100 * series[s][c] / _totals[c]) : series[s][c];
                _ends[s][c] = below + value;
            }
        }
        Percent = percent;
    }

    public bool Percent { get; }
    public int Series => _ends.Length;
    public int Categories => _totals.Length;

    /// <summary>A category's total in the values' units (before any scaling to 100).</summary>
    public double Total(int category) => _totals[category];

    /// <summary>The tallest stack, for the value axis: 100 for shares.</summary>
    public double Tallest {
        get {
            if (Percent) return 100;
            double tallest = 0;
            foreach (double t in _totals) tallest = Math.Max(tallest, t);
            return tallest;
        }
    }

    public double Start(int series, int category) => series == 0 ? 0 : _ends[series - 1][category];

    public double End(int series, int category) => _ends[series][category];

    /// <summary>
    ///     The series whose segment is under <paramref name="pixel"/> on <paramref name="y"/> in a category,
    ///     or -1: compared in pixels, so no value is read back from the screen.
    /// </summary>
    public int SeriesAt(int category, Scale<double> y, float pixel) {
        for (int s = 0; s < Series; s++) {
            float a = y.At(Start(s, category)), b = y.At(End(s, category));
            if (pixel >= Math.Min(a, b) && pixel < Math.Max(a, b)) return s;
        }
        return -1;
    }

    /// <summary>The series whose segment holds <paramref name="value"/> in a category, or -1 above the stack.</summary>
    public int SeriesAt(int category, double value) {
        if (value < 0) return -1;
        for (int s = 0; s < Series; s++)
            if (value < _ends[s][category]) return s;
        return -1;
    }
}
