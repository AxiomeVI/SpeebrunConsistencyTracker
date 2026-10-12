#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Where a step line turns between two points.</summary>
internal enum StepMode {
    /// <summary>A value holds until the next point: cumulative counts, a success rate by time (ECDF).</summary>
    After,

    /// <summary>A value holds from the previous point up to its own.</summary>
    Before,

    /// <summary>The step is halfway between the two points.</summary>
    Middle,
}

/// <summary>
///     A line's points in pixels, from <see cref="Polyline.Place{TX,TY}"/>. Consecutive points are
///     joined, except across a break: a point whose index is in <see cref="Breaks"/> starts a new
///     stretch of line (a rep with no time, a session with no data).
/// </summary>
// Hover asks which point is nearest. Along a line over x (reps in order, time) that is a binary search
// on X; for points in any order (a scatter) it is a scan, which allocates nothing and is cheap at the
// few thousand points a HUD chart can show.
internal sealed class LinePoints {
    public LinePoints(float[] x, float[] y, int[]? breaks = null) {
        if (x.Length != y.Length) throw new ArgumentException("one y per x", nameof(y));
        X = x;
        Y = y;
        Breaks = breaks ?? [];
        for (int i = 1; i < Breaks.Length; i++)
            if (Breaks[i] <= Breaks[i - 1]) throw new ArgumentException("breaks must ascend", nameof(breaks));
        Ascending = true;
        for (int i = 1; i < x.Length && Ascending; i++) Ascending = x[i] >= x[i - 1];
    }

    public float[] X { get; }
    public float[] Y { get; }

    /// <summary>Ascending indices of the points that start a new stretch; point 0 always does.</summary>
    public int[] Breaks { get; }

    /// <summary>Whether X never decreases, which lets hover search it.</summary>
    public bool Ascending { get; }

    public int Count => X.Length;

    /// <summary>Whether point <paramref name="index"/> is joined to the one before it.</summary>
    public bool JoinsPrevious(int index) => index > 0 && Array.BinarySearch(Breaks, index) < 0;

    /// <summary>The point whose x is nearest <paramref name="pixel"/>, or -1 when there is none.</summary>
    public int NearestX(float pixel) {
        if (Count == 0) return -1;
        if (!Ascending) return Nearest(pixel, float.NaN);
        int after = FirstAbove(pixel);
        if (after == 0) return 0;
        if (after == Count) return Count - 1;
        return pixel - X[after - 1] <= X[after] - pixel ? after - 1 : after;
    }

    /// <summary>
    ///     The last point at or left of <paramref name="pixel"/>, or -1 when every point is right of it.
    ///     On a step line that holds its value after each point, this is the step under the pixel.
    ///     Needs <see cref="Ascending"/>.
    /// </summary>
    public int LastAtOrBefore(float pixel) {
        if (!Ascending) throw new InvalidOperationException("LastAtOrBefore needs points in ascending x");
        return FirstAbove(pixel) - 1;
    }

    /// <summary>
    ///     The point nearest (x, y), or -1 when none is within <paramref name="within"/> pixels. A NaN y
    ///     measures along x alone.
    /// </summary>
    public int Nearest(float x, float y, float within = float.PositiveInfinity) {
        int best = -1;
        float bestDistance = within * within;
        for (int i = 0; i < Count; i++) {
            float dx = X[i] - x, dy = float.IsNaN(y) ? 0f : Y[i] - y;
            float distance = dx * dx + dy * dy;
            // Strictly nearer, so the first of equals wins; the first point found may sit on the limit.
            if (distance < bestDistance || (best < 0 && distance <= bestDistance)) {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    ///     The line's height at <paramref name="pixel"/>, between the points either side of it; NaN where there is
    ///     no line: left of the first point, right of the last, or across a break. For hovering a line anywhere
    ///     along it, not only at its points. Needs <see cref="Ascending"/>.
    /// </summary>
    public float YAt(float pixel) {
        if (!Ascending) throw new InvalidOperationException("YAt needs points in ascending x");
        int after = FirstAbove(pixel);
        if (after == 0) return float.NaN;
        if (X[after - 1] == pixel) return Y[after - 1];
        if (after == Count || !JoinsPrevious(after)) return float.NaN;
        float t = (pixel - X[after - 1]) / (X[after] - X[after - 1]);
        return Y[after - 1] + t * (Y[after] - Y[after - 1]);
    }

    private int FirstAbove(float pixel) {
        int lo = 0, hi = Count;
        while (lo < hi) {
            int mid = (lo + hi) / 2;
            if (X[mid] <= pixel) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}

/// <summary>Lines through a mod's values: points placed on two scales, and step lines built from them.</summary>
internal static class Polyline {
    /// <summary>
    ///     Each (x, y) pair as a pixel. <paramref name="breaks"/> lists the indices that start a new
    ///     stretch of line; none means one unbroken line.
    /// </summary>
    public static LinePoints Place<TX, TY>(Scale<TX> x, Scale<TY> y, IReadOnlyList<TX> xs, IReadOnlyList<TY> ys,
                                           IReadOnlyList<int>? breaks = null) {
        if (xs.Count != ys.Count) throw new ArgumentException("one y per x", nameof(ys));
        float[] px = new float[xs.Count], py = new float[xs.Count];
        for (int i = 0; i < px.Length; i++) {
            px[i] = x.At(xs[i]);
            py[i] = y.At(ys[i]);
        }
        int[]? b = null;
        if (breaks != null) {
            b = new int[breaks.Count];
            for (int i = 0; i < b.Length; i++) b[i] = breaks[i];
        }
        return new LinePoints(px, py, b);
    }

    /// <summary>
    ///     Of several lines, the one passing nearest (x, y) measured straight up or down (<see cref="LinePoints.YAt"/>),
    ///     or -1 when none passes within <paramref name="within"/> pixels. The first of equals wins, so a mod lists
    ///     the lines it would rather pick first.
    /// </summary>
    public static int NearestLine(IReadOnlyList<LinePoints> lines, float x, float y, float within = float.PositiveInfinity) {
        int best = -1;
        float bestDistance = within;
        for (int i = 0; i < lines.Count; i++) {
            float at = lines[i].YAt(x);
            if (float.IsNaN(at)) continue;
            float distance = Math.Abs(at - y);
            if (distance < bestDistance || (best < 0 && distance <= bestDistance)) {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    ///     The step line through <paramref name="line"/>'s points: every segment horizontal or vertical,
    ///     turning where <paramref name="mode"/> says. With <paramref name="endX"/>, the last value is
    ///     held on to that pixel (the plot's right edge, for a cumulative line that has ended). Breaks
    ///     are kept.
    /// </summary>
    public static LinePoints Steps(LinePoints line, StepMode mode = StepMode.After, float? endX = null) {
        List<float> x = new(line.Count * 3), y = new(line.Count * 3);
        List<int> breaks = new();
        for (int i = 0; i < line.Count; i++) {
            if (i > 0 && line.JoinsPrevious(i)) {
                float px = line.X[i - 1], py = line.Y[i - 1], cx = line.X[i], cy = line.Y[i];
                switch (mode) {
                    case StepMode.After:
                        Add(cx, py);
                        break;
                    case StepMode.Before:
                        Add(px, cy);
                        break;
                    case StepMode.Middle:
                        float mid = (px + cx) / 2f;
                        Add(mid, py);
                        Add(mid, cy);
                        break;
                }
            } else if (i > 0) {
                breaks.Add(x.Count);
            }
            Add(line.X[i], line.Y[i]);
        }
        if (endX.HasValue && line.Count > 0) Add(endX.Value, line.Y[^1]);
        return new LinePoints([.. x], [.. y], [.. breaks]);

        void Add(float px, float py) {
            x.Add(px);
            y.Add(py);
        }
    }

    /// <summary>
    ///     A line cut at a level into the part on or above it on screen and the part below it, each with
    ///     a break where the line left that side. Both parts hold the points where the line crosses, so
    ///     drawn in two colours they meet with no gap.
    /// </summary>
    // Allocates: call it when the line is built, never per frame.
    public static (LinePoints Above, LinePoints Below) SplitAtY(LinePoints line, float y) {
        Stretches above = new(), below = new();
        for (int i = 0; i < line.Count; i++) {
            bool joined = line.JoinsPrevious(i);
            bool joinsNext = i + 1 < line.Count && line.JoinsPrevious(i + 1);
            if (!joined) {
                if (!joinsNext) (line.Y[i] > y ? below : above).Point(line.X[i], line.Y[i]);
                continue;
            }
            float x1 = line.X[i - 1], y1 = line.Y[i - 1], x2 = line.X[i], y2 = line.Y[i];
            bool down1 = y1 > y, down2 = y2 > y;
            if (down1 == down2) {
                (down1 ? below : above).Segment(x1, y1, x2, y2, startsStretch: !line.JoinsPrevious(i - 1));
                continue;
            }
            float cx = x1 + (y - y1) / (y2 - y1) * (x2 - x1);
            (down1 ? below : above).Segment(x1, y1, cx, y, startsStretch: !line.JoinsPrevious(i - 1));
            (down2 ? below : above).Segment(cx, y, x2, y2, startsStretch: false);
        }
        return (above.Build(), below.Build());
    }

    private sealed class Stretches {
        private readonly List<float> _x = [], _y = [];
        private readonly List<int> _breaks = [];

        // Joins the stretch before it when it starts where that one ended and the line had no break there.
        public void Segment(float x1, float y1, float x2, float y2, bool startsStretch) {
            if (x1 == x2 && y1 == y2) return;
            int n = _x.Count;
            if (n == 0 || startsStretch || _x[n - 1] != x1 || _y[n - 1] != y1) {
                if (n > 0) _breaks.Add(n);
                _x.Add(x1);
                _y.Add(y1);
            }
            _x.Add(x2);
            _y.Add(y2);
        }

        public void Point(float x, float y) {
            if (_x.Count > 0) _breaks.Add(_x.Count);
            _x.Add(x);
            _y.Add(y);
        }

        public LinePoints Build() => new([.. _x], [.. _y], [.. _breaks]);
    }
}
