using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.UI;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static SpeebrunConsistencyTrackerModuleSettings _settings => SpeebrunConsistencyTrackerModule.Settings;
    private static SpeebrunConsistencyTrackerModule _instance => SpeebrunConsistencyTrackerModule.Instance;

    private const string ConfirmSfx = "event:/ui/main/button_select";

    private static readonly MetricOutputChoice[] AllChoices = Enum.GetValues<MetricOutputChoice>();

    // This list drives the sliders and the Turn All Off / On / Reset buttons alike.
    // No per-row Choices: every row offered AllChoices and a row that offered fewer would have to
    // say what it does when the current value is not among them.
    private record MetricDef(
        string LabelKey,
        Func<MetricOutputChoice> Get,
        Action<MetricOutputChoice> Set,
        MetricOutputChoice DefaultValue);

    private static List<MetricDef> BuildMetricDefs() =>
    [
new(DialogIds.SuccessRateId,            () => _settings.SuccessRate,             v => _settings.SuccessRate = v,             MetricOutputChoice.Both),
        new(DialogIds.CompletedRunCountId,      () => _settings.CompletedRunCount,       v => _settings.CompletedRunCount = v,       MetricOutputChoice.Both),
        new(DialogIds.TotalRunCountId,          () => _settings.TotalRunCount,           v => _settings.TotalRunCount = v,           MetricOutputChoice.Both),
        new(DialogIds.GoldRateId,               () => _settings.GoldRate,                v => _settings.GoldRate = v,                MetricOutputChoice.Off),
        new(DialogIds.DnfCountId,               () => _settings.DnfCount,                v => _settings.DnfCount = v,                MetricOutputChoice.Off),
        new(DialogIds.AverageId,                () => _settings.Average,                 v => _settings.Average = v,                 MetricOutputChoice.Both),
        new(DialogIds.MedianId,                 () => _settings.Median,                  v => _settings.Median = v,                  MetricOutputChoice.Both),
        new(DialogIds.MadId,                    () => _settings.MedianAbsoluteDeviation, v => _settings.MedianAbsoluteDeviation = v, MetricOutputChoice.Off),
        new(DialogIds.RelMadId,                 () => _settings.RelativeMAD,             v => _settings.RelativeMAD = v,             MetricOutputChoice.Off),
        new(DialogIds.ResetRateId,              () => _settings.ResetRate,               v => _settings.ResetRate = v,               MetricOutputChoice.Export),
        new(DialogIds.MinimumId,                () => _settings.Minimum,                 v => _settings.Minimum = v,                 MetricOutputChoice.Export),
        new(DialogIds.MaximumId,                () => _settings.Maximum,                 v => _settings.Maximum = v,                 MetricOutputChoice.Off),
        new(DialogIds.StandardDeviationId,      () => _settings.StandardDeviation,       v => _settings.StandardDeviation = v,       MetricOutputChoice.Both),
        new(DialogIds.CoefficientOfVariationId, () => _settings.CoefficientOfVariation,  v => _settings.CoefficientOfVariation = v,  MetricOutputChoice.Off),
        new(DialogIds.PercentileId,             () => _settings.Percentile,              v => _settings.Percentile = v,              MetricOutputChoice.Off),
        new(DialogIds.InterquartileRangeId,     () => _settings.InterquartileRange,      v => _settings.InterquartileRange = v,      MetricOutputChoice.Off),
        new(DialogIds.LinearRegressionId,       () => _settings.LinearRegression,        v => _settings.LinearRegression = v,        MetricOutputChoice.Off),
        new(DialogIds.SoBId,                    () => _settings.SoB,                     v => _settings.SoB = v,                     MetricOutputChoice.Overlay),
    ];

    private static TextMenu.Slider MetricSlider(MetricDef def)
    {
        var slider = new TextMenu.Slider(
            Dialog.Clean(def.LabelKey),
            i => Utility.EnumLabels.For(AllChoices[i]),
            0,
            AllChoices.Length - 1,
            Array.IndexOf(AllChoices, def.Get()));
        slider.Change(v => { def.Set(AllChoices[v]); MetricEngine.InvalidateSettingsHash(); });
        return slider;
    }

    private static string GetTargetTime() =>
        $"{_settings.Minutes}:{_settings.Seconds:D2}.{_settings.MillisecondsFirstDigit}{_settings.MillisecondsSecondDigit}{_settings.MillisecondsThirdDigit}";

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
        // Change does not fire at construction, so the initial visibility is set here.
        keybindButton.Visible = _settings.Enabled;

        TextMenu.OnOff enabledToggle = new(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled);
        enabledToggle.Change(value =>
        {
            _settings.Enabled = value;
            foreach (TextMenuExt.SubMenu sub in subMenus) sub.Visible = value;
            keybindButton.Visible = value;
            if (!value)
                SpeebrunConsistencyTrackerModule.Clear();
        });

        menu.Add(enabledToggle);
        foreach (TextMenuExt.SubMenu sub in subMenus)
            menu.Add(sub);
        menu.Add(keybindButton);
        // Turning this off calls Clear(), which wipes every save-state slot's data. Nothing said so.
        enabledToggle.AddDescription(menu, Dialog.Clean(DialogIds.EnabledDescId));
    }
}
