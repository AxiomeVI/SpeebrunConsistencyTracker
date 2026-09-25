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

    private object SaveLoadInstance = null;

    private const string DefaultSlotName = "Default Slot";

    private static Hook _updateTimerStateHook;
    private static int _lastKnownRoomCount = 0;
    private static Func<long> _getCurrentRoomTime;


    public SpeebrunConsistencyTrackerModule() {
        Instance = this;
#if DEBUG
        Logger.SetLogLevel(nameof(SpeebrunConsistencyTracker), LogLevel.Verbose);
#else
        Logger.SetLogLevel(nameof(SpeebrunConsistencyTracker), LogLevel.Info);
#endif
    }

    // Everest calls this immediately before Load(), the first moment the deserialized settings
    // exist. OnLoadSettings is our own method, not a framework hook, so it is called by hand.
    public override void LoadSettings() {
        base.LoadSettings();
        (_Settings as SpeebrunConsistencyTrackerModuleSettings)?.OnLoadSettings();
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
            // Re-read on every call: SRT can replace the object mid-session, so a snapshot taken
            // at Load() goes stale. Either GetValue returns null while the timer is unset.
            _getCurrentRoomTime = () => {
                object currentRoomTimerData = currentRoomTimerDataField.GetValue(null);
                if (currentRoomTimerData == null) return 0L;
                object time = timeProperty.GetValue(currentRoomTimerData);
                return time == null ? 0L : (long)time;
            };
        }
        else if (currentRoomTimerDataField == null)
        {
            Logger.Log(LogLevel.Warn, nameof(SpeebrunConsistencyTracker),
                "SpeedrunTool member not found: RoomTimerManager.CurrentRoomTimerData (non-public static field). Room times cannot be read, every timing feature stays inert.");
        }
        else
        {
            Logger.Log(LogLevel.Warn, nameof(SpeebrunConsistencyTracker),
                $"SpeedrunTool member not found: property Time (public instance) on {currentRoomTimerDataField.FieldType.FullName}. Room times cannot be read, every timing feature stays inert.");
        }
        On.Celeste.Level.Update += LevelOnUpdate;
        On.Celeste.Level.Render += LevelOnRender;
        Everest.Events.Level.OnLoadLevel += OnLoadLevel;

        var updateTimerStateMethod = typeof(RoomTimerManager).GetMethod("UpdateTimerState", BindingFlags.Public | BindingFlags.Static);
        if (updateTimerStateMethod != null) {
            var updateTimerStateDetour = typeof(SpeebrunConsistencyTrackerModule)
                .GetMethod("OnUpdateTimerState", BindingFlags.NonPublic | BindingFlags.Static);
            if (updateTimerStateDetour == null) {
                Logger.Log(LogLevel.Warn, nameof(SpeebrunConsistencyTracker),
                    "Own member not found: SpeebrunConsistencyTrackerModule.OnUpdateTimerState (non-public static method). The room timer hook cannot be built.");
            } else {
                _updateTimerStateHook = new Hook(updateTimerStateMethod, updateTimerStateDetour);
            }
        } else {
            Logger.Log(LogLevel.Warn, nameof(SpeebrunConsistencyTracker),
                "SpeedrunTool member not found: RoomTimerManager.UpdateTimerState (public static method). The room timer hook is not installed, no room completion is ever recorded.");
        }
    }

    public override void Unload() {
        SaveLoadIntegration.Unregister(SaveLoadInstance);
        On.Celeste.Level.Update -= LevelOnUpdate;
        On.Celeste.Level.Render -= LevelOnRender;
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        Clear();
        _updateTimerStateHook?.Dispose();
        _updateTimerStateHook   = null;
        _getCurrentRoomTime = null;
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
        string slot = SaveLoadIntegration.GetSlotName?.Invoke() ?? DefaultSlotName;
        SessionManager.SaveSlot(slot);
        MetricsExporter.Clear();
        MetricEngine.Clear();
        GraphManager.Init();
        _lastKnownRoomCount = 0;
        TextOverlay.SetTextVisible(false);
        GraphManager.HideGraph();
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled)
            return;
        string slot = SaveLoadIntegration.GetSlotName?.Invoke() ?? DefaultSlotName;
        SessionManager.LoadSlot(slot);
        _lastKnownRoomCount = 0;
        TextOverlay.SetTextVisible(false);
        GraphManager.HideGraph();
    }

    public static void OnClearState()
    {
        if (!Settings.Enabled)
            return;
        string slot = SaveLoadIntegration.GetSlotName?.Invoke() ?? DefaultSlotName;
        SessionManager.ClearSlot(slot);
        _lastKnownRoomCount = 0;
    }


    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        // Polled before the Enabled check: off counts as a pause, so a combo held while the mod is
        // switched back on does not fire.
        UI.Hotkeys.Set.Update(Settings.Enabled);
        if (!Settings.Enabled) {
            orig(self);
            return;
        }

        if (UI.Hotkeys.Pressed(UI.Hotkeys.ImportTargetTime)) ImportTargetTimeFromClipboard();

        if (SessionManager.CurrentSession == null) {
            orig(self);
            return;
        }

        UpdateTextOverlay(self); // need to before orig() because of RoomTimerIntegration.RoomTimerIsCompleted() behavior

        orig(self);
        // orig(self) can destroy the session, so re-check.
        if (SessionManager.CurrentSession == null) return;

        HandleExportButton();
        HandleClearButton();
        UpdateGraphOverlay(self);
        HandlePauseHide(self);
        // LevelOnUpdate returned above when Settings.Enabled is false.
        if (GraphManager.IsShowing())
            GraphManager.UpdateInteractivity();
    }

    private static void LevelOnRender(On.Celeste.Level.orig_Render orig, Level self) {
        orig(self);
        if (Settings.Enabled && SessionManager.CurrentSession != null) {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Engine.ScreenMatrix);
            TextOverlay.Render();
            GraphManager.Render();
            Draw.SpriteBatch.End();
        }
    }

    private static void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!isFromLoader) return;
        // The hotkeys are polled from Level.Update only, so a combo pressed outside a level and
        // still held here would read as a fresh press -- and ClearStats wipes the session.
        UI.Hotkeys.Set.Resync();
        TextOverlay.Init();
        SessionManager.LevelName = Utility.LevelNames.ForExportFolder(
            level.Session.Area.GetSID(), (int)level.Session.Area.Mode);
    }

    private static void UpdateTextOverlay(Level _) {
        bool timerOff = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType == RoomTimerType.Off;
        bool timerCompleted = !timerOff && RoomTimerIntegration.RoomTimerIsCompleted();

        bool roomCountChanged = false;
        if (timerCompleted) {
            int prevRoomCount = SessionManager.RoomCount;
            SessionManager.UpdateRoomCount();
            roomCountChanged = SessionManager.RoomCount != prevRoomCount;
        }

        bool visible = timerCompleted && !roomCountChanged;
        TextOverlay.SetTextVisible(visible);
        if (visible) {
            if (MetricsExporter.RefreshTextOverlayIfNecessary(SessionManager.CurrentSession, out List<string> result))
                TextOverlay.SetText(result);
        }
    }

    private static void HandleExportButton() {
        if (UI.Hotkeys.Pressed(UI.Hotkeys.StatsExport))
        {
            if (Settings.ExportMode == ExportChoice.Clipboard)
                ExportDataToClipboard();
            else
                ExportDataToFiles();
        }
    }

    private static void HandleClearButton() {
        if (UI.Hotkeys.Pressed(UI.Hotkeys.ClearStats)) {
            // Counted before the clear: a single unconfirmed keypress wipes the session, and the
            // popup said only that it had happened, not how much it took.
            int attempts = SessionManager.CurrentSession?.TotalAttempts ?? 0;
            Clear();
            PopupMessage($"{Dialog.Clean(DialogIds.PopupDataClearId)} ({attempts} {(attempts == 1 ? "run" : "runs")})");
        }
    }

    private static void UpdateGraphOverlay(Level self) {
        if (UI.Hotkeys.Pressed(UI.Hotkeys.ToggleGraph) || GraphManager.IsShowing()) {
            SessionManager.UpdateRoomCount();
        }
        int currentRoomCount = SessionManager.RoomCount;

        if (currentRoomCount != _lastKnownRoomCount) {
            _lastKnownRoomCount = currentRoomCount;
            GraphManager.RebuildEnabledSlots();
        }

        if (UI.Hotkeys.Pressed(UI.Hotkeys.ToggleGraph)) {
            if (GraphManager.IsShowing())
                GraphManager.HideGraph();
            else if (!self.Paused)
                GraphManager.CurrentGraph();
        } else if (GraphManager.IsShowing())
        {
            if (UI.Hotkeys.Pressed(UI.Hotkeys.NextGraph))
                GraphManager.NextGraph();
            else if (UI.Hotkeys.Pressed(UI.Hotkeys.PreviousGraph))
                GraphManager.PreviousGraph();
        }
    }

    private static void HandlePauseHide(Level self) {
        if (self.Paused || self.wasPaused)
            GraphManager.HideGraph();
    }

    private static void OnUpdateTimerState(Action<bool> orig, bool endPoint) {
        if (Settings.Enabled && SessionManager.CurrentSession?.CurrentAttemptIndex >= 0) {
            long segmentTime = _getCurrentRoomTime?.Invoke() ?? 0;
            if (segmentTime > 0)
                SessionManager.CompleteRoom(segmentTime);
        }
        orig(endPoint);
    }

    public static void Clear()
    {
        MetricsExporter.Clear();
        MetricEngine.Clear();
        SessionManager.ClearAll();
        TextOverlay.Clear();
        GraphManager.Clear();
        _lastKnownRoomCount = 0;
    }

    public static void ExportDataToClipboard()
    {
        if (!Settings.Enabled) return;
        DataExporter.ExportToClipboard();
    }

    public static void ExportDataToFiles()
    {
        if (!Settings.Enabled) return;
        DataExporter.ExportToFiles();
    }

    public static void ImportTargetTimeFromClipboard() {
        if (!Settings.Enabled)
            return;
        string input = TextInput.GetClipboardText()?.Trim();
        // an empty clipboard must not silently zero the target time
        TimeSpan result = TimeSpan.Zero;
        bool success = !string.IsNullOrEmpty(input) && TimeParser.TryParseTime(input, out result);
        if (success) {
            Settings.SetTargetTime(result);
            PopupMessage($"{Dialog.Clean(DialogIds.PopupTargetTimeSetId)} {result:mm\\:ss\\.fff}");
            Instance.SaveSettings();
        } else {
            PopupMessage($"{Dialog.Clean(DialogIds.PopupInvalidTargetTimeId)}");
        }
    }
}
