using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

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
    public int ChartOpacity { get; set; } = 75;

    [SettingIgnore]
    public Color RoomColorFinal    { get; set; } = ColorHelper.ToFinalColor(ColorChoice.Cyan,   75);
    [SettingIgnore]
    public Color SegmentColorFinal { get; set; } = ColorHelper.ToFinalColor(ColorChoice.Orange, 75);

    [SettingIgnore]
    public Color PrimaryChartColor   { get; set; } = Color.IndianRed;
    [SettingIgnore]
    public Color SecondaryChartColor { get; set; } = Color.CornflowerBlue;
    
    [SettingIgnore]
    public Color PrimaryChartColorFinal   { get; set; } = Color.IndianRed      * 0.75f;
    [SettingIgnore]
    public Color SecondaryChartColorFinal { get; set; } = Color.CornflowerBlue * 0.75f;

    [SettingIgnore]
    public Color TrajectoryBestColorFinal { get; set; } = Color.Gold;
    [SettingIgnore]
    public Color TrajectoryLastColorFinal { get; set; } = Color.MediumOrchid;
    [SettingIgnore]
    public Color TrajectorySobColorFinal  { get; set; } = Color.Turquoise;

    public bool ShowRoomTimeDistributionPlots { get; set; } = false;
    public int TimeLossThresholdMs { get; set; } = 493;
    public bool GraphScatter { get; set; } = true;
    public bool GraphRoomHistogram { get; set; } = false;
    public bool GraphSegmentHistogram { get; set; } = true;
    public bool GraphDnfPercent { get; set; } = true;
    public bool GraphProblemRooms { get; set; } = false;
    public bool GraphTimeLoss { get; set; } = false;
    public bool GraphRunTrajectory { get; set; } = true;
    public bool GraphBoxPlot { get; set; } = false;

    [SettingIgnore]
    public GraphType LastShownGraph { get; set; } = GraphType.Scatter;

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
    [SettingIgnore]  // hidden until the metric is implemented
    public MetricOutputChoice ConsistencyScore  { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice GoldRate { get; set; } = MetricOutputChoice.Off;
    public bool MultimodalTest { get; set; } = false;
    public bool RoomDependency { get; set; } = false;
    public bool BestSplit { get; set; } = true;

    // Not an Everest hook: nothing calls this on its own. LoadSettings calls it by hand,
    // right after deserialization.
    public void OnLoadSettings() {
        // Migrates a settings file written while the Sheets export still existed.
        if (ExportMode == ExportChoice.Sheet) ExportMode = ExportChoice.Clipboard;

        ButtonBinding[] keybinds = {
            Keybind_ImportTargetTime,
            Keybind_StatsExport,
            Keybind_ToggleGraphOverlay,
            Keybind_NextGraph,
            Keybind_PreviousGraph,
            Keybind_ClearStats,
        };

        foreach (ButtonBinding keybind in keybinds) {
            // Never create a null binding here: Everest creates it later in OnInputInitialize
            // and only then reads [DefaultButtonBinding], which it would skip if one exists.
            if (keybind == null) continue;

            keybind.Keys    ??= new();
            keybind.Buttons ??= new();
            // Keys.None is a real key that reads as held, and Everest's own rebind screen lets
            // it through, so a settings file can carry it however careful our screen is.
            keybind.Keys.RemoveAll(key => key == Keys.None);
        }
    }

    #region Hotkeys

    // [SettingIgnore] hides these from Everest's key config screen, which presents several
    // bound keys as alternatives while ComboHotkey reads them as all-held-at-once. Everest
    // still initializes them: OnInputInitialize ignores the attribute.

    [SettingName(DialogIds.KeyImportTargetTimeId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [SettingIgnore]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_ImportTargetTime { get; set; }

    [SettingName(DialogIds.KeyStatsExportId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [SettingIgnore]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_StatsExport { get; set; }

    [SettingName(DialogIds.ToggleGraphOverlayId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [SettingIgnore]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_ToggleGraphOverlay { get; set; }

    [SettingName(DialogIds.KeyNextGraphId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [SettingIgnore]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_NextGraph { get; set; }

    [SettingName(DialogIds.KeyPreviousGraphId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [SettingIgnore]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_PreviousGraph { get; set; }

    [SettingName(DialogIds.KeyClearStatsId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [SettingIgnore]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_ClearStats { get; set; }

    #endregion
}
