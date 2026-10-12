#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Bands the player can hide one by one: a hidden column shrinks to a narrow stub and gives its width to the
///     others, as SCT's charts do when a room's label is clicked. The stub still holds its pixels, so a click on
///     it brings the column back. A hidden band has no bar (<see cref="BarWidthOf"/> is 0), so every painter
///     leaves it empty and <c>Paint.HiddenStubs</c> marks it.
/// </summary>
// Built again when a column is hidden or shown: the mod keeps which are hidden (they are its settings or its
// session state) and rebuilds the chart with them.
internal sealed class HideableBands : IBandLayout {
    private readonly float[] _start;
    private readonly bool[] _hidden;

    /// <param name="hidden">Whether each band is hidden; missing ones are shown.</param>
    /// <param name="stub">A hidden band's width, in pixels.</param>
    /// <param name="minWidth">
    ///     The narrowest a shown band gets. When <paramref name="length"/> would give them less, they take this and the
    ///     layout runs past <paramref name="length"/>: <see cref="ContentLength"/> says by how much, and a caller scrolls by
    ///     passing a shifted <paramref name="start"/>. 0 means no minimum.
    /// </param>
    public HideableBands(int count, float start, float length, IReadOnlyList<bool> hidden, float stub = 6f, float fill = 0.7f,
                         bool wholePixels = true, float minWidth = 0f) {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "a band count cannot be negative");
        if (!(fill > 0f && fill <= 1f)) throw new ArgumentOutOfRangeException(nameof(fill), fill, "fill must be in (0, 1]");
        Count = count;
        Stub = stub;
        _hidden = new bool[count];
        int hiddenCount = 0;
        for (int i = 0; i < count; i++)
            if (_hidden[i] = i < hidden.Count && hidden[i]) hiddenCount++;
        int shown = count - hiddenCount;
        float width = shown == 0 ? 0f : Math.Max(0f, (length - hiddenCount * stub) / shown);
        if (wholePixels) width = MathF.Floor(width);
        if (shown > 0 && width < minWidth) width = wholePixels ? MathF.Ceiling(minWidth) : minWidth;
        float bar = width * fill;
        if (wholePixels && shown > 0) {
            bar = Math.Clamp(MathF.Floor(width * fill), 1f, Math.Max(1f, width - (width >= 2f ? 1f : 0f)));
            start = MathF.Round(start + Math.Max(0f, length - hiddenCount * stub - width * shown) / 2f);
        }
        Width = width;
        BarWidth = bar;
        _start = new float[count + 1];
        _start[0] = start;
        for (int i = 0; i < count; i++) _start[i + 1] = _start[i] + (_hidden[i] ? stub : width);
    }

    public int Count { get; }

    /// <summary>The distance from the first band's start to the last band's end. Longer than the length given when <c>minWidth</c> made the bands overflow.</summary>
    public float ContentLength => _start[Count] - _start[0];

    /// <summary>A shown band's width.</summary>
    public float Width { get; }

    /// <summary>A shown band's bar width.</summary>
    public float BarWidth { get; }

    /// <summary>A hidden band's width.</summary>
    public float Stub { get; }

    public bool IsHidden(int index) => _hidden[index];

    public float BandStart(int index) => _start[index];

    public float WidthOf(int index) => _hidden[index] ? Stub : Width;

    public float BarWidthOf(int index) => _hidden[index] ? 0f : BarWidth;

    public float BarStart(int index) => _hidden[index] ? _start[index] : _start[index] + MathF.Floor((Width - BarWidth) / 2f);

    public float Centre(int index) => _start[index] + WidthOf(index) / 2f;

    /// <summary>The band under a pixel, a stub included, or -1 outside them all.</summary>
    public int IndexAt(float pixel) {
        if (Count == 0 || pixel < _start[0] || pixel >= _start[Count]) return -1;
        int lo = 0, hi = Count - 1;
        while (lo < hi) {
            int mid = (lo + hi + 1) / 2;
            if (_start[mid] <= pixel) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>
    ///     The strip a click on a band's label zone hits: centred in the band, as wide as the band but at most
    ///     <paramref name="maxWidth"/>.
    /// </summary>
    public (float Start, float Width) StripOf(int index, float maxWidth) {
        float width = WidthOf(index);
        float strip = Math.Min(width, maxWidth);
        return (_start[index] + (width - strip) / 2f, strip);
    }

    /// <summary>
    ///     The band whose strip (<see cref="StripOf"/>) holds <paramref name="pixel"/>, or -1. A strip not wholly inside
    ///     [<paramref name="from"/>, <paramref name="to"/>] is not hit: a caller whose bands run past its plot passes the plot's edges.
    /// </summary>
    public int StripAt(float pixel, float maxWidth, float from = float.NegativeInfinity, float to = float.PositiveInfinity) {
        int index = IndexAt(pixel);
        if (index < 0) return -1;
        (float start, float width) = StripOf(index, maxWidth);
        if (pixel < start || pixel >= start + width || start < from || start + width > to) return -1;
        return index;
    }
}
