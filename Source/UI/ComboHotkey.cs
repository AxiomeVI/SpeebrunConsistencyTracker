using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.UI;

// A combo: every bound key held at once, rising-edge only, so Pressed is true for exactly one
// frame. Pattern taken from CelesteTAS Hotkeys.cs / SpeedrunTool HotkeyRebase.cs.
internal class ComboHotkey {
    private readonly Func<ButtonBinding> _getBinding;

    internal ComboHotkey(Func<ButtonBinding> getBinding) => _getBinding = getBinding;

    private static KeyboardState _kbState;
    private static GamePadState _padState;

    private bool _lastCheck;

    // True while something other than gameplay owns the keyboard. Level.Update keeps running
    // under the pause menu and the debug console, and these hotkeys read the raw keyboard rather
    // than Celeste's Input, so without this a key typed into the debug console or pressed while
    // remapping a binding fired the hotkey as well -- Clear session wipes without confirming.
    internal static bool Suppressed { get; private set; }

    // Call once per frame, before any instance's Update().
    internal static void UpdateStates()
        => UpdateStates(Keyboard.GetState(), GetGamePadState(), KeyboardIsElsewhere());

    // Seam: lets the overlap rules be exercised without a keyboard or a running Engine.
    internal static void UpdateStates(KeyboardState keyboard, GamePadState pad, bool suppressed) {
        _kbState = keyboard;
        _padState = pad;
        Suppressed = suppressed;
    }

    private static bool KeyboardIsElsewhere() {
        if (!Engine.Instance.IsActive) return true;
        if (Engine.Commands is { Open: true }) return true;
        // The mod's own remap screen is a second TextMenu added to the Level scene, so the level
        // -- and this poll -- keeps updating underneath it.
        return KeybindConfigUi.IsOpen;
    }

    private static GamePadState GetGamePadState() {
        for (int i = 0; i < 4; i++) {
            var state = GamePad.GetState((PlayerIndex) i);
            if (state.IsConnected) return state;
        }
        return default;
    }

    private bool KeysDown() {
        var keys = _getBinding()?.Keys;
        return keys is { Count: > 0 } && _kbState != default && keys.All(_kbState.IsKeyDown);
    }

    private bool ButtonsDown() {
        var buttons = _getBinding()?.Buttons;
        return buttons is { Count: > 0 } && _padState != default && buttons.All(_padState.IsButtonDown);
    }

    private bool IsDown() => KeysDown() || ButtonsDown();

    // Call once per frame per instance, after UpdateStates(). Called even while suppressed:
    // _lastCheck has to keep following the real keyboard, or a key still held when the console
    // closes reads as a fresh press on the next frame.
    public void Update() {
        bool current = IsDown();
        Pressed = !_lastCheck && current && !Suppressed;
        _lastCheck = current;
    }

    public bool Pressed { get; private set; }

    // A combo only asks that all of its own keys are held, so Toggle chart on E also fired when
    // Export on Ctrl+E did. Call once per frame after every Update(): a hotkey whose binding is a
    // strict subset of another binding held on the same frame loses the frame to it. Pressing the
    // subset on its own is unaffected -- the superset is not held then.
    internal static void ResolveOverlaps(params ComboHotkey[] hotkeys) {
        foreach (ComboHotkey hotkey in hotkeys) {
            if (!hotkey.Pressed) continue;
            foreach (ComboHotkey other in hotkeys) {
                if (ReferenceEquals(other, hotkey)) continue;
                bool shadowed =
                    (other.KeysDown() && IsStrictSubset(hotkey._getBinding()?.Keys, other._getBinding()?.Keys))
                    || (other.ButtonsDown() && IsStrictSubset(hotkey._getBinding()?.Buttons, other._getBinding()?.Buttons));
                if (shadowed) {
                    hotkey.Pressed = false;
                    break;
                }
            }
        }
    }

    private static bool IsStrictSubset<T>(List<T> inner, List<T> outer)
        => inner is { Count: > 0 } && outer != null && outer.Count > inner.Count && inner.All(outer.Contains);
}
