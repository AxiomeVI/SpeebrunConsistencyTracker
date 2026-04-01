using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.UI;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;
    private static readonly SpeebrunConsistencyTrackerModule _instance = SpeebrunConsistencyTrackerModule.Instance;

    private const string ConfirmSfx = "event:/ui/main/button_select";

    private static readonly MetricOutputChoice[] AllChoices = Enum.GetValues<MetricOutputChoice>();

    // ---------------------------------------------------------------------------
    // Metric definitions — drives sliders AND the Turn All Off / On / Reset buttons
    // ---------------------------------------------------------------------------

    private record MetricDef(
        string LabelKey,
        MetricOutputChoice[] Choices,
        Func<MetricOutputChoice> Get,
        Action<MetricOutputChoice> Set,
        MetricOutputChoice DefaultValue);

    private static List<MetricDef> BuildMetricDefs() =>
    [
        new(DialogIds.ConsistencyScoreId,       AllChoices, () => _settings.ConsistencyScore,        v => _settings.ConsistencyScore = v,        MetricOutputChoice.Off),
        new(DialogIds.SuccessRateId,            AllChoices, () => _settings.SuccessRate,             v => _settings.SuccessRate = v,             MetricOutputChoice.Both),
        new(DialogIds.TargetTimeStatId,         AllChoices, () => _settings.TargetTime,              v => _settings.TargetTime = v,              MetricOutputChoice.Export),
        new(DialogIds.CompletedRunCountId,      AllChoices, () => _settings.CompletedRunCount,       v => _settings.CompletedRunCount = v,       MetricOutputChoice.Both),
        new(DialogIds.TotalRunCountId,          AllChoices, () => _settings.TotalRunCount,           v => _settings.TotalRunCount = v,           MetricOutputChoice.Both),
        new(DialogIds.GoldRateId,               AllChoices, () => _settings.GoldRate,                v => _settings.GoldRate = v,                MetricOutputChoice.Off),
        new(DialogIds.DnfCountId,               AllChoices, () => _settings.DnfCount,                v => _settings.DnfCount = v,                MetricOutputChoice.Off),
        new(DialogIds.AverageId,                AllChoices, () => _settings.Average,                 v => _settings.Average = v,                 MetricOutputChoice.Both),
        new(DialogIds.MedianId,                 AllChoices, () => _settings.Median,                  v => _settings.Median = v,                  MetricOutputChoice.Both),
        new(DialogIds.MadID,                    AllChoices, () => _settings.MedianAbsoluteDeviation, v => _settings.MedianAbsoluteDeviation = v, MetricOutputChoice.Off),
        new(DialogIds.RelMadID,                 AllChoices, () => _settings.RelativeMAD,             v => _settings.RelativeMAD = v,             MetricOutputChoice.Off),
        new(DialogIds.ResetRateId,              AllChoices, () => _settings.ResetRate,               v => _settings.ResetRate = v,               MetricOutputChoice.Export),
        new(DialogIds.MinimumId,                AllChoices, () => _settings.Minimum,                 v => _settings.Minimum = v,                 MetricOutputChoice.Export),
        new(DialogIds.MaximumId,                AllChoices, () => _settings.Maximum,                 v => _settings.Maximum = v,                 MetricOutputChoice.Off),
        new(DialogIds.StandardDeviationId,      AllChoices, () => _settings.StandardDeviation,       v => _settings.StandardDeviation = v,       MetricOutputChoice.Both),
        new(DialogIds.CoefficientOfVariationId, AllChoices, () => _settings.CoefficientOfVariation,  v => _settings.CoefficientOfVariation = v,  MetricOutputChoice.Off),
        new(DialogIds.PercentileId,             AllChoices, () => _settings.Percentile,              v => _settings.Percentile = v,              MetricOutputChoice.Off),
        new(DialogIds.InterquartileRangeId,     AllChoices, () => _settings.InterquartileRange,      v => _settings.InterquartileRange = v,      MetricOutputChoice.Off),
        new(DialogIds.LinearRegressionId,       AllChoices, () => _settings.LinearRegression,        v => _settings.LinearRegression = v,        MetricOutputChoice.Off),
        new(DialogIds.SoBId,                    AllChoices, () => _settings.SoB,                     v => _settings.SoB = v,                     MetricOutputChoice.Overlay),
    ];

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TextMenu.Slider MetricSlider(MetricDef def)
    {
        var slider = new TextMenu.Slider(
            Dialog.Clean(def.LabelKey),
            i => def.Choices[i].ToString(),
            0,
            def.Choices.Length - 1,
            Array.IndexOf(def.Choices, def.Get()));
        slider.Change(v => { def.Set(def.Choices[v]); MetricEngine.InvalidateSettingsHash(); });
        return slider;
    }

    private static string GetTargetTime() =>
        $"{_settings.Minutes}:{_settings.Seconds:D2}.{_settings.MillisecondsFirstDigit}{_settings.MillisecondsSecondDigit}{_settings.MillisecondsThirdDigit}";

    // ---------------------------------------------------------------------------
    // Public entry point
    // ---------------------------------------------------------------------------

    public static void CreateMenu(TextMenu menu, bool inGame)
    {
        List<TextMenuExt.SubMenu> subMenus =
        [
            CreateTargetTimeSubMenu(menu, inGame),
            CreateExportSubMenu(menu, inGame),
            CreateMetricsSubMenu(menu),
            CreateTextOverlaySubMenu(menu),
            CreateGraphOverlaySubMenu(menu)
        ];

        TextMenu.Button keybindButton = new TextMenu.Button(Dialog.Clean(DialogIds.KeybindConfigId));
        keybindButton.Pressed(() => {
            menu.Focused = false;
            var ui = new KeybindConfigUi();
            ui.OnClose = () => menu.Focused = true;
            Engine.Scene.Add(ui);
            Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
        });

        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled).Change(value =>
        {
            _settings.Enabled = value;
            foreach (TextMenuExt.SubMenu sub in subMenus) sub.Visible = value;
            keybindButton.Visible = value;
            if (!value)
                SpeebrunConsistencyTrackerModule.Clear();
        }));

        foreach (TextMenuExt.SubMenu sub in subMenus)
            menu.Add(sub);
        menu.Add(keybindButton);
    }
}
