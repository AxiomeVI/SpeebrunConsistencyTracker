using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using SctSettings = Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModuleSettings;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Metrics;

// Where a metric's switch sits in Mod Options. Runs, Times and Spread are the subheaders of both
// the Stats overlay and Export options pages; ExportOnly rows are Export options' extra sections.
public enum MetricGroup { Runs, Times, Spread, ExportOnly }

public sealed class MetricDefinition
{
    public string LabelKey { get; init; }
    public Func<SctSettings, string> CsvHeader { get; init; }
    // Null for an export-only metric.
    public Func<SctSettings, string> OverlayName { get; init; }
    public Func<PracticeSession, MetricContext, bool, MetricResult> Compute { get; init; }
    public Func<SctSettings, MetricOutputChoice> Get { get; init; }
    public Action<SctSettings, MetricOutputChoice> Set { get; init; }
    // What the setting itself initialises to; KeepsTodaysDefaults pins the two together.
    public MetricOutputChoice Default { get; init; }
    public MetricGroup Group { get; init; }
    // The row's place under its subheader, which is not its CSV column.
    public int MenuSlot { get; init; }
    // Computed only while a target time is set.
    public bool NeedsTarget { get; init; }

    public bool ExportOnly => Group == MetricGroup.ExportOnly;

    public bool IsEnabled(SctSettings settings, MetricOutput mode) =>
        (!NeedsTarget || MetricEngine.TargetTimeTicksFor(settings) > 0)
        && MetricHelper.IsMetricEnabled(Get(settings), mode);
}

// Every metric, once. A new metric is a row here, its function in Metrics.cs, its label in
// Dialog/English.txt, a line in docs/metrics.md, and the settings property its row reads: Everest
// persists a property, so the switch cannot live in the row.
//
// The rows are in CSV column and overlay line order. A settings property's name is persisted:
// renaming one resets it in every player's settings file.
public static class MetricTable
{
    public static readonly MetricDefinition[] All =
    [
        Shown(   DialogIds.SuccessRateId,        "Success Rate",              "success",   Metrics.SuccessRate,             s => s.SuccessRate,             (s, v) => s.SuccessRate = v,             MetricOutputChoice.Both,    MetricGroup.Runs,   0, needsTarget: true),
        Shown(   "SCT_AVERAGE",                  "Average",                   "avg",       Metrics.Average,                 s => s.Average,                 (s, v) => s.Average = v,                 MetricOutputChoice.Both,    MetricGroup.Times,  0),
        Shown(   "SCT_MEDIAN",                   "Median",                    "med",       Metrics.Median,                  s => s.Median,                  (s, v) => s.Median = v,                  MetricOutputChoice.Both,    MetricGroup.Times,  1),
        Shown(   "SCT_MINIMUM",                  "Best",                      "best",      Metrics.Best,                    s => s.Minimum,                 (s, v) => s.Minimum = v,                 MetricOutputChoice.Export,  MetricGroup.Times,  2),
        Exported("SCT_BEST_SPLIT",               "Best Split",                             Metrics.BestSplit,               s => s.BestSplit,               (s, v) => s.BestSplit = v,               true,                                           3),
        Shown(   "SCT_SOB",                      "Session SoB",               "sob",       Metrics.SumOfBest,               s => s.SoB,                     (s, v) => s.SoB = v,                     MetricOutputChoice.Overlay, MetricGroup.Times,  5),
        Shown(   "SCT_MAXIMUM",                  "Worst",                     "worst",     Metrics.Worst,                   s => s.Maximum,                 (s, v) => s.Maximum = v,                 MetricOutputChoice.Off,     MetricGroup.Times,  3),
        Shown(   "SCT_STANDARD_DEVIATION",       "Standard Deviation",        "std",       Metrics.StdDev,                  s => s.StandardDeviation,       (s, v) => s.StandardDeviation = v,       MetricOutputChoice.Both,    MetricGroup.Spread, 0),
        Shown(   "SCT_COEFFICIENT_OF_VARIATION", "Relative StdDev",           "RelSD",     Metrics.CoefVariation,           s => s.CoefficientOfVariation,  (s, v) => s.CoefficientOfVariation = v,  MetricOutputChoice.Off,     MetricGroup.Spread, 1),
        Shown(   "SCT_MAD",                      "Median Absolute Deviation", "mad",       Metrics.MedianAbsoluteDeviation, s => s.MedianAbsoluteDeviation, (s, v) => s.MedianAbsoluteDeviation = v, MetricOutputChoice.Off,     MetricGroup.Spread, 2),
        Shown(   "SCT_RELATIVE_MAD",             "Relative MAD",              "RelMAD",    Metrics.RelativeMAD,             s => s.RelativeMAD,             (s, v) => s.RelativeMAD = v,             MetricOutputChoice.Off,     MetricGroup.Spread, 3),
        // Named after the percentile it reads, "P90" by default.
        new()
        {
            LabelKey = DialogIds.PercentileId, CsvHeader = s => $"{s.PercentileValue}", OverlayName = s => $"{s.PercentileValue}",
            Compute = Metrics.Percentile, Get = s => s.Percentile, Set = (s, v) => s.Percentile = v,
            Default = MetricOutputChoice.Off, Group = MetricGroup.Times, MenuSlot = 4,
        },
        Shown(   "SCT_IQR",                      "Interquartile Range",       "iqr",       Metrics.InterquartileRange,      s => s.InterquartileRange,      (s, v) => s.InterquartileRange = v,      MetricOutputChoice.Off,     MetricGroup.Spread, 4),
        Shown(   "SCT_TOTAL_RUN_COUNT",          "Total Run Count",           "total",     Metrics.TotalRunCount,           s => s.TotalRunCount,           (s, v) => s.TotalRunCount = v,           MetricOutputChoice.Both,    MetricGroup.Runs,   2),
        Shown(   "SCT_COMPLETED_RUN_COUNT",      "Completed Run Count",       "completed", Metrics.CompletedRunCount,       s => s.CompletedRunCount,       (s, v) => s.CompletedRunCount = v,       MetricOutputChoice.Both,    MetricGroup.Runs,   1),
        Shown(   "SCT_DNF_COUNT",                "Reset Count",               "reset",     Metrics.DnfCount,                s => s.DnfCount,                (s, v) => s.DnfCount = v,                MetricOutputChoice.Off,     MetricGroup.Runs,   3),
        Shown(   "SCT_RESET_RATE",               "Reset Rate",                "reset%",    Metrics.ResetRate,               s => s.ResetRate,               (s, v) => s.ResetRate = v,               MetricOutputChoice.Export,  MetricGroup.Runs,   4),
        Exported("SCT_RESET_SHARE",              "Reset Share",                            Metrics.ResetShare,              s => s.ResetShare,              (s, v) => s.ResetShare = v,              false,                                          0),
        Shown(   "SCT_GOLD_RATE",                "Gold Rate",                 "gold%",     Metrics.GoldRate,                s => s.GoldRate,                (s, v) => s.GoldRate = v,                MetricOutputChoice.Off,     MetricGroup.Runs,   5),
        Shown(   "SCT_LINEAR_REGRESSION",        "Trend Slope",               "trend",     Metrics.TrendSlope,              s => s.LinearRegression,        (s, v) => s.LinearRegression = v,        MetricOutputChoice.Off,     MetricGroup.Spread, 5),
        Exported("SCT_ROOM_DEPENDENCY",          "Room Dependency",                        Metrics.RoomDependency,          s => s.RoomDependency,          (s, v) => s.RoomDependency = v,          false,                                          2),
        Exported("SCT_MULTIMODAL_TEST",          "Multimodal Test",                        Metrics.MultimodalTest,          s => s.MultimodalTest,          (s, v) => s.MultimodalTest = v,          false,                                          1),
    ];

    // A metric with a switch on both pages, persisted as a MetricOutputChoice.
    private static MetricDefinition Shown(
        string labelKey, string csvHeader, string overlayName,
        Func<PracticeSession, MetricContext, bool, MetricResult> compute,
        Func<SctSettings, MetricOutputChoice> get, Action<SctSettings, MetricOutputChoice> set,
        MetricOutputChoice defaultChoice, MetricGroup group, int menuSlot, bool needsTarget = false) => new()
    {
        LabelKey = labelKey, CsvHeader = _ => csvHeader, OverlayName = _ => overlayName, Compute = compute,
        Get = get, Set = set, Default = defaultChoice, Group = group, MenuSlot = menuSlot, NeedsTarget = needsTarget,
    };

    // A metric only the CSV holds, persisted as a bool: true reads as Export, false as Off.
    private static MetricDefinition Exported(
        string labelKey, string csvHeader,
        Func<PracticeSession, MetricContext, bool, MetricResult> compute,
        Func<SctSettings, bool> get, Action<SctSettings, bool> set,
        bool defaultOn, int menuSlot) => new()
    {
        LabelKey = labelKey, CsvHeader = _ => csvHeader, Compute = compute,
        Get = s => get(s) ? MetricOutputChoice.Export : MetricOutputChoice.Off,
        Set = (s, v) => set(s, MetricHelper.IsMetricEnabled(v, MetricOutput.Export)),
        Default = defaultOn ? MetricOutputChoice.Export : MetricOutputChoice.Off,
        Group = MetricGroup.ExportOnly, MenuSlot = menuSlot,
    };
}
