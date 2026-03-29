using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static TextMenuExt.SubMenu CreateGraphOverlaySubMenu(TextMenu menu)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.GraphOverlayId), false);

        ColorChoice[] enumColors = Enum.GetValues<ColorChoice>();

        TextMenu.Slider roomColor = new(
            Dialog.Clean(DialogIds.RoomColorId),
            i => enumColors[i].ToString(), 0, enumColors.Length - 1,
            Array.IndexOf(enumColors, _settings.RoomColor));

        TextMenu.Slider segmentColor = new(
            Dialog.Clean(DialogIds.SegmentColorId),
            i => enumColors[i].ToString(), 0, enumColors.Length - 1,
            Array.IndexOf(enumColors, _settings.SegmentColor));

        FormattedIntSlider graphOpacity = new(
            Dialog.Clean(DialogIds.ChartOpacityId),
            0, 100,
            _settings.ChartOpacity,
            v => (v / 100f).ToString("0.00"));

        FormattedIntSlider timeLossThreshold = new(
            Dialog.Clean(DialogIds.TimeLossThresholdId),
            1, 118,
            (int)Math.Round(_settings.TimeLossThresholdMs / 17.0),
            v => $"{v * 17}ms");

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
            _instance.graphManager?.ClearProblemChart();
        });
        graphOpacity.Change(v =>
        {
            _settings.ChartOpacity = v;
            _settings.RoomColorFinal    = ColorHelper.ToFinalColor(_settings.RoomColor,    v);
            _settings.SegmentColorFinal = ColorHelper.ToFinalColor(_settings.SegmentColor, v);
        });

        // Per-graph enable/disable toggles
        TextMenu.OnOff graphScatter = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphScatterId), _settings.GraphScatter)
            .Change(v => { _settings.GraphScatter = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphRoomHistogram = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphRoomHistogramId), _settings.GraphRoomHistogram)
            .Change(v => { _settings.GraphRoomHistogram = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphSegmentHistogram = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphSegmentHistogramId), _settings.GraphSegmentHistogram)
            .Change(v => { _settings.GraphSegmentHistogram = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphDnfPercent = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphDnfPercentId), _settings.GraphDnfPercent)
            .Change(v => { _settings.GraphDnfPercent = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphProblemRooms = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphProblemRoomsId), _settings.GraphProblemRooms)
            .Change(v => { _settings.GraphProblemRooms = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphInconsistentRooms = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphInconsistentRoomsId), _settings.GraphInconsistentRooms)
            .Change(v => { _settings.GraphInconsistentRooms = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphTimeLoss = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphTimeLossId), _settings.GraphTimeLoss)
            .Change(v => { _settings.GraphTimeLoss = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphRunTrajectory = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphRunTrajectoryId), _settings.GraphRunTrajectory)
            .Change(v => { _settings.GraphRunTrajectory = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphBoxPlot = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphBoxPlotId), _settings.GraphBoxPlot)
            .Change(v => { _settings.GraphBoxPlot = v; RebuildGraphSlots(); });

        sub.Add(roomColor);
        sub.Add(segmentColor);
        sub.Add(graphOpacity);
        sub.Add(timeLossThreshold);
        sub.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.GraphEnabledId), false));
        sub.Add(graphScatter);
        sub.Add(graphRoomHistogram);
        sub.Add(graphSegmentHistogram);
        sub.Add(graphDnfPercent);
        sub.Add(graphProblemRooms);
        sub.Add(graphInconsistentRooms);
        sub.Add(graphTimeLoss);
        sub.Add(graphRunTrajectory);
        sub.Add(graphBoxPlot);

        timeLossThreshold.AddDescription(sub, menu, Dialog.Clean(DialogIds.TimeLossThresholdDescId));

        sub.Visible = _settings.Enabled;
        return sub;
    }

    private static void RebuildGraphSlots()
    {
        if (_instance.graphManager == null) return;
        Level level = Engine.Scene as Level;
        _instance.graphManager.RebuildEnabledSlots(level);
    }
}
