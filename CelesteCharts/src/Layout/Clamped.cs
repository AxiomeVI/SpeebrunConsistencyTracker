#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Clamping for bounds that come from geometry and can cross.</summary>
internal static class Clamped {
    /// <summary>
    ///     <paramref name="value"/> held between the bounds. When they cross, <paramref name="low"/> wins
    ///     and nothing throws, unlike <see cref="Math.Clamp(float, float, float)"/>.
    /// </summary>
    public static float Within(float value, float low, float high) => Math.Max(low, Math.Min(high, value));
}
