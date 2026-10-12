#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Lengths of time, as TimeSpan ticks in a long. Short steps are whole frames, so every tick lands
///     on a time the game can actually produce; steps of ten seconds and up are round clock times,
///     where being a frame off is no longer visible.
/// </summary>
internal sealed class Durations : IValueKind<long> {
    /// <summary>Celeste's timers advance 0.017 s a frame: 170,000 TimeSpan ticks.</summary>
    public const long CelesteFrameTicks = 170_000;

    /// <summary>Times as a speedrun timer shows them: 12.345, 1:15.000.</summary>
    public static readonly Durations Times = new();

    /// <summary>Differences against a reference, which carry their sign: +0.017, -1:15.000.</summary>
    public static readonly Durations Deltas = new(signed: true);

    // The frame-multiple steps, then the clock steps. Ascending, and each clock step is a whole
    // number of seconds, which is what lets its labels drop the milliseconds.
    private static readonly long[] FrameMultiples = [1, 2, 5, 10, 20, 50, 100, 200, 500];

    private static readonly long[] ClockSteps = [
        10 * TimeSpan.TicksPerSecond, 15 * TimeSpan.TicksPerSecond, 30 * TimeSpan.TicksPerSecond,
        TimeSpan.TicksPerMinute, 2 * TimeSpan.TicksPerMinute, 5 * TimeSpan.TicksPerMinute,
        10 * TimeSpan.TicksPerMinute, 15 * TimeSpan.TicksPerMinute, 30 * TimeSpan.TicksPerMinute,
        TimeSpan.TicksPerHour, 2 * TimeSpan.TicksPerHour, 3 * TimeSpan.TicksPerHour,
        6 * TimeSpan.TicksPerHour, 12 * TimeSpan.TicksPerHour, TimeSpan.TicksPerDay,
    ];

    private readonly long _frameTicks;
    private readonly bool _signed;

    /// <param name="frameTicks">The length of one frame, in TimeSpan ticks.</param>
    /// <param name="signed">Prefix positive values with "+", for deltas.</param>
    public Durations(long frameTicks = CelesteFrameTicks, bool signed = false) {
        if (frameTicks <= 0) throw new ArgumentOutOfRangeException(nameof(frameTicks), frameTicks, "a frame must last some time");
        _frameTicks = frameTicks;
        _signed = signed;
    }

    /// <summary>The length of one frame, in TimeSpan ticks.</summary>
    public long FrameTicks => _frameTicks;

    public int Compare(long a, long b) => a.CompareTo(b);

    public double Fraction(long value, long min, long max) =>
        max == min ? 0.5 : (double) (value - min) / (max - min);

    // The span as a double is exact to far below a frame for any duration a chart shows.
    public long Between(long min, long max, double fraction) {
        double at = min + fraction * ((double) max - min);
        return at >= long.MaxValue ? long.MaxValue : at <= long.MinValue ? long.MinValue : (long) Math.Round(at);
    }

    /// <summary>The whole number of frames nearest <paramref name="ticks"/>, in ticks: a time the game can produce.</summary>
    public long NearestFrame(long ticks) => (long) Math.Round((double) ticks / _frameTicks, MidpointRounding.AwayFromZero) * _frameTicks;

    public IReadOnlyList<Tick<long>> Ticks(long min, long max, int maxTicks) {
        List<Tick<long>> ticks = new();
        if (maxTicks < 1) return ticks;
        if (min > max) (min, max) = (max, min);

        if (min == max) {
            ticks.Add(new Tick<long>(min, Format(min)));
            return ticks;
        }

        foreach (long step in Steps()) {
            // Multiples of the step counted from zero, so 0 is a tick whenever it is in range: a
            // deviation axis always shows where "no difference" is.
            long first = CeilDiv(min, step);
            long last = FloorDiv(max, step);
            if (last - first + 1 > maxTicks) continue;

            if (last < first) {
                ticks.Add(new Tick<long>(min, Format(min)));
                return ticks;
            }
            // ⚠️ One form for the whole axis, chosen by its LARGEST tick: formatted one by one, a column
            // read "0:00" above "1:00:00", or "59.983" above "1:00.000".
            bool wholeSeconds = step % TimeSpan.TicksPerSecond == 0;
            Form form = FormOf(Math.Max(Math.Abs(first * step), Math.Abs(last * step)));
            for (long k = first; k <= last; k++) {
                long value = k * step;
                ticks.Add(new Tick<long>(value, Write(value, form, milliseconds: !wholeSeconds)));
            }
            return ticks;
        }

        // Longer than every step covers (a range of many days at a few ticks): one tick per end.
        ticks.Add(new Tick<long>(min, Format(min)));
        if (maxTicks > 1) ticks.Add(new Tick<long>(max, Format(max)));
        return ticks;
    }

    /// <summary>SCT's TimeTicks format: s.fff under a minute, m:ss.fff under an hour, then h:mm:ss.fff.</summary>
    // ⚠️ Formatted from the MAGNITUDE, with the sign put back afterwards. TimeSpan's custom format
    // specifiers print each component's absolute value, so choosing the short form from a signed
    // TotalSeconds made -75 s read "-15.000"; and a two-field form has nowhere to put an hour.
    public string Format(long value) => Write(value, FormOf(Math.Abs(value)), milliseconds: true);

    // How many clock fields a time needs. A clock step's labels always show minutes, so a column of
    // whole seconds reads 0:10, 0:20 rather than a bare 10, 20.
    private enum Form { Seconds, Minutes, Hours }

    private static Form FormOf(long magnitude) =>
        magnitude >= TimeSpan.TicksPerHour ? Form.Hours
        : magnitude >= TimeSpan.TicksPerMinute ? Form.Minutes
        : Form.Seconds;

    private string Write(long value, Form form, bool milliseconds) {
        TimeSpan abs = TimeSpan.FromTicks(Math.Abs(value));
        if (!milliseconds && form == Form.Seconds) form = Form.Minutes;
        string body = form switch {
            Form.Hours => $"{(long) abs.TotalHours}:{abs.Minutes:D2}:{abs.Seconds:D2}",
            Form.Minutes => $"{(long) abs.TotalMinutes}:{abs.Seconds:D2}",
            _ => $"{(long) abs.TotalSeconds}",
        };
        if (milliseconds) body += $".{abs.Milliseconds:D3}";
        return Sign(value) + body;
    }

    // Zero never carries a sign, even as a delta: "+0.000" reads as a gain that did not happen.
    private string Sign(long value) => value < 0 ? "-" : _signed && value > 0 ? "+" : "";

    private IEnumerable<long> Steps() {
        foreach (long multiple in FrameMultiples) yield return multiple * _frameTicks;
        long lastFrameStep = FrameMultiples[^1] * _frameTicks;
        foreach (long step in ClockSteps)
            if (step > lastFrameStep) yield return step;
    }

    // Integer division rounding toward negative and positive infinity; C#'s own truncates toward zero,
    // which would put the first tick of a negative range one step too far out.
    private static long FloorDiv(long a, long b) => a / b - (a % b != 0 && (a < 0) != (b < 0) ? 1 : 0);
    private static long CeilDiv(long a, long b) => a / b + (a % b != 0 && (a < 0) == (b < 0) ? 1 : 0);
}
