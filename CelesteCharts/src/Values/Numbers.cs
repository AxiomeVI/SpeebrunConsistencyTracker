#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Plain numbers: counts, rates, percentages, frames lost. Ticks fall on round multiples of a
///     step that is a mantissa times a power of ten (1, 2 or 5 by default).
/// </summary>
internal sealed class Numbers : IValueKind<double> {
    private static readonly double[] DefaultMantissas = [1, 2, 5];

    /// <summary>Any real number.</summary>
    public static readonly Numbers Plain = new();

    /// <summary>Counts: a step is never below 1, so no tick reads 2.5 reps.</summary>
    public static readonly Numbers Integers = new(integersOnly: true);

    /// <summary>Percentages given as 0-100, with 25 among the steps so a full scale reads in quarters.</summary>
    public static readonly Numbers Percent = new(suffix: "%", mantissas: [1, 2, 2.5, 5]);

    private readonly bool _integersOnly;
    private readonly string _suffix;
    private readonly double[] _mantissas;

    /// <param name="integersOnly">Steps of at least 1, and labels without decimals.</param>
    /// <param name="suffix">Appended to every label, e.g. "%" or " f".</param>
    /// <param name="mantissas">
    ///     The step candidates within one power of ten, ascending, each in [1, 10). Default 1, 2, 5.
    /// </param>
    public Numbers(bool integersOnly = false, string suffix = "", double[]? mantissas = null) {
        _integersOnly = integersOnly;
        _suffix = suffix;
        _mantissas = mantissas ?? DefaultMantissas;
        foreach (double m in _mantissas)
            if (!(m >= 1 && m < 10)) throw new ArgumentOutOfRangeException(nameof(mantissas), m, "each mantissa must be in [1, 10)");
        for (int i = 1; i < _mantissas.Length; i++)
            if (_mantissas[i] <= _mantissas[i - 1]) throw new ArgumentException("mantissas must ascend", nameof(mantissas));
    }

    public int Compare(double a, double b) => a.CompareTo(b);

    public double Fraction(double value, double min, double max) =>
        max == min ? 0.5 : (value - min) / (max - min);

    public double Between(double min, double max, double fraction) => min + fraction * (max - min);

    public IReadOnlyList<Tick<double>> Ticks(double min, double max, int maxTicks) {
        List<Tick<double>> ticks = new();
        // max - min is checked too: two finite bounds far enough apart overflow to infinity, and the
        // exponent below would then be computed from it.
        if (maxTicks < 1 || !double.IsFinite(min) || !double.IsFinite(max) || !double.IsFinite(max - min))
            return ticks;
        if (min > max) (min, max) = (max, min);

        if (min == max) {
            ticks.Add(new Tick<double>(min, Format(min)));
            return ticks;
        }

        // Start one power of ten below the step that would give exactly maxTicks intervals, and walk
        // up the candidates until the ticks fit. Every candidate is at most ten times the last one,
        // so the walk is a handful of iterations.
        int exponent = (int) Math.Floor(Math.Log10((max - min) / maxTicks)) - 1;
        if (_integersOnly && exponent < 0) exponent = 0;

        for (; ; exponent++) {
            foreach (double mantissa in _mantissas) {
                double step = mantissa * Math.Pow(10, exponent);
                if (_integersOnly && step % 1 != 0) continue;

                // The small tolerance keeps a bound that is itself a multiple of the step, but reached
                // through floating-point noise (0.30000000000000004), inside the range.
                double first = Math.Ceiling(min / step - 1e-9);
                double last = Math.Floor(max / step + 1e-9);
                double count = last - first + 1;
                if (count > maxTicks) continue;

                int decimals = Decimals(mantissa, exponent);
                if (count < 1) {
                    // A step wider than the whole range, with no multiple of it inside: only reached
                    // when maxTicks is tiny. One tick at the low bound keeps the "at least one" promise.
                    ticks.Add(new Tick<double>(min, Format(min)));
                    return ticks;
                }
                for (double k = first; k <= last; k++) {
                    // Rounded to the step's own decimals, so 3 * 0.1 is 0.3 and not 0.30000000000000004,
                    // and + 0.0 turns -0 (from Math.Ceiling(-0.4)) into 0, which would otherwise print "-0".
                    double value = Math.Round(k * step, Math.Min(decimals, 15)) + 0.0;
                    ticks.Add(new Tick<double>(value, Label(value, decimals)));
                }
                return ticks;
            }
        }
    }

    public string Format(double value) =>
        (_integersOnly ? Math.Round(value) + 0.0 : value + 0.0).ToString(_integersOnly ? "0" : "0.###", CultureInfo.InvariantCulture) + _suffix;

    // How many decimals a step of mantissa * 10^exponent has: the mantissa's own decimals shifted by
    // the exponent. 2.5 needs one at 10^0 (2.5), two at 10^-1 (0.25) and none at 10^1 (25).
    private static int Decimals(double mantissa, int exponent) {
        int own = 0;
        for (double m = mantissa; m % 1 != 0 && own < 15; m *= 10) own++;
        return Math.Max(0, own - exponent);
    }

    private string Label(double value, int decimals) =>
        value.ToString("F" + decimals, CultureInfo.InvariantCulture) + _suffix;
}
