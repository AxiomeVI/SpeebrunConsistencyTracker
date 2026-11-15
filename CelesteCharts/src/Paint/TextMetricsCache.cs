#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     The text metrics of one font at one size, built once and handed out until the font or the size changes.
///     <see cref="Version"/> counts the changes: a chart remembers it when it builds, and rebuilds when it moved.
/// </summary>
// The font is compared by reference: the game swaps the font object when a language with another font face
// is chosen. Nothing is held but the last font, its size and its metrics.
internal sealed class TextMetricsCache {
    private object? _font;
    private float _size;
    private TextMetrics? _metrics;

    /// <summary>How many times the metrics were built: 0 before the first call, then one more per change of font or size.</summary>
    public int Version { get; private set; }

    /// <summary>
    ///     The metrics for <paramref name="font"/> at <paramref name="size"/>: the cached ones, or built by
    ///     <paramref name="build"/> when either changed. Pass a delegate kept in a field, so a call allocates nothing.
    /// </summary>
    public TextMetrics For(object font, float size, Func<TextMetrics> build) {
        if (_metrics != null && ReferenceEquals(font, _font) && size == _size) return _metrics;
        TextMetrics metrics = build();
        _font = font;
        _size = size;
        _metrics = metrics;
        Version++;
        return metrics;
    }
}
