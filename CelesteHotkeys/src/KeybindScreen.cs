using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.CelesteHotkeys;

/// <summary>The mod-specific half of the remap screen: its strings and how it saves.</summary>
// Dialog ids are global across every loaded mod, so the strings a mod owns are passed in with its
// own prefix. The generic ones reuse vanilla's ids (KeybindScreen.Vanilla*), which the game already
// translates.
internal sealed class KeybindScreenText {
    /// <summary>The screen's title, e.g. "Hotkeys", and the label of the Mod Options row that opens it.</summary>
    public required string HeaderId { get; init; }

    /// <summary>What a combo is and how to unbind one input, drawn on the recording overlay.</summary>
    public required string ComboHintId { get; init; }

    /// <summary>How to clear a whole row: Journal or Delete.</summary>
    public required string ClearHintId { get; init; }

    /// <summary>The countdown while recording. Read raw through Dialog.Get; <c>{0}</c> is the seconds left.</summary>
    // ⚠️ Dialog.Clean deletes every {...} placeholder but {n}, so this one is formatted from Dialog.Get.
    public required string TimeoutFormatId { get; init; }
}

/// <summary>
///     A remap screen built from a keybind table: a keyboard section and a controller section, one
///     row per keybind in each.
/// </summary>
// Merged from three mods' screens that had drifted apart. Every rule below cost one of them a bug.
//
// Open it with HotkeyMenu.OpenButton, which captures the scene.
internal sealed class KeybindScreen<TSettings> : TextMenu where TSettings : class {
    internal const string VanillaKeyboardTitle = "KEY_CONFIG_TITLE";
    internal const string VanillaControllerTitle = "BTN_CONFIG_TITLE";
    internal const string VanillaKeyChanging = "KEY_CONFIG_CHANGING";
    internal const string VanillaButtonChanging = "BTN_CONFIG_CHANGING";
    internal const string VanillaNoController = "BTN_CONFIG_NOCONTROLLER";

    private const float RecordSeconds = 5f;
    private const float RefocusDelay = 0.25f;
    private const string InvalidSound = "event:/ui/main/button_invalid";

    private readonly HotkeySet<TSettings> hotkeys;
    private readonly KeybindScreenText text;
    private readonly Action save;

    private bool closing;
    private bool counted;
    private float refocusDelay;
    private bool recording;
    private float recordingEase;
    private Keybind<TSettings> recordingKeybind;
    private bool recordingKeyboard;
    private float timeout;

    /// <summary>True while the screen is waiting for the key or button to record.</summary>
    internal bool Recording => recording;

    /// <param name="hotkeys">The mod's hotkey set: its table and its live settings.</param>
    /// <param name="text">The mod's own dialog ids.</param>
    /// <param name="save">
    ///     Usually <c>MyModule.Instance.SaveSettings</c>. Called on every change: Everest saves when
    ///     Mod Options closes, but a crash or a force-quit between the two lost the binding.
    /// </param>
    internal KeybindScreen(HotkeySet<TSettings> hotkeys, KeybindScreenText text, Action save) {
        this.hotkeys = hotkeys;
        this.text = text;
        this.save = save;

        Reload();
        OnESC = OnCancel = () => { Focused = false; closing = true; };
        MinWidth = 600f;
        Position.Y = ScrollTargetY;
        Alpha = 0f;
    }

    // ⚠️ Counted on Added and released on Removed OR SceneEnd: a scene change drops the entity
    // without ever calling Removed, and a count left raised would pause every mod's hotkeys until the
    // game restarts. The flag makes the release happen once whichever comes first.
    public override void Added(Scene scene) {
        base.Added(scene);
        if (counted) return;
        counted = true;
        HotkeyPause.RemapScreenOpened();
    }

    public override void Removed(Scene scene) {
        base.Removed(scene);
        Release();
    }

    public override void SceneEnd(Scene scene) {
        base.SceneEnd(scene);
        Release();
    }

    private void Release() {
        if (!counted) return;
        counted = false;
        HotkeyPause.RemapScreenClosed();
    }

    private TSettings Settings => hotkeys.Settings();

    private void Reload(int index = -1) {
        Clear();

        Add(new Header(Dialog.Clean(text.HeaderId)));
        // The clear hint on the menu, where the gesture is used; the combo hint on the recording
        // overlay, where it is. A SubHeader is one line, and the combo hint is too long for one.
        Add(new SubHeader(Dialog.Clean(text.ClearHintId)));

        // Both sections walk the same table, so a keyboard row cannot exist without its controller
        // counterpart.
        Add(new SubHeader(Dialog.Clean(VanillaKeyboardTitle)));
        foreach (Keybind<TSettings> keybind in hotkeys.Keybinds) {
            ButtonBinding binding = keybind.Binding(Settings);
            if (binding is null) continue;
            Add(new Setting(Dialog.Clean(keybind.LabelId), binding.Keys)
                .Pressed(() => StartRecording(keybind, keyboard: true))
                .AltPressed(() => ClearRow(keybind, keyboard: true)));
        }

        Add(new SubHeader(Dialog.Clean(VanillaControllerTitle)));
        foreach (Keybind<TSettings> keybind in hotkeys.Keybinds) {
            ButtonBinding binding = keybind.Binding(Settings);
            if (binding is null) continue;
            Add(new Setting(Dialog.Clean(keybind.LabelId), binding.Buttons)
                .Pressed(() => StartRecording(keybind, keyboard: false))
                .AltPressed(() => ClearRow(keybind, keyboard: false)));
        }

        if (index >= 0) Selection = index;
    }

    private void StartRecording(Keybind<TSettings> keybind, bool keyboard) {
        recording = true;
        recordingKeybind = keybind;
        recordingKeyboard = keyboard;
        timeout = RecordSeconds;
        Focused = false;
    }

    private void Record<T>(List<T> inputs, T input) {
        recording = false;
        refocusDelay = RefocusDelay;
        Bindable.Toggle(inputs, input);
        Changed();
    }

    // Clearing a whole row is the Journal action — vanilla's own gesture, wired the same way
    // (KeyboardConfigUI → AltPressed → Clear), so a player who rebound Journal keeps one gesture for
    // "clear a binding" across the game and every mod — and Delete, handled in Update.
    private void ClearRow(Keybind<TSettings> keybind, bool keyboard) {
        ButtonBinding binding = keybind.Binding(Settings);
        if (binding is null) return;

        // Already empty: say so the way vanilla says it rather than redraw an identical list.
        if ((keyboard ? binding.Keys.Count : binding.Buttons.Count) == 0) {
            Audio.Play(InvalidSound);
            return;
        }
        if (keyboard) binding.Keys.Clear();
        else binding.Buttons.Clear();
        Changed();
    }

    private void Changed() {
        save?.Invoke();
        // Belt and braces: the screen being open already pauses every hotkey while tracking what is
        // held, so the input just recorded cannot fire when it closes. This covers a mod polling
        // somewhere that runs before the screen is counted.
        hotkeys.Resync();
        Reload(Selection);
    }

    public override void Update() {
        base.Update();

        // ⚠️ RawDeltaTime throughout, never DeltaTime. These timers measure how long the player has
        // been looking at a menu, and DeltaTime carries Engine.TimeRate and Assist Mode's game speed:
        // at 50% the five-second timeout became ten real seconds.
        if (refocusDelay > 0f && !recording) {
            refocusDelay -= Engine.RawDeltaTime;
            if (refocusDelay <= 0f) Focused = true;
        }

        recordingEase = Calc.Approach(recordingEase, recording ? 1f : 0f, Engine.RawDeltaTime * 4f);

        if (recordingEase > 0.5f && recording) {
            // ⚠️ Escape and the timeout ONLY — never Input.MenuCancel. Cancelling on it made the
            // player's own cancel input unbindable: B on a controller, and whatever their keyboard
            // cancel is, were consumed as "stop recording" and could never be recorded. Vanilla's and
            // Everest's remap screens take Escape or the timeout for the same reason.
            if (Input.ESC.Pressed || timeout <= 0f) {
                Input.ESC.ConsumePress();
                recording = false;
                Focused = true;
            } else if (recordingKeyboard) {
                Bindable.KeyPress press = Bindable.ReadKeyPress(
                    MInput.Keyboard.CurrentState.GetPressedKeys(), MInput.Keyboard.Pressed, out Keys key);
                if (press == Bindable.KeyPress.Bindable) Record(recordingKeybind.Binding(Settings).Keys, key);
                else if (press == Bindable.KeyPress.Refused) Audio.Play(InvalidSound);
            } else {
                Buttons? button = Bindable.NewlyPressedButton(
                    MInput.GamePads[Input.Gamepad].CurrentState, MInput.GamePads[Input.Gamepad].PreviousState);
                if (button.HasValue) Record(recordingKeybind.Binding(Settings).Buttons, button.Value);
            }
            timeout -= Engine.RawDeltaTime;
        }

        // Journal already clears the selected row: TextMenu.Update dispatches OnAltPressed on
        // Input.MenuJournal.Pressed inside its own `if (Focused)`, so it cannot fire mid-recording.
        // This widens the gesture to Delete through the row's own closure.
        //
        // ⚠️ Delete and NOT Backspace. Vanilla claims Backspace as menu cancel, so base.Update() above
        // has already started closing the screen by the time a Backspace check would run
        // (that shipped once). Focused is false for a quarter second
        // after a recording, so a Delete just recorded cannot also clear the row.
        if (Focused && !recording && !closing && Current?.OnAltPressed != null
            && MInput.Keyboard.Pressed(Keys.Delete)) {
            Current.OnAltPressed();
        }

        Alpha = Calc.Approach(Alpha, closing ? 0f : 1f, Engine.RawDeltaTime * 8f);
        if (!closing || Alpha > 0f) return;

        OnClose?.Invoke();
        Close();
    }

    // The overlay's lines are the mod's own text, one line each. A line wider than this is drawn
    // smaller instead of running off both edges of the screen.
    internal const float MaxLineWidth = 1760f;

    /// <summary><paramref name="preferred"/>, or less if the line would be wider than <see cref="MaxLineWidth"/>.</summary>
    internal static float FitScale(string line, float preferred) {
        float width = ActiveFont.Measure(line).X * preferred;
        return width <= MaxLineWidth ? preferred : preferred * MaxLineWidth / width;
    }

    public override void Render() {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
        base.Render();
        if (recordingEase <= 0f) return;

        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(recordingEase));
        Vector2 centre = new Vector2(1920f, 1080f) * 0.5f;
        Color grey = Color.LightGray * Ease.CubeIn(recordingEase);

        if (!recordingKeyboard && !Input.GuiInputController()) {
            ActiveFont.Draw(Dialog.Clean(VanillaNoController), centre, new Vector2(0.5f, 0.5f), Vector2.One,
                            Color.White * Ease.CubeIn(recordingEase));
            return;
        }

        string hint = Dialog.Clean(text.ComboHintId);
        ActiveFont.Draw(hint, centre + new Vector2(0f, -32f),
                        new Vector2(0.5f, 2f), Vector2.One * FitScale(hint, 0.7f), grey);
        ActiveFont.Draw(Dialog.Clean(recordingKeyboard ? VanillaKeyChanging : VanillaButtonChanging),
                        centre + new Vector2(0f, -8f), new Vector2(0.5f, 1f), Vector2.One * 0.7f, grey);
        string label = Dialog.Clean(recordingKeybind.LabelId);
        float labelScale = FitScale(label, 2f);
        ActiveFont.Draw(label, centre + new Vector2(0f, 8f),
                        new Vector2(0.5f, 0f), Vector2.One * labelScale, Color.White * Ease.CubeIn(recordingEase));
        // The screen used to give up after five seconds with nothing said, which reads as the binding
        // having failed. Ceiling, so the first thing the player sees is the full five and the last
        // whole second is not skipped.
        //
        // ⚠️ Placed from the font, not by a constant: the label above is top-justified at labelScale
        // from +8, so it ends labelScale × LineHeight lower, and any fixed offset crosses it at some
        // font size or label length.
        float belowLabel = 8f + ActiveFont.LineHeight * labelScale + 8f;
        ActiveFont.Draw(string.Format(Dialog.Get(text.TimeoutFormatId), (int) Math.Ceiling(Math.Max(0f, timeout))),
                        centre + new Vector2(0f, belowLabel), new Vector2(0.5f, 0f), Vector2.One * 0.7f, grey);
    }
}
