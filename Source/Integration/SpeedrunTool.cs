using Monocle;
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
    public static Action<Entity, bool> IgnoreSaveState;
}

// The current slot's name moved between exports in SpeedrunTool 3.29.2. Bind both; the adapter
// takes whichever SpeedrunTool filled. With neither, every slot shares one session.
[ModImportName("SpeedrunTool.SaveLoadControl")]
public static class SaveLoadControlIntegration
{
    public static Func<string> GetSlotName;  // SRT >= 3.29.2
}

[ModImportName("SpeedrunTool.SaveLoad")]
public static class LegacySlotNameIntegration
{
    public static Func<string> GetSlotName;  // SRT 3.25.0 to 3.29.1
}