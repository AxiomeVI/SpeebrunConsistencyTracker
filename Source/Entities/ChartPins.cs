using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities;

// What the player pinned on one chart, by key. Held by the chart and not by GraphInteractivity, so
// the pins last as long as the chart does: through a pause, a toggle, a visit to another chart, and
// into the chart rebuilt from new data. ChartCache decides when a chart takes nothing from the one
// before: another session.
public sealed class ChartPins
{
    private readonly List<HoverInfo> _items = [];
    // The chart's Revision when the pins were last located on it; null until they have been.
    private int? _locatedAt;

    public IReadOnlyList<HoverInfo> Items => _items;
    public int Count => _items.Count;

    // A click on a mark: pinned, or unpinned if it was. A mark with no key is matched by its label.
    public void Toggle(HoverInfo hover)
    {
        int existing = hover.Key != null
            ? _items.FindIndex(p => p.Key == hover.Key)
            : _items.FindIndex(p => p.Label == hover.Label);
        if (existing >= 0) _items.RemoveAt(existing);
        else _items.Add(hover);
    }

    public void Clear() => _items.Clear();

    // Where each pin's mark is at the chart's `revision`. Answers whether the pins were located again.
    // Once located on this chart, a mark that does not show (scrolled or zoomed out) keeps its pin at
    // Mark -1, for when it shows again. Carried from the chart this one replaced, a pin that does
    // not resolve is dropped: its cell was deleted, or the new chart has no mark like it.
    public bool Locate(int revision, Func<string, HoverInfo> locate)
    {
        if (_locatedAt == revision) return false;
        bool sameChart = _locatedAt.HasValue;
        _locatedAt = revision;
        if (_items.Count == 0) return false;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            HoverInfo located = locate(_items[i].Key);
            if (located != null) _items[i] = located;
            else if (sameChart) _items[i] = _items[i] with { Mark = -1 };
            else _items.RemoveAt(i);
        }
        return true;
    }

    // The pins of the chart this one replaces. Their marks are that chart's: none is drawn here until
    // the pins are located on this chart.
    public void TakeFrom(ChartPins previous)
    {
        _items.Clear();
        foreach (HoverInfo pin in previous._items) _items.Add(pin with { Mark = -1 });
        _locatedAt = null;
    }
}
