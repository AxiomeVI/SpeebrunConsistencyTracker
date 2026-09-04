using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Mod;
using Celeste.Mod.SpeebrunConsistencyTracker;
using Celeste.Mod.SpeebrunConsistencyTracker.UI;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// The KeybindConfigUi table shipped without a test because asserting anything about ButtonBinding
// meant naming that type in test source, which needed a compile-time Celeste reference the project
// did not have. It has one now — one csproj line, and no prerequisite that was not already there
// through the ProjectReference to Source. So the guarantee ChartDefinitionsTests gives the chart
// table is available here too: no row can read another row's binding, and no keybind property can
// exist that no row reaches.
public class KeybindTableTests
{
    // Restated independently of the production table on purpose. Distinctness alone cannot catch
    // two rows with their bindings swapped — both stay distinct, and both stay non-null. This is
    // what pins a label to the binding it actually names.
    private static readonly (string Property, string LabelKey)[] Expected =
    [
        ("Keybind_ImportTargetTime",   DialogIds.KeyImportTargetTimeId),
        ("Keybind_StatsExport",        DialogIds.KeyStatsExportId),
        ("Keybind_ToggleGraphOverlay", DialogIds.ToggleGraphOverlayId),
        ("Keybind_NextGraph",          DialogIds.KeyNextGraphId),
        ("Keybind_PreviousGraph",      DialogIds.KeyPreviousGraphId),
        ("Keybind_ClearStats",         DialogIds.KeyClearStatsId),
    ];

    private static PropertyInfo[] BindingProperties() =>
        typeof(SpeebrunConsistencyTrackerModuleSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.PropertyType == typeof(ButtonBinding))
            .ToArray();

    // Every keybind property gets its own ButtonBinding instance, so the property a row reads can
    // be named by reference. Off-engine all six start null, which would make them indistinguishable.
    private static (SpeebrunConsistencyTrackerModuleSettings Settings, Dictionary<object, string> Owner) Seeded()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings();
        var owner = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);

        foreach (PropertyInfo property in BindingProperties())
        {
            ButtonBinding sentinel = new();
            property.SetValue(settings, sentinel);
            owner[sentinel] = property.Name;
        }

        return (settings, owner);
    }

    [Fact]
    public void Every_row_has_a_distinct_non_empty_label_key()
    {
        var seen = new HashSet<string>();

        foreach (KeybindConfigUi.KeybindDef row in KeybindConfigUi.Keybinds)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.LabelKey),
                "A keybind row has no label key. Reload() draws it twice, so the row would show as " +
                "a blank line in both the keyboard and the controller list.");
            Assert.True(seen.Add(row.LabelKey),
                $"Two keybind rows share the label key '{row.LabelKey}'. Both lists would show the " +
                "same name twice and the player could not tell which line rebinds what.");
        }
    }

    [Fact]
    public void Each_row_reads_the_binding_its_label_names()
    {
        (SpeebrunConsistencyTrackerModuleSettings settings, Dictionary<object, string> owner) = Seeded();
        Dictionary<string, string> expectedByLabel = Expected.ToDictionary(e => e.LabelKey, e => e.Property);

        foreach (KeybindConfigUi.KeybindDef row in KeybindConfigUi.Keybinds)
        {
            ButtonBinding read = row.Binding(settings);
            Assert.False(read is null,
                $"The row labelled '{row.LabelKey}' returned no binding for a settings instance whose " +
                "six keybind properties were all populated. Its lambda reads something else.");

            Assert.True(owner.TryGetValue(read, out string actual),
                $"The row labelled '{row.LabelKey}' returned a ButtonBinding that is not one of the " +
                "settings keybind properties.");
            Assert.True(expectedByLabel.TryGetValue(row.LabelKey, out string expected),
                $"'{row.LabelKey}' is a keybind row this test does not pin. Add it to Expected with " +
                "the property it is meant to rebind.");

            Assert.True(expected == actual,
                $"The row labelled '{row.LabelKey}' rebinds {actual}, not {expected}. Reload() shows " +
                "the label and the binding side by side, so this mislabels a line in the keybind menu " +
                "and rebinds the wrong action when the player presses it.");
        }
    }

    [Fact]
    public void Every_keybind_property_is_reachable_from_exactly_one_row()
    {
        (SpeebrunConsistencyTrackerModuleSettings settings, Dictionary<object, string> owner) = Seeded();

        List<string> reached = KeybindConfigUi.Keybinds
            .Select(row => row.Binding(settings))
            .Where(binding => binding is not null)
            .Select(binding => owner.GetValueOrDefault(binding))
            .ToList();

        List<string> declared = BindingProperties().Select(p => p.Name).ToList();

        Assert.True(reached.Distinct().Count() == reached.Count,
            $"Two keybind rows read the same settings property: {string.Join(", ", reached)}. One of " +
            "the two bindings can never be rebound from the menu.");

        List<string> unreachable = declared.Except(reached).ToList();
        Assert.True(unreachable.Count == 0,
            $"{string.Join(", ", unreachable)} is a keybind the player cannot rebind: it is declared " +
            "on the settings but no KeybindConfigUi row reaches it. Adding the property is not enough " +
            "— the table is a second list and this is the assertion that says so.");
    }
}
