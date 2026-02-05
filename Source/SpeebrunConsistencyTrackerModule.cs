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
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Mono.Cecil;
using System.IO;

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

    private const long ONE_FRAME = 170000;

    private GraphManager graphManager;
    private TextOverlay textOverlay;

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

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
    }

    private static void OnBeforeSaveState(Level level) {
        if (!Settings.Enabled)
            return;
        Instance.textOverlay?.RemoveSelf();
        Instance.textOverlay = null;
        Instance.graphManager?.RemoveGraphs();
        Instance.graphManager = null;
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
        Instance.graphManager?.RemoveGraphs();
        Instance.graphManager = null;
    }


    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self){
        orig(self);
        if (!Settings.Enabled) return;

        if (Settings.ButtonKeyStatsExport.Pressed) ExportDataToClipboard();

        if (Settings.ButtonKeyImportTargetTime.Pressed) Instance.ImportTargetTimeFromClipboard();

        if (Settings.ButtonKeyClearStats.Pressed) SessionManager.Reset();
        
        if (Settings.ButtonToggleGraphOverlay.Pressed && Settings.OverlayEnabled && SessionManager.CurrentSession != null) {
            if (Instance.graphManager == null)
            {
                List<List<TimeTicks>> rooms = [.. Enumerable.Range(0, SessionManager.CurrentSession.RoomCount).Select(i => SessionManager.CurrentSession.GetRoomTimes(i).ToList())];
                List<TimeTicks> segment = [.. SessionManager.CurrentSession.GetSegmentTimes()];
                
                Instance.graphManager = new GraphManager(rooms, segment, MetricHelper.IsMetricEnabled(Settings.TargetTime, Enums.MetricOutput.Overlay) ? MetricEngine.GetTargetTimeTicks() : null);
                Instance.graphManager.NextGraph(self);
            }
            else if (Instance.graphManager.IsShowing())
            {
                Instance.graphManager.HideGraph();
            }
            else
            {
                Instance.graphManager.CurrentGraph(self);
            }
        }

        if (Settings.ButtonNextGraph.Pressed 
            && Settings.OverlayEnabled 
            && Instance.graphManager != null 
            && Instance.graphManager.IsShowing() 
            && SessionManager.CurrentSession != null)
        {
            Instance.graphManager.NextGraph(self);
        }

        if (Settings.ButtonPreviousGraph.Pressed 
            && Settings.OverlayEnabled 
            && Instance.graphManager != null 
            && Instance.graphManager.IsShowing() 
            && SessionManager.CurrentSession != null)
        {
            Instance.graphManager.PreviousGraph(self);
        }

        if (RoomTimerIntegration.RoomTimerIsCompleted())
        {
            if (SessionManager.HasActiveAttempt)
            {
                // Logic to take care of srt flags, and split buttons: https://gamebanana.com/mods/639197 and https://gamebanana.com/mods/619910
                if (SessionManager.EndOfChapterCutsceneSkipCounter >= 2)
                {
                    if (SessionManager.EndOfChapterCutsceneSkipCheck)
                        SessionManager.CompleteRoom(RoomTimerIntegration.GetRoomTime());
                    SessionManager.EndCurrentAttempt();
                }
                else
                {
                    if (SessionManager.EndOfChapterCutsceneSkipCounter == 0)
                    {
                        long currentTime = RoomTimerIntegration.GetRoomTime();
                        if (currentTime > SessionManager.CurrentSplitTime() + ONE_FRAME) 
                            SessionManager.CompleteRoom(RoomTimerIntegration.GetRoomTime());
                    }
                    SessionManager.EndOfChapterCutsceneSkipCounter ++;
                }
            }
            else if (Settings.OverlayEnabled) 
            {
                if (Instance.textOverlay == null)
                {
                    Instance.textOverlay = [];
                    self.Entities.Add(Instance.textOverlay);
                }
                if (MetricsExporter.ExportSessionToOverlay(SessionManager.CurrentSession, out string result))
                    Instance.textOverlay.SetText(result);
            }
        } else if (SessionManager.EndOfChapterCutsceneSkipCounter >= 1)
        {
            SessionManager.EndOfChapterCutsceneSkipCheck = true;
        }
    }

    private void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!Settings.Enabled) 
            return;

        if (isFromLoader)
            Init();
        
        if (SessionManager.HasActiveAttempt && playerIntro == Player.IntroTypes.Transition)
        {
            long segmentTime = RoomTimerIntegration.GetRoomTime();
            if (segmentTime > 0)
                SessionManager.CompleteRoom(segmentTime);
        }
    }

    public static void Reset()
    {
        SessionManager.Reset();
        Instance.textOverlay?.RemoveSelf();
        Instance.textOverlay = null;
        Instance.graphManager?.RemoveGraphs();
        Instance.graphManager = null;
    }

    public static void Init()
    {
        SessionManager.Reset();
        Instance.graphManager?.RemoveGraphs();
        Instance.graphManager = null;
    }

    public static void ExportDataToClipboard()
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

    public static void ExportDataToFiles()
    {
        if (!Settings.Enabled)
            return;

        if (SessionManager.CurrentSession?.TotalAttempts == 0)
        {
            PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        if (Settings.ExportWithSRT)
            RoomTimerManager.CmdExportRoomTimes();

        PracticeSession currentSession = SessionManager.CurrentSession;
        string baseFolder = Path.Combine(
            Everest.PathGame,
            "SpeebrunConsistencyTracker_DataExports",
            currentSession.levelName,
            currentSession.checkpoint
        );
        Directory.CreateDirectory(baseFolder);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_Metrics.csv")))
        {
            writer.WriteLine(MetricsExporter.ExportSessionToCsv(currentSession));
        }
        using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_History.csv")))
        {
            writer.WriteLine(SessionHistoryCsvExporter.ExportSessionToCsv(currentSession));
        }

        PopupMessage(Dialog.Clean(DialogIds.PopupExportToFileid));
    }

    public void ImportTargetTimeFromClipboard() {
        if (!Settings.Enabled)
            return;
        string input = TextInput.GetClipboardText()?.Trim();
        bool success = TryParseTime(input, out TimeSpan result);
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

    public static bool TryParseTime(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string[] timeFormats = [
            @"mm\:ss\.fff", @"m\:ss\.fff",
            @"mm\:ss\.ff",  @"m\:ss\.ff",
            @"mm\:ss\.f",   @"m\:ss\.f",
            @"mm\:ss",      @"m\:ss",
            @"ss\.fff",     @"s\.fff",
            @"ss\.ff",      @"s\.ff",
            @"ss\.f",       @"s\.f",
            @"ss",          @"s"
        ];

        bool success = TimeSpan.TryParseExact(input, timeFormats, 
            System.Globalization.CultureInfo.InvariantCulture, out result);

        // Fallback: If it's a pure number (e.g., "500"), treat as Milliseconds
        if (!success && int.TryParse(input, out int msResult))
        {
            result = TimeSpan.FromMilliseconds(msResult);
            success = true;
        }

        return success;
    }
}