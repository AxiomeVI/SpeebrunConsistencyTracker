using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Mod.SpeebrunConsistencyTracker;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// GraphManager.ChartDefinitions is the single declaration of a chart: enabling, cycling, building,
// caching, clearing and the settings menu all read from it. That replaced four hand-kept
// enumerations, and it is worth only as much as two guarantees the compiler cannot give:
//
//   - every GraphType has a row. A switch over an enum is not exhaustive in C# without a `_` arm,
//     so the compiler never counts the arms; a missing row is a null overlay when the player
//     cycles onto that chart, and a toggle the settings menu never lists.
//   - no row reads or writes another row's setting. Rows are near-identical and eight lines apart,
//     so the copy-paste that changes every field but one is the failure this shape invites — and
//     it compiles.
public class ChartDefinitionsTests
{
    [Fact]
    public void Every_graph_type_has_exactly_one_definition()
    {
        List<GraphType> declared = [.. GraphManager.ChartDefinitions.Select(d => d.Type)];

        List<string> missing = Enum.GetValues<GraphType>()
            .Where(t => !declared.Contains(t))
            .Select(t => t.ToString())
            .ToList();

        Assert.True(missing.Count == 0,
            "GraphType member(s) with no row in GraphManager.ChartDefinitions: " +
            $"{string.Join(", ", missing)}. Without a row the chart is unreachable: ShowCurrentSlot " +
            "leaves a null overlay for it and the settings menu never lists a toggle.");

        Assert.Equal(declared.Count, declared.Distinct().Count());
    }

    [Fact]
    public void Every_definition_has_a_distinct_non_empty_label_key()
    {
        List<string> keys = [.. GraphManager.ChartDefinitions.Select(d => d.LabelKey)];

        Assert.DoesNotContain(keys, string.IsNullOrWhiteSpace);

        List<string> duplicated = [.. keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key)];
        Assert.True(duplicated.Count == 0,
            $"Duplicate label key(s) across chart definitions: {string.Join(", ", duplicated)}. " +
            "Two toggles would appear in the settings menu under the same name.");
    }

    // A record's positional parameters are non-nullable by convention only; nothing stops a row
    // being written with a null in one slot. ExtraKey is genuinely optional and excluded.
    [Fact]
    public void Every_definition_wires_all_of_its_required_delegates()
    {
        foreach (ChartDefinition chart in GraphManager.ChartDefinitions)
        {
            Assert.NotNull(chart.Get);
            Assert.NotNull(chart.Set);
            Assert.NotNull(chart.Build);
            Assert.NotNull(chart.Slots);
        }
    }

    [Fact]
    public void Set_is_visible_through_the_same_rows_Get()
    {
        foreach (ChartDefinition chart in GraphManager.ChartDefinitions)
        {
            var settings = new SpeebrunConsistencyTrackerModuleSettings();

            chart.Set(settings, true);
            Assert.True(chart.Get(settings),
                $"{chart.Type}: Set(true) is not visible through the same row's Get — its Get and " +
                "Set name different settings properties.");

            chart.Set(settings, false);
            Assert.False(chart.Get(settings),
                $"{chart.Type}: Set(false) is not visible through the same row's Get — its Get and " +
                "Set name different settings properties.");
        }
    }

    // The direct test of cross-wiring: find, by diffing two fresh settings objects, which property
    // a row actually writes — not which one its lambda appears to name. A row pointing at another
    // chart's property, or at an unrelated bool like Enabled, fails here and nowhere else.
    [Fact]
    public void Each_row_writes_one_distinct_setting_that_belongs_to_it()
    {
        var writtenBy = new Dictionary<string, GraphType>();

        foreach (ChartDefinition chart in GraphManager.ChartDefinitions)
        {
            var before = new SpeebrunConsistencyTrackerModuleSettings();
            var after  = new SpeebrunConsistencyTrackerModuleSettings();
            chart.Set(after, !chart.Get(before));

            List<string> changed = BoolSettingProperties()
                .Where(p => !Equals(p.GetValue(before), p.GetValue(after)))
                .Select(p => p.Name)
                .ToList();

            Assert.True(changed.Count == 1,
                $"{chart.Type}: Set changed {changed.Count} boolean settings properties " +
                $"({string.Join(", ", changed)}), expected exactly one.");

            string property = changed[0];

            Assert.True(property.Contains(chart.Type.ToString(), StringComparison.Ordinal),
                $"{chart.Type}: its row writes {property}, whose name does not mention " +
                $"{chart.Type}. That is the copy-paste this test exists for — the row is wired to " +
                "another chart's setting, or to an unrelated one.");

            Assert.False(writtenBy.TryGetValue(property, out GraphType owner),
                $"{chart.Type} and {owner} both write {property}: toggling one in the settings " +
                "menu moves the other.");
            writtenBy[property] = chart.Type;
        }
    }

    // DeclaredOnly keeps this off EverestModuleSettings in Celeste.dll, and restricting to bool
    // avoids reading FNA/Everest-typed properties (Color, ButtonBinding) that this host resolves
    // for metadata but should not be made to construct.
    private static IEnumerable<PropertyInfo> BoolSettingProperties() =>
        typeof(SpeebrunConsistencyTrackerModuleSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.PropertyType == typeof(bool) && p.CanRead && p.CanWrite);
}
