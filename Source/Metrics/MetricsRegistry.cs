using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    public static class MetricRegistry
    {
        private static SpeebrunConsistencyTrackerModuleSettings.StatsSubMenu Settings => SpeebrunConsistencyTrackerModule.Settings.StatsMenu;

        public static readonly List<MetricDescriptor> AllMetrics = new()
        {
            new MetricDescriptor(
                () => MetricHelper.IsMetricEnabled(Settings.TargetTime, MetricOutput.Export) ? $"Success Rate (<={MetricEngine.GetTargetTimeTicks()})" : "Success Rate",
                () => MetricHelper.IsMetricEnabled(Settings.TargetTime, MetricOutput.Overlay) ? $"success (<={MetricEngine.GetTargetTimeTicks()})" : "success",
                (session, context, mode) => Metrics.SuccessRate(session, context),
                (mode) => MetricHelper.IsMetricEnabled(Settings.SuccessRate, mode)
            ),
            new MetricDescriptor(
                "Average",
                "avg",
                Metrics.Average,
                (mode) => MetricHelper.IsMetricEnabled(Settings.Average, mode)
            ),
            new MetricDescriptor(
                "Median",
                "med",
                Metrics.Median,
                (mode) => MetricHelper.IsMetricEnabled(Settings.Median, mode)
            ),
            new MetricDescriptor(
                "Best",
                "best",
                Metrics.Best,
                (mode) => MetricHelper.IsMetricEnabled(Settings.Minimum, mode)
            ),
            new MetricDescriptor(
                "Worst",
                "worst",
                Metrics.Worst,
                (mode) => MetricHelper.IsMetricEnabled(Settings.Maximum, mode)
            ),
            new MetricDescriptor(
                "StdDev",
                "std",
                Metrics.StdDev,
                (mode) => MetricHelper.IsMetricEnabled(Settings.StandardDeviation, mode)
            ),
            new MetricDescriptor(
                "Coef of Variation",
                "cv",
                Metrics.CoefVariation,
                (mode) => MetricHelper.IsMetricEnabled(Settings.CoefficientOfVariation, mode)
            ),
            new MetricDescriptor(
                () => $"{Settings.PercentileValue}",
                () => $"{Settings.PercentileValue}",
                Metrics.Percentile,
                (mode) => MetricHelper.IsMetricEnabled(Settings.Percentile, mode)
            ),
            new MetricDescriptor(
                "Completed Run Count",
                "completed",
                Metrics.CompletedRunCount,
                (mode) => MetricHelper.IsMetricEnabled(Settings.CompletedRunCount, mode)
            ),
            new MetricDescriptor(
                "Total Run Count",
                "total",
                Metrics.TotalRunCount,
                (mode) => MetricHelper.IsMetricEnabled(Settings.TotalRunCount, mode)
            ),
            new MetricDescriptor(
                "DNF Count",
                "dnf",
                Metrics.DnfCount,
                (mode) => MetricHelper.IsMetricEnabled(Settings.DnfCount, mode)
            ),
            new MetricDescriptor(
                "Reset Rate",
                "reset rate",
                Metrics.ResetRate,
                (mode) => MetricHelper.IsMetricEnabled(Settings.ResetRate, mode)
            ),
            new MetricDescriptor(
                "Reset Share",
                "reset share",
                Metrics.ResetShare,
                (mode) => MetricHelper.IsMetricEnabled(Settings.ResetShare, mode)
            ),
            new MetricDescriptor(
                "SoB",
                "sob",
                (session, context, mode) => Metrics.SumOfBest(session, context),
                (mode) => MetricHelper.IsMetricEnabled(Settings.SoB, mode)
            ),
            new MetricDescriptor(
                "Trend Slope",
                "trend",
                Metrics.TrendSlope,
                (mode) => MetricHelper.IsMetricEnabled(Settings.LinearRegression, mode)
            )
        };
    }
}
