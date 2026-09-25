using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Reflection;

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

    // The menu edits this; the three digits below are the persisted form and stay as they are.
    [SettingIgnore]
    public int Milliseconds
    {
        get => MillisecondsFirstDigit * 100 + MillisecondsSecondDigit * 10 + MillisecondsThirdDigit;
        set
        {
            int ms = System.Math.Clamp(value, 0, 999);
            MillisecondsFirstDigit  = ms / 100;
            MillisecondsSecondDigit = ms / 10 % 10;
            MillisecondsThirdDigit  = ms % 10;
        }
    }

    // One place for the digit split: the typed box and the clipboard import both landed here and
    // had drifted into two copies of it. TimeParser rejects anything these five fields cannot hold.
    public void SetTargetTime(System.TimeSpan time)
    {
        Minutes = (int)time.TotalMinutes;
        Seconds = time.Seconds;
        Milliseconds = time.Milliseconds;
    }

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
    // Not a metric: it decides whether the charts draw the target-time line, and it never
    // produced a CSV column, so its Export half did nothing. Shown as an On/Off under Charts.
    // Still a MetricOutputChoice, and still declared here among the metrics, because it is
    // persisted under this name and in this position -- see PersistedEnumMembersTests for what
    // moving or dropping a persisted member does to a live settings file. Off and Export read as
    // off, Overlay and Both as on, which is what they already meant.
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
    // Implemented but uncalibrated, and hidden on purpose. Its stability factor stays above 0.99
    // while relMAD*CV < 0.014 -- tight and loose sessions alike -- so it reports (1 - resetRate)^2
    // shaded by the PB gap.
    // Recalibrate before exposing it: a menu row is one line, and a player reads the number as a verdict.
    [SettingIgnore]
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

        // Reflected, not listed. A hand-written array of the same six bindings was a second copy
        // of the keybind set, and the KeybindConfigUi table is a third: a seventh keybind added to
        // one and forgotten here would never get Keys.None stripped from it. Runs once, at load.
        //
        // IsAssignableFrom, not ==, because that is what Everest's own OnInputInitialize uses to
        // decide which properties it initializes; matching it keeps the two enumerations from
        // drifting apart over a ButtonBinding subclass. DeclaredOnly is safe only because
        // EverestModuleSettings declares nothing itself — a base class holding a keybind would be
        // skipped here and still initialized by Everest. The two guards are what the hand-written
        // array gave for free: an indexer or a write-only property typed ButtonBinding would make
        // GetValue(this) throw at load, and the compiler can no longer catch that for us.
        foreach (PropertyInfo property in GetType().GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
            if (!typeof(ButtonBinding).IsAssignableFrom(property.PropertyType)) continue;
            if (property.GetIndexParameters().Length > 0 || !property.CanRead) continue;

            // Never create a null binding here: Everest creates it later in OnInputInitialize
            // and only then reads [DefaultButtonBinding], which it would skip if one exists.
            if (property.GetValue(this) is not ButtonBinding keybind) continue;

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
