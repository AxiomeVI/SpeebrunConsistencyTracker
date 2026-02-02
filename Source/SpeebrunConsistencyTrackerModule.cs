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
using FMOD.Studio;
using Celeste.Mod.SpeebrunConsistencyTracker.Menu;
using Monocle;
using System.Linq;
using Microsoft.Xna.Framework;

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

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
    {
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);
        ModMenuOptions.CreateMenu(menu, inGame);
        CreateModMenuSectionKeyBindings(menu, inGame, pauseSnapshot);
    }

    private static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void OnBeforeSaveState(Level level) {
        if (!Settings.Enabled)
            return;
        level.Entities.FindAll<TextOverlay>()
            .ToList()
            .ForEach(level.Entities.Remove);
        level.Entities.FindAll<GraphOverlay>()
            .ToList()
            .ForEach(level.Entities.Remove);
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled)
            return;
        SessionManager.OnSaveState();
    }

    public static void OnClearState()
    {
        if (!Settings.Enabled)
            return;
        SessionManager.OnClearState();
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
        level.Entities.FindAll<GraphOverlay>()
            .ToList()
            .ForEach(level.Entities.Remove);
    }


    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self){
        orig(self);
        if (!Settings.Enabled) return;

        if (Settings.ButtonKeyStatsExport.Pressed) ExportDataToCsv();

        if (Settings.ButtonKeyImportTargetTime.Pressed) Instance.ImportTargetTimeFromClipboard();

        if (Settings.ButtonKeyClearStats.Pressed) SessionManager.Reset();
        
        if (Settings.ButtonToggleGraphOverlay.Pressed && Settings.OverlayEnabled && SessionManager.CurrentSession != null) {
            List<GraphOverlay> graphs = self.Entities.FindAll<GraphOverlay>(); 
            if (graphs.Count > 0)
                self.Entities.Remove(graphs);
            else
            {
                GraphOverlay graph = new GraphOverlay(
                    [.. Enumerable.Range(0, SessionManager.CurrentSession.RoomCount).Select(i => SessionManager.CurrentSession.GetRoomTimes(i).ToList())],
                    [.. SessionManager.CurrentSession.GetSegmentTimes()],

                    new Vector2(160, 140)
                );
                self.Entities.Add(graph);
            }
        }

        //TextOverlay overlay = self.Entities.FindFirst<TextOverlay>();
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
            if (SessionManager.HasActiveAttempt)
            {
                SessionManager.EndCurrentAttempt();
            }
            if (Settings.OverlayEnabled) 
            {
                TextOverlay textOverlay = self.Entities.FindFirst<TextOverlay>(); 
                if (textOverlay == null)
                {
                    textOverlay = [];
                    self.Entities.Add(textOverlay);
                }
                textOverlay.SetText(MetricsExporter.ExportSessionToOverlay(SessionManager.CurrentSession));
            }
        }
    }

    private void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!Settings.Enabled) return;
        
        if (SessionManager.HasActiveAttempt)
        {
            long segmentTime = RoomTimerIntegration.GetRoomTime();
            if (segmentTime > 0 && playerIntro == Player.IntroTypes.Transition) {
                SessionManager.CompleteRoom(segmentTime);
            }
        }
    }

    public void Reset()
    {
        SessionManager.Reset();
        if (Engine.Scene is Level level)
        {
            level.Entities.FindAll<TextOverlay>().ToList().ForEach(level.Entities.Remove);
            level.Entities.FindAll<TextOverlay>().ToList().ForEach(level.Entities.Remove);
        }
    }

    public static void Init()
    {
        SessionManager.Reset();
    }

    public static void ExportDataToCsv()
    {
        if (!Settings.Enabled)
            return;
        if (SessionManager.CurrentSession?.TotalAttempts == 0)
        {
            PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }
        PracticeSession currentSession = SessionManager.CurrentSession;
        StringBuilder sb = new();
        if (Settings.ExportWithSRT)
        {
            // Clean current clipboard state in case srt export is done in file
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            sb.Append(TextInput.GetClipboardText());
            sb.Append("\n\n\n");
        }
        sb.Append(MetricsExporter.ExportSessionToCsv(currentSession));
        if (Settings.History)
        {
            sb.Append("\n\n\n");
            sb.Append(SessionHistoryCsvExporter.ExportSessionToCsv(currentSession));
        }
        TextInput.SetClipboardText(sb.ToString());
        PopupMessage(Dialog.Clean(DialogIds.PopupExportToClipBoardid));
    }

    public void ImportTargetTimeFromClipboard() {
        if (!Settings.Enabled)
            return;
        string input = TextInput.GetClipboardText();
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
        bool success = TimeSpan.TryParseExact(input, TimeFormats, null, out TimeSpan result);
        if (success) {
            Settings.Minutes = result.Minutes;
            Settings.Seconds = result.Seconds;
            Settings.MillisecondsFirstDigit = result.Milliseconds / 100;
            Settings.MillisecondsSecondDigit = result.Milliseconds / 10 % 10;
            Settings.MillisecondsThirdDigit = result.Milliseconds % 10;
            PopupMessage($"{Dialog.Clean(DialogIds.PopupTargetTimeSetid)} {result:mm\\:ss\\.fff}");
            SaveSettings();
        } else {
            PopupMessage($"{Dialog.Clean(DialogIds.PopupInvalidTargetTimeid)}");
        }
    }
}