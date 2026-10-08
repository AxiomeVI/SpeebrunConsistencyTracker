// MenuTools requires: TextMenuPage.cs MenuToolsDialog.cs IInputHoldingItem.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Core;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// Generic string entry menu: a letter grid with Back, Backspace and Accept options under it, and with Everest's
/// keyboard text input setting on, typing with a caret. Register a handler for <see cref="OnAccepted"/> to receive
/// the entered string.
/// </summary>
/// <remarks>
/// The Enter key accepts the string, in typing mode and on the letter grid, although it is Pause's default key; on
/// the grid, an Enter bound to Confirm is Confirm instead. ESC and Pause cancel the entry: the page goes back one
/// page and invokes <see cref="OnCanceled"/>. Cancel deletes the last character, and cancels only when the string is
/// empty and half a second has passed since its last deletion. In typing mode a keyboard key bound to Pause is text
/// and does not cancel; a controller's Pause still does. On the letter grid any other key bound to Pause cancels.
/// <para/>
/// The page is not used as a menu: handlers assigned to <see cref="TextMenu.OnCancel"/>, <see cref="TextMenu.OnESC"/>
/// or <see cref="TextMenu.OnPause"/> never run, and items added to it are neither updated nor drawn. This holds for
/// <see cref="NumberEntryPage"/> and <see cref="ColorEntryPage"/> too.
/// </remarks>
public class StringEntryPage : TextMenuPage {
    /// <summary>
    /// Invoked with the entered string when the player accepts it, after the page has returned and after
    /// <see cref="TextMenuPage.OnReturned"/>
    /// </summary>
    public Action<string> OnAccepted;
    /// <summary>
    /// Invoked when the player cancels the entry, instead of <see cref="OnAccepted"/>, after the page has returned
    /// and after <see cref="TextMenuPage.OnReturned"/>. Not invoked when <see cref="TextMenuPage.Return"/> is called
    /// from code, or when the scene ends under the page.
    /// </summary>
    public Action OnCanceled;
    /// <summary>
    /// Optional extra check on the entered string: accepting is only possible while it returns true
    /// (on top of the length limits)
    /// </summary>
    public Func<string, bool> Validator;

    // Represents the buttons, for cancel, accept, backspace, etc.
    private class SpecialOption {
        public SpecialOption(string text, float scale, Action onPressed, Func<bool> enable) {
            Text      = text;
            Width     = ActiveFont.Measure(text).X * scale;
            Scale     = scale;
            OnPressed = onPressed;
            Enable    = enable;
        }
        public readonly string Text;
        public readonly float Width;
        public readonly float Scale;
        public readonly Action OnPressed;
        public readonly Func<bool> Enable;
    }

    private const float optionsScale = 0.75f;
    private const float acceptScale  = 0.75f * 1.25f;  // Vanilla's name entry draws Accept larger too
    private const float valueScale   = 2f;
    private const float caretGap     = 4f;  // Between the caret and the character before it
    private const float counterScale = 0.75f;
    // How long after Cancel deleted a character a Cancel on the emptied string is ignored: a player who deletes with
    // repeated presses must not leave the page with one press too many
    private const float cancelGuardTime = 0.5f;
    private const float needScale    = 0.5f;
    private const float hintScale    = 0.5f;
    private const float hintKeyGap   = 8f;   // Between a hint's text and its key, and between two keys, before scaling
    private const float hintSpacing  = 32f;  // Between two hints

    // Right end of the length counter, on the line of the entered string and clear of anything drawn after it
    private static readonly Vector2 counterPos = new(1840f, 300f);

    // Right end of the line that says what the value needs, under the length counter
    private static readonly Vector2 needPos = new(1840f, 345f);

    // Right end of the key hints: where the overworld draws its Confirm and Back hints
    private static readonly Vector2 hintsPos = new(1880f, 1024f);

    // User options
    private readonly string letters;
    private readonly string[] lines;
    private readonly List<SpecialOption> options;
    private readonly int minValueLength;
    private readonly int maxValueLength;
    private readonly bool allowSpaces;

    // Layout calculations
    // The box position and padding follow vanilla's name entry screen
    private readonly float widestLetter;
    private readonly int widestLineCount;
    private readonly float widestLine;
    private readonly float lineHeight;
    private readonly float lineSpacing;
    private readonly float optionsWidth;
    private readonly float boxPadding;
    private readonly float boxWidth;
    private readonly float boxHeight;
    private readonly Vector2 boxTopLeft;

    private string value           = "";
    // Where typed text goes, as an index into value. Only moved by the keyboard; controller input keeps it at the end.
    private int caret              = 0;
    private float caretBlinkTimer  = 0f;
    private float cancelGuardTimer = 0f;
    private readonly string prompt;
    private readonly string header;

    private int charIdx;
    private int lineIdx;
    private bool selectingOptions;
    private int optionIdx;

    private Wiggler wiggler;

    private readonly string acceptHint = MenuToolsDialog.Get("ENTRY_ACCEPT", "Accept");
    private readonly string cancelHint = MenuToolsDialog.Get("ENTRY_CANCEL", "Cancel");
    private readonly string selectHint = MenuToolsDialog.Get("ENTRY_SELECT", "Select");
    private readonly string deleteHint = MenuToolsDialog.Get("ENTRY_DELETE", "Delete");
    private readonly string pasteHint  = MenuToolsDialog.Get("ENTRY_PASTE", "Paste");

    private bool consoleWasEnabled;

    private Color unselectColor = Color.LightGray;
    private Color selectColor   = Calc.HexToColor("84FF54");
    private Color disableColor  = Color.DarkSlateBlue;

    /// <summary>The string entered so far</summary>
    protected string Value => value;

    /// <summary>
    /// Screen X position of the right edge of the area where the entered string is drawn, as of the last render
    /// </summary>
    protected float ValueRight { get; private set; }

    private bool UseKeyboardInput => CoreModule.Settings.UseKeyboardForTextInput;

    // Whether the player is typing rather than using the letter grid. With Everest's keyboard text input setting on,
    // the page starts in typing mode, a controller press switches it to the grid, and typing switches it back.
    private bool typing;

    private static readonly Buttons[] gridSwitchButtons = [
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        Buttons.LeftThumbstickUp, Buttons.LeftThumbstickDown, Buttons.LeftThumbstickLeft, Buttons.LeftThumbstickRight,
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
    ];

    // ================================================= Construction ==================================================
    /// <param name="parent">The menu to return to. Must not be null.</param>
    /// <param name="subMenuParent">The recursive submenu the page is entered from, if any</param>
    /// <param name="letters">
    ///     The letter grid: its lines separated by '\n', with spaces for gaps. Vanilla's name entry grid, in the
    ///     player's language, if null (the default). A typed or pasted letter that the grid only has in the other
    ///     case is entered in the grid's case.
    /// </param>
    /// <param name="prompt">
    ///     Text shown before the entered string, like the '#' of a color code; none if empty (the default)
    /// </param>
    /// <param name="header">Text shown at the top of the page. Default: none.</param>
    /// <param name="minValueLength">
    ///     Shortest string that can be accepted. Default: 0, so the empty string can be accepted. A value greater
    ///     than <paramref name="maxValueLength"/> is not refused: no string can then be accepted, and the player can
    ///     only cancel.
    /// </param>
    /// <param name="maxValueLength">
    ///     Longest string that can be entered. Default: 12, vanilla's limit for a name.
    /// </param>
    /// <param name="allowSpaces">
    ///     Whether spaces can be entered, with a Space option next to Backspace. Default: false.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="letters"/> has no letter, only spaces and lines</exception>
    public StringEntryPage(TextMenu parent, IInputHoldingItem subMenuParent = null, string letters = null,
                           string prompt = "", string header = "", int minValueLength = 0, int maxValueLength = 12,
                           bool allowSpaces = false)
            : base(parent, subMenuParent) {
        AutoScroll = false;

        // Layout lines are split on \n, so a \r from CRLF text would otherwise become a selectable letter
        letters             = (letters ?? Dialog.Clean("NAME_LETTERS")).Replace("\r", "");
        this.letters        = letters;
        this.prompt         = prompt;
        this.header         = header;
        this.minValueLength = minValueLength;
        this.maxValueLength = maxValueLength;
        this.allowSpaces    = allowSpaces;

        lines = letters.Split('\n');
        if (!lines.Any((string line) => line.Any((char c) => c != ' '))) {
            throw new ArgumentException("The letter layout has no letters", nameof(letters));
        }

        options = [new SpecialOption(Dialog.Clean("name_back"), optionsScale, Cancel, () => true)];
        if (allowSpaces) {
            options.Add(new SpecialOption(Dialog.Clean("name_space"), optionsScale, Space, CanSpace));
        }
        if (OffersPaste) {
            options.Add(new SpecialOption(pasteHint, optionsScale, PasteClipboard, () => true));
        }
        options.AddRange([
            new SpecialOption(Dialog.Clean("name_backspace"), optionsScale, Backspace, CanBackspace),
            new SpecialOption(Dialog.Clean("name_accept"), acceptScale, Accept, CanAccept),
        ]);

        // Start on the first letter, which isn't necessarily at the top left of the layout
        while (ClosestLetterInLine(lineIdx, 0) < 0) {
            ++lineIdx;
        }
        charIdx = ClosestLetterInLine(lineIdx, 0);

        widestLetter    = letters.Select((char c) => ActiveFont.Measure(c).X).Max();
        widestLineCount = lines.Select((string line) => line.Length).Max();
        widestLine      = widestLineCount * widestLetter;
        lineHeight      = ActiveFont.LineHeight;
        lineSpacing     = ActiveFont.LineHeight * 0.1f;
        optionsWidth    = options.Select((SpecialOption option) => option.Width).Sum() +
                              widestLetter * (options.Count - 1);
        boxPadding      = widestLetter;
        boxWidth        = Math.Max(widestLine, optionsWidth) + boxPadding * 2f;
        boxHeight       = (lines.Length + 1f) * lineHeight + lines.Length * lineSpacing + boxPadding * 3f;
        boxTopLeft      = new Vector2((1920f - boxWidth) / 2f, 360f + (680f - boxHeight) / 2f);

        wiggler = Wiggler.Create(0.25f, 4f);
    }

    // ============================================= Entering and leaving ==============================================
    /// <summary>Enter the page with nothing entered. Does nothing if the page is already entered.</summary>
    public override void Enter() {
        Enter("");
    }

    /// <summary>
    /// Enter the page with some initial text, keeping only the characters that could be entered on this page, up to
    /// the maximum length. Does nothing if the page is already entered.
    /// </summary>
    /// <param name="initialValue">The text entered at first; null is the empty string</param>
    public void Enter(string initialValue) {
        if (Entered) {
            return;
        }
        SetValue(initialValue);
        base.Enter();
    }

    /// <summary>
    /// Starts receiving typed text, and starts in typing mode if Everest's keyboard text input setting is on. Hides
    /// the overworld's Confirm and Back hints, which the page replaces with its own, and keeps the debug console
    /// from opening. An override must call the base method.
    /// </summary>
    protected override void SetUp() {
        typing           = UseKeyboardInput;
        cancelGuardTimer = 0f;
        TextInput.OnInput -= OnTextInput;
        TextInput.OnInput += OnTextInput;
        // The debug console opens on . and ~ by default, which are text here
        consoleWasEnabled       = Engine.Commands.Enabled;
        Engine.Commands.Enabled = false;
        // Those hints name the bound buttons, which here type, enter a letter or delete one
        HideOverworldHints();
        if (!typing) {
            wiggler.Start();
        }
    }

    /// <summary>
    /// Stops receiving typed text as soon as the page returns: it stays in the scene until the next frame. Gives
    /// the overworld its hints back. An override must call the base method.
    /// </summary>
    protected override void CleanUp() {
        TextInput.OnInput -= OnTextInput;
        Engine.Commands.Enabled = consoleWasEnabled;
        RestoreOverworldHints();
    }

    /// <summary>
    /// Replace the entered string, keeping only the characters that could be entered on this page, and put the caret
    /// at its end. Each character is checked as if entered in turn: the grid has it (in either case), or it is an
    /// allowed space, and <see cref="CanInsert"/> takes it at the end of what is kept so far. The rest is dropped,
    /// and so is everything past the maximum length. The minimum length is not checked.
    /// </summary>
    /// <param name="newValue">The text to enter; null is the empty string</param>
    protected void SetValue(string newValue) {
        value = "";
        caret = 0;
        InsertAccepted(newValue ?? "");
        caretBlinkTimer = 0f;
    }

    // Inserts at the caret the characters that could be entered there one by one, up to the maximum length
    private void InsertAccepted(string text) {
        foreach (char c in text) {
            if (value.Length >= maxValueLength) {
                break;
            }
            if (AcceptedChar(c) is char accepted && CanInsert(accepted, caret)) {
                InsertText(accepted.ToString());
            }
        }
    }

    // Enters a character at the caret, with the sound of a letter taken or refused
    private bool TryInsert(char? c) {
        bool taken = c is char accepted && value.Length < maxValueLength && CanInsert(accepted, caret);
        if (taken) {
            InsertText(c.Value.ToString());
        }
        Audio.Play(taken ? SFX.ui_main_rename_entry_char : SFX.ui_main_button_invalid);
        return taken;
    }

    /// <summary>
    /// Enter pasted text at the caret. By default this keeps the characters that could have been entered one by one,
    /// until the maximum length is reached; override it to read the text as a whole. The page calls it for Ctrl+V,
    /// and for the Paste option of a page that has one.
    /// </summary>
    /// <param name="text">The clipboard text; never null when the page calls it</param>
    /// <returns>
    ///     Whether the paste was taken, which by default means that at least one character went in; if not, the page
    ///     plays the invalid sound
    /// </returns>
    protected virtual bool Paste(string text) {
        int oldLength = value.Length;
        InsertAccepted(text);
        return value.Length > oldLength;
    }

    // ================================================ Input handling =================================================
    private void OnTextInput(char c) {
        // Unfocused: another page is open over this one, and the text is for it
        if (!UseKeyboardInput || !Focused) {
            return;
        }
        // Text input arrives before Update, so the keypress that switches back to typing isn't also read as grid input
        typing = true;

        if (c == (char) 8) {
            Backspace();

        } else if (c == (char) 127) {
            // Delete: like Backspace, it comes as a character, repeated while the key is held
            DeleteForward();

        } else if (c == (char) 22) {
            PasteClipboard();

        } else if (!char.IsControl(c)) {
            TryInsert(AcceptedChar(c));
        }
    }

    private void ProcessNonTextInputs() {
        if (Input.MenuRight.Pressed || Input.MenuLeft.Pressed) {
            int dir = Input.MenuRight.Pressed ? 1 : -1;
            if (selectingOptions) {
                MoveOptionSelection(dir);
            } else {
                MoveInLine(dir);
            }
            wiggler.Start();
            Audio.Play(SFX.ui_main_rename_entry_roll);

        } else if (Input.MenuDown.Pressed && !selectingOptions) {
            MoveDown();
            wiggler.Start();
            Audio.Play(SFX.ui_main_rename_entry_roll);

        } else if ((Input.MenuUp.Pressed && (lineIdx > 0 || selectingOptions)) ||
                       (selectingOptions && !options[optionIdx].Enable())) {
            MoveUp();
            wiggler.Start();
            Audio.Play(SFX.ui_main_rename_entry_roll);

        } else if (Input.MenuConfirm.Pressed) {
            if (selectingOptions) {
                options[optionIdx].OnPressed();
            } else if (TryInsert(lines[lineIdx][charIdx])) {
                wiggler.Start();
            }

        } else if (Input.MenuCancel.Pressed) {
            if (value.Length > 0) {
                Backspace();
                cancelGuardTimer = cancelGuardTime;
            } else if (cancelGuardTimer > 0f) {
                // Still deleting: each ignored press starts the wait again
                cancelGuardTimer = cancelGuardTime;
                Audio.Play(SFX.ui_main_button_invalid);
            } else {
                Cancel();
            }
        }
    }

    /// <summary>
    /// Handles the page's input in place of the menu's own: ESC, Pause and Enter, the letter grid, and the keys of
    /// typing mode
    /// </summary>
    public override void Update() {
        cancelGuardTimer = Math.Max(0f, cancelGuardTimer - Engine.RawDeltaTime);
        if (!Focused) {
            // Another page is open over this one: the buttons are for it
            return;
        }
        // Escape doesn't show up in the text inputs and enter has a synchronization issue with
        // ConsumePress(), so process them here instead of in OnTextInput
        if (Input.ESC.Pressed || PausePressed()) {
            Cancel();
        } else if (!UseKeyboardInput && PasteShortcutPressed()) {
            // With keyboard text input, Ctrl+V arrives as typed text instead
            PasteClipboard();
        } else if (typing && GamepadPressed()) {
            // Switch to the letter grid; the press only switches, so it doesn't also enter a letter or cancel
            typing = false;
            caret  = value.Length;
            wiggler.Start();
            Audio.Play(SFX.ui_main_rename_entry_roll);
        } else if (MInput.Keyboard.Pressed(Keys.Enter) &&
                       (typing || !Input.MenuConfirm.Binding.Keyboard.Contains(Keys.Enter))) {
            // On the grid, an Enter bound to Confirm is Confirm: it must still select a letter
            Accept();
        } else if (typing) {
            ProcessCaretKeys();
        } else {
            ProcessNonTextInputs();
        }
        caretBlinkTimer += Engine.RawDeltaTime;
        wiggler.Update();
    }

    // Pause goes back one page, like ESC. Enter is its default key and accepts instead, and while typing any other
    // key bound to it is text: the key's buffered press is dropped so that it can't cancel a few frames later. So
    // while typing only a controller's Pause counts.
    private bool PausePressed() {
        bool keyHeld = typing ? Input.Pause.Binding.Keyboard.Any((Keys key) => MInput.Keyboard.Check(key))
                              : MInput.Keyboard.Check(Keys.Enter);
        if (keyHeld) {
            Input.Pause.ConsumeBuffer();
            return false;
        }
        return Input.Pause.Pressed && (typing || !Input.MenuConfirm.Pressed);
    }

    // Ctrl+V read from the keys, for a page that is not getting typed text. Drops the press of a menu button bound
    // to V, so that the paste is not also a Confirm or a Cancel.
    internal static bool PasteShortcutPressed() {
        if (!MInput.Keyboard.Pressed(Keys.V) ||
                !(MInput.Keyboard.Check(Keys.LeftControl) || MInput.Keyboard.Check(Keys.RightControl))) {
            return false;
        }
        foreach (VirtualButton button in new[] { Input.MenuConfirm, Input.MenuCancel, Input.Pause }) {
            if (button.Binding.Keyboard.Contains(Keys.V)) {
                button.ConsumePress();
            }
        }
        return true;
    }

    private static bool GamepadPressed() {
        MInput.GamePadData pad = MInput.GamePads[Input.Gamepad];
        return pad.Attached && gridSwitchButtons.Any(pad.Pressed);
    }

    // Caret movement. Delete comes through OnTextInput, which repeats it while the key is held.
    private void ProcessCaretKeys() {
        if (MInput.Keyboard.Pressed(Microsoft.Xna.Framework.Input.Keys.Left)) {
            MoveCaret(-1);
        } else if (MInput.Keyboard.Pressed(Microsoft.Xna.Framework.Input.Keys.Right)) {
            MoveCaret(1);
        } else if (MInput.Keyboard.Pressed(Microsoft.Xna.Framework.Input.Keys.Home)) {
            MoveCaret(-value.Length);
        } else if (MInput.Keyboard.Pressed(Microsoft.Xna.Framework.Input.Keys.End)) {
            MoveCaret(value.Length);
        }
    }

    private void MoveCaret(int offset) {
        caret           = Calc.Clamp(caret + offset, 0, value.Length);
        caretBlinkTimer = 0f;
    }

    private void InsertText(string text) {
        value            = value.Insert(caret, text);
        caret           += text.Length;
        caretBlinkTimer  = 0f;
    }

    // ============================================ Cursor movement helpers ============================================
    // The cursor is always on a letter (a non-space character) unless it's in the options row

    private bool IsLetter(int findLineIdx, int findCharIdx) {
        return findCharIdx >= 0 && findCharIdx < lines[findLineIdx].Length && lines[findLineIdx][findCharIdx] != ' ';
    }

    // Column of the letter closest to findCharIdx in the line (preferring the left on ties), or -1 if it has none
    private int ClosestLetterInLine(int findLineIdx, int findCharIdx) {
        for (int distance = 0; distance < Math.Max(lines[findLineIdx].Length, findCharIdx + 1); ++distance) {
            if (IsLetter(findLineIdx, findCharIdx - distance)) {
                return findCharIdx - distance;
            }
            if (IsLetter(findLineIdx, findCharIdx + distance)) {
                return findCharIdx + distance;
            }
        }
        return -1;
    }

    // Move to the next letter in the current line, wrapping around
    private void MoveInLine(int dir) {
        int length = lines[lineIdx].Length;
        for (int step = 1; step <= length; ++step) {
            int candidateCharIdx = ((charIdx + dir * step) % length + length) % length;
            if (IsLetter(lineIdx, candidateCharIdx)) {
                charIdx = candidateCharIdx;
                return;
            }
        }
    }

    // Move to the next line below with a letter in the same column, or into the options row if there's none
    private void MoveDown() {
        for (int searchLineIdx = lineIdx + 1; searchLineIdx < lines.Length; ++searchLineIdx) {
            if (IsLetter(searchLineIdx, charIdx)) {
                lineIdx = searchLineIdx;
                return;
            }
        }
        selectingOptions = true;
        EnterOptionsFromX(GetCharPosition(lineIdx, charIdx).X);
    }

    // Move to the next line above with a letter in the same column, or failing that the next line above with any
    // letter, at its closest column. Coming from the options row, start from the column closest to the option.
    private void MoveUp() {
        if (selectingOptions) {
            lineIdx          = lines.Length;
            selectingOptions = false;
            ExitOptionsToCharIdx();
        }
        for (int searchLineIdx = lineIdx - 1; searchLineIdx >= 0; --searchLineIdx) {
            if (IsLetter(searchLineIdx, charIdx)) {
                lineIdx = searchLineIdx;
                return;
            }
        }
        for (int searchLineIdx = lineIdx - 1; searchLineIdx >= 0; --searchLineIdx) {
            int closestCharIdx = ClosestLetterInLine(searchLineIdx, charIdx);
            if (closestCharIdx >= 0) {
                lineIdx = searchLineIdx;
                charIdx = closestCharIdx;
                return;
            }
        }
    }

    // The character to enter for a typed or pasted character, or null if the page doesn't accept it.
    // Letters match case-insensitively when the layout only has the other case (e.g. uppercase hex digits).
    private char? AcceptedChar(char c) {
        if (c == ' ') {
            return allowSpaces ? ' ' : null;
        }
        if (char.IsControl(c)) {
            return null;
        }
        foreach (char candidate in new[] { c, char.ToLowerInvariant(c), char.ToUpperInvariant(c) }) {
            if (letters.Contains(candidate)) {
                return candidate;
            }
        }
        return null;
    }

    private void MoveOptionSelection(int inc) {
        for (int newOptionIdx = optionIdx + inc; newOptionIdx >= 0 && newOptionIdx < options.Count;
                 newOptionIdx += inc) {
            if (options[newOptionIdx].Enable()) {
                optionIdx = newOptionIdx;
                return;
            }
        }
    }

    private void EnterOptionsFromX(float enterX) {
        // Look for the selectable option that minimizes the distance by which the selected character's x position is
        // out of bounds of the x range taken up by that option (that is, the closest option in the x dimension)
        float optionLeftX    = GetOptionLeftX(0);
        int closestOptionIdx = -1;
        float minXError      = float.PositiveInfinity;
        for (int candidateOptionIdx = 0; candidateOptionIdx < options.Count; ++candidateOptionIdx) {
            SpecialOption option = options[candidateOptionIdx];
            float error          = Math.Max(optionLeftX - enterX,
                                            enterX - (optionLeftX + option.Width));
            if (option.Enable() && error < minXError) {
                minXError        = error;
                closestOptionIdx = candidateOptionIdx;
            }
            optionLeftX += option.Width + widestLetter;
        }
        // No selectable option, so no candidate: the selection stays on the letters
        if (closestOptionIdx < 0) {
            selectingOptions = false;
        } else {
            optionIdx = closestOptionIdx;
        }
    }

    private void ExitOptionsToCharIdx() {
        // Find the column of letters among the non-empty columns
        // that is closest to the center of the selected option in the x dimension
        float exitX        = GetOptionLeftX(optionIdx) + options[optionIdx].Width / 2f;
        int closestCharIdx = 0;
        float minError     = float.PositiveInfinity;
        for (int candidateCharIdx = 0; candidateCharIdx < widestLineCount; ++candidateCharIdx) {
            bool columnIsEmpty = true;
            for (int searchLineIdx = 0; searchLineIdx < lines.Length; ++searchLineIdx) {
                columnIsEmpty &= !IsLetter(searchLineIdx, candidateCharIdx);
            }
            float error = Math.Abs(exitX - GetCharPosition(0, candidateCharIdx).X);
            if (!columnIsEmpty && error < minError) {
                minError       = error;
                closestCharIdx = candidateCharIdx;
            }
        }
        charIdx = closestCharIdx;
    }

    // ==================================================== Options ====================================================
    private void Cancel() {
        Audio.Play(SFX.ui_main_button_back);
        Return();
        OnCanceled?.Invoke();
    }

    private void PasteClipboard() {
        bool pasted = Paste(TextInput.GetClipboardText() ?? "");
        Audio.Play(pasted ? SFX.ui_main_rename_entry_char : SFX.ui_main_button_invalid);
    }

    private void Space() {
        if (CanSpace()) {
            Audio.Play(SFX.ui_main_rename_entry_char);
            InsertText(" ");
        }
    }

    private void Backspace() {
        if (CanBackspace()) {
            value = value.Remove(caret - 1, 1);
            MoveCaret(-1);
            Audio.Play(SFX.ui_main_rename_entry_backspace);
        } else {
            Audio.Play(SFX.ui_main_button_invalid);
        }
    }

    private void DeleteForward() {
        if (caret < value.Length) {
            value = value.Remove(caret, 1);
            Audio.Play(SFX.ui_main_rename_entry_backspace);
        } else {
            Audio.Play(SFX.ui_main_button_invalid);
        }
    }

    private void Accept() {
        if (CanAccept()) {
            Audio.Play(SFX.ui_main_button_select);
            // A handler may enter this page again and change value
            string acceptedValue = value;
            Return();
            OnAccepted?.Invoke(acceptedValue);
            Accepted(acceptedValue);
        } else {
            Audio.Play(SFX.ui_main_button_invalid);
        }
    }

    /// <summary>
    /// Called when the player accepts the entered string, after the page has returned and after
    /// <see cref="OnAccepted"/>. For a subclass that reports the value in another form.
    /// </summary>
    /// <param name="acceptedValue">The string that was accepted</param>
    protected virtual void Accepted(string acceptedValue) {}

    private bool CanSpace() {
        return allowSpaces && value.Length < maxValueLength && CanInsert(' ', caret);
    }

    private bool CanBackspace() {
        return caret > 0;
    }

    private bool CanAccept() {
        return value.Length >= minValueLength && value.Length <= maxValueLength && CanAcceptValue(value);
    }

    /// <summary>
    /// What the entered string needs before it can be accepted, drawn small under the length counter; null or empty
    /// for nothing. By default the minimum length, if there is one. <see cref="Validator"/> has no text of its own:
    /// a subclass that restricts the value says how here.
    /// </summary>
    /// <returns>The text to draw, in the player's language</returns>
    protected virtual string RequirementHint() {
        if (minValueLength <= 0) {
            return null;
        }
        if (minValueLength == 1) {
            return MenuToolsDialog.Get("ENTRY_MIN_ONE", "At least 1 character");
        }
        return MenuToolsDialog.Get("ENTRY_MIN", "At least ((count)) characters")
                   .Replace("((count))", minValueLength.ToString());
    }

    /// <summary>
    /// Whether a letter of the layout may be inserted at a position in <see cref="Value"/>, e.g. to only allow some
    /// letters at the start. Checked for every entered, typed or pasted letter, for a space, and for the initial
    /// value. Not checked when a character is deleted. By default every position is allowed.
    /// </summary>
    /// <param name="letter">The character to insert: a letter of the layout, or a space</param>
    /// <param name="position">Where it would go, as an index into <see cref="Value"/> before the insertion</param>
    /// <returns>Whether it may be inserted there</returns>
    protected virtual bool CanInsert(char letter, int position) {
        return true;
    }

    /// <summary>
    /// Whether the entered string can be accepted, on top of the length limits. By default this is
    /// <see cref="Validator"/>; an override that restricts the value further must also call the base method, or
    /// <see cref="Validator"/> is ignored.
    /// </summary>
    /// <param name="enteredValue">The string entered so far</param>
    /// <returns>Whether the player may accept it; the Accept option is greyed out while it is false</returns>
    protected virtual bool CanAcceptValue(string enteredValue) {
        return Validator?.Invoke(enteredValue) ?? true;
    }

    // =================================================== Rendering ===================================================
    private void DrawEntryText(string text, Vector2 position, Vector2 justify) {
        ActiveFont.DrawEdgeOutline(text,
                                   position, justify,
                                   Vector2.One * valueScale,
                                   Color.Gray,
                                   4f, Color.DarkSlateBlue,
                                   2f, Color.Black);
    }

    /// <summary>
    /// Draws the letter grid, the options, the header, the prompt and the entered string with its caret, the length
    /// counter, what the value needs and the key hints
    /// </summary>
    public override void Render() {
        float wiggle = wiggler.Value * 8f;

        for (int drawLineIdx = 0; drawLineIdx < lines.Length; ++drawLineIdx) {
            for (int drawCharIdx = 0; drawCharIdx < lines[drawLineIdx].Length; ++drawCharIdx) {
                bool selected   = !typing && !selectingOptions &&
                                      (drawLineIdx == lineIdx) && (drawCharIdx == charIdx);
                Vector2 scale   = Vector2.One * (selected ? 1.2f : 1f);
                Vector2 charPos = GetCharPosition(drawLineIdx, drawCharIdx);
                if (selected) {
                    charPos.Y += wiggle;
                }
                DrawOptionText(lines[drawLineIdx][drawCharIdx].ToString(),
                               charPos, Vector2.One * 0.5f, scale, selected, true);
            }
        }

        Vector2 optionPos = new(GetOptionLeftX(0), boxTopLeft.Y + boxHeight - boxPadding);

        Draw.Rect(boxTopLeft.X + boxPadding, optionPos.Y - lineHeight - boxPadding * 0.5f,
                  boxWidth - boxPadding * 2f, 4f, Color.White);

        for (int drawOptionIdx = 0; drawOptionIdx < options.Count; ++drawOptionIdx) {
            SpecialOption option = options[drawOptionIdx];
            bool selected        = selectingOptions && (optionIdx == drawOptionIdx);
            DrawOptionText(option.Text,
                           optionPos + new Vector2(0f, selected ? wiggle : 0f),
                           new Vector2(0f, 1f),
                           Vector2.One * option.Scale,
                           selected,
                           option.Enable());
            optionPos.X += option.Width + widestLetter;
        }

        Vector2 headerPos = new(960f, 150f);
        DrawEntryText(header, headerPos, new Vector2(0.5f, 0.5f));

        // Draw the string being entered
        // Align the text center if no prompt, left if there is a prompt
        float reservedTextWidth =
            (prompt == "") ? ActiveFont.Measure(value).X * valueScale
                           : (ActiveFont.Measure(prompt).X + widestLetter * maxValueLength) * valueScale;
        Vector2 textPos = new(960f - reservedTextWidth / 2f, 300f);
        ValueRight      = 960f + reservedTextWidth / 2f;
        DrawEntryText(prompt, textPos, new Vector2(0f, 0.5f));
        textPos.X += ActiveFont.Measure(prompt).X * valueScale;
        DrawEntryText(value, textPos, new Vector2(0f, 0.5f));

        // Draw the caret: where typed text goes, which on the letter grid is the end of the string
        if (caretBlinkTimer % 1f < 0.5f) {
            float caretX      = textPos.X + ActiveFont.Measure(value.Substring(0, caret)).X * valueScale + caretGap;
            float caretHeight = ActiveFont.LineHeight * valueScale * 0.8f;
            Draw.Rect(caretX, textPos.Y - caretHeight / 2f, 4f, caretHeight, Color.Gray);
        }

        ActiveFont.DrawOutline($"{value.Length}/{maxValueLength}", counterPos, new Vector2(1f, 0.5f),
                               Vector2.One * counterScale, unselectColor, 2f, Color.Black);

        string need = RequirementHint();
        if (!string.IsNullOrEmpty(need)) {
            ActiveFont.DrawOutline(need, needPos, new Vector2(1f, 0.5f), Vector2.One * needScale, Color.Gray, 2f,
                                   Color.Black);
        }

        RenderHints(Hints());
    }

    // ========================================== Rendering / layout helpers ===========================================
    private Vector2 GetCharPosition(int findLineIdx, int findCharIdx) {
        return boxTopLeft +
                   new Vector2((boxWidth - widestLine) / 2f, boxPadding) +
                   new Vector2(widestLetter * findCharIdx, (lineHeight + lineSpacing) * findLineIdx) +
                   new Vector2(widestLetter, lineHeight) / 2f;
    }

    private float GetOptionLeftX(int findOptionIdx) {
        float x = boxTopLeft.X + (boxWidth - optionsWidth) / 2f;
        for (int accumulateIdx = 0; accumulateIdx < findOptionIdx; ++accumulateIdx) {
            x += options[accumulateIdx].Width + widestLetter;
        }
        return x;
    }

    private static MTexture GuiButton(VirtualButton button) {
        return Input.GuiButton(button, Input.PrefixMode.Latest);
    }

    // Whether the page has a Paste option next to Backspace, and hints Ctrl+V from a keyboard. Every entry page takes
    // a Ctrl+V, and only offers a paste where a whole value is commonly pasted. Read by the constructor: an override
    // must not depend on the subclass's own fields.
    private protected virtual bool OffersPaste => false;

    // What the keys do in the current mode, from right to left: the text, then its keys in reading order
    private List<(string label, MTexture[] keys)> Hints() {
        if (!typing) {
            // On the grid, Cancel only cancels an empty string, and not right after deleting its last character
            List<(string label, MTexture[] keys)> gridHints = [
                (value.Length > 0 || cancelGuardTimer > 0f ? deleteHint : cancelHint, [GuiButton(Input.MenuCancel)]),
                (selectHint, [GuiButton(Input.MenuConfirm)]),
            ];
            // With keyboard text input the keyboard types, and a key press leaves the grid
            if (OffersPaste && !UseKeyboardInput) {
                gridHints.Add((pasteHint, [Input.GuiKey(Keys.LeftControl), Input.GuiKey(Keys.V)]));
            }
            return gridHints;
        }
        List<(string label, MTexture[] keys)> hints = [
            (cancelHint, [Input.GuiKey(Keys.Escape)]),
            (acceptHint, [Input.GuiKey(Keys.Enter)]),
        ];
        if (OffersPaste) {
            hints.Add((pasteHint, [Input.GuiKey(Keys.LeftControl), Input.GuiKey(Keys.V)]));
        }
        return hints;
    }

    // Each hint as the overworld draws its own, from right to left: the text, then its keys in reading order
    internal static void RenderHints(List<(string label, MTexture[] keys)> hints) {
        Vector2 right = hintsPos;
        foreach ((string label, MTexture[] keys) in hints) {
            for (int i = keys.Length - 1; i >= 0; --i) {
                keys[i].Draw(right, new Vector2(keys[i].Width, keys[i].Height / 2f), Color.White, hintScale);
                right.X -= (keys[i].Width + hintKeyGap) * hintScale;
            }
            ActiveFont.DrawOutline(label, right, new Vector2(1f, 0.5f), Vector2.One * hintScale, Color.White, 2f,
                                   Color.Black);
            right.X -= ActiveFont.Measure(label).X * hintScale + hintSpacing;
        }
    }

    private void DrawOptionText(string text, Vector2 at, Vector2 justify,
                                Vector2 scale, bool selected, bool enabled) {
        // Only draw "interactively" if not using the keyboard for input
        if (typing) {
            selected = false;
            enabled  = false;
        }

        Color color     = enabled ? (selected ? selectColor : unselectColor) : disableColor;
        Color edgeColor = enabled ? Color.Gray : Color.Lerp(disableColor, Color.Black, 0.7f);
        ActiveFont.DrawEdgeOutline(text, at, justify, scale, color, 4f, edgeColor);
    }
}
