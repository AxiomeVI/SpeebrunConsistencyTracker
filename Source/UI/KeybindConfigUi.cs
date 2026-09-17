using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.UI;

[Tracked]
internal class KeybindConfigUi : TextMenu {
    // One row per logical keybind, and the row is the whole declaration: the label, the binding it
    // remaps, and its position in both lists. This replaces a twelve-value Slot enum that had to be
    // enumerated six times — twice in Reload(), once in each ApplyRemap overload, and twice more to
    // decide keyboard-versus-controller and to label the remap prompt. Four of those six ended in a
    // `_ => throw` standing in for an exhaustiveness C# does not give over an enum. A seventh
    // keybind is now one row, and a row cannot name the wrong binding without saying so out loud.
    internal sealed record KeybindDef(
        string LabelKey,
        Func<SpeebrunConsistencyTrackerModuleSettings, ButtonBinding> Binding);

    internal static readonly KeybindDef[] Keybinds =
    [
        new(DialogIds.KeyImportTargetTimeId, s => s.Keybind_ImportTargetTime),
        new(DialogIds.KeyStatsExportId,      s => s.Keybind_StatsExport),
        new(DialogIds.ToggleGraphOverlayId,  s => s.Keybind_ToggleGraphOverlay),
        new(DialogIds.KeyNextGraphId,        s => s.Keybind_NextGraph),
        new(DialogIds.KeyPreviousGraphId,    s => s.Keybind_PreviousGraph),
        new(DialogIds.KeyClearStatsId,       s => s.Keybind_ClearStats),
    ];

    private static readonly Buttons[] AllButtons = {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftTrigger, Buttons.RightTrigger,
        Buttons.Back, Buttons.Start,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
    };

    private bool _closing;
    private float _inputDelay;
    private bool _remapping;
    private float _remappingEase;
    private KeybindDef _remappingBind;
    private bool _remappingKeyboard;
    private float _timeout;

    private string RemappingLabel => Dialog.Clean(_remappingBind.LabelKey);

    public KeybindConfigUi() {
        Reload();
        OnESC = OnCancel = () => { Focused = false; _closing = true; };
        MinWidth = 600f;
        Position.Y = ScrollTargetY;
        Alpha = 0f;
    }

    private void Reload(int index = -1) {
        Clear();
        var s = SpeebrunConsistencyTrackerModule.Settings;

        Add(new Header(Dialog.Clean(DialogIds.KeybindConfigId)));
        Add(new SubHeader(Dialog.Clean(DialogIds.KeybindClearSubId)));

        Add(new SubHeader(Dialog.Clean(DialogIds.KeyConfigTitle)));
        foreach (KeybindDef bind in Keybinds)
            Add(new Setting(Dialog.Clean(bind.LabelKey), bind.Binding(s).Keys)
                .Pressed(() => StartRemap(bind, keyboard: true))
                .AltPressed(() => ClearBinding(bind, keyboard: true)));

        Add(new SubHeader(Dialog.Clean(DialogIds.BtnConfigTitle)));
        foreach (KeybindDef bind in Keybinds)
            Add(new Setting(Dialog.Clean(bind.LabelKey), bind.Binding(s).Buttons)
                .Pressed(() => StartRemap(bind, keyboard: false))
                .AltPressed(() => ClearBinding(bind, keyboard: false)));

        if (index >= 0) Selection = index;
    }

    private void StartRemap(KeybindDef bind, bool keyboard) {
        _remapping         = true;
        _remappingBind     = bind;
        _remappingKeyboard = keyboard;
        _timeout           = 5f;
        Focused            = false;
    }

    // Keys.None is not "no key": FNA returns it for any keycode absent from its SDL->XNA table,
    // which on AZERTY is most of the digit row, and a keyboard state reports it held — so a
    // binding carrying it fires on all of them at once. F1/F2/F3/F5 stay unbindable: they are
    // Everest's debug keys, which is what a player needs when a mod misbehaves.
    private static bool IsBindable(Keys key)
        => key != Keys.None && key != Keys.F1 && key != Keys.F2 && key != Keys.F3 && key != Keys.F5;

    private void ApplyRemap<T>(T input, List<T> list) {
        _remapping = false;
        _inputDelay = 0.25f;
        if (!list.Remove(input)) list.Add(input);
        Reload(Selection);
    }

    // Clearing a whole row is the Journal action, which vanilla spells Tab by default and wires
    // the same way (KeyboardConfigUI.AddMapForceLabel -> AltPressed -> Clear). Naming the action
    // rather than the key is the point: a player who rebound Journal keeps one gesture for "clear
    // a binding" across the game and every mod that uses Everest's screen.
    //
    // Nothing here refreshes the ButtonBinding's VirtualButton, for the same reason ApplyRemap
    // does not: ComboHotkey reads the Keys list directly every frame and no VirtualButton is on
    // its path. A mod whose hotkeys went through Input would need that refresh here.
    private void ClearBinding(KeybindDef bind, bool keyboard) {
        ButtonBinding binding = bind.Binding(SpeebrunConsistencyTrackerModule.Settings);
        // Already empty: say so the way vanilla says it, rather than redrawing an identical list.
        if ((keyboard ? binding.Keys.Count : binding.Buttons.Count) == 0) {
            Audio.Play("event:/ui/main/button_invalid");
            return;
        }
        if (keyboard) binding.Keys.Clear();
        else binding.Buttons.Clear();
        Reload(Selection);
    }

    private ButtonBinding RemappingBinding
        => _remappingBind.Binding(SpeebrunConsistencyTrackerModule.Settings);

    public override void Update() {
        base.Update();

        if (_inputDelay > 0f && !_remapping) {
            _inputDelay -= Engine.DeltaTime;
            if (_inputDelay <= 0f) Focused = true;
        }

        _remappingEase = Calc.Approach(_remappingEase, _remapping ? 1f : 0f, Engine.DeltaTime * 4f);

        if (_remappingEase > 0.5f && _remapping) {
            if (Input.ESC.Pressed || Input.MenuCancel || _timeout <= 0f) {
                Input.ESC.ConsumePress();
                _remapping = false;
                Focused = true;
            } else if (_remappingKeyboard) {
                Keys[] pressed = MInput.Keyboard.CurrentState.GetPressedKeys();
                if (pressed?.LastOrDefault(IsBindable) is { } k && k != Keys.None && MInput.Keyboard.Pressed(k))
                    ApplyRemap(k, RemappingBinding.Keys);
            } else {
                var cur  = MInput.GamePads[Input.Gamepad].CurrentState;
                var prev = MInput.GamePads[Input.Gamepad].PreviousState;
                foreach (var btn in AllButtons)
                    if (cur.IsButtonDown(btn) && !prev.IsButtonDown(btn)) { ApplyRemap(btn, RemappingBinding.Buttons); break; }
            }
            _timeout -= Engine.DeltaTime;
        }

        // Journal already clears the selected row: TextMenu.Update dispatches OnAltPressed on
        // Input.MenuJournal.Pressed, inside its own `if (Focused)`, so it cannot fire mid-remap.
        // This only widens the gesture to Delete and Backspace, which is what SpeedrunTool's
        // HotkeyConfigUi does -- but through the row's own closure rather than its positional
        // lookup (`Selection - 3`), which breaks silently the moment a header moves.
        if (Focused && !_remapping && !_closing && Current?.OnAltPressed != null
            && (MInput.Keyboard.Pressed(Keys.Delete) || MInput.Keyboard.Pressed(Keys.Back)))
            Current.OnAltPressed();

        Alpha = Calc.Approach(Alpha, _closing ? 0f : 1f, Engine.DeltaTime * 8f);
        if (!_closing || Alpha > 0f) return;

        OnClose?.Invoke();
        Close();
    }

    public override void Render() {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
        base.Render();
        if (_remappingEase <= 0f) return;

        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(_remappingEase));
        Vector2 pos = new Vector2(1920f, 1080f) * 0.5f;

        if (_remappingKeyboard || Input.GuiInputController()) {
            ActiveFont.Draw(
                Dialog.Clean(DialogIds.KeybindComboSubId),
                pos + new Vector2(0f, -32f),
                new Vector2(0.5f, 2f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(_remappingEase));
            ActiveFont.Draw(
                Dialog.Clean(_remappingKeyboard ? DialogIds.KeyConfigChanging : DialogIds.BtnConfigChanging),
                pos + new Vector2(0f, -8f),
                new Vector2(0.5f, 1f), Vector2.One * 0.7f,
                Color.LightGray * Ease.CubeIn(_remappingEase));
            ActiveFont.Draw(
                RemappingLabel,
                pos + new Vector2(0f, 8f),
                new Vector2(0.5f, 0f), Vector2.One * 2f,
                Color.White * Ease.CubeIn(_remappingEase));
        } else {
            ActiveFont.Draw(
                Dialog.Clean(DialogIds.BtnConfigNoController),
                pos, new Vector2(0.5f, 0.5f), Vector2.One,
                Color.White * Ease.CubeIn(_remappingEase));
        }
    }
}
