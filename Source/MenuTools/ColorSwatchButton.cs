// MenuTools requires: ColorHex.cs
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A menu button showing a color, as its hex code and a swatch, on the right of its label. Pressing it opens a page
/// to change the color: subclasses choose the page by overriding <see cref="OpenPage"/>, and call
/// <see cref="SetValueFromPage"/> when the page changes the color.
/// </summary>
/// <remarks>
/// The page opens from <see cref="ConfirmPressed"/>, which leaves <see cref="TextMenu.Item.OnPressed"/> to the mod:
/// a handler set with <see cref="TextMenu.Item.Pressed"/> runs once the page is open.
/// </remarks>
public abstract class ColorSwatchButton : TextMenu.Button {
    private const float swatchSize   = 40f;
    private const float swatchBorder = 3f;
    private const float valueScale   = 0.8f;
    private const float valueGap     = 16f;  // Between the hex code and the swatch
    private const float rightPadding = 28f;  // Ends the swatch where option values end their ">"

    /// <summary>
    /// The color shown, updated when the page changes it. Its alpha is ignored: the swatch and the hex code show the
    /// opaque color, and the pages only report opaque colors. Setting it does not invoke <see cref="OnValueChange"/>.
    /// </summary>
    public Color Value { get; set; }

    /// <summary>Invoked with the new color when the page changes it</summary>
    public Action<Color> OnValueChange;

    /// <summary>Header of the page the button opens</summary>
    protected string PageHeader { get; }

    /// <param name="label">Label of the button</param>
    /// <param name="value">Color shown initially</param>
    /// <param name="pageHeader">Header of the page; the label if null</param>
    protected ColorSwatchButton(string label, Color value, string pageHeader = null) : base(label) {
        Value      = value;
        PageHeader = pageHeader ?? label;
    }

    /// <summary>Set <see cref="OnValueChange"/></summary>
    /// <param name="onValueChange">The handler; it replaces the one set before</param>
    /// <returns>This button</returns>
    public ColorSwatchButton Change(Action<Color> onValueChange) {
        OnValueChange = onValueChange;
        return this;
    }

    /// <summary>
    /// Open the page for this button, as pressing it does. The button must be in a menu: the page returns to that
    /// menu.
    /// </summary>
    /// <returns>The page, already entered</returns>
    public abstract TextMenu OpenPage();

    /// <summary>
    /// Plays the button's confirm sound and opens the page. A menu calls this just before
    /// <see cref="TextMenu.Item.OnPressed"/>.
    /// </summary>
    public override void ConfirmPressed() {
        base.ConfirmPressed();
        OpenPage();
    }

    /// <summary>
    /// Set <see cref="Value"/> and invoke <see cref="OnValueChange"/>, for the page to call when the color changes
    /// </summary>
    /// <param name="color">The new color</param>
    protected void SetValueFromPage(Color color) {
        Value = color;
        OnValueChange?.Invoke(color);
    }

    // Drawn every frame: written again only when the color changes
    private string hexValue;
    private Color hexValueColor;

    private string HexValue {
        get {
            if (hexValue == null || hexValueColor != Value) {
                hexValueColor = Value;
                hexValue      = "#" + ColorHex.Format(Value);
            }
            return hexValue;
        }
    }

    /// <summary>The width reserved on the right of the label for the hex code and the swatch</summary>
    public override float RightWidth() {
        return ColorHex.WidestCodeWidth() * valueScale + valueGap + swatchSize + rightPadding;
    }

    /// <summary>Draws the label, then the hex code and the opaque swatch at the right edge of the menu</summary>
    public override void Render(Vector2 position, bool highlighted) {
        base.Render(position, highlighted);
        float alpha       = Container.Alpha;
        Color textColor   = Disabled ? Color.DarkSlateGray
                                     : ((highlighted ? Container.HighlightColor : Color.White) * alpha);
        Color strokeColor = Color.Black * (alpha * alpha * alpha);
        float swatchLeft  = position.X + Container.Width - rightPadding - swatchSize;
        ActiveFont.DrawOutline(HexValue, new Vector2(swatchLeft - valueGap, position.Y), new Vector2(1f, 0.5f),
                               Vector2.One * valueScale, textColor, 2f, strokeColor);
        Draw.Rect(swatchLeft - swatchBorder, position.Y - swatchSize / 2f - swatchBorder,
                  swatchSize + swatchBorder * 2f, swatchSize + swatchBorder * 2f, strokeColor);
        Draw.Rect(swatchLeft, position.Y - swatchSize / 2f, swatchSize, swatchSize,
                  new Color(Value.R, Value.G, Value.B) * (Disabled ? 0.5f * alpha : alpha));
    }
}
