using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using SctSettings = Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModuleSettings;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

// The 18 metrics the Stats overlay and Export options pages list, in page order. Each stays one
// persisted MetricOutputChoice, so a settings file does not change: Off=0, Overlay=1, Export=2 and
// Both=3 are the [Flags] MetricOutput sums, and each page reads and writes one bit of it.
// TargetTime is not here: it is the target line toggle under Charts.
internal static class MetricToggles
{
    internal record Metric(
        string LabelKey,
        Func<SctSettings, MetricOutputChoice> Get,
        Action<SctSettings, MetricOutputChoice> Set,
        MetricOutputChoice Default);

    internal record Group(string HeaderKey, Metric[] Metrics);

    // The defaults are the ones the settings declare; KeepsTodaysDefaults pins them together.
    internal static readonly Group[] Groups =
    [
        new(DialogIds.GroupRunsId,
        [
            new(DialogIds.SuccessRateId,       s => s.SuccessRate,       (s, v) => s.SuccessRate = v,       MetricOutputChoice.Both),
            new(DialogIds.CompletedRunCountId, s => s.CompletedRunCount, (s, v) => s.CompletedRunCount = v, MetricOutputChoice.Both),
            new(DialogIds.TotalRunCountId,     s => s.TotalRunCount,     (s, v) => s.TotalRunCount = v,     MetricOutputChoice.Both),
            new(DialogIds.DnfCountId,          s => s.DnfCount,          (s, v) => s.DnfCount = v,          MetricOutputChoice.Off),
            new(DialogIds.ResetRateId,         s => s.ResetRate,         (s, v) => s.ResetRate = v,         MetricOutputChoice.Export),
            new(DialogIds.GoldRateId,          s => s.GoldRate,          (s, v) => s.GoldRate = v,          MetricOutputChoice.Off),
        ]),
        new(DialogIds.GroupTimesId,
        [
            new(DialogIds.AverageId,    s => s.Average,    (s, v) => s.Average = v,    MetricOutputChoice.Both),
            new(DialogIds.MedianId,     s => s.Median,     (s, v) => s.Median = v,     MetricOutputChoice.Both),
            new(DialogIds.MinimumId,    s => s.Minimum,    (s, v) => s.Minimum = v,    MetricOutputChoice.Export),
            new(DialogIds.MaximumId,    s => s.Maximum,    (s, v) => s.Maximum = v,    MetricOutputChoice.Off),
            new(DialogIds.PercentileId, s => s.Percentile, (s, v) => s.Percentile = v, MetricOutputChoice.Off),
            new(DialogIds.SoBId,        s => s.SoB,        (s, v) => s.SoB = v,        MetricOutputChoice.Overlay),
        ]),
        new(DialogIds.GroupSpreadId,
        [
            new(DialogIds.StandardDeviationId,      s => s.StandardDeviation,       (s, v) => s.StandardDeviation = v,       MetricOutputChoice.Both),
            new(DialogIds.CoefficientOfVariationId, s => s.CoefficientOfVariation,  (s, v) => s.CoefficientOfVariation = v,  MetricOutputChoice.Off),
            new(DialogIds.MadId,                    s => s.MedianAbsoluteDeviation, (s, v) => s.MedianAbsoluteDeviation = v, MetricOutputChoice.Off),
            new(DialogIds.RelMadId,                 s => s.RelativeMAD,             (s, v) => s.RelativeMAD = v,             MetricOutputChoice.Off),
            new(DialogIds.InterquartileRangeId,     s => s.InterquartileRange,      (s, v) => s.InterquartileRange = v,      MetricOutputChoice.Off),
            new(DialogIds.LinearRegressionId,       s => s.LinearRegression,        (s, v) => s.LinearRegression = v,        MetricOutputChoice.Off),
        ]),
    ];

    internal static IEnumerable<Metric> All => Groups.SelectMany(g => g.Metrics);

    internal static bool Has(MetricOutputChoice choice, MetricOutput bit) => ((int)choice & (int)bit) != 0;

    internal static MetricOutputChoice With(MetricOutputChoice choice, MetricOutput bit, bool on) =>
        (MetricOutputChoice)(on ? (int)choice | (int)bit : (int)choice & ~(int)bit);

    internal static bool Get(SctSettings settings, Metric metric, MetricOutput bit) => Has(metric.Get(settings), bit);

    internal static void Set(SctSettings settings, Metric metric, MetricOutput bit, bool on) =>
        metric.Set(settings, With(metric.Get(settings), bit, on));

    internal static bool DefaultOf(Metric metric, MetricOutput bit) => Has(metric.Default, bit);

    // The bulk buttons. Each touches one bit, and the Export options page's also sets the sections.
    internal static void SetAll(SctSettings settings, MetricOutput bit, bool on)
    {
        foreach (Metric metric in All) Set(settings, metric, bit, on);
        if (bit == MetricOutput.Export)
            foreach (Section section in ExportSections) section.Set(settings, on);
    }

    internal static void ApplyDefaults(SctSettings settings, MetricOutput bit)
    {
        foreach (Metric metric in All) Set(settings, metric, bit, DefaultOf(metric, bit));
        settings.PercentileValue = DefaultPercentileValue;
        if (bit == MetricOutput.Export)
            foreach (Section section in ExportSections) section.Set(settings, section.Default);
    }

    internal const PercentileChoice DefaultPercentileValue = PercentileChoice.P90;

    // The five export-only sections, which the Export options page's bulk buttons also set.
    internal record Section(
        string LabelKey,
        Func<SctSettings, bool> Get,
        Action<SctSettings, bool> Set,
        bool Default);

    internal static readonly Section[] ExportSections =
    [
        new(DialogIds.RunHistoryId,     s => s.History,        (s, v) => s.History = v,        false),
        new(DialogIds.ResetShareId,     s => s.ResetShare,     (s, v) => s.ResetShare = v,     false),
        new(DialogIds.MultimodalTestId, s => s.MultimodalTest, (s, v) => s.MultimodalTest = v, false),
        new(DialogIds.RoomDependencyId, s => s.RoomDependency, (s, v) => s.RoomDependency = v, false),
        new(DialogIds.BestSplitId,      s => s.BestSplit,      (s, v) => s.BestSplit = v,      true),
    ];
}
