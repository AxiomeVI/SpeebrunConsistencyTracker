using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    // Drives the five export-only toggles below from one list, the same way MetricDef drives
    // the sliders — so a sixth toggle means adding one entry here, not remembering three
    // hand-written blocks in turnAllOff/turnAllOn/resetAll.
    private record ToggleDef(TextMenu.OnOff Item, Action<bool> Set, bool Default);

    private static TextMenuExt.SubMenu CreateMetricsSubMenu(TextMenu menu)
    {
        PercentileChoice[] enumPercentileValues = Enum.GetValues<PercentileChoice>();

        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.StatsSubMenuId), false);

        TextMenu.OnOff history        = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RunHistoryId),    _settings.History).Change(b => _settings.History = b);
        TextMenu.OnOff resetShare     = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.ResetShareId),    _settings.ResetShare).Change(b => _settings.ResetShare = b);
        TextMenu.OnOff multimodalTest = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.MultimodalTestId),_settings.MultimodalTest).Change(b => _settings.MultimodalTest = b);
        TextMenu.OnOff roomDependency = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RoomDependencyId),_settings.RoomDependency).Change(b => _settings.RoomDependency = b);
        TextMenu.OnOff bestSplit      = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.BestSplitId),     _settings.BestSplit).Change(b => _settings.BestSplit = b);

        List<ToggleDef> toggles =
        [
            new(history,        b => _settings.History        = b, false),
            new(resetShare,     b => _settings.ResetShare     = b, false),
            new(multimodalTest, b => _settings.MultimodalTest = b, false),
            new(roomDependency, b => _settings.RoomDependency = b, false),
            new(bestSplit,      b => _settings.BestSplit      = b, true),
        ];

        // Visibility follows the Percentile slider.
        TextMenu.Slider percentileValue = new(
            Dialog.Clean(DialogIds.PercentileValueId),
            i => enumPercentileValues[i].ToString(),
            0, enumPercentileValues.Length - 1,
            Array.IndexOf(enumPercentileValues, _settings.PercentileValue))
        {
            Disabled = _settings.Percentile == MetricOutputChoice.Off
        };
        percentileValue.Change(v => { _settings.PercentileValue = enumPercentileValues[v]; MetricEngine.InvalidateSettingsHash(); });

        List<MetricDef> defs = BuildMetricDefs();
        var sliders = new Dictionary<string, TextMenu.Slider>();
        foreach (MetricDef def in defs)
        {
            TextMenu.Slider slider = MetricSlider(def);
            if (def.LabelKey == DialogIds.PercentileId)
                slider.Change(v => percentileValue.Disabled = def.Choices[v] == MetricOutputChoice.Off);
            sliders[def.LabelKey] = slider;
        }

        TextMenu.Button turnAllOff = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonAllOffId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                foreach (ToggleDef t in toggles) { t.Item.Index = 0; t.Set(false); }
                foreach (MetricDef def in defs)
                {
                    def.Set(MetricOutputChoice.Off);
                    sliders[def.LabelKey].Index = 0;
                }
                percentileValue.Disabled = true;
                _instance.SaveSettings();
                MetricEngine.InvalidateSettingsHash();
            });

        TextMenu.Button turnAllOn = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonAllOnId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                foreach (ToggleDef t in toggles) { t.Item.Index = 1; t.Set(true); }
                foreach (MetricDef def in defs)
                {
                    MetricOutputChoice best = def.Choices[^1];
                    def.Set(best);
                    sliders[def.LabelKey].Index = def.Choices.Length - 1;
                }
                percentileValue.Disabled = false;
                _instance.SaveSettings();
                MetricEngine.InvalidateSettingsHash();
            });

        TextMenu.Button resetAll = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonResetId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                foreach (ToggleDef t in toggles) { t.Item.Index = t.Default ? 1 : 0; t.Set(t.Default); }
                foreach (MetricDef def in defs)
                {
                    def.Set(def.DefaultValue);
                    sliders[def.LabelKey].Index = Array.IndexOf(def.Choices, def.DefaultValue);
                }
                percentileValue.Index    = Array.IndexOf(enumPercentileValues, PercentileChoice.P90);
                percentileValue.Disabled = _settings.Percentile == MetricOutputChoice.Off;
                _instance.SaveSettings();
                MetricEngine.InvalidateSettingsHash();
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
        foreach (ToggleDef t in toggles)
            sub.Add(t.Item);

        sub.Visible = _settings.Enabled;
        return sub;
    }
}
