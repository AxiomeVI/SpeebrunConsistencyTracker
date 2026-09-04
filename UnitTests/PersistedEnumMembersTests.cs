using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Mod.SpeebrunConsistencyTracker;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// Generalises the guard in ExportChoiceTests to every enum-typed property of
// SpeebrunConsistencyTrackerModuleSettings. YamlDotNet resolves a persisted enum by NAME and
// throws on one it does not recognise, aborting the rest of the settings document — the
// ExportMode: Sheet incident (2026-09-03) removed one enum member and reset every property
// declared after it: target time, overlay, all charts, all metrics and all keybinds, silently,
// for any player who had launched with the old build.
//
// A member REMOVED or RENAMED must fail here loudly, because that is the exact edit that
// strands a live player's settings file. A member ADDED must NOT fail here, because that is
// safe — an old file simply never names the new one.
public class PersistedEnumMembersTests
{
    // Snapshot of every enum-typed Settings property's members, taken 2026-09-03 when GraphType
    // joined ExportChoice as a pinned enum. When a new enum-typed property is added to
    // SpeebrunConsistencyTrackerModuleSettings, Every_enum_persisted_through_settings_is_pinned_here
    // fails until its current members are added here — that failure is the mechanism, not a
    // suggestion to remember.
    private static readonly Dictionary<string, string[]> PinnedMembers = new()
    {
        ["ExportChoice"] = ["Clipboard", "File", "Sheet"],
        ["MetricOutputChoice"] = ["Off", "Overlay", "Export", "Both"],
        ["PercentileChoice"] = ["P10", "P20", "P30", "P40", "P60", "P70", "P80", "P90"],
        ["StatTextPosition"] =
        [
            "TopLeft", "TopCenter", "TopRight",
            "MiddleLeft", "MiddleCenter", "MiddleRight",
            "BottomLeft", "BottomCenter", "BottomRight",
        ],
        ["StatTextOrientation"] = ["Horizontal", "Vertical"],
        ["ColorChoice"] =
        [
            "Cyan", "Orange", "Blue", "Gold", "Purple", "Green", "Turquoise", "Coral",
            "Indigo", "Pink", "LightGreen", "Yellow", "MadelineRed", "BadelinePurple",
        ],
        // Moved from SessionManagement/GraphManager.cs (2026-09-03) specifically because that
        // location hid its persistence contract from whoever edited it next.
        ["GraphType"] =
        [
            "Scatter", "RoomHistogram", "SegmentHistogram", "DnfPercent",
            "ProblemRooms", "TimeLoss", "RunTrajectory", "BoxPlot",
        ],
    };

    // typeof(...) needs no instance — SpeebrunConsistencyTrackerModuleSettings derives from
    // EverestModuleSettings and is awkward to construct outside a running Celeste/Everest.
    // DeclaredOnly matters here for a second reason: walking up to EverestModuleSettings (base
    // class, in Celeste.dll) forces the runtime to resolve that assembly, which this test host
    // cannot load. Every persisted enum lives on this class directly, so declared-only members
    // are the whole set anyway.
    private static IEnumerable<Type> SettingsEnumTypes =>
        typeof(SpeebrunConsistencyTrackerModuleSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.PropertyType)
            .Where(t => t.IsEnum)
            .Distinct();

    [Fact]
    public void Every_enum_persisted_through_settings_is_pinned_here()
    {
        List<string> unpinned = SettingsEnumTypes
            .Select(t => t.Name)
            .Except(PinnedMembers.Keys)
            .ToList();

        Assert.True(unpinned.Count == 0,
            $"{string.Join(", ", unpinned)} is a new enum-typed Settings property with no entry " +
            "in PersistedEnumMembersTests.PinnedMembers. Add it with its current member names " +
            "before shipping: YamlDotNet resolves a persisted enum by name and throws on one it " +
            "does not recognise, aborting the rest of the settings document (the ExportMode: " +
            "Sheet incident, 2026-09-03, reset every property declared after it).");
    }

    [Fact]
    public void No_pinned_member_name_was_removed_or_renamed()
    {
        Dictionary<string, Type> settingsEnumsByName = SettingsEnumTypes.ToDictionary(t => t.Name);
        List<string> missing = [];

        foreach ((string enumName, string[] expectedMembers) in PinnedMembers)
        {
            // No longer a Settings property at all: not this test's concern, and not the failure
            // mode the incident was about (a removed member of a still-persisted enum).
            if (!settingsEnumsByName.TryGetValue(enumName, out Type enumType))
                continue;

            HashSet<string> actualMembers = [.. Enum.GetNames(enumType)];
            missing.AddRange(
                expectedMembers.Where(m => !actualMembers.Contains(m)).Select(m => $"{enumName}.{m}"));
        }

        Assert.True(missing.Count == 0,
            $"Removed or renamed: {string.Join(", ", missing)}. That is the ExportMode: Sheet " +
            "incident (2026-09-03) one enum over: YamlDotNet throws on an unknown enum member " +
            "name and aborts the rest of the settings document, silently resetting every property " +
            "declared after it for any player whose file still names the old member. Adding a " +
            "member is safe and never trips this assertion; only delete one a version after a " +
            "migration (like OnLoadSettings' Sheet -> Clipboard) has rewritten it out of live " +
            "files, the same way ExportChoice.Sheet is scheduled to go.");
    }
}
