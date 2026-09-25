using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time
{
    public readonly struct TimeTicks(long ticks) : IComparable<TimeTicks>
    {
        public long Ticks { get; } = ticks;

        // Formatted from the magnitude, with the sign put back afterwards. TimeSpan's custom
        // format specifiers print each component's absolute value, so picking the short form on
        // the signed TotalSeconds made -75 s read "-15.000"; and the two-field form has nowhere
        // to put an hour, so an hour-long time read "0:00.000". Trajectory deltas and trend
        // slopes are routinely negative.
        public override string ToString()
        {
            TimeSpan abs = TimeSpan.FromTicks(Math.Abs(Ticks));
            string sign = Ticks < 0 ? "-" : "";
            if (abs.TotalHours >= 1)
                return $"{sign}{(int)abs.TotalHours}:{abs.Minutes:D2}:{abs.Seconds:D2}.{abs.Milliseconds:D3}";
            return sign + abs.ToString(abs.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
        }

        public static TimeTicks operator +(TimeTicks a, TimeTicks b) => new(a.Ticks + b.Ticks);
        public static TimeTicks operator -(TimeTicks a, TimeTicks b) => new(a.Ticks - b.Ticks);

        public static bool operator <(TimeTicks a, TimeTicks b) => a.Ticks < b.Ticks;
        public static bool operator >(TimeTicks a, TimeTicks b) => a.Ticks > b.Ticks;
        public static bool operator <=(TimeTicks a, TimeTicks b) => a.Ticks <= b.Ticks;
        public static bool operator >=(TimeTicks a, TimeTicks b) => a.Ticks >= b.Ticks;

        public int CompareTo(TimeTicks other) => Ticks.CompareTo(other.Ticks);

        public static readonly TimeTicks Zero = new(0);

        public static implicit operator double(TimeTicks t) => t.Ticks;
    }
}
