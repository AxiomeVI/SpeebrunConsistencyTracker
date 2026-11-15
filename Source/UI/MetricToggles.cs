using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using SctSettings = Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModuleSettings;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

// The Stats overlay and Export options pages, read from MetricTable. Each metric with a switch on
// both pages stays one persisted MetricOutputChoice, so a settings file does not change: Off=0,
// Overlay=1, Export=2 and Both=3 are the [Flags] MetricOutput sums, and each page reads and writes
// one bit of it. TargetTime is not here: it is the target line toggle under Charts.
internal static class MetricToggles
{
    internal record Group(string HeaderKey, MetricDefinition[] Metrics);

    internal static readonly Group[] Groups =
    [
        GroupOf(DialogIds.GroupRunsId,   MetricGroup.Runs),
        GroupOf(DialogIds.GroupTimesId,  MetricGroup.Times),
        GroupOf(DialogIds.GroupSpreadId, MetricGroup.Spread),
    ];

    private static Group GroupOf(string headerKey, MetricGroup group) =>
        new(headerKey, [.. InMenuOrder(group)]);

    private static IEnumerable<MetricDefinition> InMenuOrder(MetricGroup group) =>
        MetricTable.All.Where(m => m.Group == group).OrderBy(m => m.MenuSlot);

    internal static IEnumerable<MetricDefinition> All => Groups.SelectMany(g => g.Metrics);

    internal static bool Has(MetricOutputChoice choice, MetricOutput bit) => ((int)choice & (int)bit) != 0;

    internal static MetricOutputChoice With(MetricOutputChoice choice, MetricOutput bit, bool on) =>
        (MetricOutputChoice)(on ? (int)choice | (int)bit : (int)choice & ~(int)bit);

    internal static bool Get(SctSettings settings, MetricDefinition metric, MetricOutput bit) => Has(metric.Get(settings), bit);

    internal static void Set(SctSettings settings, MetricDefinition metric, MetricOutput bit, bool on) =>
        metric.Set(settings, With(metric.Get(settings), bit, on));

    internal static bool DefaultOf(MetricDefinition metric, MetricOutput bit) => Has(metric.Default, bit);

    // The bulk buttons. Each touches one bit, and the Export options page's also sets the sections.
    internal static void SetAll(SctSettings settings, MetricOutput bit, bool on)
    {
        foreach (MetricDefinition metric in All) Set(settings, metric, bit, on);
        if (bit == MetricOutput.Export)
            foreach (Section section in ExportSections) section.Set(settings, on);
    }

    internal static void ApplyDefaults(SctSettings settings, MetricOutput bit)
    {
        foreach (MetricDefinition metric in All) Set(settings, metric, bit, DefaultOf(metric, bit));
        settings.PercentileValue = DefaultPercentileValue;
        if (bit == MetricOutput.Export)
            foreach (Section section in ExportSections) section.Set(settings, section.Default);
    }

    internal const PercentileChoice DefaultPercentileValue = PercentileChoice.P90;

    // The export page's extra sections: the run history, then the export-only metrics. Its bulk
    // buttons set them too.
    internal record Section(
        string LabelKey,
        Func<SctSettings, bool> Get,
        Action<SctSettings, bool> Set,
        bool Default);

    internal static readonly Section[] ExportSections =
    [
        new(DialogIds.RunHistoryId, s => s.History, (s, v) => s.History = v, false),
        .. InMenuOrder(MetricGroup.ExportOnly).Select(SectionOf),
    ];

    private static Section SectionOf(MetricDefinition metric) => new(
        metric.LabelKey,
        s => Get(s, metric, MetricOutput.Export),
        (s, on) => metric.Set(s, on ? MetricOutputChoice.Export : MetricOutputChoice.Off),
        DefaultOf(metric, MetricOutput.Export));
}
