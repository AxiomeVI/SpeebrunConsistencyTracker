using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Mod.SpeedrunTool;
using Celeste.Mod.SpeedrunTool.Message;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using MonoMod.ModInterop;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Integration;

// The one class that names SpeedrunTool's types. An instance exists only once
// SpeedrunToolMembers.FindMissing has found every member, so its methods are JIT-compiled against
// a SpeedrunTool that has them. Resolve itself names no SpeedrunTool type: a method that did would
// throw on JIT, before its own try could run.
internal sealed class SpeedrunToolAdapter : ISpeedrunTool
{
    private readonly FieldInfo _currentRoomTimerData;
    private readonly PropertyInfo _time;
    private readonly MethodInfo _updateTimerState;

    private object _saveLoadHandle;
    private Hook _updateTimerStateHook;
    private Action<bool> _onUpdateTimerState;

    private SpeedrunToolAdapter(FieldInfo currentRoomTimerData, PropertyInfo time, MethodInfo updateTimerState)
    {
        _currentRoomTimerData = currentRoomTimerData;
        _time = time;
        _updateTimerState = updateTimerState;
    }

    // Never throws. Returns an UnavailableSpeedrunTool with the reason when anything is missing.
    internal static ISpeedrunTool Resolve()
    {
        try
        {
            EverestModule module = Everest.Modules.FirstOrDefault(m => m.Metadata?.Name == "SpeedrunTool");
            if (module == null)
                return new UnavailableSpeedrunTool("SpeedrunTool is not loaded.");
            Assembly assembly = module.GetType().Assembly;
            string version = $"SpeedrunTool {module.Metadata.Version}";

            List<string> missing = SpeedrunToolMembers.FindMissing(assembly);
            if (missing.Count > 0)
                return new UnavailableSpeedrunTool($"{version} lacks: {string.Join("; ", missing)}.");

            typeof(SaveLoadIntegration).ModInterop();
            typeof(RoomTimerIntegration).ModInterop();
            if (SaveLoadIntegration.RegisterSaveLoadAction == null || SaveLoadIntegration.Unregister == null)
                return new UnavailableSpeedrunTool($"{version} exports no SpeedrunTool.SaveLoad RegisterSaveLoadAction and Unregister.");
            if (RoomTimerIntegration.RoomTimerIsCompleted == null)
                return new UnavailableSpeedrunTool($"{version} exports no SpeedrunTool.RoomTimer RoomTimerIsCompleted.");

            Type manager = assembly.GetType(SpeedrunToolMembers.ManagerType);
            FieldInfo data = manager.GetField(SpeedrunToolMembers.CurrentRoomTimerData, SpeedrunToolMembers.NonPublicStatic);
            var adapter = new SpeedrunToolAdapter(
                data,
                data.FieldType.GetProperty(SpeedrunToolMembers.Time, SpeedrunToolMembers.PublicInstance),
                manager.GetMethod(SpeedrunToolMembers.UpdateTimerState, SpeedrunToolMembers.PublicStatic, null, [typeof(bool)], null));
            Compile();
            return adapter;
        }
        catch (Exception e)
        {
            return new UnavailableSpeedrunTool($"Binding SpeedrunTool failed: {e.GetType().Name}: {e.Message}");
        }
    }

    // JIT-compiles every method that names a SpeedrunTool member, without running it, so a binding
    // the name check missed fails here rather than in Level.Update.
    private static void Compile()
    {
        foreach (string name in new[] { nameof(ReadTimerType), nameof(ReadNumberOfRooms), nameof(ExportRoomTimesNow), nameof(PopupNow) })
            RuntimeHelpers.PrepareMethod(
                typeof(SpeedrunToolAdapter).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).MethodHandle);
    }

    // Null on success. On failure, whatever was attached is detached again.
    internal string Attach(
        Action<Dictionary<Type, Dictionary<string, object>>, Level> onSaveState,
        Action<Dictionary<Type, Dictionary<string, object>>, Level> onLoadState,
        Action onClearState,
        Action<Level> onBeforeLoadState,
        Action<bool> onUpdateTimerState)
    {
        try
        {
            _saveLoadHandle = SaveLoadIntegration.RegisterSaveLoadAction(
                onSaveState, onLoadState, onClearState, null, onBeforeLoadState, null);
            _onUpdateTimerState = onUpdateTimerState;
            _updateTimerStateHook = new Hook(_updateTimerState, (Action<Action<bool>, bool>)OnUpdateTimerState);
            return null;
        }
        catch (Exception e)
        {
            Detach();
            return $"Attaching to SpeedrunTool failed: {e.GetType().Name}: {e.Message}";
        }
    }

    internal void Detach()
    {
        if (_saveLoadHandle != null) SaveLoadIntegration.Unregister(_saveLoadHandle);
        _saveLoadHandle = null;
        _updateTimerStateHook?.Dispose();
        _updateTimerStateHook = null;
        _onUpdateTimerState = null;
    }

    // Called before SpeedrunTool's own UpdateTimerState, with its argument: the room it reads is
    // the one being left.
    private void OnUpdateTimerState(Action<bool> orig, bool endPoint)
    {
        _onUpdateTimerState?.Invoke(endPoint);
        orig(endPoint);
    }

    public bool Available => true;
    public string UnavailableReason => null;

    public RoomTimerMode TimerType => ReadTimerType();
    public int NumberOfRooms => ReadNumberOfRooms();

    // Re-read on every call: SpeedrunTool can replace the object mid-session, so a snapshot taken
    // at Load goes stale. Either read is null while the timer is unset.
    public long CurrentRoomTime
    {
        get
        {
            object data = _currentRoomTimerData.GetValue(null);
            if (data == null) return 0L;
            object time = _time.GetValue(data);
            return time == null ? 0L : (long)time;
        }
    }

    public bool TimerCompleted => RoomTimerIntegration.RoomTimerIsCompleted();
    public string SlotName => SaveLoadIntegration.GetSlotName?.Invoke();

    public void ExportRoomTimes() => ExportRoomTimesNow();
    public void Popup(string message) => PopupNow(message);

    // Each SpeedrunTool member in a method of its own, which Compile names.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static RoomTimerMode ReadTimerType() => (RoomTimerMode)(int)SpeedrunToolSettings.Instance.RoomTimerType;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadNumberOfRooms() => SpeedrunToolSettings.Instance.NumberOfRooms;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExportRoomTimesNow() => RoomTimerManager.CmdExportRoomTimes();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PopupNow(string message) => PopupMessageUtils.Show(message, null);
}
