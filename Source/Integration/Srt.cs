using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Integration;

/// <summary>The SpeedrunTool every reader goes through. Resolved once, in the module's Load.</summary>
public static class Srt
{
    // Never null. Unit tests replace it with a stub.
    public static ISpeedrunTool Tool { get; internal set; } =
        new UnavailableSpeedrunTool("SpeedrunTool is not resolved yet: the module's Load has not run.");

    private static SpeedrunToolAdapter _attached;

    // Binds SpeedrunTool, registers the save-state callbacks and hooks the room timer. Never
    // throws: a failure leaves Tool unavailable, with the reason logged.
    public static void Load(
        Action<Dictionary<Type, Dictionary<string, object>>, Level> onSaveState,
        Action<Dictionary<Type, Dictionary<string, object>>, Level> onLoadState,
        Action onClearState,
        Action<Level> onBeforeLoadState,
        Action<bool> onUpdateTimerState)
    {
        ISpeedrunTool tool = SpeedrunToolAdapter.Resolve();
        if (tool is SpeedrunToolAdapter adapter)
        {
            string failure = adapter.Attach(onSaveState, onLoadState, onClearState, onBeforeLoadState, onUpdateTimerState);
            if (failure == null)
                _attached = adapter;
            else
                tool = new UnavailableSpeedrunTool(failure);
        }
        Tool = tool;
        if (!tool.Available)
            Logger.Log(LogLevel.Warn, nameof(SpeebrunConsistencyTracker),
                $"SpeedrunTool is unavailable, nothing will be recorded. {tool.UnavailableReason}");
    }

    public static void Unload()
    {
        _attached?.Detach();
        _attached = null;
        Tool = new UnavailableSpeedrunTool("The module is unloaded.");
    }
}
