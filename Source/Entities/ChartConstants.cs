using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    internal static class ChartConstants
    {
        internal static class Layout
        {
            internal const float ChartWidth   = 1820f;
            internal const float ChartHeight  = 900f;
            internal const float ChartMargin  = 80f;
            internal const float ChartMarginH = 90f;
            internal const float MaxBarWidth = 120f;
        }

        internal static class Screen
        {
            internal const int ScreenWidth  = 1920;
            internal const int ScreenHeight = 1080;
        }

        internal static class Time
        {
            internal const long OneFrameTicks = 170_000L;
        }

        internal static class Axis
        {
            internal const int   MaxTickMarks    = 13;
            internal const int   PercentTickCount = 10;
            internal const float YLabelMarginX   = 10f; // horizontal gap between axis line and Y labels
        }

        internal static class FontScale
        {
            internal const float Title           = 0.7f;
            internal const float AxisLabel       = 0.35f;
            internal const float AxisLabelMedium = 0.4f;
            internal const float AxisLabelSmall  = 0.3f;
            internal const float BarValueTiny    = 0.22f;
            internal const float HistogramYLabel = 0.5f;
        }

        internal static class Stroke
        {
            internal const float OutlineSize = 2f;
        }

        internal static class Colors
        {
            internal static readonly Color BackgroundColor = Color.Black * 0.8f;
            internal static readonly Color GridLineColor   = Color.Gray * 0.5f;
            internal static readonly Color BaselineColor   = Color.Gray * 0.6f;
        }

        internal static class XAxisLabel
        {
            internal const float BaseOffsetY      = 10f;
            internal const float StaggerOffsetY   = 20f;
            internal const int   StaggerThreshold = 25;
            internal const float TickEvenLabelY   = 10f;  // histogram X tick label even-index Y offset
            internal const float TickOddLabelY    = 30f;  // histogram X tick label odd-index Y offset
            internal const float TickEvenLineEndY = 5f;   // histogram tick mark end even
            internal const float TickOddLineEndY  = 25f;  // histogram tick mark end odd
            internal const float StatsOffsetY     = 58f;  // "Total: N" / "Attempts: N" label Y offset
        }

        internal static class BarLayout
        {
            internal const float GroupSpacingRatio       = 0.15f;
            internal const float BarSpacingRatio         = 0.05f;
            internal const float SingleBarSpacingRatio   = 0.1f;  // single-bar charts (histogram)
            internal const float WideBarThreshold        = 30f;   // DrawBarLabel font scale thresholds
            internal const float NarrowBarThreshold      = 15f;
            internal const float BarLabelOffsetY         = 3f;    // gap above bar top for value labels
            internal const float StackedLabelMinHeight   = 15f;   // PercentBarChart label visibility
            internal const float SingleBarLabelMinHeight = 20f;   // Histogram bar label visibility
        }

        internal static class Legend
        {
            internal const float LegendOffsetY     = 55f;
            internal const float LegendEntrySpacing = 40f;
            internal const float LegendBoxSize     = 12f;
            internal const float LegendBoxTextGap  = 5f;
        }

        internal static class Scatter
        {
            internal const float DotRadius             = 2f;
            internal const float JitterRatio           = 0.4f;
            internal const int   LabelTruncationLength = 10;
        }

        internal static class Trajectory
        {
            internal const int   TotalYTicks          = 12;
            internal const float BrightnessMin        = 0.1f;
            internal const float BrightnessMax        = 0.8f;
            internal const float RightLabelMarginX    = 10f; // x + w + this for right-side axis labels
            internal const float LabelMinSpacingExtra = 4f;  // nudge: minSpacing = labelHeight + this
        }
    }
}
