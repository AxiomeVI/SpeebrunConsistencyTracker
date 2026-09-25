using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.UI;

/// <summary>
///     Typed entry for the target time, over the Mod Options menu. Offers only what a time can hold,
///     shows the current time until the first key replaces it, and reads AZERTY's number row as digits.
/// </summary>
// Everest's OuiModOptionString did this before, and none of those three can be configured there.
//
// Typing and the grid both work, whatever Everest's Use Keyboard For Text Input says. A key that
// types something here skips the menu input for that frame: a player can bind menu directions or
// Confirm to the number row, and typed Enter and Backspace are MenuConfirm and MenuCancel too, so
// reading both would move, confirm or erase as well. A key typing nothing, such as vanilla's C for
// Confirm, still drives the grid. SDL delivers text before Engine.Update, so the flag is set first.
internal sealed class TargetTimeEntry : Entity {
    private const string CancelId = "name_back";
    private const string BackspaceId = "name_backspace";
    private const string AcceptId = "name_accept";
    private const float InputDelay = 0.2f;
    // Every row is three wide, the actions included, so Up and Down keep the column.
    private const int Columns = 3;
    private static readonly int KeypadRows = TimeEntry.GridCharacters.Length / Columns;
    private const float CellWidth = 120f;
    private const float RowHeight = 90f;

    private readonly TimeEntry entry = new();
    private readonly string current;
    private readonly Action<string> onConfirm;
    private readonly Action onClose;
    private readonly Wiggler wiggler;

    private float delay = InputDelay;
    private bool typedThisFrame;
    private bool finishRequested;
    private float ease;
    private bool closing;
    private bool counted;
    private bool commandsWereEnabled;
    // Rows 0 to KeypadRows - 1 are the keypad, the last one the three actions.
    private int row;
    private int column;

    /// <param name="onConfirm">Called with what was typed; never with an empty entry.</param>
    public TargetTimeEntry(string current, Action<string> onConfirm, Action onClose) {
        this.current = current;
        this.onConfirm = onConfirm;
        this.onClose = onClose;
        Tag = Tags.HUD | Tags.PauseUpdate;
        Depth = -1000000;
        Add(wiggler = Wiggler.Create(0.25f, 4f));
    }

    public override void Added(Scene scene) {
        base.Added(scene);
        // Keys typed here must not fire any mod's hotkey, nor open the debug console.
        CelesteHotkeys.HotkeyPause.RemapScreenOpened();
        counted = true;
        commandsWereEnabled = Engine.Commands.Enabled;
        Engine.Commands.Enabled = false;
        TextInput.OnInput += OnTextInput;
    }

    public override void Removed(Scene scene) {
        Release();
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene) {
        Release();
        base.SceneEnd(scene);
    }

    private void Release() {
        TextInput.OnInput -= OnTextInput;
        if (!counted) return;
        counted = false;
        CelesteHotkeys.HotkeyPause.RemapScreenClosed();
        Engine.Commands.Enabled = commandsWereEnabled;
    }

    // ⚠️ Enter only REQUESTS Finish; Update acts on it. Text arrives before MInput.Update, which
    // clears a ConsumePress made now, so closing here let the refocused submenu read Enter as
    // Confirm on the button that opens this screen, and a new one opened at once.
    private void OnTextInput(char c) {
        if (closing || delay > 0f) return;
        if (c == '\r') finishRequested = true;
        else if (c == '\b') Backspace();
        else if (TimeEntry.Normalize(c) is null) return;
        else TypeChar(c);
        typedThisFrame = true;
    }

    public override void Update() {
        base.Update();
        ease = Calc.Approach(ease, closing ? 0f : 1f, Engine.RawDeltaTime * 6f);
        if (closing) {
            if (ease <= 0f) RemoveSelf();
            return;
        }
        if (delay > 0f) {
            delay -= Engine.RawDeltaTime;
            return;
        }

        if (Input.ESC.Pressed) {
            Cancel();
            return;
        }
        if (finishRequested) {
            finishRequested = false;
            typedThisFrame = false;
            Finish();
            return;
        }
        if (typedThisFrame) {
            typedThisFrame = false;
            Input.MenuConfirm.ConsumePress();
            Input.MenuCancel.ConsumePress();
            return;
        }

        int rows = KeypadRows + 1;
        if (Input.MenuRight.Pressed) Move(() => column = (column + 1) % Columns);
        else if (Input.MenuLeft.Pressed) Move(() => column = (column + Columns - 1) % Columns);
        else if (Input.MenuDown.Pressed) Move(() => row = (row + 1) % rows);
        else if (Input.MenuUp.Pressed) Move(() => row = (row + rows - 1) % rows);
        else if (Input.MenuConfirm.Pressed) {
            if (row < KeypadRows) TypeChar(TimeEntry.GridCharacters[row * Columns + column]);
            else if (column == 0) Cancel();
            else if (column == 1) Backspace();
            else Finish();
        } else if (Input.MenuCancel.Pressed) {
            if (entry.Text.Length > 0) Backspace();
            else Cancel();
        }
    }

    private void Move(Action move) {
        move();
        wiggler.Start();
        Audio.Play("event:/ui/main/rename_entry_rollover");
    }

    private void TypeChar(char c) {
        if (entry.Type(c)) {
            wiggler.Start();
            Audio.Play("event:/ui/main/rename_entry_char");
        } else {
            Audio.Play("event:/ui/main/button_invalid");
        }
    }

    private void Backspace() {
        Audio.Play(entry.Backspace() ? "event:/ui/main/rename_entry_backspace" : "event:/ui/main/button_invalid");
    }

    // Confirming with nothing typed keeps the current time: the empty string parses as zero, and
    // Reset target time is the button for that.
    private void Finish() {
        if (entry.Text.Length == 0) {
            Cancel();
            return;
        }
        Audio.Play("event:/ui/main/rename_entry_accept");
        Close();
        onConfirm(entry.Text);
    }

    private void Cancel() {
        Audio.Play("event:/ui/main/button_back");
        Close();
    }

    private void Close() {
        closing = true;
        Release();
        // The press that closed this must not also act on the menu that gets focus back.
        Input.MenuConfirm.ConsumePress();
        Input.MenuCancel.ConsumePress();
        Input.ESC.ConsumePress();
        onClose();
    }

    public override void Render() {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * ease);
        Color white = Color.White * ease;
        Color grey = Color.Gray * ease;

        ActiveFont.Draw(Dialog.Clean(DialogIds.TargetTimeId), new Vector2(960f, 170f), new Vector2(0.5f, 0.5f), Vector2.One * 1.2f, white);
        // The current time until something is typed, then only what was typed.
        bool typed = entry.Text.Length > 0;
        ActiveFont.DrawOutline(typed ? entry.Text : current, new Vector2(960f, 290f), new Vector2(0.5f, 0.5f),
                               Vector2.One * 2f, typed ? white : grey, 2f, Color.Black * ease);
        ActiveFont.Draw(Dialog.Clean(DialogIds.TargetTimeFormatId), new Vector2(960f, 375f), new Vector2(0.5f, 0.5f), Vector2.One * 0.6f, grey);

        string chars = TimeEntry.GridCharacters;
        for (int i = 0; i < chars.Length; i++) {
            int r = i / Columns, c = i % Columns;
            DrawOption(chars[i].ToString(), new Vector2(960f + (c - 1) * CellWidth, 470f + r * RowHeight),
                       row == r && column == c, 1.2f);
        }

        string[] actions = { Dialog.Clean(CancelId), Dialog.Clean(BackspaceId), Dialog.Clean(AcceptId) };
        float actionsY = 470f + KeypadRows * RowHeight + 30f;
        for (int i = 0; i < actions.Length; i++) {
            DrawOption(actions[i], new Vector2(960f + (i - 1) * 360f, actionsY), row == KeypadRows && column == i, 0.75f);
        }
    }

    private void DrawOption(string text, Vector2 at, bool selected, float scale) {
        if (selected) at += new Vector2(0f, wiggler.Value * 8f);
        Color color = (selected ? Calc.HexToColor("84FF54") : Color.White) * ease;
        ActiveFont.DrawOutline(text, at, new Vector2(0.5f, 0.5f), Vector2.One * scale * (selected ? 1.2f : 1f), color, 2f, Color.Black * ease);
    }
}
