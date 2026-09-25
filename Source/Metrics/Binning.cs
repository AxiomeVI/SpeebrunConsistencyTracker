using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics;

public static class Binning
{
    public readonly record struct Bucket(long MinTick, long MaxTick, int Count);

    // Celeste times are whole frames, so a bin narrower or wider than a whole number of them lets
    // some bins structurally hold one more frame than their neighbours: the histogram combs, and
    // the shape is the binning's, not the data's. Widths here are whole frames, and the bin count
    // follows from the range rather than a fixed floor -- a one-frame spread used to be forced
    // into five bins of a third of a frame each, three of which could never hold anything.
    public static List<Bucket> FrameAligned(IReadOnlyList<long> sortedTicks, double preferredWidth, long frameTicks, int maxBins)
    {
        var buckets = new List<Bucket>();
        if (sortedTicks.Count == 0) return buckets;

        long min = sortedTicks[0];
        long max = sortedTicks[^1];
        long span = max - min + 1; // inclusive: a single frame of spread is one frame wide

        long width = WholeFrames(preferredWidth, frameTicks);
        int count = (int)Math.Ceiling(span / (double)width);
        if (count > maxBins)
        {
            width = WholeFrames(span / (double)maxBins, frameTicks);
            count = (int)Math.Ceiling(span / (double)width);
        }

        int[] counts = new int[count];
        foreach (long t in sortedTicks)
            counts[Math.Clamp((int)((t - min) / width), 0, count - 1)]++;

        for (int i = 0; i < count; i++)
        {
            long lower = min + i * width;
            // The last bucket reports the real maximum: the axis draws its right edge.
            long upper = i == count - 1 ? Math.Max(max, lower) : lower + width;
            buckets.Add(new Bucket(lower, upper, counts[i]));
        }
        return buckets;
    }

    private static long WholeFrames(double width, long frameTicks)
        => Math.Max(1L, (long)Math.Ceiling(width / frameTicks)) * frameTicks;
}
