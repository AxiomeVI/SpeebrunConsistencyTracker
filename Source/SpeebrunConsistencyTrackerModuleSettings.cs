using Microsoft.Xna.Framework.Input;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker;

[SettingName(DialogIds.SpeebrunConsistencyTracker)]
public class SpeebrunConsistencyTrackerModuleSettings : EverestModuleSettings {

    [SettingName(DialogIds.EnabledId)]
    public bool Enabled { get; set; } = true;

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

    #endregion

    [SettingSubMenu]
    public class TargetTimeSubMenu {
        [SettingName(DialogIds.Minutes), SettingRange(min: 0, max: 30, largeRange: true), SettingSubHeader(DialogIds.TargetTimeFormatId)]
        public int Minutes { get; set; } = 0;

        [SettingName(DialogIds.Seconds), SettingRange(min: 0, max: 59, largeRange: true)]
        public int Seconds { get; set; } = 0;

        [SettingName(DialogIds.Milliseconds), SettingSubText(DialogIds.MillisecondsFirst), SettingRange(min: 0, max: 9)]
        public int MillisecondsFirstDigit { get; set; } = 0;
        [SettingName(DialogIds.Milliseconds), SettingSubText(DialogIds.MillisecondsSecond), SettingRange(min: 0, max: 9)]
        public int MillisecondsSecondDigit { get; set; } = 0;
        [SettingName(DialogIds.Milliseconds), SettingSubText(DialogIds.MillisecondsThird), SettingRange(min: 0, max: 9)]
        public int MillisecondsThirdDigit { get; set; } = 0;
    }

    [SettingName(DialogIds.TargetTimeId)]
    public TargetTimeSubMenu TargetTime { get; set; } = new();

    [SettingSubMenu]
    public class IngameOverlaySubMenu {
        [SettingName(DialogIds.OverlayEnabledId)]
        public bool OverlayEnabled { get; set; } = true;

        [SettingName(DialogIds.ShowInPauseMenuId), SettingSubText(DialogIds.ShowInPauseMenuSubTextId)]
        public bool ShowInPauseMenu { get; set; } = true;

        [SettingRange(min: 0, max: 100), SettingName(DialogIds.TextSizeId)]
        public int TextSize { get; set; } = 50;

        public int TextOffsetX { get; set; } = 5;

        public int TextOffsetY { get; set; } = 0;

        [SettingName(DialogIds.TextPositionId)]
        public StatTextPosition TextPosition { get; set; } = StatTextPosition.TopLeft;
    }

    [SettingName(DialogIds.IngameOverlayId)]
    public IngameOverlaySubMenu IngameOverlay { get; set; } = new();

    [SettingSubMenu]
    public class StatsSubMenu {
        [SettingName(DialogIds.RunHistoryId), SettingSubText(DialogIds.OnlyInExportId)]
        public bool RunHistory { get; set; } = true;
        [SettingName(DialogIds.SuccessRateId), SettingSubText(DialogIds.SuccessRateSubTextId)]
        public StatOutput SuccessRate { get; set; } = StatOutput.Both;
        [SettingName(DialogIds.TargetTimeStatId)]
        public StatOutput TargetTime { get; set; } = StatOutput.Both;
        [SettingName(DialogIds.RunCountId)]
        public StatOutput RunCount { get; set; } = StatOutput.Both;
        [SettingName(DialogIds.AverageId)]
        public StatOutput Average { get; set; } = StatOutput.Both;
        [SettingName(DialogIds.MedianId)]
        public StatOutput Median { get; set; } = StatOutput.Both;
        [SettingName(DialogIds.CompletionRateId), SettingSubText(DialogIds.CompleteRunSubTextId)]
        public StatOutput CompletionRate { get; set; } = StatOutput.Export;
        [SettingName(DialogIds.MinimumId)]
        public StatOutput Minimum { get; set; } = StatOutput.Export;
        [SettingName(DialogIds.MaximumId)]
        public StatOutput Maximum { get; set; } = StatOutput.Export;
        [SettingName(DialogIds.StandardDeviationId)]
        public StatOutput StandardDeviation { get; set; } = StatOutput.Export;
        [SettingName(DialogIds.PercentileId)]
        public StatOutput Percentile { get; set; } = StatOutput.Export;
        [SettingName(DialogIds.PercentileValueId)]
        public PercentileChoice PercentileValue { get; set; } = PercentileChoice.P90;
        [SettingName(DialogIds.LinearRegressionId), SettingSubText(DialogIds.LinearRegressionSubTextId)]
        public bool LinearRegression { get; set; } = false;
    }

    [SettingName(DialogIds.StatsSubMenuId)]
    public StatsSubMenu StatsMenu { get; set; } = new();
}