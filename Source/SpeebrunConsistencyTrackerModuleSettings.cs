using Microsoft.Xna.Framework.Input;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker;

[SettingName(DialogIds.SpeebrunConsistencyTracker)]
public class SpeebrunConsistencyTrackerModuleSettings : EverestModuleSettings {

    public bool Enabled { get; set; } = true;

    // Target Time menu
    public int Minutes { get; set; } = 0;
    public int Seconds { get; set; } = 0;
    public int MillisecondsFirstDigit { get; set; } = 0;
    public int MillisecondsSecondDigit { get; set; } = 0;
    public int MillisecondsThirdDigit { get; set; } = 0;

    // Overlay menu
    public bool OverlayEnabled { get; set; } = true;
    public int TextSize { get; set; } = 65;
    public int TextOffsetX { get; set; } = 5;
    public int TextOffsetY { get; set; } = 0;
    public StatTextPosition TextPosition { get; set; } = StatTextPosition.TopLeft;
    public StatTextOrientation TextOrientation { get; set; } = StatTextOrientation.Horizontal;

    // Metrics menu
    public bool History { get; set; } = true;
    public MetricOutputChoice SuccessRate { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice TargetTime { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice CompletedRunCount { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice TotalRunCount { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice DnfCount { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice Average { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice Median { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice ResetRate { get; set; } = MetricOutputChoice.Export;
    public bool ResetShare { get; set; } = true;
    public MetricOutputChoice Minimum { get; set; } = MetricOutputChoice.Export;
    public MetricOutputChoice Maximum { get; set; } = MetricOutputChoice.Export;
    public MetricOutputChoice StandardDeviation { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice CoefficientOfVariation { get; set; } = MetricOutputChoice.Export;
    public MetricOutputChoice Percentile { get; set; } = MetricOutputChoice.Export;
    public PercentileChoice PercentileValue { get; set; } = PercentileChoice.P90;
    public MetricOutputChoice LinearRegression { get; set; } = MetricOutputChoice.Export;
    public MetricOutputChoice SoB { get; set; } = MetricOutputChoice.Both;

    public bool ExportWithSRT { get; set; } = true;

    #region Hotkeys

    [SettingName(DialogIds.KeyStatsExportId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonKeyStatsExport { get; set; } = new(0, Keys.None);

    [SettingName(DialogIds.ToggleIngameOverlayId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonToggleIngameOverlay { get; set; }  = new(0, Keys.None);

    [SettingName(DialogIds.KeyClearStatsId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonKeyClearStats { get; set; }  = new(0, Keys.None);

    [SettingName(DialogIds.KeyImportTargetTimeId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonKeyImportTargetTime { get; set; }  = new(0, Keys.None);

    #endregion
}