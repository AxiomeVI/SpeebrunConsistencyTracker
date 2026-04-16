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
    [SettingIgnore]  // ConsistencyScore metric is forcefully disabled; hide from menu until implementation is ready.
    public MetricOutputChoice ConsistencyScore  { get; set; } = MetricOutputChoice.Off;
    public MetricOutputChoice GoldRate { get; set; } = MetricOutputChoice.Off;
    public bool MultimodalTest { get; set; } = false;
    public bool RoomDependency { get; set; } = false;

    public void OnLoadSettings() {
        Keybind_ImportTargetTime  ??= new ButtonBinding();
        Keybind_StatsExport       ??= new ButtonBinding();
        Keybind_ToggleGraphOverlay ??= new ButtonBinding();
        Keybind_NextGraph         ??= new ButtonBinding();
        Keybind_PreviousGraph     ??= new ButtonBinding();
        Keybind_ClearStats        ??= new ButtonBinding();

        if (Keybind_ImportTargetTime.Keys   == null) Keybind_ImportTargetTime.Keys   = new();
        if (Keybind_ImportTargetTime.Buttons == null) Keybind_ImportTargetTime.Buttons = new();
        if (Keybind_StatsExport.Keys        == null) Keybind_StatsExport.Keys        = new();
        if (Keybind_StatsExport.Buttons     == null) Keybind_StatsExport.Buttons     = new();
        if (Keybind_ToggleGraphOverlay.Keys    == null) Keybind_ToggleGraphOverlay.Keys    = new();
        if (Keybind_ToggleGraphOverlay.Buttons == null) Keybind_ToggleGraphOverlay.Buttons = new();
        if (Keybind_NextGraph.Keys          == null) Keybind_NextGraph.Keys          = new();
        if (Keybind_NextGraph.Buttons       == null) Keybind_NextGraph.Buttons       = new();
        if (Keybind_PreviousGraph.Keys      == null) Keybind_PreviousGraph.Keys      = new();
        if (Keybind_PreviousGraph.Buttons   == null) Keybind_PreviousGraph.Buttons   = new();
        if (Keybind_ClearStats.Keys         == null) Keybind_ClearStats.Keys         = new();
        if (Keybind_ClearStats.Buttons      == null) Keybind_ClearStats.Buttons      = new();
    }

    #region Hotkeys

    [SettingName(DialogIds.KeyImportTargetTimeId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_ImportTargetTime { get; set; }

    [SettingName(DialogIds.KeyStatsExportId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_StatsExport { get; set; }

    [SettingName(DialogIds.ToggleGraphOverlayId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_ToggleGraphOverlay { get; set; }

    [SettingName(DialogIds.KeyNextGraphId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_NextGraph { get; set; }

    [SettingName(DialogIds.KeyPreviousGraphId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_PreviousGraph { get; set; }

    [SettingName(DialogIds.KeyClearStatsId)]
    [SettingSubText(DialogIds.KeybindComboSubId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding Keybind_ClearStats { get; set; }

    #endregion
}
