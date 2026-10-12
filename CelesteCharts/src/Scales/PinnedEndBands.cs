#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A <see cref="HideableBands"/> that may scroll, plus one more band pinned after them at a fixed place: a total
///     column that must stay in view while the others scroll beside it. The scrolling bands come first in the index, the
///     pinned band is the last one.
/// </summary>
// Not read from the scrolling bands: they hold no band for the pinned one, only its place is given here.
internal sealed class PinnedEndBands : IBandLayout {
    private readonly int _scrollingCount;
    private readonly float _pinnedStart, _pinnedWidth, _pinnedBar, _left;

    /// <param name="scrolling">The scrolling bands; a band of its own for the pinned one, if it has one, is not read.</param>
    /// <param name="scrollingCount">How many of <paramref name="scrolling"/>'s bands are in this layout.</param>
    /// <param name="left">Where the window of the scrolling bands starts.</param>
    /// <param name="pinnedStart">Where the pinned band starts, which is where the window ends.</param>
    /// <param name="fill">The pinned bar's width as a share of the pinned band's.</param>
    public PinnedEndBands(HideableBands scrolling, int scrollingCount, float left, float pinnedStart, float pinnedWidth, float fill) {
        Scrolling = scrolling;
        _scrollingCount = scrollingCount;
        _left = left;
        _pinnedStart = pinnedStart;
        _pinnedWidth = pinnedWidth;
        _pinnedBar = pinnedWidth * fill;
    }

    public HideableBands Scrolling { get; }

    public int Count => _scrollingCount + 1;
    public float Width => Scrolling.Width;
    public float BarWidth => Scrolling.BarWidth;

    public float BandStart(int index) => index < _scrollingCount ? Scrolling.BandStart(index) : _pinnedStart;
    public float WidthOf(int index) => index < _scrollingCount ? Scrolling.WidthOf(index) : _pinnedWidth;
    public float BarWidthOf(int index) => index < _scrollingCount ? Scrolling.BarWidthOf(index) : _pinnedBar;
    public float BarStart(int index) =>
        index < _scrollingCount ? Scrolling.BarStart(index) : _pinnedStart + MathF.Floor((_pinnedWidth - _pinnedBar) / 2f);
    public float Centre(int index) => BandStart(index) + WidthOf(index) / 2f;

    public int IndexAt(float pixel) => IndexAt(pixel, 0f);

    /// <summary>
    ///     The band under a pixel on screen, the scrolling bands being shifted left by <paramref name="shift"/>: the pinned
    ///     band from its start, -1 left of the window and for a scrolling band cut off by it.
    /// </summary>
    public int IndexAt(float pixel, float shift) {
        if (pixel >= _pinnedStart) return pixel < _pinnedStart + _pinnedWidth ? _scrollingCount : -1;
        if (pixel < _left) return -1;
        int index = Scrolling.IndexAt(pixel + shift);
        return index < _scrollingCount ? index : -1;
    }
}
