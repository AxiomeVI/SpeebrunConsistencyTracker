using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static TextMenuExt.SubMenu CreateMetricsSubMenu(TextMenu menu)
    {
        PercentileChoice[] enumPercentileValues = Enum.GetValues<PercentileChoice>();

        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.StatsSubMenuId), false);

        // Boolean options
        TextMenu.OnOff history        = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RunHistoryId),    _settings.History).Change(b => _settings.History = b);
        TextMenu.OnOff resetShare     = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.ResetShareId),    _settings.ResetShare).Change(b => _settings.ResetShare = b);
        TextMenu.OnOff multimodalTest = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.MultimodalTestId),_settings.MultimodalTest).Change(b => _settings.MultimodalTest = b);
        TextMenu.OnOff roomDependency = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RoomDependencyId),_settings.RoomDependency).Change(b => _settings.RoomDependency = b);

        // Percentile value (special: depends on Percentile slider)
        TextMenu.Slider percentileValue = new(
            Dialog.Clean(DialogIds.PercentileValueId),
            i => enumPercentileValues[i].ToString(),
            0, enumPercentileValues.Length - 1,
            Array.IndexOf(enumPercentileValues, _settings.PercentileValue))
        {
            Disabled = _settings.Percentile == MetricOutputChoice.Off
        };
        percentileValue.Change(v => _settings.PercentileValue = enumPercentileValues[v]);

        // Build all metric sliders from definitions
        List<MetricDef> defs = BuildMetricDefs();
        var sliders = new Dictionary<string, TextMenu.Slider>();
        foreach (MetricDef def in defs)
        {
            TextMenu.Slider slider = MetricSlider(def);
            if (def.LabelKey == DialogIds.PercentileId)
                slider.Change(v => percentileValue.Disabled = def.Choices[v] == MetricOutputChoice.Off);
            sliders[def.LabelKey] = slider;
        }

        // --- Bulk action buttons ---

        TextMenu.Button turnAllOff = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonAllOffId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                history.Index        = 0; _settings.History        = false;
                resetShare.Index     = 0; _settings.ResetShare     = false;
                multimodalTest.Index = 0; _settings.MultimodalTest = false;
                roomDependency.Index = 0; _settings.RoomDependency = false;
                foreach (MetricDef def in defs)
                {
                    def.Set(MetricOutputChoice.Off);
                    sliders[def.LabelKey].Index = 0;
                }
                percentileValue.Disabled = true;
                _instance.SaveSettings();
            });

        TextMenu.Button turnAllOn = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonAllOnId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                history.Index        = 1; _settings.History        = true;
                resetShare.Index     = 1; _settings.ResetShare     = true;
                multimodalTest.Index = 1; _settings.MultimodalTest = true;
                roomDependency.Index = 1; _settings.RoomDependency = true;
                foreach (MetricDef def in defs)
                {
                    MetricOutputChoice best = def.Choices[def.Choices.Length - 1];
                    def.Set(best);
                    sliders[def.LabelKey].Index = def.Choices.Length - 1;
                }
                percentileValue.Disabled = false;
                _instance.SaveSettings();
            });

        TextMenu.Button resetAll = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonResetId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                history.Index        = 1; _settings.History        = true;
                resetShare.Index     = 1; _settings.ResetShare     = true;
                multimodalTest.Index = 1; _settings.MultimodalTest = true;
                roomDependency.Index = 1; _settings.RoomDependency = true;
                foreach (MetricDef def in defs)
                {
                    def.Set(def.DefaultValue);
                    sliders[def.LabelKey].Index = Array.IndexOf(def.Choices, def.DefaultValue);
                }
                percentileValue.Index    = Array.IndexOf(enumPercentileValues, PercentileChoice.P90);
                percentileValue.Disabled = _settings.Percentile == MetricOutputChoice.Off;
                _instance.SaveSettings();
            });

        sub.Add(turnAllOff);
        sub.Add(turnAllOn);
        sub.Add(resetAll);
        sub.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.MetricsSubHeaderId), false));

        foreach (MetricDef def in defs)
        {
            sub.Add(sliders[def.LabelKey]);
            if (def.LabelKey == DialogIds.PercentileId)
                sub.Add(percentileValue);
        }

        sliders[DialogIds.SuccessRateId].AddDescription(sub, menu, Dialog.Clean(DialogIds.SuccessRateSubTextId));
        turnAllOn.AddDescription(sub, menu, Dialog.Clean(DialogIds.AllOnDescId));

        sub.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.ExportOnlyId), false));
        sub.Add(history);
        sub.Add(resetShare);
        sub.Add(multimodalTest);
        sub.Add(roomDependency);

        sub.Visible = _settings.Enabled;
        return sub;
    }
}
