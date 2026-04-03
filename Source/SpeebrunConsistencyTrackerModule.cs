using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
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
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
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

    internal SessionManager sessionManager;
    private static Hook _updateTimerStateHook;
    private static int _lastKnownRoomCount = 0;
    private static Func<long> _getCurrentRoomTime;

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
        var currentRoomTimerDataField = typeof(RoomTimerManager)
            .GetField("CurrentRoomTimerData", BindingFlags.NonPublic | BindingFlags.Static);
        var timeProperty = currentRoomTimerDataField?.FieldType
            .GetProperty("Time", BindingFlags.Public | BindingFlags.Instance);
        if (currentRoomTimerDataField != null && timeProperty != null)
        {
            var instance = currentRoomTimerDataField.GetValue(null);
            _getCurrentRoomTime = () => (long)timeProperty.GetValue(instance);
        }
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
        _getCurrentRoomTime = null;
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
        Clear();
        Instance.sessionManager = new();
        MetricsExporter.Clear();
        MetricEngine.Clear();
        GraphManager.Init(Instance.sessionManager);
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
        GraphManager.HideGraph();
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
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Engine.ScreenMatrix);
            TextOverlay.Render();
            GraphManager.Render();
            Draw.SpriteBatch.End();
        }
    }

    private static void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!isFromLoader) return;
        TextOverlay.Init();
    }

    private static void UpdateTextOverlay(Level _) {
        bool timerOff = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType == RoomTimerType.Off;
        bool timerCompleted = !timerOff && RoomTimerIntegration.RoomTimerIsCompleted();

        bool roomCountChanged = false;
        if (timerCompleted) {
            int prevRoomCount = Instance.sessionManager.RoomCount;
            Instance.sessionManager.UpdateRoomCount();
            roomCountChanged = Instance.sessionManager.RoomCount != prevRoomCount;
        }

        bool visible = timerCompleted && !roomCountChanged;
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

    private static void UpdateGraphOverlay(Level self) {
        bool timerOff = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType == RoomTimerType.Off;
        if (timerOff) {
            if (GraphManager.IsShowing())
                GraphManager.HideGraph();
            return;
        }
        if (_toggleGraphHotkey.Pressed || GraphManager.IsShowing()) {
            Instance.sessionManager.UpdateRoomCount();
        }
        int currentRoomCount = Instance.sessionManager.RoomCount;

        if (currentRoomCount > _lastKnownRoomCount) {
            _lastKnownRoomCount = currentRoomCount;
            GraphManager.RebuildEnabledSlots();
        }

        if (_toggleGraphHotkey.Pressed) {
            if (GraphManager.IsShowing())
                GraphManager.HideGraph();
            else if (!self.Paused)
                GraphManager.CurrentGraph();
        } else if (GraphManager.IsShowing())
        {
            if (_nextGraphHotkey.Pressed)
                GraphManager.NextGraph();
            else if (_previousGraphHotkey.Pressed)
                GraphManager.PreviousGraph();
        }
    }

    private static void HandlePauseHide(Level self) {
        if (self.Paused || self.wasPaused)
            GraphManager.HideGraph();
    }

    private static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (Settings.Enabled && Instance.sessionManager != null && Instance.sessionManager.HasActiveAttempt) {
            long segmentTime = _getCurrentRoomTime?.Invoke() ?? 0;
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
        TextOverlay.Clear();
        GraphManager.Clear();
        _lastKnownRoomCount = 0;
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
