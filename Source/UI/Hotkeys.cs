using Celeste.Mod.CelesteHotkeys;
using SctSettings = Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModuleSettings;

namespace Celeste.Mod.SpeebrunConsistencyTracker.UI;

// A new hotkey is a [SettingIgnore] ButtonBinding property on the settings and one row here. Row
// order is the order the remap screen lists them in.
internal static class Hotkeys {
    internal static readonly Keybind<SctSettings> ImportTargetTime = new(DialogIds.KeyImportTargetTimeId, nameof(SctSettings.Keybind_ImportTargetTime));
    internal static readonly Keybind<SctSettings> StatsExport      = new(DialogIds.KeyStatsExportId,      nameof(SctSettings.Keybind_StatsExport));
    internal static readonly Keybind<SctSettings> ToggleGraph      = new(DialogIds.ToggleGraphOverlayId,  nameof(SctSettings.Keybind_ToggleGraphOverlay));
    internal static readonly Keybind<SctSettings> NextGraph        = new(DialogIds.KeyNextGraphId,        nameof(SctSettings.Keybind_NextGraph));
    internal static readonly Keybind<SctSettings> PreviousGraph    = new(DialogIds.KeyPreviousGraphId,    nameof(SctSettings.Keybind_PreviousGraph));
    internal static readonly Keybind<SctSettings> ClearStats       = new(DialogIds.KeyClearStatsId,       nameof(SctSettings.Keybind_ClearStats));

    internal static readonly Keybind<SctSettings>[] All =
        [ImportTargetTime, StatsExport, ToggleGraph, NextGraph, PreviousGraph, ClearStats];

    internal static readonly HotkeySet<SctSettings> Set = new(() => SpeebrunConsistencyTrackerModule.Settings, All);

    internal static readonly KeybindScreenText Text = new() {
        HeaderId        = DialogIds.KeybindConfigId,
        ComboHintId     = DialogIds.KeybindComboSubId,
        PageComboHintId = DialogIds.KeybindPageComboFmt,
        ClearHintId     = DialogIds.KeybindClearSubId,
        TimeoutFormatId = DialogIds.KeybindTimeoutFmt,
    };

    internal static bool Pressed(Keybind<SctSettings> keybind) => Set.Pressed(keybind);
}
