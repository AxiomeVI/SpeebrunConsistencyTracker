using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeebrunConsistencyTracker.Export;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics;
using MonoMod.ModInterop;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using FMOD.Studio;
using Celeste.Mod.SpeebrunConsistencyTracker.Menu;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using MonoMod.RuntimeDetour;
using System.Reflection;
using System.Threading.Tasks;

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
        Logger.Log(LogLevel.Info, "OnBeforeSaveState", "Start");
        if (!Settings.Enabled)
            return;
        Instance.textOverlay?.RemoveSelf();
        Instance.textOverlay = null;
        Instance.graphManager?.RemoveGraphs();
        Instance.graphManager = null;
        Logger.Log(LogLevel.Info, "OnBeforeSaveState", "End");
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        Logger.Log(LogLevel.Info, "OnSaveState", "Start");
        if (!Settings.Enabled)
            return;
        Instance.sessionManager = new();
        MetricsExporter.Clear();
        MetricEngine.Clear();
        Logger.Log(LogLevel.Info, "OnSaveState", "End");
    }

    public static void OnClearState()
    {
        Logger.Log(LogLevel.Info, "OnClearState", "Start");
        if (!Settings.Enabled)
            return;
        Clear();
        Logger.Log(LogLevel.Info, "OnClearState", "End");
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


    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        if (!Settings.Enabled) {
            orig(self);
            return;
        }

        if (Settings.ButtonKeyImportTargetTime.Pressed) ImportTargetTimeFromClipboard();

        if (Instance.sessionManager == null) {
            orig(self);
            return;
        }

        UpdateTextOverlay(self); // before orig() because of RoomTimerIntegration.RoomTimerIsCompleted() behavior

        orig(self);
        // Need to check again because orig(self) can destroy the sessionManager
        if (Instance.sessionManager == null) return;

        HandleExportButton();
        HandleClearButton();
        UpdateGraphOverlay(self);
        HandlePauseHide(self);
    }

    private static void UpdateTextOverlay(Level self) {
        Logger.Log(LogLevel.Info, "UpdateTextOverlay", "Start");
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
            Logger.Log(LogLevel.Info, "UpdateTextOverlay", "Instance.textOverlay initialised");
        }
        else
        {
            Instance.textOverlay?.RemoveSelf();
            Instance.textOverlay = null;
            Logger.Log(LogLevel.Info, "UpdateTextOverlay", "Instance.textOverlay removed");
        }
        Logger.Log(LogLevel.Info, "UpdateTextOverlay", "End");
    }

    private static void HandleExportButton() {
        if (Settings.ButtonKeyStatsExport.Pressed)
        {
            if (Settings.ExportMode == ExportChoice.Clipboard)
                ExportDataToClipboard();
            else if (Settings.ExportMode == ExportChoice.File)
                ExportDataToFiles();
            else 
                ExportDataToSheet();
        }
    }

    private static void HandleClearButton() {
        if (Settings.ButtonKeyClearStats.Pressed) {
            Clear();
            PopupMessage(Dialog.Clean(DialogIds.PopupDataClearId));
        }
    }

    private static GraphManager BuildGraphManager(SessionManager mgr, int segmentLength) {
        List<List<TimeTicks>> rooms = [.. Enumerable.Range(0, segmentLength)
            .Select<int, List<TimeTicks>>(i => [.. mgr.CurrentSession.GetRoomTimes(i)])
            .Where(roomList => roomList.Count > 0)];
        List<TimeTicks> segment = [.. mgr.CurrentSession.GetSegmentTimes(segmentLength)];
        return new GraphManager(
            rooms, segment,
            mgr.CurrentSession.DnfPerRoom,
            mgr.CurrentSession.TotalAttemptsPerRoom,
            mgr.CurrentSession.Attempts,
            MetricHelper.IsMetricEnabled(Settings.TargetTime, MetricOutput.Overlay) ? MetricEngine.GetTargetTimeTicks() : null);
    }

    private static void UpdateGraphOverlay(Level self) {
        SessionManager activeSessionManager = Instance.sessionManager;
        int segmentLength = activeSessionManager.DynamicRoomCount();

        if (Settings.ButtonToggleGraphOverlay.Pressed) {
            if (Instance.graphManager == null)
            {
                Instance.graphManager = BuildGraphManager(activeSessionManager, segmentLength);
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
            } else if (!Instance.graphManager.SameLength(segmentLength))
            {
                var (prevType, prevRoomIndex) = Instance.graphManager.GetCurrentSlot();
                bool wasShowing = Instance.graphManager.IsShowing();

                Instance.graphManager.RemoveGraphs();
                Instance.graphManager = BuildGraphManager(activeSessionManager, segmentLength);
                Instance.graphManager.RestoreSlot(prevType, prevRoomIndex);

                if (!self.Paused && wasShowing)
                    Instance.graphManager.CurrentGraph(self);
            }
        }
    }

    private static void HandlePauseHide(Level self) {
        if (self.Paused || self.wasPaused)
            Instance.graphManager?.HideGraph();
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
        if (!Settings.Enabled) return;
        DataExporter.ExportToClipboard(Instance.sessionManager);
    }

    public static void ExportDataToFiles()
    {
        if (!Settings.Enabled) return;
        DataExporter.ExportToFiles(Instance.sessionManager);
    }

    public static async void ExportDataToSheet()
    {
        if (!Settings.Enabled) return;
        await DataExporter.ExportToSheet(Instance.sessionManager);
    }

    public static string SanitizeFileName(string input) => DataExporter.SanitizeFileName(input);

    public static void ImportTargetTimeFromClipboard() {
        if (!Settings.Enabled)
            return;
        string input = TextInput.GetClipboardText()?.Trim();
        bool success = TimeParser.TryParseTime(input, out TimeSpan result);
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
}
