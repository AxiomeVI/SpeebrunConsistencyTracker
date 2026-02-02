using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time
{
    public readonly struct TimeTicks(long ticks) : IComparable<TimeTicks>
    {
        public long Ticks { get; } = ticks;

        public override string ToString()
        {
            TimeSpan ts = TimeSpan.FromTicks(Ticks);
            string sign = ts < TimeSpan.Zero ? "-" : "";
            return sign + ts.ToString(ts.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
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
