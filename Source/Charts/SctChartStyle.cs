using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// The look every chart shares: CelesteCharts' own, with axis lines a pixel thicker and a grid
// twice as opaque. The colours of the data are the player's, read from
// ChartPalette where each chart paints.
internal static class SctChartStyle
{
    public static readonly ChartStyle Current = ChartStyle.Default with
    {
        AxisThickness = 2f,
        Grid          = Rgba.White.Times(0.16f),
        // The target and the median: a one-pixel line is lost on a bar.
        ReferenceThickness = 2f,
    };

    // A label one size down: bar values and bin edges.
    public static TextStyle Small(ChartStyle style) => style.Label with { Scale = style.Label.Scale * 0.75f };
}
