#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>A named group of items along a route: a checkpoint's rooms, or a single summary column.</summary>
internal sealed record RouteGroup(string Name, IReadOnlyList<string> Items);

/// <summary>
///     Items in route order along one axis, grouped: rooms under their checkpoints, or SpeebrunConsistency-
///     Tracker's rooms then its whole-segment column. Every item gets a band of the same width, and groups
///     are set apart by a gap. Like <see cref="Bands"/>, one object answers where an item is and which item
///     a pixel is on.
/// </summary>
// Layout only: which rooms, in which order, under which names, is the mod's (from a segment, or from
// checkpoint sections), converted to plain groups of names at the boundary.
internal sealed class RouteAxis : IBandLayout {
    private readonly int[] _groupOf;
    private readonly int[] _firstOf;
    private readonly string[] _items;

    public RouteAxis(IReadOnlyList<RouteGroup> groups, float start, float length, float groupGap = 16f, float fill = 0.7f,
                     bool wholePixels = true) {
        if (!(fill > 0f && fill <= 1f)) throw new ArgumentOutOfRangeException(nameof(fill), fill, "fill must be in (0, 1]");
        Groups = groups;
        List<int> groupOf = new();
        List<string> items = new();
        _firstOf = new int[groups.Count];
        for (int g = 0; g < groups.Count; g++) {
            if (groups[g].Items.Count == 0) throw new ArgumentException($"group {g} ({groups[g].Name}) has no items", nameof(groups));
            _firstOf[g] = items.Count;
            foreach (string item in groups[g].Items) {
                items.Add(item);
                groupOf.Add(g);
            }
        }
        _groupOf = [.. groupOf];
        _items = [.. items];
        GroupGap = groups.Count > 1 ? groupGap : 0f;
        WholePixels = wholePixels;

        float gaps = GroupGap * Math.Max(0, groups.Count - 1);
        float width = Count == 0 ? 0f : Math.Max(0f, (length - gaps) / Count);
        float bar = width * fill;
        if (wholePixels && Count > 0) {
            GroupGap = MathF.Round(GroupGap);
            width = MathF.Floor(Math.Max(0f, (length - GroupGap * Math.Max(0, groups.Count - 1)) / Count));
            bar = Math.Clamp(MathF.Floor(width * fill), 1f, Math.Max(1f, width - (width >= 2f ? 1f : 0f)));
            start = MathF.Round(start + (length - width * Count - GroupGap * Math.Max(0, groups.Count - 1)) / 2f);
        }
        Start = start;
        Width = width;
        BarWidth = bar;
    }

    public IReadOnlyList<RouteGroup> Groups { get; }

    /// <summary>Every item, in route order.</summary>
    public int Count => _items.Length;

    public int GroupCount => Groups.Count;

    /// <summary>The pixels between two groups.</summary>
    public float GroupGap { get; }

    public float Start { get; }
    public float Width { get; }
    public float BarWidth { get; }
    public bool WholePixels { get; }

    /// <summary>Item <paramref name="index"/>'s name.</summary>
    public string Item(int index) => _items[index];

    /// <summary>Every item's name, in route order.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>The group item <paramref name="index"/> is in.</summary>
    public int GroupOf(int index) => _groupOf[index];

    /// <summary>From the first pixel of a group's first band to the last pixel of its last.</summary>
    public (float Start, float End) GroupSpan(int group) {
        int last = group + 1 < GroupCount ? _firstOf[group + 1] - 1 : Count - 1;
        return (BandStart(_firstOf[group]), BandStart(last) + Width);
    }

    public float BandStart(int index) => Start + index * Width + _groupOf[index] * GroupGap;

    public float BarStart(int index) {
        float offset = (Width - BarWidth) / 2f;
        return BandStart(index) + (WholePixels ? MathF.Floor(offset) : offset);
    }

    public float Centre(int index) => BandStart(index) + Width / 2f;

    public float WidthOf(int index) => Width;

    public float BarWidthOf(int index) => BarWidth;

    /// <summary>The group whose span holds a pixel, or -1 in a gap: a click on a checkpoint's name.</summary>
    public int GroupAt(float pixel) {
        for (int g = 0; g < GroupCount; g++) {
            (float start, float end) = GroupSpan(g);
            if (pixel >= start && pixel < end) return g;
        }
        return -1;
    }

    public int IndexAt(float pixel) {
        if (Count == 0 || Width <= 0f) return -1;
        for (int g = 0; g < GroupCount; g++) {
            (float start, float end) = GroupSpan(g);
            if (pixel < start || pixel >= end) continue;
            return Math.Min(_firstOf[g] + (int) ((pixel - start) / Width), g + 1 < GroupCount ? _firstOf[g + 1] - 1 : Count - 1);
        }
        return -1;
    }
}
