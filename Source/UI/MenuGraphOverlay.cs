using System;
using System.Globalization;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static TextMenuExt.SubMenu CreateGraphOverlaySubMenu(TextMenu menu)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.GraphOverlayId), false);

        ColorChoice[] enumColors = Enum.GetValues<ColorChoice>();

        TextMenu.Slider roomColor = new(
            Dialog.Clean(DialogIds.RoomColorId),
            i => Utility.EnumLabels.For(enumColors[i]), 0, enumColors.Length - 1,
            Array.IndexOf(enumColors, _settings.RoomColor));

        TextMenu.Slider segmentColor = new(
            Dialog.Clean(DialogIds.SegmentColorId),
            i => Utility.EnumLabels.For(enumColors[i]), 0, enumColors.Length - 1,
            Array.IndexOf(enumColors, _settings.SegmentColor));

        FormattedIntSlider graphOpacity = new(
            Dialog.Clean(DialogIds.ChartOpacityId),
            0, 100,
            _settings.ChartOpacity,
            v => (v / 100f).ToString("0.00", CultureInfo.InvariantCulture));

        FormattedIntSlider timeLossThreshold = new(
            Dialog.Clean(DialogIds.TimeLossThresholdId),
            1, 118,
            (int)Math.Round(_settings.TimeLossThresholdMs / 17.0),
            v => $"{v}f / {v * 17}ms");

        roomColor.Change(v =>
        {
            _settings.RoomColor = enumColors[v];
            _settings.RoomColorFinal = ColorHelper.ToFinalColor(enumColors[v], _settings.ChartOpacity);
        });
        segmentColor.Change(v =>
        {
            _settings.SegmentColor = enumColors[v];
            _settings.SegmentColorFinal = ColorHelper.ToFinalColor(enumColors[v], _settings.ChartOpacity);
        });
        timeLossThreshold.Change(v =>
        {
            _settings.TimeLossThresholdMs = v * 17;
            GraphManager.ClearChart(GraphType.ProblemRooms);
        });
        graphOpacity.Change(v =>
        {
            _settings.ChartOpacity = v;
            _settings.RoomColorFinal           = ColorHelper.ToFinalColor(_settings.RoomColor, v);
            _settings.SegmentColorFinal        = ColorHelper.ToFinalColor(_settings.SegmentColor, v);
            _settings.PrimaryChartColorFinal   = _settings.PrimaryChartColor * (v / 100f);
            _settings.SecondaryChartColorFinal = _settings.SecondaryChartColor * (v / 100f);
        });

        TextMenu.OnOff targetLine = new(
            Dialog.Clean(DialogIds.ShowTargetLineId),
            Metrics.MetricHelper.IsMetricEnabled(_settings.TargetTime, Enums.MetricOutput.Overlay));
        targetLine.Change(v =>
        {
            _settings.TargetTime = v ? Enums.MetricOutputChoice.Overlay : Enums.MetricOutputChoice.Off;
            GraphManager.ClearChart(GraphType.Scatter);
        });

        sub.Add(roomColor);
        sub.Add(segmentColor);
        sub.Add(graphOpacity);
        sub.Add(timeLossThreshold);
        sub.Add(targetLine);
        sub.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.GraphEnabledId), false));

        // One toggle per row of GraphManager.ChartDefinitions, in table order — which is also the
        // order the Next/Previous keybinds cycle through. Turning a chart off clears its cache so
        // a later session never redraws a stale one.
        foreach (ChartDefinition chart in GraphManager.ChartDefinitions)
        {
            sub.Add(new TextMenu.OnOff(Dialog.Clean(chart.LabelKey), chart.Get(_settings))
                .Change(v =>
                {
                    chart.Set(_settings, v);
                    if (!v) GraphManager.ClearChart(chart.Type);
                    RebuildGraphSlots();
                }));
        }

        timeLossThreshold.AddDescription(sub, menu, Dialog.Clean(DialogIds.TimeLossThresholdDescId));

        sub.Visible = _settings.Enabled;
        return sub;
    }

    private static void RebuildGraphSlots()
    {
        if (!GraphManager.HasSession) return;
        GraphManager.RebuildEnabledSlots();
    }
}
