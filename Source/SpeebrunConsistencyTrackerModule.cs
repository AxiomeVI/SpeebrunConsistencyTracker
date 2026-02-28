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
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using System.IO;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using MonoMod.RuntimeDetour;
using System.Reflection;

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

    public GraphManager graphManager;
    public TextOverlay textOverlay;
    private SessionManager sessionManager;
    private static Hook _updateTimerStateHook;

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
        Everest.Events.Level.OnExit += Level_OnLevelExit;

        var updateTimerStateMethod = typeof(RoomTimerManager).GetMethod("UpdateTimerState", BindingFlags.Public | BindingFlags.Static);
        if (updateTimerStateMethod != null) {
            _updateTimerStateHook = new Hook(
                updateTimerStateMethod,
                typeof(SpeebrunConsistencyTrackerModule).GetMethod("OnUpdateTimerState", BindingFlags.NonPublic | BindingFlags.Static)
            );
        }
    }

    public override void Unload() {
        SaveLoadIntegration.Unregister(SaveLoadInstance);
        On.Celeste.Level.Update -= LevelOnUpdate;
        Everest.Events.Level.OnExit -= Level_OnLevelExit;
        Clear();
        _updateTimerStateHook?.Dispose();
        _updateTimerStateHook = null;
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
        Instance.sessionManager = new();
        MetricsExporter.Clear();
        MetricEngine.Clear();
    }

    public static void OnClearState()
    {
        if (!Settings.Enabled)
            return;
        Clear();
    }

    public static void OnBeforeLoadState(Level level)
    {
        if (!Settings.Enabled)
            return;
        Instance.graphManager?.RemoveGraphs();
        Instance.graphManager = null;
        Instance.textOverlay?.RemoveSelf();
        Instance.textOverlay = null;
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled)
            return;
        Instance.sessionManager?.OnLoadState();
    }


    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self){
        if (!Settings.Enabled) {
            orig(self);
            return;
        }

        if (Settings.ButtonKeyImportTargetTime.Pressed) ImportTargetTimeFromClipboard();

        if (Instance.sessionManager == null)
        {
            orig(self);
            return;
        }

        if (RoomTimerIntegration.RoomTimerIsCompleted() && Settings.OverlayEnabled)
        {
            if (Instance.textOverlay == null)
            {
                Instance.textOverlay = [];
                self.Entities.Add(Instance.textOverlay);
            }
            if (MetricsExporter.TryExportSessionToOverlay(Instance.sessionManager.CurrentSession, Instance.sessionManager.DynamicRoomCount(), out List<string> result))
            {
                Instance.textOverlay.SetText(result); 
            }
        }
        else
        {
            Instance.textOverlay?.RemoveSelf();
            Instance.textOverlay = null;
        }

        orig(self);
        // Need to check again because orig(self) can destroy the sessionManager
        if (Instance.sessionManager == null) return;

        if (Settings.ButtonKeyStatsExport.Pressed) 
        {
            if (Settings.ExportMode == ExportChoice.Clipboard)
                ExportDataToClipboard();
            else
                ExportDataToFiles();
        }


        if (Settings.ButtonKeyClearStats.Pressed) {
            Clear();
            PopupMessage(Dialog.Clean(DialogIds.PopupDataClearId));
        }
        
        SessionManager activeSessionManager = Instance.sessionManager;
        int segmentLength = activeSessionManager.DynamicRoomCount();
        if (Settings.ButtonToggleGraphOverlay.Pressed) {
            if (Instance.graphManager == null)
            {
                List<List<TimeTicks>> rooms = [.. Enumerable.Range(0, segmentLength).Select<int, List<TimeTicks>>(i => [.. activeSessionManager.CurrentSession.GetRoomTimes(i)]).Where(roomList => roomList.Count > 0)];
                List<TimeTicks> segment = [.. activeSessionManager.CurrentSession.GetSegmentTimes(segmentLength)];
                Instance.graphManager = new GraphManager(rooms, segment, activeSessionManager.CurrentSession.DnfPerRoom, activeSessionManager.CurrentSession.TotalAttemptsPerRoom, MetricHelper.IsMetricEnabled(Settings.TargetTime, MetricOutput.Overlay) ? MetricEngine.GetTargetTimeTicks() : null);
                if (!self.Paused)
                    Instance.graphManager.CurrentGraph(self);
            }
            else if (Instance.graphManager.IsShowing())
            {
                Instance.graphManager.HideGraph();
            }
            else if (!self.Paused)
            {
                Instance.graphManager.CurrentGraph(self);
            }
        } else if (Instance.graphManager != null && Instance.graphManager.IsShowing())
        {
            if (Settings.ButtonNextGraph.Pressed)
            {
                Instance.graphManager.NextGraph(self);
            } else if (Settings.ButtonPreviousGraph.Pressed)
            {
                Instance.graphManager.PreviousGraph(self);
            } else if (!Instance.graphManager.SameSettings(segmentLength))
            {
                List<List<TimeTicks>> rooms = [.. Enumerable.Range(0, segmentLength)
                    .Select<int, List<TimeTicks>>(i => [.. activeSessionManager.CurrentSession.GetRoomTimes(i)])
                    .Where(roomList => roomList.Count > 0)];
                List<TimeTicks> segment = [.. activeSessionManager.CurrentSession.GetSegmentTimes(segmentLength)];

                var (prevType, prevRoomIndex) = Instance.graphManager.GetCurrentSlot();
                bool wasShowing = Instance.graphManager.IsShowing();

                Instance.graphManager.RemoveGraphs();
                Instance.graphManager = new GraphManager(
                    rooms, segment,
                    activeSessionManager.CurrentSession.DnfPerRoom,
                    activeSessionManager.CurrentSession.TotalAttemptsPerRoom,
                    MetricHelper.IsMetricEnabled(Settings.TargetTime, MetricOutput.Overlay) ? MetricEngine.GetTargetTimeTicks() : null);

                Instance.graphManager.RestoreSlot(prevType, prevRoomIndex);

                if (!self.Paused && wasShowing)
                    Instance.graphManager.CurrentGraph(self);
            }
        }

        if (self.Paused || self.wasPaused)
        {
            Instance.graphManager?.HideGraph();
        }
    }

    private static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (Settings.Enabled && Instance.sessionManager != null && Instance.sessionManager.HasActiveAttempt) {
            long segmentTime = RoomTimerIntegration.GetRoomTime();
            if (segmentTime > 0)
                Instance.sessionManager.CompleteRoom(segmentTime);
        }
        orig(endPoint);
    }

    private static void Level_OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
        Clear();
    }

    public static void Clear()
    {
        MetricsExporter.Clear();
        MetricEngine.Clear();
        Instance.sessionManager = null;
        Instance.textOverlay?.RemoveSelf();
        Instance.textOverlay = null;
        Instance.graphManager?.Dispose();
        Instance.graphManager = null;
    }

    public static void ExportDataToClipboard()
    {
        if (!Settings.Enabled)
            return;

        SessionManager activeSessionManager = Instance.sessionManager;
        if (activeSessionManager == null || activeSessionManager.CurrentSession?.TotalAttempts == 0)
        {
            PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        PracticeSession currentSession = activeSessionManager.CurrentSession;
        int roomCount = activeSessionManager.DynamicRoomCount();
        StringBuilder sb = new();
        if (Settings.ExportWithSRT)
        {
            // Clean current clipboard state in case srt export is done in file
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            sb.Append(TextInput.GetClipboardText());
            sb.Append("\n\n\n");
        }
        sb.Append(MetricsExporter.ExportSessionToCsv(currentSession, roomCount));
        if (Settings.History)
        {
            sb.Append("\n\n\n");
            sb.Append(SessionHistoryCsvExporter.ExportSessionToCsv(currentSession, roomCount));
        }
        TextInput.SetClipboardText(sb.ToString());
        PopupMessage(Dialog.Clean(DialogIds.PopupExportToClipBoardid));
    }

    public static void ExportDataToFiles()
    {
        if (!Settings.Enabled)
            return;

        SessionManager activeSessionManager = Instance.sessionManager;
        if (activeSessionManager == null || activeSessionManager.CurrentSession?.TotalAttempts == 0)
        {
            PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        PracticeSession currentSession = activeSessionManager.CurrentSession;
        int roomCount = activeSessionManager.DynamicRoomCount();

        if (Settings.ExportWithSRT)
            RoomTimerManager.CmdExportRoomTimes();

        string baseFolder = Path.Combine(
            Everest.PathGame,
            "SCT_Exports",
            SanitizeFileName(currentSession.levelName)
        );
        Directory.CreateDirectory(baseFolder);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_Metrics.csv")))
        {
            writer.WriteLine(MetricsExporter.ExportSessionToCsv(currentSession, roomCount));
        }
        using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_History.csv")))
        {
            writer.WriteLine(SessionHistoryCsvExporter.ExportSessionToCsv(currentSession, roomCount));
        }

        PopupMessage(Dialog.Clean(DialogIds.PopupExportToFileid));
    }

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
        var sanitized = new string(
            [.. input.Where(ch => !invalidChars.Contains(ch))]
        );
        return sanitized.TrimEnd(' ', '.');
    }

    public static void ImportTargetTimeFromClipboard() {
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
            Instance.SaveSettings();
        } else {
            PopupMessage($"{Dialog.Clean(DialogIds.PopupInvalidTargetTimeid)}");
        }
    }

    public static bool TryParseTime(string input, out TimeSpan result)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            result = TimeSpan.Zero;
            return true;
        }

        // Handle pure zero inputs before any trimming
        if (input.Trim() == "0" || input.Trim() == "00")
        {
            result = TimeSpan.Zero;
            return true;
        }

        string[] timeFormats = [
            @"mm\:ss\.fff", @"m\:ss\.fff",
            @"mm\:ss\.ff",  @"m\:ss\.ff",
            @"mm\:ss\.f",   @"m\:ss\.f",
            @"mm\:ss",      @"m\:ss",
            @"ss\.fff",     @"s\.fff",
            @"ss\.ff",      @"s\.ff",
            @"ss\.f",       @"s\.f",
            @"ss",          @"s",
            @"\.fff",       @"\.ff",       @"\.f"
        ];

        string trimmed = input.TrimStart('0', ':');
        if (string.IsNullOrEmpty(trimmed)) 
        {
            result = TimeSpan.Zero;
            return true;
        }

        bool success = TimeSpan.TryParseExact(
            trimmed, timeFormats,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);

        // Fallback: pure number treated as milliseconds
        if (!success && int.TryParse(input, out int msResult))
        {
            result = TimeSpan.FromMilliseconds(msResult);
            success = true;
        }

        return success;
    }
}
