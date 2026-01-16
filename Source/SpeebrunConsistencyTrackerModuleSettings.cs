using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SpeebrunConsistencyTracker;

[SettingName(DialogIds.SpeebrunConsistencyTracker)]
public class SpeebrunConsistencyTrackerModuleSettings : EverestModuleSettings {

    [SettingName(DialogIds.EnabledId)]
    public bool Enabled { get; set; } = true;

    [SettingName(DialogIds.KeyStatsExportId)]
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding KeyStatsExport { get; set; } = new(0, Keys.None);

    [SettingName(DialogIds.TargetTimeId), SettingRange(min: 0, max: 35295, largeRange: true)] // 35295f == 10min
    public int TargetTime { get; set; }

}