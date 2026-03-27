using Microsoft.Xna.Framework.Input;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker;

[SettingName(DialogIds.SpeebrunConsistencyTracker)]
public class SpeebrunConsistencyTrackerModuleSettings : EverestModuleSettings {

    public bool Enabled { get; set; } = true;

    // Export 
    public bool ExportWithSRT { get; set; } = false;
    public ExportChoice ExportMode { get; set; } = ExportChoice.Clipboard;

    // Target Time menu
    public int Minutes { get; set; } = 0;
    public int Seconds { get; set; } = 0;
    public int MillisecondsFirstDigit { get; set; } = 0;
    public int MillisecondsSecondDigit { get; set; } = 0;
    public int MillisecondsThirdDigit { get; set; } = 0;

    // Text Overlay menu
    public bool OverlayEnabled { get; set; } = true;
    public int TextSize { get; set; } = 65;
    public int TextOffsetX { get; set; } = 5;
    public int TextOffsetY { get; set; } = 0;
    public int TextAlpha { get; set; } = 90;

    public StatTextPosition TextPosition { get; set; } = StatTextPosition.TopLeft;
    public StatTextOrientation TextOrientation { get; set; } = StatTextOrientation.Horizontal;

    // Graph Overlay menu
    public ColorChoice RoomColor { get; set; } = ColorChoice.Cyan;
    public ColorChoice SegmentColor { get; set; } = ColorChoice.Orange;
    public bool ShowRoomTimeDistributionPlots { get; set; } = false;
    public int TimeLossThresholdMs { get; set; } = 493;
    public int ChartOpacity { get; set; } = 75;
    public bool GraphScatter { get; set; } = true;
    public bool GraphRoomHistogram { get; set; } = false;
    public bool GraphSegmentHistogram { get; set; } = true;
    public bool GraphDnfPercent { get; set; } = true;
    public bool GraphProblemRooms { get; set; } = false;
    public bool GraphInconsistentRooms { get; set; } = false;
    public bool GraphTimeLoss { get; set; } = false;
    public bool GraphRunTrajectory { get; set; } = false;
    public bool GraphBoxPlot { get; set; } = false;

    // Metrics menu
    public bool History { get; set; } = false;
    public MetricOutputChoice SuccessRate { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice TargetTime { get; set; } = MetricOutputChoice.Export;
    public MetricOutputChoice CompletedRunCount { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice TotalRunCount { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice DnfCount { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice Average { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice Median { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice ResetRate { get; set; } = MetricOutputChoice.Export;
    public bool ResetShare { get; set; } = false;
    public MetricOutputChoice Minimum { get; set; } = MetricOutputChoice.Export;
    public MetricOutputChoice Maximum { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice StandardDeviation { get; set; } = MetricOutputChoice.Both;
    public MetricOutputChoice CoefficientOfVariation { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice Percentile { get; set; } = MetricOutputChoice.Off;
    public PercentileChoice PercentileValue { get; set; } = PercentileChoice.P90;
    public MetricOutputChoice InterquartileRange { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice LinearRegression { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice SoB { get; set; } = MetricOutputChoice.Overlay;
    public MetricOutputChoice MedianAbsoluteDeviation  { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice RelativeMAD  { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice ConsistencyScore  { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice GoldRate { get; set; } = MetricOutputChoice.Off;
    public bool MultimodalTest { get; set; } = false;
    public bool RoomDependency { get; set; } = false;

    #region Hotkeys

    [SettingName(DialogIds.KeyImportTargetTimeId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonKeyImportTargetTime { get; set; }

    [SettingName(DialogIds.KeyStatsExportId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonKeyStatsExport { get; set; }

    [SettingName(DialogIds.ToggleGraphOverlayId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonToggleGraphOverlay { get; set; }

    [SettingName(DialogIds.KeyNextGraphId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonNextGraph { get; set; }

    [SettingName(DialogIds.KeyPreviousGraphId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonPreviousGraph { get; set; }

    [SettingName(DialogIds.KeyClearStatsId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonKeyClearStats { get; set; }

    #endregion
}
