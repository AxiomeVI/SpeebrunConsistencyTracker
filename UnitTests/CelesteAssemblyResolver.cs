using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace SpeebrunConsistencyTracker.UnitTests;

// SpeebrunConsistencyTrackerModuleSettings derives from EverestModuleSettings (Celeste.dll) and
// some of its own properties are FNA/Everest types (Color, ButtonBinding), so merely naming the
// type in reflection forces the CLR to resolve those assemblies. Source/*.csproj marks every
// game reference Private="false" — supplied by the real Everest install at runtime — so none of
// them land in UnitTests' own output.
//
// This registers a resolver, once, before any test runs: a module initializer fires the first
// time this assembly is touched, which happens ahead of xUnit's test discovery. It only helps the
// CLR *locate* the assemblies to read their type metadata; nothing under test executes any
// Celeste/FNA code, so there is no dependency on the game actually running.
internal static class CelesteAssemblyResolver
{
    [ModuleInitializer]
    internal static void Register()
    {
        List<string> searchDirectories = SearchDirectories().ToList();
        Dictionary<string, string> assembliesByName = FindAssembliesByName(searchDirectories);

        // The lookup below only runs when the CLR actually needs to resolve an assembly it
        // could not find any other way — i.e. only for a test that touches a Celeste/FNA/Everest
        // type. Measured 2026-09-04: 7 of 148 tests do — five in ChartDefinitionsTests, two in
        // PersistedEnumMembersTests. The other 141 never reach this handler.
        //
        // That isolation covers less than it looks like it does. A wrong -p:CelestePrefix never
        // gets here at all: Source/*.csproj needs Celeste.dll at compile time, so a bad prefix
        // fails the build with MSB4018 out of the Publicizer task. What this diagnostic catches is
        // the assemblies moving after a build that succeeded.
        AssemblyLoadContext.Default.Resolving += (_, requested) =>
        {
            if (assembliesByName.TryGetValue(requested.Name ?? string.Empty, out string path))
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

            // Getting here with a missing/empty setup means the bare FileNotFoundException the
            // CLR would otherwise throw (naming only "Celeste.dll", never why) is about to hide
            // the actual, fixable cause. Diagnose it instead.
            if (!searchDirectories.Any(Directory.Exists) || !assembliesByName.ContainsKey("Celeste"))
            {
                string searched = searchDirectories.Count == 0
                    ? "(no CelestePrefix/LibsDir configured)"
                    : string.Join(", ", searchDirectories);
                throw new InvalidOperationException(
                    $"Could not resolve assembly '{requested.Name}': no Celeste install was found. " +
                    $"Searched: {searched}. " +
                    "Pass -p:CelestePrefix=<path to a directory holding Celeste.dll, FNA.dll, " +
                    "MMHOOK_Celeste.dll> when running the tests.");
            }

            return null;
        };
    }

    private static Dictionary<string, string> FindAssembliesByName(IEnumerable<string> searchDirectories)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in searchDirectories)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string dll in Directory.EnumerateFiles(dir, "*.dll"))
                result[Path.GetFileNameWithoutExtension(dll)] = dll;
        }
        return result;
    }

    // CelestePrefix and LibsDir are baked in by UnitTests.csproj as AssemblyMetadata, from the
    // same CelestePrefix property the build itself resolves — so this stays in sync with
    // whatever -p:CelestePrefix the build was given, and needs no path hardcoded here.
    private static IEnumerable<string> SearchDirectories()
    {
        foreach (AssemblyMetadataAttribute meta in
                 typeof(CelesteAssemblyResolver).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if ((meta.Key is "CelestePrefix" or "LibsDir") && !string.IsNullOrEmpty(meta.Value))
                yield return meta.Value;
        }
    }
}
