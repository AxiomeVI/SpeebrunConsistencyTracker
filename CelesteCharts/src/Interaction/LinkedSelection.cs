#nullable enable

using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     What several charts point at together: hover a room in one chart and the same room lights up in the
///     others; click it and it stays picked in all of them. The charts share one object; a key is whatever the
///     mod says it is (a room's index, a session's, a cell's).
/// </summary>
// No chart owns it and none needs to know the others: each tells it what the mouse is on (Hover), each
// reads what to highlight (Hovered, IsSelected) and rebuilds its highlight when Version changes. The
// hovering chart is named (HoveredBy), so it alone draws a tooltip and the others only light the key.
//
// One mod's charts share one; it is not shared across mods (keys mean what one mod says). A chart that stops
// being drawn while hovering must say so (Hover(id, -1)), or its key stays lit. Pure; allocates only when
// the first key is picked.
internal sealed class LinkedSelection {
    private readonly HashSet<int> _selected = new();

    /// <summary>The key the mouse is on in some chart, or -1.</summary>
    public int Hovered { get; private set; } = -1;

    /// <summary>The chart that reported <see cref="Hovered"/>, or 0.</summary>
    public int HoveredBy { get; private set; }

    /// <summary>Changes whenever the hovered or the picked keys change: a chart rebuilds its highlight when it does.</summary>
    public long Version { get; private set; }

    public int SelectedCount => _selected.Count;

    /// <summary>
    ///     What chart <paramref name="chart"/> (not 0) has under the mouse: a key, or -1 for nothing. Nothing
    ///     clears the hover only when this chart was the one hovering, so charts reporting in any order agree.
    /// </summary>
    public void Hover(int chart, int key) {
        if (key < 0) {
            if (HoveredBy != chart || Hovered < 0) return;
            (Hovered, HoveredBy) = (-1, 0);
            Version++;
            return;
        }
        if (Hovered == key && HoveredBy == chart) return;
        (Hovered, HoveredBy) = (key, chart);
        Version++;
    }

    public bool IsSelected(int key) => _selected.Contains(key);

    /// <summary>Picks a key, or unpicks it when it was picked.</summary>
    public void Toggle(int key) {
        if (!_selected.Remove(key)) _selected.Add(key);
        Version++;
    }

    /// <summary>Picks only this key.</summary>
    public void Select(int key) {
        if (_selected.Count == 1 && _selected.Contains(key)) return;
        _selected.Clear();
        _selected.Add(key);
        Version++;
    }

    public void Clear() {
        if (_selected.Count == 0) return;
        _selected.Clear();
        Version++;
    }
}
