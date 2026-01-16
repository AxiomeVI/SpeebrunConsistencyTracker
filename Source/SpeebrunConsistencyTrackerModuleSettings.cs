using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SpeebrunConsistencyTracker;

[SettingName(DialogIds.SpeebrunConsistencyTracker)]
public class SpeebrunConsistencyTrackerModuleSettings : EverestModuleSettings {

    [SettingName(DialogIds.EnabledId)]
    public bool Enabled { get; set; } = true;

    [SettingName(DialogIds.KeyStatsExportId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding KeyStatsExport { get; set; } = new(0, Keys.None);

    [SettingSubMenu]
    public class TargetTimeSubMenu {
        [SettingName(DialogIds.Minutes), SettingRange(min: 0, max: 30)]
        public int Minutes { get; set; } = 0;

        [SettingName(DialogIds.Seconds), SettingRange(min: 0, max: 59, largeRange: true)]
        public int Seconds { get; set; } = 0;

        [SettingName(DialogIds.MillisecondsFirst), SettingRange(min: 0, max: 9, largeRange: true)]
        public int MillisecondsFirstDigit { get; set; } = 0;
        [SettingName(DialogIds.MillisecondsSecond), SettingRange(min: 0, max: 9, largeRange: true)]
        public int MillisecondsSecondDigit { get; set; } = 0;
        [SettingName(DialogIds.MillisecondsThird), SettingRange(min: 0, max: 9, largeRange: true)]
        public int MillisecondsThirdDigit { get; set; } = 0;
    }

    [SettingName(DialogIds.TargetTimeId)]
    public TargetTimeSubMenu TargetTime { get; set; } = new();

}