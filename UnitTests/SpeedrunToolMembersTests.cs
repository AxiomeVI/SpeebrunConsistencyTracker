using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// The canary for a SpeedrunTool update: replace libs/SpeedrunTool.dll with the new release and
// run this. In game, a missing member leaves the mod inert with one popup, which is the right
// failure for a player and the wrong place for a developer to find out.
public class SpeedrunToolMembersTests
{
    private static Assembly LibsSpeedrunTool()
    {
        string libsDir = typeof(SpeedrunToolMembersTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(m => m.Key == "LibsDir").Value;
        string path = Path.Combine(libsDir!, "SpeedrunTool.dll");
        Assert.True(File.Exists(path), $"{path} is missing. See libs/README.md.");
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }

    [Fact]
    public void Every_member_the_adapter_binds_exists_in_libs_SpeedrunTool_dll()
    {
        Assert.Equal([], SpeedrunToolMembers.FindMissing(LibsSpeedrunTool()));
    }

    // Without this, a check that finds nothing because it looks at nothing would pass the test above.
    [Fact]
    public void The_check_names_each_type_an_assembly_lacks()
    {
        List<string> missing = SpeedrunToolMembers.FindMissing(typeof(object).Assembly);

        Assert.Equal(
            [
                $"type {SpeedrunToolMembers.SettingsType}",
                $"type {SpeedrunToolMembers.TimerTypeEnum}",
                $"type {SpeedrunToolMembers.ManagerType}",
                $"type {SpeedrunToolMembers.EndPointType}",
                $"type {SpeedrunToolMembers.PopupType}",
            ],
            missing);
    }

    // ModInterop binds by name, and leaves a field null when SpeedrunTool exports no such method.
    [Theory]
    [InlineData(typeof(RoomTimerIntegration))]
    [InlineData(typeof(SaveLoadIntegration))]
    public void Every_interop_import_has_a_matching_export(Type imports)
    {
        Assert.Equal([], Unmatched(imports, LibsSpeedrunTool()));
    }

    // GetSlotName moved from SpeedrunTool.SaveLoad to SpeedrunTool.SaveLoadControl in 3.29.2.
    // Whichever release is in libs/, one of the two imports must bind.
    [Fact]
    public void The_slot_name_is_exported_under_one_of_its_two_names()
    {
        Assembly srt = LibsSpeedrunTool();
        bool current = Unmatched(typeof(SaveLoadControlIntegration), srt) is [];
        bool legacy = Unmatched(typeof(LegacySlotNameIntegration), srt) is [];
        Assert.True(current || legacy, "Neither SpeedrunTool.SaveLoadControl nor SpeedrunTool.SaveLoad exports GetSlotName.");
    }

    // The import fields SpeedrunTool exports no matching method for; every field when it has no
    // export of that name at all.
    private static List<string> Unmatched(Type imports, Assembly srt)
    {
        string exportName = (string)imports.GetCustomAttributesData()
            .Single(a => a.AttributeType.Name == "ModImportNameAttribute")
            .ConstructorArguments[0].Value;
        Type[] exports = [.. ExportedTypes(srt, exportName)];

        List<string> unmatched = [];
        foreach (FieldInfo field in imports.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            MethodInfo invoke = field.FieldType.GetMethod("Invoke")!;
            string[] wanted = [.. invoke.GetParameters().Select(p => p.ParameterType.FullName)];
            bool found = exports
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Any(m => m.Name == field.Name
                       && m.ReturnType.FullName == invoke.ReturnType.FullName
                       && m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(wanted));
            if (!found) unmatched.Add(field.Name);
        }
        return unmatched;
    }

    private static IEnumerable<Type> ExportedTypes(Assembly assembly, string exportName)
    {
        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = [.. e.Types.Where(t => t != null)]; }
        return types.Where(t => t.GetCustomAttributesData().Any(a =>
            a.AttributeType.Name == "ModExportNameAttribute"
            && (string)a.ConstructorArguments[0].Value == exportName));
    }
}
