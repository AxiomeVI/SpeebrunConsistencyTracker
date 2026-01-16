using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.StatsManager;
using Celeste.Mod.SpeedrunTool.Message;
using MonoMod.ModInterop;
using Microsoft.Xna.Framework;
using Monocle;

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
        // TODO: apply any hooks that should always be active
        On.Monocle.Engine.Update += Engine_Update;
        typeof(SaveLoadIntegration).ModInterop();
        SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
            StaticStatsManager.OnSaveState, 
            StaticStatsManager.OnLoadState, 
            StaticStatsManager.OnClearState, 
            null,
            StaticStatsManager.OnBeforeLoadState,
            null
        );
        typeof(RoomTimerIntegration).ModInterop();
        On.Celeste.Level.Update += LevelOnUpdate;
    }

    public override void Unload() {
        // TODO: unapply any hooks applied in Load()
        On.Monocle.Engine.Update -= Engine_Update;
        SaveLoadIntegration.Unregister(SaveLoadInstance);
        On.Celeste.Level.Update -= LevelOnUpdate;
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void Engine_Update(On.Monocle.Engine.orig_Update orig, Engine self, GameTime gameTime) {
        if (!Settings.Enabled) return;
        if (RoomTimerIntegration.RoomTimerIsCompleted()) {
            StaticStatsManager.AddSegmentTime(RoomTimerIntegration.GetRoomTime());
        }
        orig(self, gameTime);
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self){
        orig(self);
        if (Settings.Enabled && Settings.KeyStatsExport.Pressed) StaticStatsManager.ExportHotkey();
    }
}