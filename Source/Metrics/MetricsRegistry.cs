using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics
{
    public static class MetricRegistry
    {
        private static SpeebrunConsistencyTrackerModuleSettings _settings => SpeebrunConsistencyTrackerModule.Settings;

        public static readonly List<MetricDescriptor> AllMetrics =
        [
            new MetricDescriptor(
                "Consistency Score",
                "score",
                Metrics.ConsistencyScore,
                (mode) => MetricHelper.IsMetricEnabled(_settings.ConsistencyScore, mode)
            ),
            new MetricDescriptor(
                () => MetricHelper.IsMetricEnabled(_settings.TargetTime, MetricOutput.Export) ? $"Success Rate (≤{MetricEngine.GetTargetTimeTicks()})" : "Success Rate",
                () => MetricHelper.IsMetricEnabled(_settings.TargetTime, MetricOutput.Overlay) ? $"success (≤{MetricEngine.GetTargetTimeTicks()})" : "success",
                Metrics.SuccessRate,
                (mode) => MetricHelper.IsMetricEnabled(_settings.SuccessRate, mode)
            ),
            new MetricDescriptor(
                "Average",
                "avg",
                Metrics.Average,
                (mode) => MetricHelper.IsMetricEnabled(_settings.Average, mode)
            ),
            new MetricDescriptor(
                "Median",
                "med",
                Metrics.Median,
                (mode) => MetricHelper.IsMetricEnabled(_settings.Median, mode)
            ),
            new MetricDescriptor(
                "Median Absolute Deviation",
                "mad",
                Metrics.MedianAbsoluteDeviation,
                (mode) => MetricHelper.IsMetricEnabled(_settings.MedianAbsoluteDeviation, mode)
            ),
            new MetricDescriptor(
                "Best",
                "best",
                Metrics.Best,
                (mode) => MetricHelper.IsMetricEnabled(_settings.Minimum, mode)
            ),
            new MetricDescriptor(
                "Worst",
                "worst",
                Metrics.Worst,
                (mode) => MetricHelper.IsMetricEnabled(_settings.Maximum, mode)
            ),
            new MetricDescriptor(
                "StdDev",
                "std",
                Metrics.StdDev,
                (mode) => MetricHelper.IsMetricEnabled(_settings.StandardDeviation, mode)
            ),
            new MetricDescriptor(
                "Coef of Variation",
                "cv",
                Metrics.CoefVariation,
                (mode) => MetricHelper.IsMetricEnabled(_settings.CoefficientOfVariation, mode)
            ),
            new MetricDescriptor(
                () => $"{_settings.PercentileValue}",
                () => $"{_settings.PercentileValue}",
                Metrics.Percentile,
                (mode) => MetricHelper.IsMetricEnabled(_settings.Percentile, mode)
            ),
            new MetricDescriptor(
                "Interquartile Range",
                "iqr",
                Metrics.InterquartileRange,
                (mode) => MetricHelper.IsMetricEnabled(_settings.InterquartileRange, mode)
            ),
            new MetricDescriptor(
                "Completed Run Count",
                "completed",
                Metrics.CompletedRunCount,
                (mode) => MetricHelper.IsMetricEnabled(_settings.CompletedRunCount, mode)
            ),
            new MetricDescriptor(
                "Total Run Count",
                "total",
                Metrics.TotalRunCount,
                (mode) => MetricHelper.IsMetricEnabled(_settings.TotalRunCount, mode)
            ),
            new MetricDescriptor(
                "DNF Count",
                "dnf",
                Metrics.DnfCount,
                (mode) => MetricHelper.IsMetricEnabled(_settings.DnfCount, mode)
            ),
            new MetricDescriptor(
                "Reset Rate",
                "reset rate",
                Metrics.ResetRate,
                (mode) => MetricHelper.IsMetricEnabled(_settings.ResetRate, mode)
            ),
            new MetricDescriptor(
                "Reset Share",
                "reset share",
                Metrics.ResetShare,
                (mode) => MetricHelper.IsMetricEnabled(_settings.ResetShare, mode)
            ),
            new MetricDescriptor(
                "SoB",
                "sob",
                Metrics.SumOfBest,
                (mode) => MetricHelper.IsMetricEnabled(_settings.SoB, mode)
            ),
            new MetricDescriptor(
                "Trend Slope",
                "trend",
                Metrics.TrendSlope,
                (mode) => MetricHelper.IsMetricEnabled(_settings.LinearRegression, mode)
            )
        ];
    }
}
