#nullable enable

using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A row of tabs, each as wide as its label: switching what a window shows (histogram, box, reps in order).
///     Planned once for its labels; <see cref="TabAt"/> answers the mouse, <c>Paint.Tabs</c> draws it.
/// </summary>
// What each tab means, and rebuilding the chart when the choice changes, are the mod's.
internal sealed class TabStrip {
    private readonly Area[] _tabs;

    private TabStrip(IReadOnlyList<string> labels, Area[] tabs) {
        Labels = labels;
        _tabs = tabs;
    }

    public IReadOnlyList<string> Labels { get; }

    public int Count => _tabs.Length;

    /// <summary>Tab <paramref name="index"/>'s area.</summary>
    public Area Tab(int index) => _tabs[index];

    /// <summary>The whole strip.</summary>
    public Area Bounds => _tabs.Length == 0 ? default : new Area(_tabs[0].X, _tabs[0].Y, _tabs[^1].Right - _tabs[0].X, _tabs[0].Height);

    public float Height => _tabs.Length == 0 ? 0f : _tabs[0].Height;

    /// <summary>The tab under a point, or -1.</summary>
    public int TabAt(float x, float y) {
        for (int i = 0; i < _tabs.Length; i++)
            if (_tabs[i].Contains(x, y)) return i;
        return -1;
    }

    /// <summary>Tabs from (x, y) rightwards, in the label style, each padded by the label gap.</summary>
    public static TabStrip Plan(TextMetrics metrics, IReadOnlyList<string> labels, float x, float y, ChartStyle style) {
        float pad = style.LabelGap, height = System.MathF.Ceiling(metrics.HeightAt(style.Label.Scale) + 6f);
        Area[] tabs = new Area[labels.Count];
        for (int i = 0; i < labels.Count; i++) {
            float width = System.MathF.Ceiling(metrics.WidthAt(labels[i], style.Label.Scale) + 2 * pad);
            tabs[i] = new Area(x, y, width, height);
            x += width;
        }
        return new TabStrip(labels, tabs);
    }
}
