// MenuTools requires: TextMenuPage.cs MenuToolsDialog.cs ColorHex.cs IInputHoldingItem.cs ColorEntryPage.cs StringEntryPage.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A page for picking a color on a hue and saturation wheel, with rows for a named preset, hue, saturation,
/// brightness, typing or copying a hex code, and confirming. Changes preview live: <see cref="OnColorChange"/> is
/// invoked on every change. Only the Confirm row keeps the color: Cancel, ESC and Pause go back to the color the
/// page was entered with. The Enter key confirms from any row but the two hex code rows, which it presses, whatever
/// it is bound to. Ctrl+V takes a hex code from the clipboard. The page draws its own key hints.
/// </summary>
/// <remarks>
/// The wheel can also be clicked or dragged with the mouse. Enter the page with <see cref="Enter(Color)"/>.
/// The page is opaque-only: it ignores the alpha of the colors it is given (the initial color and the presets), and
/// every color it reports has alpha 255.
/// </remarks>
public class ColorWheelPage : TextMenuPage {
    // ==================================== Layout, in HUD coordinates (1920x1080) =====================================
    private const float headerY          = 150f;
    private const float headerScale      = 2f;
    private const float wheelRadius      = 240f;
    private const float outlineWidth     = 4f;
    private const float cursorRadius     = 14f;
    private const float swatchTop        = 820f;
    private const float swatchWidth      = 180f;
    private const float swatchHeight     = 90f;
    private const float swatchBorder     = 4f;
    private const float swatchLabelGap   = 12f;
    private const float swatchLabelScale = 0.7f;
    private const float menuCenterX      = 1440f;
    private const float mouseShownTime   = 2f;  // How long the mouse pointer stays drawn after it last moved
    private const float hintGap          = 26f;  // From the wheel's outline to the middle of the mouse hint
    private const float hintScale        = 0.5f;

    private static readonly Vector2 wheelCenter = new(480f, 520f);

    // ============================================== Handlers and state ===============================================
    /// <summary>
    /// Invoked with the new color, always opaque, on every change the player makes while the page is open. When the
    /// page stops being shown without <see cref="Accept"/> and the color has changed, it is invoked once more, with
    /// <see cref="InitialColor"/>. Entering the page does not invoke it.
    /// </summary>
    public Action<Color> OnColorChange;

    /// <summary>
    /// Invoked with the color the player confirmed, after the page has returned and after
    /// <see cref="TextMenuPage.OnReturned"/>
    /// </summary>
    public Action<Color> OnAccepted;

    /// <summary>The current color, always opaque</summary>
    public Color Value => value;

    /// <summary>The color the page was entered with, made opaque, which the page goes back to unless accepted</summary>
    public Color InitialColor { get; private set; } = Color.White;

    private readonly string header;
    private readonly string beforeLabel = MenuToolsDialog.Get("COLORWHEEL_BEFORE", "Before");
    private readonly string afterLabel  = MenuToolsDialog.Get("COLORWHEEL_AFTER", "After");
    // The call stays on one line: test/build-everest-versions.sh reads it
    private readonly string confirmHint = MenuToolsDialog.Get("COLORWHEEL_CONFIRM", "Confirm");
    private readonly string cancelHint  = MenuToolsDialog.Get("ENTRY_CANCEL", "Cancel");
    private readonly string pasteHint   = MenuToolsDialog.Get("ENTRY_PASTE", "Paste");
    private readonly string mouseHint   =
        MenuToolsDialog.Get("COLORWHEEL_MOUSE", "Click or drag on the wheel with the mouse");
    private readonly List<(string name, Color color)> presets;
    private readonly WheelOption presetOption;  // Null without presets
    private readonly WheelOption hueOption;
    private readonly WheelOption saturationOption;
    private readonly WheelOption brightnessOption;
    private readonly HexButton hexButton;

    private Color value = Color.White;
    private bool accepted;
    // What the rows and the cursor show. They are the source of truth for the wheel: black has no hue or saturation
    // and grey has no hue, so deriving them from the color would lose what the player set.
    private int hue;         // 0-359
    private int saturation;  // 0-100
    private int brightness;  // 0-100

    private Texture2D wheelTexture;
    private bool dragging;
    private Vector2 lastMousePosition;
    private float mouseShownTimer;

    /// <param name="parent">The menu to return to. Must not be null.</param>
    /// <param name="subMenuParent">The recursive submenu the page is entered from, if any</param>
    /// <param name="header">Text shown at the top of the page. Default: none.</param>
    /// <param name="presets">
    ///     Named colors offered in the Preset row, which shows "Custom" for any other color; no Preset row if null
    ///     (the default) or empty. The list is copied.
    /// </param>
    public ColorWheelPage(TextMenu parent, IInputHoldingItem subMenuParent = null, string header = "",
                          IReadOnlyList<(string name, Color color)> presets = null)
            : base(parent, subMenuParent) {
        this.header  = header ?? "";
        this.presets = presets == null ? [] : [..presets];
        AutoScroll   = false;
        Position     = new Vector2(menuCenterX, wheelCenter.Y);

        if (this.presets.Count > 0) {
            string presetLabel = MenuToolsDialog.Get("COLORWHEEL_PRESET", "Preset");
            presetOption = new WheelOption(presetLabel, wraps: true, accelerates: false);
            for (int i = 0; i < this.presets.Count; ++i) {
                presetOption.Add(this.presets[i].name, i);
            }
            presetOption.Add(MenuToolsDialog.Get("COLORWHEEL_CUSTOM", "Custom"), -1);
            presetOption.CustomIndex = this.presets.Count;
            presetOption.Change(index => SetColor(this.presets[index].color));
            Add(presetOption);
        }

        hueOption = new WheelOption(MenuToolsDialog.Get("COLORWHEEL_HUE", "Hue"), wraps: true, accelerates: true);
        for (int degrees = 0; degrees < 360; ++degrees) {
            hueOption.Add($"{degrees}°", degrees);
        }
        hueOption.Change(degrees => {
            hue = degrees;
            SetColorFromHsv();
        });
        Add(hueOption);

        string saturationLabel = MenuToolsDialog.Get("COLORWHEEL_SATURATION", "Saturation");
        string brightnessLabel = MenuToolsDialog.Get("COLORWHEEL_BRIGHTNESS", "Brightness");
        saturationOption = new WheelOption(saturationLabel, wraps: false, accelerates: true);
        brightnessOption = new WheelOption(brightnessLabel, wraps: false, accelerates: true);
        for (int percent = 0; percent <= 100; ++percent) {
            saturationOption.Add($"{percent}%", percent);
            brightnessOption.Add($"{percent}%", percent);
        }
        saturationOption.Change(percent => {
            saturation = percent;
            SetColorFromHsv();
        });
        brightnessOption.Change(percent => {
            brightness = percent;
            SetColorFromHsv();
        });
        Add(saturationOption);
        Add(brightnessOption);

        hexButton = new HexButton(MenuToolsDialog.Get("COLORWHEEL_HEX", "Hex code"), () => value);
        Add(hexButton.Pressed(() => EnterHexCode()));
        Add(new Button(MenuToolsDialog.Get("COLORWHEEL_COPY", "Copy hex code")).Pressed(Copy));
        // Accept plays the sound, which ConfirmPressed would play as well
        Add(new Button(confirmHint) { ConfirmSfx = null }.Pressed(Accept));
    }

    // ============================================ Entering and returning =============================================
    /// <summary>
    /// Enter the page with the color it last showed (white on a new page). Does nothing if the page is already
    /// entered.
    /// </summary>
    public override void Enter() {
        Enter(value);
    }

    /// <summary>
    /// Enter the page with a color, which it goes back to unless the player confirms another. Does nothing if the page
    /// is already entered.
    /// </summary>
    /// <param name="initialColor">The color shown at first; its alpha is ignored</param>
    public void Enter(Color initialColor) {
        if (Entered) {
            return;
        }
        accepted     = false;
        InitialColor = Opaque(initialColor);
        value        = InitialColor;
        SetHsvFromColor(value);
        if (presetOption != null) {
            // Forget the preset shown on the last visit, so that UpdateRows finds the first one of this color
            presetOption.Index = presetOption.CustomIndex;
        }
        UpdateRows();
        base.Enter();
    }

    /// <summary>
    /// Builds the wheel's texture, forgets the mouse state of the last visit, and hides the overworld's Confirm and
    /// Back hints, which the page replaces with its own. An override must call the base method.
    /// </summary>
    protected override void SetUp() {
        HideOverworldHints();
        dragging          = false;
        lastMousePosition = MInput.Mouse.Position;
        mouseShownTimer   = 0f;
        EnsureWheelTexture();
    }

    /// <summary>
    /// Disposes of the wheel's texture, gives the overworld its hints back, and goes back to
    /// <see cref="InitialColor"/> unless the color was accepted. An override must call the base method, or the
    /// texture is never freed and a canceled change is kept.
    /// </summary>
    protected override void CleanUp() {
        RestoreOverworldHints();
        wheelTexture?.Dispose();
        wheelTexture = null;
        // Here and not in Return: a page closed or removed from the scene must not keep an unconfirmed color either
        if (!accepted && !SameRgb(value, InitialColor)) {
            SetColor(InitialColor);
        }
    }

    /// <summary>
    /// Keep the current color and return, as the Confirm row does: <see cref="OnAccepted"/> is invoked with it. Does
    /// nothing if the page is not entered.
    /// </summary>
    public void Accept() {
        if (!Entered) {
            return;
        }
        Audio.Play(SFX.ui_main_button_select);
        accepted = true;
        // A handler may enter this page again and change value
        Color acceptedColor = value;
        Return();
        OnAccepted?.Invoke(acceptedColor);
    }

    // ================================================= Color changes =================================================
    /// <summary>
    /// Open a <see cref="ColorEntryPage"/> over this page to type or paste a hex code, as the Hex code row does.
    /// Accepting the code sets the color and comes back here, where it still has to be confirmed.
    /// </summary>
    /// <returns>The entry page, a new one on every call, already entered with the current color</returns>
    public ColorEntryPage EnterHexCode() {
        ColorEntryPage entry  = new(this, null, header);
        entry.OnColorAccepted = SetColor;
        entry.Enter(value);
        return entry;
    }

    private void Copy() {
        TextInput.SetClipboardText("#" + ColorHex.Format(value));
    }

    private void PasteText(string text) {
        if (ColorHex.Parse(text) is Color pasted) {
            Audio.Play(SFX.ui_main_button_select);
            SetColor(pasted);
        } else {
            Audio.Play(SFX.ui_main_button_invalid);
        }
    }

    // A color from outside the wheel: a preset, an entered or pasted code, or the initial color. The rows follow it,
    // but keep the hue and saturation it doesn't define.
    private void SetColor(Color color) {
        value = Opaque(color);
        SetHsvFromColor(value);
        UpdateRows();
        OnColorChange?.Invoke(value);
    }

    private void SetColorFromHsv() {
        value = HsvToColor(hue, saturation / 100f, brightness / 100f);
        UpdateRows();
        OnColorChange?.Invoke(value);
    }

    private void SetHsvFromColor(Color color) {
        ColorToHsv(color, out float h, out float s, out float v);
        brightness = (int) Math.Round(v * 100f);
        if (brightness == 0) {
            return;
        }
        saturation = (int) Math.Round(s * 100f);
        if (saturation == 0) {
            return;
        }
        hue = (int) Math.Round(h) % 360;
    }

    // Show the current values without invoking the rows' handlers
    private void UpdateRows() {
        hueOption.Index        = hue;
        saturationOption.Index = saturation;
        brightnessOption.Index = brightness;
        if (presetOption != null) {
            // Keep the preset shown while it matches: with two presets of one color, going to the first match would
            // make the second unreachable, and Right would never get past the first
            int shown = presetOption.Index;
            if (shown >= presets.Count || !SameRgb(presets[shown].color, value)) {
                int match = presets.FindIndex(preset => SameRgb(preset.color, value));
                presetOption.Index = match >= 0 ? match : presetOption.CustomIndex;
            }
        }
    }

    private static bool SameRgb(Color a, Color b) {
        return a.R == b.R && a.G == b.G && a.B == b.B;
    }

    // ================================================== Conversions ==================================================
    /// <summary>
    /// The opaque color with a hue (in degrees, counter-clockwise from red), a saturation and a value (brightness)
    /// </summary>
    /// <param name="hue">Hue in degrees; any value, taken modulo 360</param>
    /// <param name="saturation">Saturation from 0 to 1; clamped to that range</param>
    /// <param name="value">Value (brightness) from 0 to 1; clamped to that range</param>
    /// <returns>The color, with alpha 255</returns>
    public static Color HsvToColor(float hue, float saturation, float value) {
        hue        = ((hue % 360f) + 360f) % 360f;
        saturation = Calc.Clamp(saturation, 0f, 1f);
        value      = Calc.Clamp(value, 0f, 1f);
        float chroma = value * saturation;
        float sector = hue / 60f;
        float second = chroma * (1f - Math.Abs(sector % 2f - 1f));
        (float r, float g, float b) = (int) sector switch {
            0 => (chroma, second, 0f),
            1 => (second, chroma, 0f),
            2 => (0f, chroma, second),
            3 => (0f, second, chroma),
            4 => (second, 0f, chroma),
            _ => (chroma, 0f, second),
        };
        float lightest = value - chroma;
        return new Color(ToByte(r + lightest), ToByte(g + lightest), ToByte(b + lightest));
    }

    /// <summary>
    /// The hue (in degrees, 0 to 360), saturation and value (brightness) of a color, ignoring its alpha. The hue is 0
    /// for greys, and the saturation 0 for black.
    /// </summary>
    /// <param name="color">The color to convert</param>
    /// <param name="hue">Receives the hue in degrees, counter-clockwise from red</param>
    /// <param name="saturation">Receives the saturation, from 0 to 1</param>
    /// <param name="value">Receives the value (brightness), from 0 to 1</param>
    public static void ColorToHsv(Color color, out float hue, out float saturation, out float value) {
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
        float max   = Math.Max(r, Math.Max(g, b));
        float delta = max - Math.Min(r, Math.Min(g, b));
        value      = max;
        saturation = max > 0f ? delta / max : 0f;
        if (delta == 0f) {
            hue = 0f;
        } else if (max == r) {
            hue = 60f * ((g - b) / delta);
        } else if (max == g) {
            hue = 60f * ((b - r) / delta + 2f);
        } else {
            hue = 60f * ((r - g) / delta + 4f);
        }
        if (hue < 0f) {
            hue += 360f;
        }
    }

    private static int ToByte(float channel) {
        return (int) Math.Round(Calc.Clamp(channel, 0f, 1f) * 255f);
    }

    // ============================================== Keyboard and mouse ===============================================
    /// <summary>
    /// Reads Ctrl+V and the Enter key, then updates the page as a <see cref="TextMenuPage"/> and reads the mouse on
    /// the wheel, all while the page is entered and focused
    /// </summary>
    public override void Update() {
        if (Entered && Focused && UpdateKeys()) {
            return;
        }
        base.Update();
        if (Entered && Focused) {
            UpdateMouse();
        }
    }

    // Whether a key took this frame
    private bool UpdateKeys() {
        if (StringEntryPage.PasteShortcutPressed()) {
            PasteText(TextInput.GetClipboardText());
            return true;
        }
        // Enter accepts, whether it is bound to Pause (its default, which would cancel), to Confirm (which does nothing
        // on an option row) or to both. On a button row it presses the row.
        if (!MInput.Keyboard.Pressed(Keys.Enter)) {
            return false;
        }
        if (Current is Button button) {
            if (Input.MenuConfirm.Binding.Keyboard.Contains(Keys.Enter)) {
                // The menu presses the row
                return false;
            }
            Input.Pause.ConsumePress();
            button.ConfirmPressed();
            button.OnPressed?.Invoke();
        } else {
            Accept();
        }
        return true;
    }

    // What the keys do, from right to left. Enter and Ctrl+V are keys: no hint for them while a controller is in use.
    private List<(string label, MTexture[] keys)> Hints() {
        List<(string label, MTexture[] keys)> hints = [
            (cancelHint, [Input.GuiButton(Input.MenuCancel, Input.PrefixMode.Latest)]),
        ];
        if (!Input.GuiInputController(Input.PrefixMode.Latest)) {
            hints.Add((confirmHint, [Input.GuiKey(Keys.Enter)]));
            hints.Add((pasteHint, [Input.GuiKey(Keys.LeftControl), Input.GuiKey(Keys.V)]));
        }
        return hints;
    }

    private void UpdateMouse() {
        // Already in HUD coordinates: MInput removes Engine.Viewport's offset (letterboxing) and scale
        Vector2 mouse = MInput.Mouse.Position;
        if (mouse != lastMousePosition) {
            lastMousePosition = mouse;
            mouseShownTimer   = mouseShownTime;
        } else {
            mouseShownTimer = Math.Max(0f, mouseShownTimer - Engine.RawDeltaTime);
        }

        Vector2 offset = mouse - wheelCenter;
        if (MInput.Mouse.PressedLeftButton && offset.Length() <= wheelRadius) {
            dragging = true;
        }
        if (!MInput.Mouse.CheckLeftButton) {
            dragging = false;
        }
        if (dragging) {
            mouseShownTimer = mouseShownTime;
            SetHueSaturationAt(offset);
        }
    }

    private void SetHueSaturationAt(Vector2 offset) {
        // Screen Y points down, and the hue increases counter-clockwise
        int newSaturation = (int) Math.Round(Math.Min(offset.Length() / wheelRadius, 1f) * 100f);
        int newHue        = hue;
        if (newSaturation > 0) {
            float degrees = MathHelper.ToDegrees((float) Math.Atan2(-offset.Y, offset.X));
            newHue = ((int) Math.Round(degrees) % 360 + 360) % 360;
        }
        if (newHue != hue || newSaturation != saturation) {
            hue        = newHue;
            saturation = newSaturation;
            SetColorFromHsv();
        }
    }

    // =================================================== Rendering ===================================================
    /// <summary>
    /// Draws the rows, the header, the wheel with its cursor, the mouse hint, the before and after swatches, the key
    /// hints, and the mouse pointer while the mouse is in use
    /// </summary>
    public override void Render() {
        base.Render();
        float alpha       = Alpha;
        Color strokeColor = Color.Black * (alpha * alpha * alpha);

        if (header != "") {
            ActiveFont.DrawEdgeOutline(header, new Vector2(960f, headerY), new Vector2(0.5f, 0.5f),
                                       Vector2.One * headerScale, Color.Gray * alpha, 4f,
                                       Color.DarkSlateBlue * alpha, 2f, strokeColor);
        }

        // Multiplying a color's RGB by the brightness is exactly HSV value, so the full-brightness wheel is only tinted
        EnsureWheelTexture();
        float v = brightness / 100f;
        Draw.SpriteBatch.Draw(wheelTexture, wheelCenter - new Vector2(wheelTexture.Width, wheelTexture.Height) / 2f,
                              new Color(v, v, v) * alpha);

        float radians  = MathHelper.ToRadians(hue);
        Vector2 cursor = wheelCenter + new Vector2((float) Math.Cos(radians), -(float) Math.Sin(radians)) *
                                           (saturation / 100f * wheelRadius);
        Draw.Circle(cursor, cursorRadius, strokeColor, 6f, 24);
        Draw.Circle(cursor, cursorRadius, Color.White * alpha, 2f, 24);

        // Styled like vanilla's subheaders
        ActiveFont.DrawOutline(mouseHint, wheelCenter + Vector2.UnitY * (wheelRadius + outlineWidth + hintGap),
                               new Vector2(0.5f, 0.5f), Vector2.One * hintScale, Color.Gray * alpha, 2f, strokeColor);

        // Before | After
        float left = wheelCenter.X - swatchWidth;
        Draw.Rect(left - swatchBorder, swatchTop - swatchBorder, swatchWidth * 2f + swatchBorder * 2f,
                  swatchHeight + swatchBorder * 2f, strokeColor);
        Draw.Rect(left, swatchTop, swatchWidth, swatchHeight, InitialColor * alpha);
        Draw.Rect(wheelCenter.X, swatchTop, swatchWidth, swatchHeight, value * alpha);
        Draw.Rect(wheelCenter.X - swatchBorder / 2f, swatchTop, swatchBorder, swatchHeight, strokeColor);
        float labelY = swatchTop + swatchHeight + swatchBorder + swatchLabelGap;
        ActiveFont.DrawOutline(beforeLabel, new Vector2(left + swatchWidth / 2f, labelY), new Vector2(0.5f, 0f),
                               Vector2.One * swatchLabelScale, Color.White * alpha, 2f, strokeColor);
        ActiveFont.DrawOutline(afterLabel, new Vector2(wheelCenter.X + swatchWidth / 2f, labelY),
                               new Vector2(0.5f, 0f), Vector2.One * swatchLabelScale, Color.White * alpha, 2f,
                               strokeColor);

        StringEntryPage.RenderHints(Hints());

        // The game hides the system pointer, so show where the mouse is while it's in use
        if (mouseShownTimer > 0f) {
            Vector2 pointer = lastMousePosition;
            Draw.Line(pointer - Vector2.UnitX * 12f, pointer + Vector2.UnitX * 12f, strokeColor, 6f);
            Draw.Line(pointer - Vector2.UnitY * 12f, pointer + Vector2.UnitY * 12f, strokeColor, 6f);
            Draw.Line(pointer - Vector2.UnitX * 10f, pointer + Vector2.UnitX * 10f, Color.White * alpha, 2f);
            Draw.Line(pointer - Vector2.UnitY * 10f, pointer + Vector2.UnitY * 10f, Color.White * alpha, 2f);
        }
    }

    // The page is opaque-only: every color it keeps or reports has alpha 255
    private static Color Opaque(Color color) {
        return new Color(color.R, color.G, color.B);
    }

    // Also rebuilds the texture if something disposed of it, such as a graphics device reset
    private void EnsureWheelTexture() {
        if (wheelTexture != null && !wheelTexture.IsDisposed) {
            return;
        }
        int size      = (int) Math.Ceiling((wheelRadius + outlineWidth) * 2f) + 2;
        float center  = size / 2f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; ++y) {
            for (int x = 0; x < size; ++x) {
                float dx       = x + 0.5f - center;
                float dy       = y + 0.5f - center;
                float distance = (float) Math.Sqrt(dx * dx + dy * dy);
                // Coverage of the outlined disc and of the colored disc inside it, antialiased over one pixel
                float discCoverage  = Calc.Clamp(wheelRadius + outlineWidth + 0.5f - distance, 0f, 1f);
                float colorCoverage = Calc.Clamp(wheelRadius + 0.5f - distance, 0f, 1f);
                if (discCoverage <= 0f) {
                    continue;
                }
                float degrees = MathHelper.ToDegrees((float) Math.Atan2(-dy, dx));
                Color color   = HsvToColor(degrees, Math.Min(distance / wheelRadius, 1f), 1f);
                // SpriteBatch expects premultiplied alpha
                pixels[y * size + x] = Color.Lerp(Color.Black, color, colorCoverage) * discCoverage;
            }
        }
        wheelTexture = new Texture2D(Engine.Graphics.GraphicsDevice, size, size);
        wheelTexture.SetData(pixels);
    }

    // ===================================================== Rows ======================================================
    /// <summary>
    /// An option whose value wraps around or not, and moves faster while Left or Right is held. With a
    /// <see cref="CustomIndex"/>, that value is shown when nothing else matches and is skipped when cycling.
    /// </summary>
    private class WheelOption : Option<int> {
        private const int slowRepeats = 8;   // Menu repeats come every 0.1s, after 0.4s
        private const int fastRepeats = 20;

        /// <summary>
        /// Index of the value shown when no other value matches, or -1. Left and Right from it go to the last and
        /// the first of the other values.
        /// </summary>
        public int CustomIndex = -1;

        private readonly bool wraps;
        private readonly bool accelerates;
        private int heldRepeats;

        public WheelOption(string label, bool wraps, bool accelerates) : base(label) {
            this.wraps       = wraps;
            this.accelerates = accelerates;
        }

        private int Count => CustomIndex >= 0 ? CustomIndex : Values.Count;

        public override void LeftPressed() {
            Move(-1, Input.MenuLeft);
        }

        public override void RightPressed() {
            Move(1, Input.MenuRight);
        }

        // Vanilla toggles an option with two values on Confirm, which would select Custom here
        public override void ConfirmPressed() {}

        private void Move(int direction, VirtualButton button) {
            heldRepeats = button.Repeating ? heldRepeats + 1 : 0;
            int next;
            if (Index == CustomIndex) {
                next = direction < 0 ? Count - 1 : 0;
            } else {
                next = Index + direction * Step();
                next = wraps ? ((next % Count) + Count) % Count : Calc.Clamp(next, 0, Count - 1);
            }
            if (next == Index) {
                return;
            }
            Audio.Play(direction < 0 ? SFX.ui_main_button_toggle_off : SFX.ui_main_button_toggle_on);
            PreviousIndex = Index;
            Index         = next;
            lastDir       = direction;
            ValueWiggler.Start();
            OnValueChange?.Invoke(Values[Index].Item2);
        }

        private int Step() {
            if (!accelerates || heldRepeats < slowRepeats) {
                return 1;
            }
            return Math.Max(1, Count / (heldRepeats < fastRepeats ? 50 : 20));
        }

        private bool CanMove(int direction) {
            return wraps || Index == CustomIndex || (direction < 0 ? Index > 0 : Index < Count - 1);
        }

        // Vanilla's, except for when the arrows are lit
        public override void Render(Vector2 position, bool highlighted) {
            float alpha       = Container.Alpha;
            Color strokeColor = Color.Black * (alpha * alpha * alpha);
            Color color       = Disabled ? Color.DarkSlateGray
                                         : ((highlighted ? Container.HighlightColor : UnselectedColor) * alpha);
            ActiveFont.DrawOutline(Label, position, new Vector2(0f, 0.5f), Vector2.One, color, 2f, strokeColor);
            if (Values.Count == 0) {
                return;
            }
            float rightWidth = RightWidth();
            ActiveFont.DrawOutline(Values[Index].Item1,
                                   position + new Vector2(Container.Width - rightWidth * 0.5f +
                                                              lastDir * ValueWiggler.Value * 8f, 0f),
                                   new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, color, 2f, strokeColor);
            Vector2 bounce  = Vector2.UnitX * (highlighted ? (float) Math.Sin(sine * 4f) * 4f : 0f);
            bool canLeft    = CanMove(-1);
            Vector2 leftPos = position + new Vector2(Container.Width - rightWidth + 40f +
                                                         (lastDir < 0 ? -ValueWiggler.Value * 8f : 0f), 0f) -
                                  (canLeft ? bounce : Vector2.Zero);
            ActiveFont.DrawOutline("<", leftPos, new Vector2(0.5f, 0.5f), Vector2.One,
                                   canLeft ? color : Color.DarkSlateGray * alpha, 2f, strokeColor);
            bool canRight    = CanMove(1);
            Vector2 rightPos = position + new Vector2(Container.Width - 40f +
                                                          (lastDir > 0 ? ValueWiggler.Value * 8f : 0f), 0f) +
                                   (canRight ? bounce : Vector2.Zero);
            ActiveFont.DrawOutline(">", rightPos, new Vector2(0.5f, 0.5f), Vector2.One,
                                   canRight ? color : Color.DarkSlateGray * alpha, 2f, strokeColor);
        }
    }

    /// <summary>A button showing a color's hex code on its right</summary>
    private class HexButton : Button {
        private const float valueScale   = 0.8f;
        private const float rightPadding = 28f;

        private readonly Func<Color> color;
        // Drawn every frame: written again only when the color changes
        private string hexValue;
        private Color hexValueColor;

        public HexButton(string label, Func<Color> color) : base(label) {
            this.color = color;
        }

        public override float RightWidth() {
            return ColorHex.WidestCodeWidth() * valueScale + rightPadding;
        }

        public override void Render(Vector2 position, bool highlighted) {
            base.Render(position, highlighted);
            float alpha     = Container.Alpha;
            Color textColor = Disabled ? Color.DarkSlateGray
                                       : ((highlighted ? Container.HighlightColor : Color.White) * alpha);
            Color value = color();
            if (hexValue == null || hexValueColor != value) {
                hexValueColor = value;
                hexValue      = "#" + ColorHex.Format(value);
            }
            ActiveFont.DrawOutline(hexValue,
                                   new Vector2(position.X + Container.Width - rightPadding, position.Y),
                                   new Vector2(1f, 0.5f), Vector2.One * valueScale, textColor, 2f,
                                   Color.Black * (alpha * alpha * alpha));
        }
    }
}
