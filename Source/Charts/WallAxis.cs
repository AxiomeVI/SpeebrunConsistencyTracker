using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// One axis shared by every room tile, so a narrow hill is a consistent room on every tile.
//
// The bin width is one figure on the shared axis. Each room cuts it into a whole, odd number of
// frames centred on its median frame: times are whole frames, so no frame falls on an edge and no
// bin holds more frames than its neighbours.
internal sealed class WallAxis
{
    // Times past the far-out fences (Tukey's 3 IQR, pooled over the rooms) fold into the end bin:
    // one disastrous run must not squash every tile.
    private const double FenceIqrs = 3.0;
    private const int MaxBins = 40;

    public WallScale Scale { get; }
    // The shared axis, in percent or in ticks away from the median.
    public double Min { get; }
    public double Max { get; }
    // The ends before padding: a folded time is counted where the last unfolded ones are.
    private readonly double _low, _high;
    private readonly long[] _medians;
    private readonly long[] _binTicks;

    private WallAxis(WallScale scale, double low, double high, double pad, long[] medians, long[] binTicks)
    {
        (Scale, _low, _high, Min, Max, _medians, _binTicks) = (scale, low, high, low - pad, high + pad, medians, binTicks);
    }

    public bool HasRoom(int room) => _medians[room] > 0;

    // Where a time of the room sits on the shared axis.
    public double At(int room, long ticks) => Scale == WallScale.Relative
        ? 100.0 * (ticks - _medians[room]) / _medians[room]
        : ticks - _medians[room];

    // The rooms' times, each ascending. Null for Fit, or when no room has a time.
    public static WallAxis Of(WallScale scale, IReadOnlyList<TimeTicks[]> rooms, long frame)
    {
        if (scale == WallScale.Fit) return null;
        long[] medians = [.. rooms.Select(times => times.Length == 0 ? 0L : RoundToFrame(MedianOf(times), frame))];
        List<double> pooled = [];
        List<int> counts = [];
        for (int r = 0; r < rooms.Count; r++)
        {
            if (medians[r] <= 0) continue;
            counts.Add(rooms[r].Length);
            foreach (TimeTicks t in rooms[r]) pooled.Add(Deviation(scale, t.Ticks, medians[r]));
        }
        if (pooled.Count == 0) return null;
        pooled.Sort();

        // Freedman-Diaconis on the pooled deviations, for a tile of the typical run count.
        counts.Sort();
        double typical = counts[counts.Count / 2];
        double q1 = Percentile(pooled, 0.25), q3 = Percentile(pooled, 0.75), iqr = q3 - q1;
        double medianOfMedians = medians.Where(m => m > 0).OrderBy(m => m).ElementAt(counts.Count / 2);
        double oneFrame = scale == WallScale.Relative ? 100.0 * frame / medianOfMedians : frame;
        double width = Math.Max(oneFrame, 2 * iqr * Math.Pow(typical, -1.0 / 3.0));

        // Each room fences its own outliers: pooled, a tight room's IQR would cut a loose room's
        // ordinary spread.
        double low = double.MaxValue, high = double.MinValue;
        for (int r = 0; r < rooms.Count; r++)
        {
            if (medians[r] <= 0) continue;
            List<double> own = [.. rooms[r].Select(t => Deviation(scale, t.Ticks, medians[r]))];
            double rq1 = Percentile(own, 0.25), rq3 = Percentile(own, 0.75);
            double reach = FenceIqrs * Math.Max(rq3 - rq1, width);
            low  = Math.Min(low, Math.Max(own[0], rq1 - reach));
            high = Math.Max(high, Math.Min(own[^1], rq3 + reach));
        }
        // Never narrower than a few bins: a perfect session is not one bar the width of the tile.
        double least = 4 * width;
        if (high - low < least)
        {
            double mid = (high + low) / 2;
            (low, high) = (mid - least / 2, mid + least / 2);
        }
        if ((high - low) / width > MaxBins) width = (high - low) / MaxBins;

        long[] binTicks = new long[rooms.Count];
        for (int r = 0; r < rooms.Count; r++)
        {
            if (medians[r] <= 0) continue;
            double ticks = scale == WallScale.Relative ? width / 100.0 * medians[r] : width;
            binTicks[r] = OddFrames(ticks, frame) * frame;
        }
        // Half a bin of room at each end, so the end bins are whole.
        return new WallAxis(scale, low, high, width / 2, medians, binTicks);
    }

    // The room's bins on the shared axis, left to right: where each starts and ends, and how many
    // times it holds. Times past either end of the axis are counted in the end bin and returned
    // apart, so the tile can say so.
    public (List<(double From, double To, int Count)> Bins, int Below, int Above) Bins(int room, TimeTicks[] sorted)
    {
        long median = _medians[room], bin = _binTicks[room];
        Dictionary<long, int> counts = [];
        int below = 0, above = 0;
        foreach (TimeTicks t in sorted)
        {
            long ticks = t.Ticks;
            double x = At(room, ticks);
            if (x < Min) { below++; ticks = TicksAt(room, _low); }
            else if (x > Max) { above++; ticks = TicksAt(room, _high); }
            long index = FloorDiv(ticks - median + bin / 2, bin);
            counts[index] = counts.GetValueOrDefault(index) + 1;
        }
        List<(double, double, int)> bins = [];
        foreach ((long index, int count) in counts.OrderBy(kv => kv.Key))
        {
            long lower = median + index * bin - bin / 2;
            bins.Add((Math.Max(Min, At(room, lower)), Math.Min(Max, At(room, lower + bin)), count));
        }
        return (bins, below, above);
    }

    private long TicksAt(int room, double x) => Scale == WallScale.Relative
        ? _medians[room] + (long)Math.Round(x / 100.0 * _medians[room])
        : _medians[room] + (long)Math.Round(x);

    private static double Deviation(WallScale scale, long ticks, long median) =>
        scale == WallScale.Relative ? 100.0 * (ticks - median) / median : ticks - median;

    private static long MedianOf(TimeTicks[] sorted)
    {
        int n = sorted.Length;
        return n % 2 == 1 ? sorted[n / 2].Ticks : (sorted[n / 2 - 1].Ticks + sorted[n / 2].Ticks) / 2;
    }

    private static long RoundToFrame(long ticks, long frame) => (long)Math.Round((double)ticks / frame) * frame;

    // At least one, and odd, so a bin centred on a frame holds whole frames on both sides.
    private static long OddFrames(double ticks, long frame)
    {
        long frames = Math.Max(1, (long)Math.Round(ticks / frame));
        return frames % 2 == 1 ? frames : frames + 1;
    }

    private static long FloorDiv(long a, long b) => a / b - (a % b < 0 ? 1 : 0);

    private static double Percentile(List<double> sorted, double p)
    {
        double at = p * (sorted.Count - 1);
        int i = (int)Math.Floor(at);
        return i + 1 < sorted.Count ? sorted[i] + (at - i) * (sorted[i + 1] - sorted[i]) : sorted[i];
    }
}
