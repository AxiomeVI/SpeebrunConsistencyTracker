#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Bands in a row, one per column of a chart: <see cref="Bands"/> (evenly spaced) and
///     <see cref="RouteAxis"/> (rooms grouped under checkpoints, with gaps). Painters take either.
/// </summary>
// Painters take it as a generic constraint (Paint.Bars<T, TBands>), so a Bands struct is not boxed.
internal interface IBandLayout {
    int Count { get; }

    /// <summary>One band's width.</summary>
    float Width { get; }

    /// <summary>A bar's width inside its band.</summary>
    float BarWidth { get; }

    float BandStart(int index);
    float BarStart(int index);
    float Centre(int index);

    /// <summary>Band <paramref name="index"/>'s own width: <see cref="Width"/>, unless the layout lets bands differ (a hidden column's stub).</summary>
    float WidthOf(int index);

    /// <summary>
    ///     The bar's width in band <paramref name="index"/>. 0 means the band has no bar — a hidden column — and
    ///     every painter draws nothing there.
    /// </summary>
    float BarWidthOf(int index);

    /// <summary>Which band a pixel falls in, or -1 outside every band (a gap between groups included).</summary>
    int IndexAt(float pixel);
}

/// <summary>
///     Splits a stretch of pixels into equal bands, one per column of a bar chart, with a bar centred
///     in each. The same object answers which band a pixel falls in, so drawing and hit-testing cannot
///     disagree about where a column is.
/// </summary>
internal readonly struct Bands : IBandLayout {
    public int Count { get; }

    /// <summary>Where the first band starts, after any leftover padding.</summary>
    public float Start { get; }

    /// <summary>One band's width: a bar plus its share of the gap.</summary>
    public float Width { get; }

    /// <summary>A bar's width inside its band.</summary>
    public float BarWidth { get; }

    /// <summary>The distance from the first band's start to the last band's end. Longer than the length given when <c>minWidth</c> made the bands overflow.</summary>
    public float ContentLength => Count * Width;

    /// <summary>Whether bands and bars were snapped to whole pixels.</summary>
    public bool WholePixels { get; }

    /// <param name="count">How many bands. Zero gives an empty set that holds no pixel.</param>
    /// <param name="start">The stretch's first pixel.</param>
    /// <param name="length">The stretch's length in pixels.</param>
    /// <param name="fill">How much of a band its bar takes, between 0 and 1.</param>
    /// <param name="wholePixels">
    ///     Snap bands and bars to whole pixels, with the leftover split evenly before the first band and
    ///     after the last. At widget size a fractional bar is blurred on one edge and bars that should be
    ///     equal differ by a pixel; whole pixels keep them crisp and identical.
    /// </param>
    /// <param name="minWidth">
    ///     The narrowest a band gets. When <paramref name="length"/> would give less, bands take this and run past
    ///     <paramref name="length"/> (see <see cref="ContentLength"/>); a caller scrolls by shifting <paramref name="start"/>. 0 means none.
    /// </param>
    public Bands(int count, float start, float length, float fill = 0.7f, bool wholePixels = false, float minWidth = 0f) {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "a band count cannot be negative");
        if (!(fill > 0f && fill <= 1f)) throw new ArgumentOutOfRangeException(nameof(fill), fill, "fill must be in (0, 1]");
        Count = count;
        WholePixels = wholePixels;
        if (count == 0 || (length <= 0f && minWidth <= 0f)) {
            Start = start;
            Width = 0f;
            BarWidth = 0f;
            return;
        }

        float width = length / count;
        if (wholePixels) width = MathF.Floor(width);
        if (width < minWidth) width = wholePixels ? MathF.Ceiling(minWidth) : minWidth;
        float bar = width * fill;
        if (wholePixels) {
            // The fill of the SNAPPED band, so every bar is the same fraction of what is drawn. At least
            // one pixel, and at least one pixel of gap whenever the band is wide enough for one.
            bar = Math.Clamp(MathF.Floor(width * fill), 1f, Math.Max(1f, width - (width >= 2f ? 1f : 0f)));
            start = MathF.Round(start + Math.Max(0f, length - width * count) / 2f);
        }
        Start = start;
        Width = width;
        BarWidth = bar;
    }

    /// <summary>The first pixel of band <paramref name="index"/>.</summary>
    public float BandStart(int index) => Start + index * Width;

    /// <summary>The first pixel of the bar in band <paramref name="index"/>.</summary>
    // Rounded down in whole-pixel mode, so an odd leftover sits on the same side of every bar.
    public float BarStart(int index) {
        float offset = (Width - BarWidth) / 2f;
        return BandStart(index) + (WholePixels ? MathF.Floor(offset) : offset);
    }

    /// <summary>The middle of band <paramref name="index"/>, where its label centres.</summary>
    public float Centre(int index) => BandStart(index) + Width / 2f;

    public float WidthOf(int index) => Width;

    public float BarWidthOf(int index) => BarWidth;

    /// <summary>
    ///     Bands inside the bar of band <paramref name="index"/> of <paramref name="outer"/>: one per series
    ///     of a grouped bar chart. Hit-testing is the outer band, then the inner one.
    /// </summary>
    public static Bands Within<TBands>(TBands outer, int index, int count, float fill = 0.9f) where TBands : IBandLayout =>
        new(count, outer.BarStart(index), outer.BarWidthOf(index), fill);

    /// <summary>Which band a pixel falls in, or -1 outside every band.</summary>
    public int IndexAt(float pixel) {
        if (Count == 0 || Width <= 0f) return -1;
        float at = (pixel - Start) / Width;
        if (at < 0f || at >= Count) return -1;
        return (int) at;
    }
}
