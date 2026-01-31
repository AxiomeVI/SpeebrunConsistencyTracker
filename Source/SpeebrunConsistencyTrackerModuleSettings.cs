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

    [SettingName(DialogIds.KeyImportTargetTimeId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ButtonKeyImportTargetTime { get; set; }  = new(0, Keys.None);

    #endregion

    [SettingIgnore]
    public ButtonBinding SetTargetTimeBinding { get; set; }

    public void CreateSetTargetTimeBindingEntry(TextMenu menu, bool inGame) {
        menu.Add(new TextMenu.Button(Dialog.Clean(DialogIds.KeyImportTargetTimeId)).Pressed(() => {
                SpeebrunConsistencyTrackerModule.Instance.ImportTargetTimeFromClipboard();
            })
        );
    }

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

        [SettingRange(min: 0, max: 100), SettingName(DialogIds.TextSizeId)]
        public int TextSize { get; set; } = 65;

        public int TextOffsetX { get; set; } = 5;

        public int TextOffsetY { get; set; } = 0;

        [SettingName(DialogIds.TextPositionId)]
        public StatTextPosition TextPosition { get; set; } = StatTextPosition.TopLeft;
    }

    [SettingName(DialogIds.IngameOverlayId)]
    public IngameOverlaySubMenu IngameOverlay { get; set; } = new();

    [SettingSubMenu]
    public class StatsSubMenu {
        [SettingName(DialogIds.RunHistoryId)]
        public bool History { get; set; } = true;
        [SettingName(DialogIds.SuccessRateId), SettingSubText(DialogIds.SuccessRateSubTextId)]
        public MetricOutputChoice SuccessRate { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.TargetTimeStatId)]
        public MetricOutputChoice TargetTime { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.CompletedRunCountId)]
        public MetricOutputChoice CompletedRunCount { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.TotalRunCountId)]
        public MetricOutputChoice TotalRunCount { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.DnfCountId)]
        public MetricOutputChoice DnfCount { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.AverageId)]
        public MetricOutputChoice Average { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.MedianId)]
        public MetricOutputChoice Median { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.ResetRateId)]
        public MetricOutputChoice ResetRate { get; set; } = MetricOutputChoice.Export;
        [SettingName(DialogIds.ResetShareId)]
        public bool ResetShare { get; set; } = true;
        [SettingName(DialogIds.MinimumId)]
        public MetricOutputChoice Minimum { get; set; } = MetricOutputChoice.Export;
        [SettingName(DialogIds.MaximumId)]
        public MetricOutputChoice Maximum { get; set; } = MetricOutputChoice.Export;
        [SettingName(DialogIds.StandardDeviationId)]
        public MetricOutputChoice StandardDeviation { get; set; } = MetricOutputChoice.Both;
        [SettingName(DialogIds.CoefficientOfVariationId)]
        public MetricOutputChoice CoefficientOfVariation { get; set; } = MetricOutputChoice.Export;
        [SettingName(DialogIds.PercentileId)]
        public MetricOutputChoice Percentile { get; set; } = MetricOutputChoice.Export;
        [SettingName(DialogIds.PercentileValueId)]
        public PercentileChoice PercentileValue { get; set; } = PercentileChoice.P90;
        [SettingName(DialogIds.LinearRegressionId), SettingSubText(DialogIds.LinearRegressionSubTextId)]
        public bool LinearRegression { get; set; } = true;
        [SettingName(DialogIds.SoBId)]
        public MetricOutputChoice SoB { get; set; } = MetricOutputChoice.Both;
    }

    [SettingName(DialogIds.StatsSubMenuId)]
    public StatsSubMenu StatsMenu { get; set; } = new();

    [SettingIgnore]
    public ButtonBinding ExportStatsBinding { get; set; }

    public void CreateExportStatsBindingEntry(TextMenu menu, bool inGame) {
        if (!inGame) return;
        menu.Add(new TextMenu.Button(Dialog.Clean(DialogIds.KeyStatsExportId))
            .Pressed(SpeebrunConsistencyTrackerModule.Instance.ExportDataToCsv)
        );
    }

    [SettingName(DialogIds.SrtExportId)]
    public bool ExportWithSRT { get; set; } = true;
}