using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.StatsManager;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeedrunTool.Message;
using MonoMod.ModInterop;

namespace Celeste.Mod.SpeebrunConsistencyTracker;

public class SpeebrunConsistencyTrackerModule : EverestModule {
    public static SpeebrunConsistencyTrackerModule Instance { get; private set; }

    public override Type SettingsType => typeof(SpeebrunConsistencyTrackerModuleSettings);
    public static SpeebrunConsistencyTrackerModuleSettings Settings => (SpeebrunConsistencyTrackerModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(SpeebrunConsistencyTrackerModuleSession);
    public static SpeebrunConsistencyTrackerModuleSession Session => (SpeebrunConsistencyTrackerModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(SpeebrunConsistencyTrackerModuleSaveData);
    public static SpeebrunConsistencyTrackerModuleSaveData SaveData => (SpeebrunConsistencyTrackerModuleSaveData) Instance._SaveData;

    private object SaveLoadInstance = null;

    public TextOverlay IngameOverlay;

    public SpeebrunConsistencyTrackerModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(SpeebrunConsistencyTrackerModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(SpeebrunConsistencyTrackerModule), LogLevel.Info);
#endif
    }

    public override void Load() {
        typeof(SaveLoadIntegration).ModInterop();
        SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
            StaticStatsManager.OnSaveState, 
            StaticStatsManager.OnLoadState, 
            StaticStatsManager.OnClearState, 
            StaticStatsManager.OnBeforeSaveState,
            StaticStatsManager.OnBeforeLoadState,
            null
        );
        typeof(RoomTimerIntegration).ModInterop();
        On.Celeste.Level.Update += LevelOnUpdate;
        Everest.Events.Level.OnLoadLevel += Level_OnLoadLevel;
    }

    public override void Unload() {
        SaveLoadIntegration.Unregister(SaveLoadInstance);
        On.Celeste.Level.Update -= LevelOnUpdate;
        Everest.Events.Level.OnLoadLevel -= Level_OnLoadLevel;
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self){
        orig(self);
        if (!Settings.Enabled) return;

        if (Settings.ButtonKeyStatsExport.Pressed) StaticStatsManager.ExportHotkey();

        if (Settings.ButtonToggleIngameOverlay.Pressed) {
            var overlaySettings = Settings.IngameOverlay;
            overlaySettings.OverlayEnabled = !overlaySettings.OverlayEnabled;
            Instance.SaveSettings();
        }

        TextOverlay overlay = self.Entities.FindFirst<TextOverlay>();
        if (RoomTimerIntegration.RoomTimerIsCompleted()) {
            StaticStatsManager.AddSegmentTime(RoomTimerIntegration.GetRoomTime());
            overlay?.SetTextVisible(Settings.IngameOverlay.OverlayEnabled);
            if (overlay?.Visible == true) overlay?.SetText(StaticStatsManager.ToStringForOverlay());
        } else if (self.PauseMainMenuOpen && Settings.IngameOverlay.ShowInPauseMenu) {
            overlay?.SetTextVisible(Settings.IngameOverlay.OverlayEnabled);
            if (overlay?.Visible == true) overlay?.SetText(StaticStatsManager.ToStringForOverlay());
        } else {
            overlay?.SetTextVisible(false);
        }
    }

    private void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!Settings.Enabled) return;
        if (level.Entities.FindFirst<TextOverlay>() == null) level.Entities.Add(new TextOverlay());
    }
}