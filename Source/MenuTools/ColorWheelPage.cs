// MenuTools requires: TextMenuPage.cs MenuToolsDialog.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A page for picking a color on a hue and saturation wheel, with rows for a named preset, hue, saturation,
/// brightness, pasting a hex code and reverting. Changes apply live: <see cref="OnColorChange"/> is invoked on every
/// change, and the page keeps the current color however it returns (Back, Cancel, ESC or Pause).
/// </summary>
/// <remarks>
/// The wheel can also be clicked or dragged with the mouse. Enter the page with <see cref="Enter(Color)"/>.
/// </remarks>
public class ColorWheelPage : TextMenuPage {
    // =================================================================================================================
    // Layout, in HUD coordinates (1920x1080)
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

    private static readonly Vector2 wheelCenter = new(480f, 520f);

    // =================================================================================================================
    /// <summary>
    /// Invoked with the new color on every change the player makes, including <see cref="Revert"/>
    /// </summary>
    public Action<Color> OnColorChange;

    /// <summary>
    /// The current color
    /// </summary>
    public Color Value => value;

    /// <summary>
    /// The color the page was entered with, which <see cref="Revert"/> goes back to
    /// </summary>
    public Color InitialColor { get; private set; } = Color.White;

    private readonly string header;
    private readonly string beforeLabel = MenuToolsDialog.Get("COLORWHEEL_BEFORE", "Before");
    private readonly string afterLabel  = MenuToolsDialog.Get("COLORWHEEL_AFTER", "After");
    private readonly List<(string name, Color color)> presets;
    private readonly WheelOption presetOption;  // Null without presets
    private readonly WheelOption hueOption;
    private readonly WheelOption saturationOption;
    private readonly WheelOption brightnessOption;

    private Color value = Color.White;
    // What the rows and the cursor show. They are the source of truth for the wheel: black has no hue or saturation
    // and grey has no hue, so deriving them from the color would lose what the player set.
    private int hue;         // 0-359
    private int saturation;  // 0-100
    private int brightness;  // 0-100

    private Texture2D wheelTexture;
    private bool dragging;
    private Vector2 lastMousePosition;
    private float mouseShownTimer;

    /// <param name="parent">The menu to return to</param>
    /// <param name="subMenuParent">The recursive submenu the page is entered from, if any</param>
    /// <param name="header">Text shown at the top of the page</param>
    /// <param name="presets">Named colors offered in the Preset row; no Preset row if null or empty</param>
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

        Add(new HexButton(MenuToolsDialog.Get("COLORWHEEL_PASTE", "Paste hex"), () => value).Pressed(Paste));
        Add(new Button(MenuToolsDialog.Get("COLORWHEEL_REVERT", "Revert")).Pressed(Revert));
    }

    // =================================================================================================================
    // Entering and returning
    /// <summary>
    /// Enter the page with the color it last showed (white on a new page)
    /// </summary>
    public override void Enter() {
        Enter(value);
    }

    /// <summary>
    /// Enter the page with a color, which <see cref="Revert"/> then goes back to
    /// </summary>
    public void Enter(Color initialColor) {
        if (Entered) {
            return;
        }
        InitialColor = initialColor;
        value        = initialColor;
        SetHsvFromColor(initialColor);
        UpdateRows();
        dragging          = false;
        lastMousePosition = MInput.Mouse.Position;
        mouseShownTimer   = 0f;
        EnsureWheelTexture();
        base.Enter();
    }

    public override void Return() {
        base.Return();
        DisposeWheelTexture();
    }

    public override void Removed(Scene scene) {
        base.Removed(scene);
        DisposeWheelTexture();
    }

    public override void SceneEnd(Scene scene) {
        base.SceneEnd(scene);
        DisposeWheelTexture();
    }

    // =================================================================================================================
    // Color changes
    /// <summary>
    /// Go back to the color the page was entered with, invoking <see cref="OnColorChange"/> with it
    /// </summary>
    public void Revert() {
        SetColor(InitialColor);
    }

    private void Paste() {
        PasteText(TextInput.GetClipboardText());
    }

    private void PasteText(string text) {
        if (ParseHex(text) is Color pasted) {
            Audio.Play(SFX.ui_main_button_select);
            SetColor(pasted);
        } else {
            Audio.Play(SFX.ui_main_button_invalid);
        }
    }

    // A color from outside the wheel: a preset, a pasted code or the initial color. The rows follow it, but keep the
    // hue and saturation it doesn't define.
    private void SetColor(Color color) {
        value = color;
        SetHsvFromColor(color);
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
            int match = presets.FindIndex(preset => SameRgb(preset.color, value));
            presetOption.Index = match >= 0 ? match : presetOption.CustomIndex;
        }
    }

    private static bool SameRgb(Color a, Color b) {
        return a.R == b.R && a.G == b.G && a.B == b.B;
    }

    // =================================================================================================================
    // Conversions
    /// <summary>
    /// The opaque color with a hue (in degrees, counter-clockwise from red), a saturation and a value (brightness)
    /// </summary>
    /// <param name="hue">Hue in degrees; any value, taken modulo 360</param>
    /// <param name="saturation">Saturation from 0 to 1</param>
    /// <param name="value">Value (brightness) from 0 to 1</param>
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

    /// <summary>
    /// The opaque color of a hex code, <c>#rrggbb</c> or <c>rrggbb</c> in any case, ignoring surrounding whitespace
    /// </summary>
    /// <returns>The color, or null if the text is anything else</returns>
    public static Color? ParseHex(string text) {
        if (text == null) {
            return null;
        }
        text = text.Trim();
        if (text.StartsWith('#')) {
            text = text.Substring(1);
        }
        if (text.Length != 6) {
            return null;
        }
        foreach (char c in text) {
            if (!Uri.IsHexDigit(c)) {
                return null;
            }
        }
        int rgb = int.Parse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
    }

    private static int ToByte(float channel) {
        return (int) Math.Round(Calc.Clamp(channel, 0f, 1f) * 255f);
    }

    // =================================================================================================================
    // Mouse
    public override void Update() {
        base.Update();
        if (Entered && Focused) {
            UpdateMouse();
        }
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

    // =================================================================================================================
    // Rendering
    public override void Render() {
        base.Render();
        float alpha       = Alpha;
        Color strokeColor = Color.Black * (alpha * alpha * alpha);

        if (header != "") {
            ActiveFont.DrawEdgeOutline(header, new Vector2(960f, headerY), new Vector2(0.5f, 0.5f),
                                       Vector2.One * headerScale, Color.DarkSlateGray * alpha, 4f,
                                       Color.MidnightBlue * alpha, 2f, strokeColor);
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

        // Before | After
        float left = wheelCenter.X - swatchWidth;
        Draw.Rect(left - swatchBorder, swatchTop - swatchBorder, swatchWidth * 2f + swatchBorder * 2f,
                  swatchHeight + swatchBorder * 2f, strokeColor);
        Draw.Rect(left, swatchTop, swatchWidth, swatchHeight, Opaque(InitialColor) * alpha);
        Draw.Rect(wheelCenter.X, swatchTop, swatchWidth, swatchHeight, Opaque(value) * alpha);
        Draw.Rect(wheelCenter.X - swatchBorder / 2f, swatchTop, swatchBorder, swatchHeight, strokeColor);
        float labelY = swatchTop + swatchHeight + swatchBorder + swatchLabelGap;
        ActiveFont.DrawOutline(beforeLabel, new Vector2(left + swatchWidth / 2f, labelY), new Vector2(0.5f, 0f),
                               Vector2.One * swatchLabelScale, Color.White * alpha, 2f, strokeColor);
        ActiveFont.DrawOutline(afterLabel, new Vector2(wheelCenter.X + swatchWidth / 2f, labelY),
                               new Vector2(0.5f, 0f), Vector2.One * swatchLabelScale, Color.White * alpha, 2f,
                               strokeColor);

        // The game hides the system pointer, so show where the mouse is while it's in use
        if (mouseShownTimer > 0f) {
            Vector2 pointer = lastMousePosition;
            Draw.Line(pointer - Vector2.UnitX * 12f, pointer + Vector2.UnitX * 12f, strokeColor, 6f);
            Draw.Line(pointer - Vector2.UnitY * 12f, pointer + Vector2.UnitY * 12f, strokeColor, 6f);
            Draw.Line(pointer - Vector2.UnitX * 10f, pointer + Vector2.UnitX * 10f, Color.White * alpha, 2f);
            Draw.Line(pointer - Vector2.UnitY * 10f, pointer + Vector2.UnitY * 10f, Color.White * alpha, 2f);
        }
    }

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

    private void DisposeWheelTexture() {
        wheelTexture?.Dispose();
        wheelTexture = null;
    }

    // =================================================================================================================
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

    /// <summary>
    /// A button showing a color's hex code on its right
    /// </summary>
    private class HexButton : Button {
        private const float valueScale   = 0.8f;
        private const float rightPadding = 28f;

        private readonly Func<Color> color;

        public HexButton(string label, Func<Color> color) : base(label) {
            this.color = color;
            // The press plays its own sound, which depends on whether the clipboard held a color
            ConfirmSfx = null;
        }

        public override float RightWidth() {
            return ActiveFont.Measure("#ffffff").X * valueScale + rightPadding;
        }

        public override void Render(Vector2 position, bool highlighted) {
            base.Render(position, highlighted);
            float alpha     = Container.Alpha;
            Color textColor = Disabled ? Color.DarkSlateGray
                                       : ((highlighted ? Container.HighlightColor : Color.White) * alpha);
            Color value     = color();
            ActiveFont.DrawOutline($"#{value.R:x2}{value.G:x2}{value.B:x2}",
                                   new Vector2(position.X + Container.Width - rightPadding, position.Y),
                                   new Vector2(1f, 0.5f), Vector2.One * valueScale, textColor, 2f,
                                   Color.Black * (alpha * alpha * alpha));
        }
    }
}
