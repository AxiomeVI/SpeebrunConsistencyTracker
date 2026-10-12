#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Celeste.Mod.CelesteCharts;

/// <summary>The calendar unit a date step counts in.</summary>
internal enum DateUnit { Second, Minute, Hour, Day, Week, Month, Year }

/// <summary>
///     Points in time: when a session started, when a personal best was set. Ticks fall on calendar
///     boundaries (the hour, midnight, a Monday, the first of a month), which is arithmetic a plain
///     number cannot do: months are not all one length.
/// </summary>
// Works on whatever DateTimeKind it is given and never converts between local time and UTC. Steps
// are added with DateTime's own Add* methods, which ignore daylight saving: across a change, one
// hourly tick is an hour off in wall-clock terms, which an axis can live with.
internal sealed class Dates : IValueKind<DateTime> {
    /// <summary>English month names, 24-hour times.</summary>
    public static readonly Dates Default = new();

    private static readonly (DateUnit Unit, int Count)[] Steps = [
        (DateUnit.Second, 1), (DateUnit.Second, 2), (DateUnit.Second, 5), (DateUnit.Second, 10),
        (DateUnit.Second, 15), (DateUnit.Second, 30),
        (DateUnit.Minute, 1), (DateUnit.Minute, 2), (DateUnit.Minute, 5), (DateUnit.Minute, 10),
        (DateUnit.Minute, 15), (DateUnit.Minute, 30),
        (DateUnit.Hour, 1), (DateUnit.Hour, 2), (DateUnit.Hour, 3), (DateUnit.Hour, 6), (DateUnit.Hour, 12),
        (DateUnit.Day, 1), (DateUnit.Day, 2), (DateUnit.Week, 1),
        (DateUnit.Month, 1), (DateUnit.Month, 2), (DateUnit.Month, 3), (DateUnit.Month, 6),
        (DateUnit.Year, 1), (DateUnit.Year, 2), (DateUnit.Year, 5), (DateUnit.Year, 10),
        (DateUnit.Year, 20), (DateUnit.Year, 50), (DateUnit.Year, 100), (DateUnit.Year, 1000),
    ];

    private readonly Func<DateTime, DateUnit, string> _label;

    /// <param name="label">
    ///     Writes a tick's label from its value and the unit its step counts in. Pass one to follow the
    ///     game's language; the default writes English month names with the invariant culture, since
    ///     the game's language is not the system's culture.
    /// </param>
    public Dates(Func<DateTime, DateUnit, string>? label = null) {
        _label = label ?? DefaultLabel;
    }

    public int Compare(DateTime a, DateTime b) => a.CompareTo(b);

    public double Fraction(DateTime value, DateTime min, DateTime max) =>
        max == min ? 0.5 : (double) (value - min).Ticks / (max - min).Ticks;

    // Only the offset from min goes through a double: the date itself keeps every tick.
    public DateTime Between(DateTime min, DateTime max, double fraction) {
        double offset = fraction * (max - min).Ticks;
        double at = min.Ticks + offset;
        if (at <= DateTime.MinValue.Ticks) return DateTime.MinValue;
        if (at >= DateTime.MaxValue.Ticks) return DateTime.MaxValue;
        return new DateTime(min.Ticks + (long) Math.Round(offset), min.Kind);
    }

    public IReadOnlyList<Tick<DateTime>> Ticks(DateTime min, DateTime max, int maxTicks) {
        List<Tick<DateTime>> ticks = new();
        if (maxTicks < 1) return ticks;
        if (min > max) (min, max) = (max, min);

        if (min == max) {
            ticks.Add(new Tick<DateTime>(min, Format(min)));
            return ticks;
        }

        foreach ((DateUnit unit, int count) in Steps) {
            // Counted by walking, not by dividing, because a month step has no fixed length. The walk
            // stops one past maxTicks, so a step far too fine costs maxTicks + 1 additions.
            List<DateTime> values = new();
            DateTime? at = FirstAtOrAfter(min, unit, count);
            while (at is DateTime t && t <= max && values.Count <= maxTicks) {
                values.Add(t);
                at = Add(t, unit, count);
            }
            if (values.Count > maxTicks) continue;

            if (values.Count == 0) {
                ticks.Add(new Tick<DateTime>(min, Format(min)));
                return ticks;
            }
            foreach (DateTime value in values) ticks.Add(new Tick<DateTime>(value, _label(value, unit)));
            return ticks;
        }

        // A range wider than a thousand years a tick: one tick per end.
        ticks.Add(new Tick<DateTime>(min, Format(min)));
        if (maxTicks > 1) ticks.Add(new Tick<DateTime>(max, Format(max)));
        return ticks;
    }

    public string Format(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    // Minute and hour ticks read as clock times, except the one at midnight, which reads as the new
    // day: without it a range across midnight shows 22:00, 00:00, 02:00 and no hint which day.
    private static string DefaultLabel(DateTime value, DateUnit unit) {
        CultureInfo c = CultureInfo.InvariantCulture;
        return unit switch {
            DateUnit.Second => value.ToString(value.TimeOfDay == TimeSpan.Zero ? "MMM d" : "HH:mm:ss", c),
            DateUnit.Minute or DateUnit.Hour => value.ToString(value.TimeOfDay == TimeSpan.Zero ? "MMM d" : "HH:mm", c),
            DateUnit.Day or DateUnit.Week => value.ToString("MMM d", c),
            DateUnit.Month => value.ToString("MMM yyyy", c),
            _ => value.ToString("yyyy", c),
        };
    }

    // The first boundary of the step at or after `value`: truncate to the unit, then step forward
    // one unit at a time until the value is reached and the boundary is aligned. A step's count
    // divides its enclosing unit (15 minutes into an hour, 6 hours into a day), so alignment is a
    // remainder; at most `count` extra units are walked.
    //
    // ⚠️ A week is walked a DAY at a time. One unit of Week is seven days, so walking by it from a
    // Wednesday only ever visits Wednesdays and never finds the Monday it is aligning to.
    private static DateTime? FirstAtOrAfter(DateTime value, DateUnit unit, int count) {
        DateUnit walk = unit == DateUnit.Week ? DateUnit.Day : unit;
        DateTime? t = Truncate(value, unit);
        while (t is DateTime d && (d < value || !Aligned(d, unit, count))) t = Add(d, walk, 1);
        return t;
    }

    private static DateTime Truncate(DateTime v, DateUnit unit) => unit switch {
        DateUnit.Second => new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, v.Second, v.Kind),
        DateUnit.Minute => new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0, v.Kind),
        DateUnit.Hour => new DateTime(v.Year, v.Month, v.Day, v.Hour, 0, 0, v.Kind),
        DateUnit.Day or DateUnit.Week => v.Date,
        DateUnit.Month => new DateTime(v.Year, v.Month, 1, 0, 0, 0, v.Kind),
        _ => new DateTime(v.Year, 1, 1, 0, 0, 0, v.Kind),
    };

    private static bool Aligned(DateTime d, DateUnit unit, int count) => unit switch {
        DateUnit.Second => d.Second % count == 0,
        DateUnit.Minute => d.Minute % count == 0,
        DateUnit.Hour => d.Hour % count == 0,
        // Days since year 1, so a two-day step keeps the same parity across months and years.
        DateUnit.Day => (d.Ticks / TimeSpan.TicksPerDay) % count == 0,
        DateUnit.Week => d.DayOfWeek == DayOfWeek.Monday,
        DateUnit.Month => (d.Month - 1) % count == 0,
        _ => d.Year % count == 0,
    };

    // Null past DateTime.MaxValue, which ends the walk instead of throwing.
    private static DateTime? Add(DateTime d, DateUnit unit, int count) {
        try {
            return unit switch {
                DateUnit.Second => d.AddSeconds(count),
                DateUnit.Minute => d.AddMinutes(count),
                DateUnit.Hour => d.AddHours(count),
                DateUnit.Day => d.AddDays(count),
                DateUnit.Week => d.AddDays(7 * count),
                DateUnit.Month => d.AddMonths(count),
                _ => d.AddYears(count),
            };
        } catch (ArgumentOutOfRangeException) {
            return null;
        }
    }
}
