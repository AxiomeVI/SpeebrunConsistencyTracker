using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time
{
    public readonly struct TimeTicks : IComparable<TimeTicks>
    {
        public long Ticks { get; }

        public TimeTicks(long ticks)
        {
            Ticks = ticks;
        }

        public override string ToString()
        {
            TimeSpan ts = TimeSpan.FromTicks(Ticks);
            string sign = ts < TimeSpan.Zero ? "-" : "";
            return sign + ts.ToString(ts.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
        }

        public double Seconds => Ticks / (double)TimeSpan.TicksPerSecond;

        public static TimeTicks operator +(TimeTicks a, TimeTicks b) => new(a.Ticks + b.Ticks);
        public static TimeTicks operator -(TimeTicks a, TimeTicks b) => new(a.Ticks - b.Ticks);

        public static bool operator <(TimeTicks a, TimeTicks b) => a.Ticks < b.Ticks;
        public static bool operator >(TimeTicks a, TimeTicks b) => a.Ticks > b.Ticks;
        public static bool operator <=(TimeTicks a, TimeTicks b) => a.Ticks <= b.Ticks;
        public static bool operator >=(TimeTicks a, TimeTicks b) => a.Ticks >= b.Ticks;

        public int CompareTo(TimeTicks other) => Ticks.CompareTo(other.Ticks);

        public static readonly TimeTicks Zero = new TimeTicks(0);
    }
}
