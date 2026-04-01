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

    public GraphManager graphManager;
    internal SessionManager sessionManager;
    private static Hook _updateTimerStateHook;

    private static UI.ComboHotkey _importTargetTimeHotkey;
    private static UI.ComboHotkey _statsExportHotkey;
    private static UI.ComboHotkey _toggleGraphHotkey;
    private static UI.ComboHotkey _nextGraphHotkey;
    private static UI.ComboHotkey _previousGraphHotkey;
    private static UI.ComboHotkey _clearStatsHotkey;

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
            null,
            null,
            null
        );
        typeof(RoomTimerIntegration).ModInterop();
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.Level.Render += LevelOnRender;
        Everest.Events.Level.OnExit += Level_OnLevelExit;
        Everest.Events.Level.OnLoadLevel += OnLoadLevel;

        var updateTimerStateMethod = typeof(RoomTimerManager).GetMethod("UpdateTimerState", BindingFlags.Public | BindingFlags.Static);
        if (updateTimerStateMethod != null) {
            _updateTimerStateHook = new Hook(
                updateTimerStateMethod,
                typeof(SpeebrunConsistencyTrackerModule).GetMethod("OnUpdateTimerState", BindingFlags.NonPublic | BindingFlags.Static)
            );
        }

        _importTargetTimeHotkey = new UI.ComboHotkey(Settings.ButtonKeyImportTargetTime);
        _statsExportHotkey      = new UI.ComboHotkey(Settings.ButtonKeyStatsExport);
        _toggleGraphHotkey      = new UI.ComboHotkey(Settings.ButtonToggleGraphOverlay);
        _nextGraphHotkey        = new UI.ComboHotkey(Settings.ButtonNextGraph);
        _previousGraphHotkey    = new UI.ComboHotkey(Settings.ButtonPreviousGraph);
        _clearStatsHotkey       = new UI.ComboHotkey(Settings.ButtonKeyClearStats);
    }

    public override void Unload() {
        SaveLoadIntegration.Unregister(SaveLoadInstance);
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.Level.Render -= LevelOnRender;
        Everest.Events.Level.OnExit -= Level_OnLevelExit;
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        Clear();
        _updateTimerStateHook?.Dispose();
        _updateTimerStateHook   = null;
        _importTargetTimeHotkey = null;
        _statsExportHotkey      = null;
        _toggleGraphHotkey      = null;
        _nextGraphHotkey        = null;
        _previousGraphHotkey    = null;
        _clearStatsHotkey       = null;
    }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance pauseSnapshot)
    {
        CreateModMenuSectionHeader(menu, inGame, pauseSnapshot);
        ModMenuOptions.CreateMenu(menu, inGame);
    }

    public static void PopupMessage(string message) {
        PopupMessageUtils.Show(message, null);
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

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled)
            return;
        Instance.sessionManager?.OnLoadState();
        TextOverlay.SetTextVisible(false);
    }


    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        if (!Settings.Enabled) {
            orig(self);
            return;
        }

        UI.ComboHotkey.UpdateStates();
        _importTargetTimeHotkey.Update();
        _statsExportHotkey.Update();
        _toggleGraphHotkey.Update();
        _nextGraphHotkey.Update();
        _previousGraphHotkey.Update();
        _clearStatsHotkey.Update();

        if (_importTargetTimeHotkey.Pressed) ImportTargetTimeFromClipboard();

        if (Instance.sessionManager == null) {
            orig(self);
            return;
        }

        UpdateTextOverlay(self); // need to before orig() because of RoomTimerIntegration.RoomTimerIsCompleted() behavior

        orig(self);
        // Need to check again because orig(self) can destroy the sessionManager
        if (Instance.sessionManager == null) return;

        HandleExportButton();
        HandleClearButton();
        UpdateGraphOverlay(self);
        HandlePauseHide(self);
    }

    private static void LevelOnRender(On.Celeste.Level.orig_Render orig, Level self) {
        orig(self);
        if (Settings.Enabled && Instance.sessionManager != null) {
            Draw.SpriteBatch.Begin();
            TextOverlay.Render();
            Draw.SpriteBatch.End();
        }
    }

    private static void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!isFromLoader) return;
        TextOverlay.Init();
    }

    private static void UpdateTextOverlay(Level _) {
        int prevRoomCount = Instance.sessionManager.RoomCount;
        Instance.sessionManager.UpdateRoomCount();
        bool roomCountChanged = Instance.sessionManager.RoomCount != prevRoomCount;

        bool visible = !roomCountChanged && RoomTimerIntegration.RoomTimerIsCompleted();
        TextOverlay.SetTextVisible(visible);
        if (visible) {
            if (MetricsExporter.RefreshTextOverlayIfNecessary(Instance.sessionManager.CurrentSession, out List<string> result))
                TextOverlay.SetText(result);
        }
    }

    private static void HandleExportButton() {
        if (_statsExportHotkey.Pressed)
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
        if (_clearStatsHotkey.Pressed) {
            Clear();
            PopupMessage(Dialog.Clean(DialogIds.PopupDataClearId));
        }
    }

    private static GraphManager BuildGraphManager(SessionManager mgr) {
        int segmentLength = mgr.RoomCount;
        List<List<TimeTicks>> rooms = [.. Enumerable.Range(0, segmentLength)
            .Select<int, List<TimeTicks>>(i => [.. mgr.CurrentSession.GetRoomTimes(i)])
            .Where(roomList => roomList.Count > 0)];
        List<TimeTicks> segment = [.. mgr.CurrentSession.GetSegmentTimes()];
        return new GraphManager(
            rooms, segment,
            mgr.CurrentSession.DnfPerRoom,
            mgr.CurrentSession.TotalAttemptsPerRoom,
            mgr.CurrentSession.Attempts,
            MetricHelper.IsMetricEnabled(Settings.TargetTime, MetricOutput.Overlay) ? MetricEngine.GetTargetTimeTicks() : null);
    }

    private static void UpdateGraphOverlay(Level self) {
        SessionManager activeSessionManager = Instance.sessionManager;
        activeSessionManager.UpdateRoomCount();
        int segmentLength = activeSessionManager.RoomCount;

        if (_toggleGraphHotkey.Pressed) {
            if (Instance.graphManager == null)
            {
                Instance.graphManager = BuildGraphManager(activeSessionManager);
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
            if (_nextGraphHotkey.Pressed)
            {
                Instance.graphManager.NextGraph(self);
            } else if (_previousGraphHotkey.Pressed)
            {
                Instance.graphManager.PreviousGraph(self);
            } else if (!Instance.graphManager.SameLength(segmentLength))
            {
                var (prevType, prevRoomIndex) = Instance.graphManager.GetCurrentSlot();
                bool wasShowing = Instance.graphManager.IsShowing();

                Instance.graphManager.RemoveGraphs();
                Instance.graphManager = BuildGraphManager(activeSessionManager);
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
            if (segmentTime > 0) {
                Instance.sessionManager.CompleteRoom(segmentTime);

                if (Instance.graphManager != null) {
                    Instance.sessionManager.UpdateRoomCount();
                    var (prevType, prevRoomIndex) = Instance.graphManager.GetCurrentSlot();
                    bool wasShowing = Instance.graphManager.IsShowing();

                    Instance.graphManager.RemoveGraphs();
                    Instance.graphManager = BuildGraphManager(Instance.sessionManager);
                    Instance.graphManager.RestoreSlot(prevType, prevRoomIndex);

                    if (wasShowing && Engine.Scene is Level level)
                        Instance.graphManager.CurrentGraph(level);
                }
            }
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
        TextOverlay.Clear();
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
