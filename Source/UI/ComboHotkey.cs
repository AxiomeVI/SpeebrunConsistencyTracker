using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SpeebrunConsistencyTracker.UI;

/// Wraps a ButtonBinding and detects combo presses (all bound keys held simultaneously).
/// Rising-edge only: Pressed is true for exactly one frame when the combo activates.
/// Pattern taken from CelesteTAS Hotkeys.cs / SpeedrunTool HotkeyRebase.cs.
internal class ComboHotkey(Func<ButtonBinding> getBinding) {
    // Shared input states — updated once per frame by UpdateStates()
    private static KeyboardState _kbState;
    private static GamePadState _padState;

    private bool _lastCheck;

    /// Call once per frame before updating any ComboHotkey instances.
    internal static void UpdateStates() {
        _kbState = Keyboard.GetState();
        _padState = GetGamePadState();
    }

    private static GamePadState GetGamePadState() {
        for (int i = 0; i < 4; i++) {
            var state = GamePad.GetState((PlayerIndex) i);
            if (state.IsConnected) return state;
        }
        return default;
    }

    private bool IsDown() {
        var binding = getBinding();
        if (binding?.Keys is { Count: > 0 } keys && _kbState != default && keys.All(_kbState.IsKeyDown))
            return true;
        if (binding?.Buttons is { Count: > 0 } buttons && _padState != default && buttons.All(_padState.IsButtonDown))
            return true;
        return false;
    }

    /// Call once per frame per instance, after UpdateStates().
    public void Update() {
        bool current = IsDown();
        Pressed = !_lastCheck && current;
        _lastCheck = current;
    }

    public bool Pressed { get; private set; }
}
