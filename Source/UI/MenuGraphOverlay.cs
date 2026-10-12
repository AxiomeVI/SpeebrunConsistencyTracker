using System;
using System.Globalization;
using System.Linq;
using Celeste.Mod.MenuTools;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static void OpenChartsPage(TextMenu menu)
    {
        TextMenuPage page = NewPage(menu, DialogIds.ChartsPageId);

        // The wheel's presets.
        (string name, Color color)[] presets = [.. Enum.GetValues<ColorChoice>()
            .Select(c => (EnumLabels.For(c), ColorHelper.ToColor(c)))];
        ChartPalette palette = ChartPalette.Current;

        void AddColor(string labelId, Color current, Action<string> save) =>
            page.Add(new ColorWheelButton(Dialog.Clean(labelId), current, presets: presets)
                .Change(c => save(ColorHelper.ToHex(c))));

        page.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.ColorsHeaderId)));
        AddColor(DialogIds.RoomColorId,      palette.Room,      hex => _settings.RoomColorHex = hex);
        AddColor(DialogIds.SegmentColorId,   palette.Segment,   hex => _settings.SegmentColorHex = hex);
        AddColor(DialogIds.PrimaryColorId,   palette.Primary,   hex => _settings.PrimaryChartColorHex = hex);
        AddColor(DialogIds.SecondaryColorId, palette.Secondary, hex => _settings.SecondaryChartColorHex = hex);
        AddColor(DialogIds.BestColorId,      palette.Best,      hex => _settings.TrajectoryBestColorHex = hex);
        AddColor(DialogIds.LastColorId,      palette.Last,      hex => _settings.TrajectoryLastColorHex = hex);
        AddColor(DialogIds.SobColorId,       palette.Sob,       hex => _settings.TrajectorySobColorHex = hex);

        FormattedIntSlider fillOpacity = new(
            Dialog.Clean(DialogIds.FillOpacityId),
            0, 100,
            _settings.ChartOpacity,
            v => (v / 100f).ToString("0.00", CultureInfo.InvariantCulture));
        fillOpacity.Change(v => _settings.ChartOpacity = v);
        page.Add(fillOpacity);
        fillOpacity.AddDescription(page, Dialog.Clean(DialogIds.FillOpacityDescId));

        page.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.ContentHeaderId)));

        TextMenu.OnOff targetLine = new(
            Dialog.Clean(DialogIds.ShowTargetLineId),
            Metrics.MetricHelper.IsMetricEnabled(_settings.TargetTime, MetricOutput.Overlay));
        targetLine.Change(v =>
        {
            _settings.TargetTime = v ? MetricOutputChoice.Overlay : MetricOutputChoice.Off;
            GraphManager.ClearChart(GraphType.Scatter);
        });
        page.Add(targetLine);

        FormattedIntSlider timeLossThreshold = new(
            Dialog.Clean(DialogIds.TimeLossThresholdId),
            Charts.BarsChart.MinThresholdFrames, Charts.BarsChart.MaxThresholdFrames,
            (int)Math.Round(_settings.TimeLossThresholdMs / (double)Charts.BarsChart.FrameMs),
            Charts.BarsChart.ThresholdText);
        timeLossThreshold.Change(v =>
        {
            _settings.TimeLossThresholdMs = v * Charts.BarsChart.FrameMs;
            GraphManager.ClearChart(GraphType.ProblemRooms);
        });
        page.Add(timeLossThreshold);
        timeLossThreshold.AddDescription(page, Dialog.Clean(DialogIds.TimeLossThresholdDescId));

        page.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.TrajectoryWidgetHeaderId)));

        TextMenu.OnOff widget = new(Dialog.Clean(DialogIds.TrajectoryWidgetId), _settings.TrajectoryWidget);
        widget.Change(v => _settings.TrajectoryWidget = v);
        page.Add(widget);
        widget.AddDescription(page, Dialog.Clean(DialogIds.TrajectoryWidgetDescId));

        FormattedIntSlider widgetOpacity = new(
            Dialog.Clean(DialogIds.TrajectoryWidgetOpacityId),
            20, 100,
            Math.Clamp(_settings.TrajectoryWidgetOpacity, 20, 100),
            v => (v / 100f).ToString("0.00", CultureInfo.InvariantCulture));
        widgetOpacity.Change(v => _settings.TrajectoryWidgetOpacity = v);
        page.Add(widgetOpacity);

        TextMenu.OnOff widgetDelta = new(Dialog.Clean(DialogIds.TrajectoryWidgetDeltaId), _settings.TrajectoryWidgetDelta);
        widgetDelta.Change(v => _settings.TrajectoryWidgetDelta = v);
        page.Add(widgetDelta);
        widgetDelta.AddDescription(page, Dialog.Clean(DialogIds.TrajectoryWidgetDeltaDescId));

        page.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.GraphEnabledId)));

        // One toggle per row of GraphManager.ChartDefinitions, in table order — which is also the
        // order the Next/Previous keybinds cycle through. Turning a chart off clears its cache so
        // a later session never redraws a stale one.
        foreach (ChartDefinition chart in GraphManager.ChartDefinitions)
        {
            page.Add(new TextMenu.OnOff(Dialog.Clean(chart.LabelKey), chart.Get(_settings))
                .Change(v =>
                {
                    chart.Set(_settings, v);
                    if (!v) GraphManager.ClearChart(chart.Type);
                    RebuildGraphSlots();
                }));
        }

        page.Enter();
    }

    private static void RebuildGraphSlots()
    {
        if (!GraphManager.HasSession) return;
        GraphManager.RebuildEnabledSlots();
    }
}
