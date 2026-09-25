using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Mod;
using Celeste.Mod.SpeebrunConsistencyTracker;
using Celeste.Mod.SpeebrunConsistencyTracker.UI;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// The hotkey table is a second list beside the settings properties, and a second list drifts.
public class KeybindTableTests
{
    // Restated independently of the production table on purpose. Distinctness alone cannot catch
    // two rows with their properties swapped -- both stay distinct. This is what pins a label to the
    // binding it actually names.
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
            .GetProperties()
            .Where(p => typeof(ButtonBinding).IsAssignableFrom(p.PropertyType))
            .ToArray();

    [Fact]
    public void Every_row_has_a_distinct_non_empty_label_key()
    {
        var seen = new HashSet<string>();

        foreach (var row in Hotkeys.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.LabelId),
                $"The row for {row.Property} has no label key, so it draws as a blank line in both sections.");
            Assert.True(seen.Add(row.LabelId),
                $"Two rows share the label key '{row.LabelId}'; the player could not tell which line rebinds what.");
        }
    }

    [Fact]
    public void Each_row_binds_the_property_its_label_names()
    {
        Dictionary<string, string> expectedByLabel = Expected.ToDictionary(e => e.LabelKey, e => e.Property);

        foreach (var row in Hotkeys.All)
        {
            Assert.True(expectedByLabel.TryGetValue(row.LabelId, out string expected),
                $"'{row.LabelId}' is a row this test does not pin. Add it to Expected with the property it rebinds.");
            Assert.True(expected == row.Property,
                $"The row labelled '{row.LabelId}' rebinds {row.Property}, not {expected}: the menu line " +
                "would rebind the wrong action.");
        }
    }

    [Fact]
    public void Every_keybind_property_is_reachable_from_exactly_one_row()
    {
        List<string> reached = [.. Hotkeys.All.Select(row => row.Property)];
        Assert.True(reached.Distinct().Count() == reached.Count,
            $"Two rows bind the same property: {string.Join(", ", reached)}.");

        List<string> unreachable = [.. BindingProperties().Select(p => p.Name).Except(reached)];
        Assert.True(unreachable.Count == 0,
            $"{string.Join(", ", unreachable)} has no row in UI.Hotkeys, and it is [SettingIgnore], so nobody can bind it.");
    }

    // Everest's own key-config rows read several bound keys as "any one of them"; the hotkeys read
    // them as a combo. A binding shown on both screens means two different things.
    [Fact]
    public void Every_keybind_property_is_hidden_from_Everests_key_config()
    {
        string[] shown = [.. BindingProperties()
            .Where(p => p.GetCustomAttribute<SettingIgnoreAttribute>() is null)
            .Select(p => p.Name)];

        Assert.Equal([], shown);
    }
}
