using MonoMod.ModInterop;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Integration;
[ModImportName("SpeedrunTool.RoomTimer")]
public static class RoomTimerIntegration
{
    public static Func<bool> RoomTimerIsCompleted;
    public static Func<long> GetRoomTime;
}

[ModImportName("SpeedrunTool.SaveLoad")]
public static class SaveLoadIntegration
{
    public static Func<Action<Dictionary<Type, Dictionary<string, object>>, Level>,
        Action<Dictionary<Type, Dictionary<string, object>>, Level>, Action,
        Action<Level>, Action<Level>, Action, object> RegisterSaveLoadAction;
    public static Action<object> Unregister;
}