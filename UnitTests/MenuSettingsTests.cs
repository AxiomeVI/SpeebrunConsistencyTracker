using System;
using System.Linq;
using System.Reflection;
using Celeste.Mod.SpeebrunConsistencyTracker;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Menu;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// The Mod Options pages split each persisted MetricOutputChoice into an overlay bit and an export
// bit, and the colour wheel saves hex strings. Nothing here may change what a settings file holds.
// Enum-valued theory rows are passed by name: the runner could not match rows carrying this
// project's enums to their results, and counted none of them.
public class MenuSettingsTests
{
    // ------------------------------------------------------------------ the bit split

    [Theory]
    [InlineData("Off",     false, false)]
    [InlineData("Overlay", true,  false)]
    [InlineData("Export",  false, true)]
    [InlineData("Both",    true,  true)]
    public void Each_page_reads_its_own_bit(string name, bool overlay, bool export)
    {
        MetricOutputChoice choice = Enum.Parse<MetricOutputChoice>(name);
        Assert.Equal(overlay, MetricToggles.Has(choice, MetricOutput.Overlay));
        Assert.Equal(export,  MetricToggles.Has(choice, MetricOutput.Export));
    }

    [Theory]
    [InlineData("Off",     "Overlay", "Off")]
    [InlineData("Overlay", "Overlay", "Off")]
    [InlineData("Export",  "Both",    "Export")]
    [InlineData("Both",    "Both",    "Export")]
    public void The_overlay_page_writes_only_the_overlay_bit(string startName, string whenOnName, string whenOffName)
    {
        (MetricOutputChoice start, MetricOutputChoice whenOn, MetricOutputChoice whenOff) = (
            Enum.Parse<MetricOutputChoice>(startName), Enum.Parse<MetricOutputChoice>(whenOnName), Enum.Parse<MetricOutputChoice>(whenOffName));
        Assert.Equal(whenOn,  MetricToggles.With(start, MetricOutput.Overlay, true));
        Assert.Equal(whenOff, MetricToggles.With(start, MetricOutput.Overlay, false));
    }

    [Theory]
    [InlineData("Off",     "Export", "Off")]
    [InlineData("Overlay", "Both",   "Overlay")]
    [InlineData("Export",  "Export", "Off")]
    [InlineData("Both",    "Both",   "Overlay")]
    public void The_export_page_writes_only_the_export_bit(string startName, string whenOnName, string whenOffName)
    {
        (MetricOutputChoice start, MetricOutputChoice whenOn, MetricOutputChoice whenOff) = (
            Enum.Parse<MetricOutputChoice>(startName), Enum.Parse<MetricOutputChoice>(whenOnName), Enum.Parse<MetricOutputChoice>(whenOffName));
        Assert.Equal(whenOn,  MetricToggles.With(start, MetricOutput.Export, true));
        Assert.Equal(whenOff, MetricToggles.With(start, MetricOutput.Export, false));
    }

    [Fact]
    public void A_toggle_writes_through_to_the_setting_it_names()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings { Median = MetricOutputChoice.Both };
        MetricToggles.Metric median = MetricToggles.All.Single(m => m.LabelKey == DialogIds.MedianId);

        MetricToggles.Set(settings, median, MetricOutput.Overlay, false);

        Assert.Equal(MetricOutputChoice.Export, settings.Median);
    }

    // ------------------------------------------------------------------ the table

    [Fact]
    public void The_pages_list_every_metric_setting_but_the_target_line_and_the_hidden_score()
    {
        PropertyInfo[] properties = [.. typeof(SpeebrunConsistencyTrackerModuleSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.PropertyType == typeof(MetricOutputChoice))
            .Where(p => p.Name is not ("TargetTime" or "ConsistencyScore"))];

        // Which property each row writes: set it on an all-Off object and see what moved.
        string Writes(MetricToggles.Metric metric)
        {
            var probe = new SpeebrunConsistencyTrackerModuleSettings();
            foreach (PropertyInfo p in properties) p.SetValue(probe, MetricOutputChoice.Off);
            metric.Set(probe, MetricOutputChoice.Both);
            return properties.Single(p => (MetricOutputChoice)p.GetValue(probe)! == MetricOutputChoice.Both).Name;
        }

        Assert.Equal(18, properties.Length);
        Assert.Equal(properties.Select(p => p.Name).Order(), MetricToggles.All.Select(Writes).Order());
    }

    [Fact]
    public void KeepsTodaysDefaults()
    {
        var fresh = new SpeebrunConsistencyTrackerModuleSettings();
        string[] drifted = [.. MetricToggles.All.Where(m => m.Get(fresh) != m.Default).Select(m => m.LabelKey)];
        Assert.Equal([], drifted);
    }

    // ------------------------------------------------------------------ the bulk buttons

    [Theory]
    [InlineData("Off")]
    [InlineData("Both")]
    [InlineData("Overlay")]
    [InlineData("Export")]
    public void Both_pages_Defaults_together_restore_every_metric(string startName)
    {
        MetricOutputChoice start = Enum.Parse<MetricOutputChoice>(startName);
        var settings = new SpeebrunConsistencyTrackerModuleSettings { PercentileValue = PercentileChoice.P10 };
        foreach (MetricToggles.Metric m in MetricToggles.All) m.Set(settings, start);
        foreach (MetricToggles.Section s in MetricToggles.ExportSections) s.Set(settings, !s.Default);

        MetricToggles.ApplyDefaults(settings, MetricOutput.Overlay);
        MetricToggles.ApplyDefaults(settings, MetricOutput.Export);

        var fresh = new SpeebrunConsistencyTrackerModuleSettings();
        foreach (MetricToggles.Metric m in MetricToggles.All)
            Assert.True(m.Get(fresh) == m.Get(settings), $"{m.LabelKey}: {m.Get(settings)}, expected {m.Get(fresh)}");
        foreach (MetricToggles.Section s in MetricToggles.ExportSections)
            Assert.True(s.Get(fresh) == s.Get(settings), s.LabelKey);
        Assert.Equal(fresh.PercentileValue, settings.PercentileValue);
    }

    [Fact]
    public void The_overlay_Defaults_leaves_the_export_bit_and_the_sections_alone()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings();
        MetricToggles.SetAll(settings, MetricOutput.Export, true);

        MetricToggles.ApplyDefaults(settings, MetricOutput.Overlay);

        Assert.All(MetricToggles.All, m => Assert.True(MetricToggles.Get(settings, m, MetricOutput.Export), m.LabelKey));
        Assert.All(MetricToggles.ExportSections, s => Assert.True(s.Get(settings), s.LabelKey));
    }

    [Fact]
    public void Show_all_and_Hide_all_touch_only_the_overlay_bit()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings();
        var before = MetricToggles.All.ToDictionary(m => m.LabelKey, m => MetricToggles.Get(settings, m, MetricOutput.Export));

        MetricToggles.SetAll(settings, MetricOutput.Overlay, true);
        Assert.All(MetricToggles.All, m => Assert.True(MetricToggles.Get(settings, m, MetricOutput.Overlay)));
        MetricToggles.SetAll(settings, MetricOutput.Overlay, false);
        Assert.All(MetricToggles.All, m => Assert.False(MetricToggles.Get(settings, m, MetricOutput.Overlay)));

        Assert.All(MetricToggles.All, m => Assert.Equal(before[m.LabelKey], MetricToggles.Get(settings, m, MetricOutput.Export)));
    }

    [Fact]
    public void Include_all_and_Include_none_set_the_sections_too()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings();

        MetricToggles.SetAll(settings, MetricOutput.Export, true);
        Assert.All(MetricToggles.ExportSections, s => Assert.True(s.Get(settings), s.LabelKey));
        MetricToggles.SetAll(settings, MetricOutput.Export, false);
        Assert.All(MetricToggles.ExportSections, s => Assert.False(s.Get(settings), s.LabelKey));
    }

    // ------------------------------------------------------------------ colours

    [Fact]
    public void A_file_with_only_the_old_choices_keeps_its_room_and_segment_colours()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings
        {
            RoomColor = ColorChoice.Blue, SegmentColor = ColorChoice.MadelineRed,
        };

        settings.MigrateColors();

        Assert.Equal("6495ed", settings.RoomColorHex);
        Assert.Equal("ff5963", settings.SegmentColorHex);
    }

    [Fact]
    public void A_file_with_neither_key_gets_the_defaults()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings();

        settings.MigrateColors();

        Assert.Equal("00ffff", settings.RoomColorHex);
        Assert.Equal("ffa500", settings.SegmentColorHex);
    }

    [Fact]
    public void A_saved_hex_wins_over_the_old_choice()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings
        {
            RoomColor = ColorChoice.Blue, RoomColorHex = "123456",
        };

        settings.MigrateColors();

        Assert.Equal("123456", settings.RoomColorHex);
    }

    [Theory]
    [InlineData("00ffff")]
    [InlineData("cd5c5c")]
    [InlineData("000000")]
    [InlineData("ffffff")]
    public void Hex_round_trips(string hex)
    {
        Assert.True(ColorHelper.TryParseHex(hex, out Color color));
        Assert.Equal(hex, ColorHelper.ToHex(color));
    }

    [Theory]
    [InlineData("#ABCDEF", "abcdef")]
    [InlineData("  AbCdEf ", "abcdef")]
    public void Hex_reads_a_hash_and_any_case(string text, string expected)
    {
        Assert.True(ColorHelper.TryParseHex(text, out Color color));
        Assert.Equal(expected, ColorHelper.ToHex(color));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fff")]
    [InlineData("gggggg")]
    [InlineData("+12345")]
    [InlineData("1234567")]
    public void Hex_rejects_anything_else(string text)
    {
        Assert.False(ColorHelper.TryParseHex(text, out _));
    }

    [Fact]
    public void Fill_opacity_fades_the_fills_and_nothing_else()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings { RoomColorHex = "ff0000", ChartOpacity = 50 };

        ChartPalette palette = ChartPalette.For(settings);

        Assert.Equal(new Color(255, 0, 0), palette.Room);
        Assert.Equal(new Color(255, 0, 0) * 0.5f, palette.RoomFill);
    }

    [Fact]
    public void The_palette_follows_a_change_without_being_told()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings { RoomColorHex = "ff0000" };
        Assert.Equal(new Color(255, 0, 0), ChartPalette.For(settings).Room);

        settings.RoomColorHex = "00ff00";

        Assert.Equal(new Color(0, 255, 0), ChartPalette.For(settings).Room);
    }

    [Fact]
    public void An_unreadable_hex_draws_the_default()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings { PrimaryChartColorHex = "nope", RoomColorHex = null };

        ChartPalette palette = ChartPalette.For(settings);

        Assert.Equal(new Color(205, 92, 92), palette.Primary);
        Assert.Equal(new Color(0, 255, 255), palette.Room);
    }
}
