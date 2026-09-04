using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    // Y-axis rendering shared by the percent-scaled bar charts (0-100%): PercentBarChartOverlay
    // and GroupedPercentOverlay. Static: neither method touches instance state, and no chart
    // needs both halves of what used to be BarChartBase.
    internal static class PercentAxis
    {
        // Gridlines every 10%. Call from a DrawGrid override.
        public static void DrawPercentGrid(float x, float y, float w, float h)
        {
            for (int i = 1; i <= ChartConstants.Axis.PercentTickCount; i++)
            {
                float pct  = i * 10f;
                float yPos = y + h - (pct / 100f * h);
                Draw.Line(new Vector2(x, yPos), new Vector2(x + w, yPos),
                          ChartConstants.Colors.GridLineColor, 1f);
            }
        }

        // Call from DrawLabels.
        public static void DrawPercentYAxisLabels(float x, float y, float w, float h)
        {
            for (int i = 0; i <= ChartConstants.Axis.PercentTickCount; i++)
            {
                float pct  = i * 10f;
                float yPos = y + h - (pct / 100f * h);
                string label = $"{pct:0}%";
                Vector2 labelSize = ActiveFont.Measure(label) * ChartConstants.FontScale.AxisLabel;

                ActiveFont.DrawOutline(
                    label,
                    new Vector2(x - labelSize.X - 10, yPos - labelSize.Y / 2),
                    new Vector2(0f, 0f),
                    Vector2.One * ChartConstants.FontScale.AxisLabel,
                    Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
        }
    }
}
