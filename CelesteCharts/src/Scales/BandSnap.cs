#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>The whole-pixel rules every band layout shares, so equal bands snap the same way in each.</summary>
internal static class BandSnap {
    /// <summary>
    ///     The bar of a whole-pixel band <paramref name="width"/> wide: the floor of its fill, at least one pixel, and one
    ///     pixel of gap whenever the band is wide enough for one. 0 when the band is less than a pixel wide: it holds no bar.
    /// </summary>
    public static float Bar(float width, float fill) =>
        width < 1f ? 0f : Math.Clamp(MathF.Floor(width * fill), 1f, Math.Max(1f, width - (width >= 2f ? 1f : 0f)));

    /// <summary>Where a bar starts inside its band. <paramref name="floor"/> puts an odd leftover on the same side of every bar.</summary>
    public static float Offset(float width, float bar, bool floor) {
        float offset = (width - bar) / 2f;
        return floor ? MathF.Floor(offset) : offset;
    }

    /// <summary>The first band's start once the <paramref name="spare"/> pixels the bands left over are split before and after them.</summary>
    public static float Centred(float start, float spare) => MathF.Round(start + Math.Max(0f, spare) / 2f);
}
