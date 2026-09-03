using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class ExportChoiceTests
{
    // YamlDotNet resolves an enum by name and throws when the name is absent, aborting the rest
    // of the document: dropping Sheet would cost a stale settings file the 67 properties declared
    // after ExportMode, plus the keybinds. Remove the member a version after OnLoadSettings has
    // rewritten the value, not because this test is in the way.
    [Fact]
    public void Sheet_still_resolves_by_name_so_an_old_settings_file_still_loads()
    {
        Assert.True(Enum.TryParse("Sheet", out ExportChoice parsed));
        Assert.Equal(ExportChoice.Sheet, parsed);
    }

    [Fact]
    public void The_two_live_export_targets_still_resolve_by_name()
    {
        Assert.True(Enum.TryParse("Clipboard", out ExportChoice clipboard));
        Assert.True(Enum.TryParse("File", out ExportChoice file));
        Assert.Equal(ExportChoice.Clipboard, clipboard);
        Assert.Equal(ExportChoice.File, file);
    }

    // Clipboard must stay the zero value: it is the default a migrated file falls back to.
    [Fact]
    public void Clipboard_is_the_default_value_of_the_enum()
    {
        Assert.Equal(ExportChoice.Clipboard, default(ExportChoice));
    }
}
