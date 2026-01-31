using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.History;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics;
using MonoMod.ModInterop;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using System.Text;

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
        typeof(SaveLoadIntegration).ModInterop();
        SaveLoadInstance = SaveLoadIntegration.RegisterSaveLoadAction(
            OnSaveState, 
            OnLoadState, 
            OnClearState, 
            OnBeforeSaveState,
            OnBeforeLoadState,
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

    private static void OnBeforeSaveState(Level level) {
        if (!Settings.Enabled)
            return;
        level.Entities.FindAll<TextOverlay>().ForEach(overlay => {
            overlay.SetText("");
        });    
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled)
            return;
        SessionManager.OnSaveState();
        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), "New session started");
    }

    public static void OnClearState()
    {
        if (!Settings.Enabled)
            return;
        SessionManager.OnClearState();
        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), "Session cleared");
    }

    public static void OnBeforeLoadState(Level level)
    {
        if (!Settings.Enabled)
            return;
        SessionManager.OnBeforeLoadState();
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled)
            return;
        SessionManager.OnLoadState();
        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), "New attempt started");
    }


    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self){
        orig(self);
        if (!Settings.Enabled) return;

        if (Settings.ButtonKeyStatsExport.Pressed) Instance.ExportDataToCsv();

        if (Settings.ButtonKeyImportTargetTime.Pressed) Instance.ImportTargetTimeFromClipboard();

        if (Settings.ButtonKeyClearStats.Pressed) SessionManager.Reset();
        
        if (Settings.ButtonToggleIngameOverlay.Pressed) {
            var overlaySettings = Settings.IngameOverlay;
            overlaySettings.OverlayEnabled = !overlaySettings.OverlayEnabled;
            Instance.SaveSettings();
        }

        TextOverlay overlay = self.Entities.FindFirst<TextOverlay>();
        // if (RoomTimerIntegration.RoomTimerIsCompleted()) {
        //     // if (StaticStatsManager.runCompleted) {
        //     //     StaticStatsManager.AddSegmentTime(RoomTimerIntegration.GetRoomTime());
        //     //     // overlay?.SetTextVisible(Settings.IngameOverlay.OverlayEnabled);
        //     //     // if (overlay?.Visible == true) overlay?.SetText(StaticStatsManager.ToStringForOverlay());
        //     // } else {
        //     //     StaticStatsManager.AddRoomTime(RoomTimerIntegration.GetRoomTime());
        //     //     StaticStatsManager.runCompleted = true;
        //     // }
        // } else {
        //     // overlay?.SetTextVisible(false);
        // }
        if (RoomTimerIntegration.RoomTimerIsCompleted())
        {
            //SessionManager.CompleteRoom(RoomTimerIntegration.GetRoomTime());
            if (SessionManager.IsActive)
            {
                SessionManager.EndCurrentAttempt();
                if (overlay?.Visible == true) overlay?.SetText(MetricsExporter.ExportSessionToOverlay(SessionManager.CurrentSession));
                PracticeSession currentSession = SessionManager.CurrentSession;
                Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), "Attempt complete");
                Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"Total attempts: {currentSession.TotalAttempts}");
                Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"Total DNFs: {currentSession.TotalDnfs}");

                foreach (var attempt in currentSession.Attempts)
                {
                    Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"Attempt #{currentSession.TotalAttempts}: {attempt.Outcome}");
                    foreach (var room in attempt.CompletedRooms)
                        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"  Room {room.Key}: {room.Value} ticks");
                    if (attempt.DnfInfo != null)
                        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"  DNF at Room {attempt.DnfInfo.Room} ({attempt.DnfInfo.TimeIntoRoomTicks} ticks)");
                }

                Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"Current ExportSessionToCsv: \n{SessionHistoryCsvExporter.ExportSessionToCsv(currentSession)}");
            }
            overlay?.SetTextVisible(Settings.IngameOverlay.OverlayEnabled);
        } else {
            overlay?.SetTextVisible(false);
        }
    }

    private void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!Settings.Enabled) return;

        if (level.Entities.FindFirst<TextOverlay>() == null) level.Entities.Add(new TextOverlay());
        
        if (SessionManager.IsActive)
        {
            long segmentTime = RoomTimerIntegration.GetRoomTime();
            if (segmentTime > 0 && playerIntro == Player.IntroTypes.Transition) {
                SessionManager.CompleteRoom(segmentTime);
            }
        }
    }

    public void ExportDataToCsv()
    {
        if (!Settings.Enabled)
            return;
        PracticeSession currentSession = SessionManager.CurrentSession;
        if (currentSession.TotalAttempts == 0)
        {
            PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }
        StringBuilder sb = new StringBuilder();
        if (Settings.ExportWithSRT)
        {
            // Clean current clipboard state in case srt export is done in file
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            sb.Append(TextInput.GetClipboardText());
            sb.AppendLine();
            sb.AppendLine();
        }
        sb.Append(MetricsExporter.ExportSessionToCsv(currentSession));
        if (Settings.StatsMenu.History)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(SessionHistoryCsvExporter.ExportSessionToCsv(currentSession));
        }
        TextInput.SetClipboardText(sb.ToString());
        PopupMessage(Dialog.Clean(DialogIds.PopupExportToClipBoardid));
    }

    public void ImportTargetTimeFromClipboard() {
        if (!Settings.Enabled)
            return;
        string input = TextInput.GetClipboardText();
        TimeSpan result;
        string[] TimeFormats = [
                "mm\\:ss\\.fff", "m\\:ss\\.fff",
                "mm\\:ss\\.ff", "m\\:ss\\.ff",
                "mm\\:ss\\.f", "m\\:ss\\.f",
                "ss\\.fff", "s\\.fff",
                "ss\\.ff", "s\\.ff",
                "ss\\.f", "s\\.f",
                "mm\\:ss", "m\\:ss",
                "ss", "s",
                "fff"
            ];
        bool success = TimeSpan.TryParseExact(input, TimeFormats, null, out result);
        if (success) {
            var targetTimeSettings = Settings.TargetTime;
            targetTimeSettings.Minutes = result.Minutes;
            targetTimeSettings.Seconds = result.Seconds;
            targetTimeSettings.MillisecondsFirstDigit = result.Milliseconds / 100;
            targetTimeSettings.MillisecondsSecondDigit = result.Milliseconds / 10 % 10;
            targetTimeSettings.MillisecondsThirdDigit = result.Milliseconds % 10;
            PopupMessage($"{Dialog.Clean(DialogIds.PopupTargetTimeSetid)} {result:mm\\:ss\\.fff}");
            SaveSettings();
        } else {
            PopupMessage($"{Dialog.Clean(DialogIds.PopupInvalidTargetTimeid)}");
        }
    }
}