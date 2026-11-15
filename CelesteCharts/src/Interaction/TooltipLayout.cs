#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Tooltips placed next to what they name, kept inside an area and off each other: several pinned at once,
///     plus the hovered one.
/// </summary>
internal static class TooltipLayout {
    /// <summary>
    ///     Where a tooltip of <paramref name="width"/> by <paramref name="height"/> goes for a point at (x, y): centred
    ///     above it with a gap, below it when there is no room above, and moved sideways to stay inside
    ///     <paramref name="within"/>.
    /// </summary>
    public static Area At(float x, float y, float width, float height, Area within, float gap = 8f) {
        float top = y - gap - height;
        if (top < within.Y) top = y + gap;
        float left = Math.Clamp(x - width / 2f, within.X, Math.Max(within.X, within.Right - width));
        top = Math.Clamp(top, within.Y, Math.Max(within.Y, within.Bottom - height));
        return new Area(MathF.Round(left), MathF.Round(top), width, height);
    }

    /// <summary>
    ///     Where a tooltip goes for an item with a size of its own (a button): centred above its top edge with a
    ///     gap, below its bottom edge when there is no room above, and kept inside <paramref name="within"/>.
    /// </summary>
    public static Area Beside(Area item, float width, float height, Area within, float gap = 8f) {
        float top = item.Y - gap - height;
        if (top < within.Y) top = item.Bottom + gap;
        float left = Math.Clamp(item.X + item.Width / 2f - width / 2f, within.X, Math.Max(within.X, within.Right - width));
        top = Math.Clamp(top, within.Y, Math.Max(within.Y, within.Bottom - height));
        return new Area(MathF.Round(left), MathF.Round(top), width, height);
    }

    /// <summary>
    ///     Moves each area, in order, up past any placed before it that it overlaps — or down, when that would leave
    ///     <paramref name="within"/> — so no two cover each other. Areas are changed in place.
    /// </summary>
    public static void Separate(Area[] areas, Area within, float gap = 2f) {
        for (int i = 1; i < areas.Length; i++)
            for (int tries = 0; tries < areas.Length; tries++) {
                int hit = -1;
                for (int j = 0; j < i && hit < 0; j++)
                    if (Overlap(areas[i], areas[j])) hit = j;
                if (hit < 0) break;
                float up = areas[hit].Y - gap - areas[i].Height;
                areas[i] = areas[i] with { Y = up >= within.Y ? up : areas[hit].Bottom + gap };
            }
    }

    private static bool Overlap(Area a, Area b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}
