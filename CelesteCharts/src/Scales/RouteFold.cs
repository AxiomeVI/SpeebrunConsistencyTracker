#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A route with some groups folded: a folded checkpoint shows as one band instead of its rooms, so a long
///     chapter fits and the part being practised gets the width. <see cref="Shown"/> is what the
///     <see cref="RouteAxis"/> lays out; <see cref="ItemOf"/> maps each band back to the full route's room,
///     or -1 for a folded group's band, whose value (the checkpoint's total, its average) is the mod's.
/// </summary>
// Folding is layout; what a folded band reads is a statistic, so the mod computes it (the maths boundary).
// Built when a group is folded or opened, then kept.
internal sealed class RouteFold {
    private readonly int[] _itemOf, _groupOf;
    private readonly int[] _firstItem, _size, _firstShown;   // per group: its first item in the full route, its item count, its first shown band
    private readonly bool[] _folded;

    /// <param name="folded">Whether each group is folded, by its index in <paramref name="groups"/>; missing ones are open.</param>
    /// <param name="foldedItem">The one band's name for a folded group, such as "7 rooms".</param>
    public RouteFold(IReadOnlyList<RouteGroup> groups, IReadOnlyList<bool> folded, Func<RouteGroup, string> foldedItem) {
        List<RouteGroup> shown = new();
        List<int> itemOf = new(), groupOf = new();
        _firstItem = new int[groups.Count];
        _size = new int[groups.Count];
        _firstShown = new int[groups.Count];
        _folded = new bool[groups.Count];
        int first = 0;
        for (int g = 0; g < groups.Count; g++) {
            RouteGroup group = groups[g];
            _firstItem[g] = first;
            _size[g] = group.Items.Count;
            _firstShown[g] = itemOf.Count;
            _folded[g] = g < folded.Count && folded[g];
            if (_folded[g]) {
                shown.Add(new RouteGroup(group.Name, [foldedItem(group)]));
                itemOf.Add(-1);
                groupOf.Add(g);
            } else {
                shown.Add(group);
                for (int i = 0; i < group.Items.Count; i++) {
                    itemOf.Add(first + i);
                    groupOf.Add(g);
                }
            }
            first += group.Items.Count;
        }
        Shown = shown;
        _itemOf = itemOf.ToArray();
        _groupOf = groupOf.ToArray();
    }

    /// <summary>The groups as laid out: folded ones with a single item.</summary>
    public IReadOnlyList<RouteGroup> Shown { get; }

    /// <summary>How many bands the shown route has.</summary>
    public int Count => _itemOf.Length;

    /// <summary>Whether group <paramref name="group"/> is folded.</summary>
    public bool IsFolded(int group) => _folded[group];

    /// <summary>The full route's item at a shown band, or -1 when the band is a folded group.</summary>
    public int ItemOf(int shown) => _itemOf[shown];

    /// <summary>The group a shown band belongs to.</summary>
    public int GroupOf(int shown) => _groupOf[shown];

    /// <summary>The shown band of a full route's item: its own band, or its folded group's; -1 past the route.</summary>
    public int ShownOf(int item) {
        for (int g = _firstItem.Length - 1; g >= 0; g--) {
            if (item < _firstItem[g]) continue;
            if (item >= _firstItem[g] + _size[g]) return -1;
            return _folded[g] ? _firstShown[g] : _firstShown[g] + item - _firstItem[g];
        }
        return -1;
    }
}

/// <summary>
///     CCT's following window: the few rooms around the current one, out of a whole route, with the rooms
///     left out on each side counted, so a strip at each end can show how much lies beyond.
/// </summary>
internal readonly record struct FollowWindow(int First, int Count, int HiddenBefore, int HiddenAfter) {
    /// <summary>Up to <paramref name="size"/> items with <paramref name="current"/> in the middle, slid to stay inside the route at its ends.</summary>
    public static FollowWindow Around(int total, int current, int size) {
        if (total <= 0 || size <= 0) return new FollowWindow(0, 0, 0, Math.Max(0, total));
        int count = Math.Min(size, total);
        int first = Math.Clamp(Math.Clamp(current, 0, total - 1) - (count - 1) / 2, 0, total - count);
        return new FollowWindow(first, count, first, total - first - count);
    }

    /// <summary>Whether an item of the full route shows in the window.</summary>
    public bool Holds(int item) => item >= First && item < First + Count;

    /// <summary>
    ///     How wide the strip for <paramref name="hidden"/> items is: in proportion to the whole route, at most
    ///     <paramref name="room"/>, and at least 2 pixels while anything is hidden so it never vanishes.
    /// </summary>
    public static float StripWidth(int hidden, int total, float room) =>
        hidden <= 0 || total <= 0 ? 0f : MathF.Max(2f, MathF.Round(room * hidden / total));
}
