using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    // The three metric groups as On/Off rows for one bit, the Stats overlay page's or the Export
    // options page's. Returns what puts the rows back in step with the settings, for the bulk
    // buttons: setting Index does not call Change.
    private static Action AddMetricGroups(TextMenu page, MetricOutput bit)
    {
        PercentileChoice[] percentiles = Enum.GetValues<PercentileChoice>();
        List<(MetricToggles.Metric Metric, TextMenu.OnOff Row)> rows = [];
        TextMenu.Slider percentileValue = null;
        MetricToggles.Metric percentile = null;

        foreach (MetricToggles.Group group in MetricToggles.Groups)
        {
            page.Add(new TextMenu.SubHeader(Dialog.Clean(group.HeaderKey)));
            foreach (MetricToggles.Metric metric in group.Metrics)
            {
                TextMenu.OnOff row = new(Dialog.Clean(metric.LabelKey), MetricToggles.Get(_settings, metric, bit));
                row.Change(on =>
                {
                    MetricToggles.Set(_settings, metric, bit, on);
                    if (metric == percentile) percentileValue.Disabled = !on;
                    MetricEngine.InvalidateSettingsHash();
                });
                page.Add(row);
                rows.Add((metric, row));

                if (metric.LabelKey == DialogIds.SuccessRateId)
                    row.AddDescription(page, Dialog.Clean(DialogIds.SuccessRateSubTextId));

                // One setting, shown on both pages; each page greys it out by its own Percentile row.
                if (metric.LabelKey == DialogIds.PercentileId)
                {
                    percentile = metric;
                    percentileValue = new TextMenu.Slider(
                        Dialog.Clean(DialogIds.PercentileValueId),
                        i => Utility.EnumLabels.For(percentiles[i]),
                        0, percentiles.Length - 1,
                        Array.IndexOf(percentiles, _settings.PercentileValue))
                    {
                        Disabled = !MetricToggles.Get(_settings, metric, bit),
                    };
                    percentileValue.Change(v =>
                    {
                        _settings.PercentileValue = percentiles[v];
                        MetricEngine.InvalidateSettingsHash();
                    });
                    page.Add(percentileValue);
                }
            }
        }

        return () =>
        {
            foreach ((MetricToggles.Metric metric, TextMenu.OnOff row) in rows)
                row.Index = MetricToggles.Get(_settings, metric, bit) ? 1 : 0;
            percentileValue.Index    = Array.IndexOf(percentiles, _settings.PercentileValue);
            percentileValue.Disabled = !MetricToggles.Get(_settings, percentile, bit);
        };
    }

    // All on, all off and defaults for one bit, under their own subheader at the end of a page.
    private static void AddBulkButtons(TextMenu page, MetricOutput bit, string allOnId, string allOffId, Action refresh)
    {
        void Apply(Action<SpeebrunConsistencyTrackerModuleSettings> change)
        {
            change(_settings);
            refresh();
            _instance.SaveSettings();
            MetricEngine.InvalidateSettingsHash();
        }

        page.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.BulkHeaderId)));
        page.Add(new TextMenu.Button(Dialog.Clean(allOnId))
            .Pressed(() => Apply(s => MetricToggles.SetAll(s, bit, true))));
        page.Add(new TextMenu.Button(Dialog.Clean(allOffId))
            .Pressed(() => Apply(s => MetricToggles.SetAll(s, bit, false))));
        page.Add(new TextMenu.Button(Dialog.Clean(DialogIds.DefaultsId))
            .Pressed(() => Apply(s => MetricToggles.ApplyDefaults(s, bit))));
    }
}
