#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     What a chart's mouse is on, and what the player has pinned: SCT's hover and pins in one object. The chart
///     finds the key under the mouse with its own geometry (a bar, a dot, a cell) and hands it in every frame; a
///     click on it pins it (its tooltip stays), a click on a pinned one unpins it. <see cref="Changed"/> says when
///     to rebuild the highlight sketch, so hovering allocates nothing between changes.
/// </summary>
// A key is the mod's own number for a thing (a run, a room, a room's run as room * 1000 + run). Pins keep the
// order they were made in; past Max the oldest goes. What a pin means beyond staying lit (SCT's "delete these
// runs") is the mod's.
internal sealed class HoverPins {
    private readonly List<long> _pinned = new();

    // A pin changed outside Update (a button's Clear, a Toggle from code): the next Update must still say so,
    // or a page that clears and then hands the hover in redraws nothing until the mouse moves.
    private bool _unseen;

    /// <summary>The key under the mouse this frame, or -1.</summary>
    public long Hovered { get; private set; } = -1;

    /// <summary>Pinned keys, oldest first.</summary>
    public IReadOnlyList<long> Pinned => _pinned;

    /// <summary>The most pins kept; a new pin past it drops the oldest.</summary>
    public int Max { get; set; } = 8;

    /// <summary>Whether the hover or the pins changed this frame: rebuild the highlight.</summary>
    public bool Changed { get; private set; }

    /// <summary>Changes whenever the pins do, for anything built from them (a "delete runs" button).</summary>
    public long Version { get; private set; }

    public bool IsPinned(long key) => _pinned.Contains(key);

    /// <param name="hovered">The key under the mouse, or -1 for nothing.</param>
    /// <param name="clicked">Whether the mouse was clicked this frame on the chart (not on a button over it).</param>
    public void Update(long hovered, bool clicked) {
        Changed = _unseen || hovered != Hovered;
        _unseen = false;
        Hovered = hovered;
        if (!clicked || hovered < 0) return;
        Toggle(hovered);
        _unseen = false;   // this Update already says it
    }

    public void Toggle(long key) {
        if (!_pinned.Remove(key)) {
            _pinned.Add(key);
            if (_pinned.Count > Math.Max(1, Max)) _pinned.RemoveAt(0);
        }
        Changed = _unseen = true;
        Version++;
    }

    public void Clear() {
        if (_pinned.Count == 0) return;
        _pinned.Clear();
        Changed = _unseen = true;
        Version++;
    }

    /// <summary>Forgets the hover too: the chart was rebuilt, and what was under the mouse may not exist.</summary>
    public void Reset() {
        Hovered = -1;
        Changed = _unseen = true;
    }
}

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
