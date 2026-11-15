using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Integration;

// Every SpeedrunTool type and member SpeedrunToolAdapter binds, by name, so they can be checked
// before any method naming them is JIT-compiled. The same check runs in Load against the
// installed SpeedrunTool and in a unit test against libs/SpeedrunTool.dll.
//
// A member SpeedrunToolAdapter starts to use goes here too, or the check passes and the adapter
// throws instead of reporting it.
internal static class SpeedrunToolMembers
{
    internal const string SettingsType = "Celeste.Mod.SpeedrunTool.SpeedrunToolSettings";
    internal const string TimerTypeEnum = "Celeste.Mod.SpeedrunTool.RoomTimer.RoomTimerType";
    internal const string ManagerType = "Celeste.Mod.SpeedrunTool.RoomTimer.RoomTimerManager";
    internal const string PopupType = "Celeste.Mod.SpeedrunTool.Message.PopupMessageUtils";
    internal const string EndPointType = "Celeste.Mod.SpeedrunTool.RoomTimer.EndPoint";
    internal const string TooltipType = "Celeste.Mod.SpeedrunTool.Message.Tooltip";
    internal const string DialogBoxType = "Celeste.Mod.SpeedrunTool.Message.NonFrozenMiniTextbox";

    internal const string CurrentRoomTimerData = "CurrentRoomTimerData";
    internal const string Time = "Time";
    internal const string ThisRunTimes = "ThisRunTimes";
    internal const string UpdateTimerState = "UpdateTimerState";

    internal const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    internal const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    internal const BindingFlags NonPublicStatic = BindingFlags.NonPublic | BindingFlags.Static;

    // One line per missing member, empty when all are there.
    internal static List<string> FindMissing(Assembly speedrunTool)
    {
        List<string> missing = [];

        Type settings = RequireType(speedrunTool, SettingsType, missing);
        Type timerType = RequireType(speedrunTool, TimerTypeEnum, missing);
        if (settings != null)
        {
            RequireProperty(settings, "Instance", PublicStatic, settings, missing);
            if (timerType != null)
                RequireProperty(settings, "RoomTimerType", PublicInstance, timerType, missing);
            RequireProperty(settings, "NumberOfRooms", PublicInstance, typeof(int), missing);
        }
        if (timerType != null)
            RequireSameEnum(timerType, typeof(RoomTimerMode), missing);

        Type manager = RequireType(speedrunTool, ManagerType, missing);
        if (manager != null)
        {
            FieldInfo data = manager.GetField(CurrentRoomTimerData, NonPublicStatic);
            if (data == null)
                missing.Add($"{Short(ManagerType)}.{CurrentRoomTimerData} (non-public static field)");
            else
            {
                RequireProperty(data.FieldType, Time, PublicInstance, typeof(long), missing);
                RequireProperty(data.FieldType, ThisRunTimes, PublicInstance, typeof(Dictionary<string, long>), missing);
            }
            RequireMethod(manager, UpdateTimerState, [typeof(bool)], missing);
            RequireMethod(manager, "CmdExportRoomTimes", [], missing);
        }

        Type endPoint = RequireType(speedrunTool, EndPointType, missing);
        if (endPoint != null)
        {
            RequireProperty(endPoint, "IsExist", PublicStatic, typeof(bool), missing);
            RequireProperty(endPoint, "IsReachedRoomIdEndPoint", PublicStatic, typeof(bool), missing);
        }

        Type popup = RequireType(speedrunTool, PopupType, missing);
        if (popup != null)
            RequireMethod(popup, "Show", [typeof(string), typeof(string)], missing);
        // The two entities that popup adds, which the adapter looks for in the level.
        RequireType(speedrunTool, TooltipType, missing);
        RequireType(speedrunTool, DialogBoxType, missing);

        return missing;
    }

    private static Type RequireType(Assembly assembly, string fullName, List<string> missing)
    {
        Type type = assembly.GetType(fullName, throwOnError: false);
        if (type == null) missing.Add($"type {fullName}");
        return type;
    }

    private static void RequireProperty(Type owner, string name, BindingFlags flags, Type propertyType, List<string> missing)
    {
        PropertyInfo property = owner.GetProperty(name, flags);
        string kind = flags.HasFlag(BindingFlags.Static) ? "static" : "instance";
        if (property == null || property.GetMethod == null)
            missing.Add($"{owner.Name}.{name} (public {kind} property)");
        else if (property.PropertyType != propertyType)
            missing.Add($"{owner.Name}.{name} is {property.PropertyType.Name}, expected {propertyType.Name}");
    }

    // Public static, returning void, with exactly these parameters.
    private static void RequireMethod(Type owner, string name, Type[] parameters, List<string> missing)
    {
        MethodInfo method = owner.GetMethod(name, PublicStatic, null, parameters, null);
        if (method == null || method.ReturnType != typeof(void))
            missing.Add($"{owner.Name}.{name}({string.Join(", ", Array.ConvertAll(parameters, p => p.Name))}) (public static void method)");
    }

    // The adapter casts between the two by value.
    private static void RequireSameEnum(Type theirs, Type ours, List<string> missing)
    {
        if (!theirs.IsEnum)
        {
            missing.Add($"{theirs.Name} is not an enum");
            return;
        }
        foreach (object value in Enum.GetValues(ours))
        {
            string name = Enum.GetName(ours, value);
            FieldInfo field = theirs.GetField(name, PublicStatic);
            if (field == null || Convert.ToInt32(field.GetRawConstantValue()) != (int)value)
                missing.Add($"{theirs.Name}.{name} = {(int)value}");
        }
        if (Enum.GetNames(theirs).Length != Enum.GetNames(ours).Length)
            missing.Add($"{theirs.Name} has {Enum.GetNames(theirs).Length} values, {ours.Name} has {Enum.GetNames(ours).Length}");
    }

    private static string Short(string fullName) => fullName[(fullName.LastIndexOf('.') + 1)..];
}
