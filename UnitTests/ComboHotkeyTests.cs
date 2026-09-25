using System.Collections.Generic;
using Celeste.Mod;
using Celeste.Mod.SpeebrunConsistencyTracker.UI;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// Drives ComboHotkey through its injected-state overload, so nothing here needs a keyboard or a
// running Engine. Only the rules are under test: whether the game is the thing being typed into
// is decided by KeyboardIsElsewhere, which does need one.
public class ComboHotkeyTests
{
    private static ButtonBinding Binding(params Keys[] keys)
    {
        ButtonBinding binding = new();
        binding.Keys = new List<Keys>(keys);
        binding.Buttons = [];
        return binding;
    }

    private static ComboHotkey Hotkey(ButtonBinding binding) => new(() => binding);

    private static void Frame(bool suppressed, Keys[] held, params ComboHotkey[] hotkeys)
    {
        ComboHotkey.UpdateStates(new KeyboardState(held), default, suppressed);
        foreach (ComboHotkey hotkey in hotkeys) hotkey.Update();
        ComboHotkey.ResolveOverlaps(hotkeys);
    }

    [Fact]
    public void A_combo_fires_once_on_the_frame_its_last_key_goes_down()
    {
        ComboHotkey hotkey = Hotkey(Binding(Keys.LeftControl, Keys.E));

        Frame(false, [Keys.LeftControl], hotkey);
        Assert.False(hotkey.Pressed);

        Frame(false, [Keys.LeftControl, Keys.E], hotkey);
        Assert.True(hotkey.Pressed);

        Frame(false, [Keys.LeftControl, Keys.E], hotkey);
        Assert.False(hotkey.Pressed);
    }

    // Ctrl+E used to fire Export AND the bare-E hotkey, because a combo only checks its own keys.
    [Fact]
    public void A_binding_that_is_a_subset_of_a_held_binding_does_not_fire()
    {
        ComboHotkey toggle = Hotkey(Binding(Keys.E));
        ComboHotkey export = Hotkey(Binding(Keys.LeftControl, Keys.E));

        Frame(false, [Keys.LeftControl], toggle, export);
        Frame(false, [Keys.LeftControl, Keys.E], toggle, export);

        Assert.True(export.Pressed);
        Assert.False(toggle.Pressed);
    }

    [Fact]
    public void The_subset_still_fires_when_pressed_on_its_own()
    {
        ComboHotkey toggle = Hotkey(Binding(Keys.E));
        ComboHotkey export = Hotkey(Binding(Keys.LeftControl, Keys.E));

        Frame(false, [], toggle, export);
        Frame(false, [Keys.E], toggle, export);

        Assert.True(toggle.Pressed);
        Assert.False(export.Pressed);
    }

    // The remap screen and the debug console run on top of a Level that keeps updating.
    [Fact]
    public void A_suppressed_frame_swallows_the_press()
    {
        ComboHotkey clear = Hotkey(Binding(Keys.E));

        Frame(true, [], clear);
        Frame(true, [Keys.E], clear);

        Assert.False(clear.Pressed);
    }

    // The point of still calling Update() while suppressed: the key was already down when the
    // console closed, so releasing and re-pressing it is what should fire, not the close itself.
    [Fact]
    public void A_key_held_through_the_suppressed_frames_does_not_fire_when_they_end()
    {
        ComboHotkey clear = Hotkey(Binding(Keys.E));

        Frame(true, [Keys.E], clear);
        Frame(false, [Keys.E], clear);
        Assert.False(clear.Pressed);

        Frame(false, [], clear);
        Frame(false, [Keys.E], clear);
        Assert.True(clear.Pressed);
    }
}
