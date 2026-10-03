// MenuTools requires: nothing else
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A menu button showing a color, as its hex code and a swatch, on the right of its label. Pressing it opens a page
/// to change the color: subclasses choose the page by overriding <see cref="OpenPage"/>, and call
/// <see cref="ChangeValue"/> when the page changes the color.
/// </summary>
/// <remarks>
/// Pressing the button runs <see cref="TextMenu.Item.OnPressed"/>, which calls <see cref="OpenPage"/>: replacing it
/// (for example with <see cref="TextMenu.Item.Pressed"/>) stops the button from opening the page.
/// </remarks>
public abstract class ColorSwatchButton : TextMenu.Button {
    private const float swatchSize   = 40f;
    private const float swatchBorder = 3f;
    private const float valueScale   = 0.8f;
    private const float valueGap     = 16f;  // Between the hex code and the swatch
    private const float rightPadding = 28f;  // Ends the swatch where option values end their ">"

    /// <summary>
    /// The color shown, updated when the page changes it
    /// </summary>
    public Color Value { get; set; }

    /// <summary>
    /// Invoked with the new color when the page changes it
    /// </summary>
    public Action<Color> OnValueChange;

    /// <summary>
    /// Header of the page the button opens
    /// </summary>
    protected string PageHeader { get; }

    /// <param name="label">Label of the button</param>
    /// <param name="value">Color shown initially</param>
    /// <param name="pageHeader">Header of the page; the label if null</param>
    protected ColorSwatchButton(string label, Color value, string pageHeader = null) : base(label) {
        Value      = value;
        PageHeader = pageHeader ?? label;
        OnPressed  = () => OpenPage();
    }

    /// <summary>
    /// Set <see cref="OnValueChange"/>
    /// </summary>
    public ColorSwatchButton Change(Action<Color> onValueChange) {
        OnValueChange = onValueChange;
        return this;
    }

    /// <summary>
    /// Open the page for this button, as pressing it does
    /// </summary>
    /// <returns>The page, already entered</returns>
    public abstract TextMenu OpenPage();

    /// <summary>
    /// Set <see cref="Value"/> and invoke <see cref="OnValueChange"/>, for the page to call when the color changes
    /// </summary>
    protected void ChangeValue(Color color) {
        Value = color;
        OnValueChange?.Invoke(color);
    }

    private string HexValue => $"#{Value.R:x2}{Value.G:x2}{Value.B:x2}";

    public override float RightWidth() {
        return ActiveFont.Measure("#ffffff").X * valueScale + valueGap + swatchSize + rightPadding;
    }

    public override void Render(Vector2 position, bool highlighted) {
        base.Render(position, highlighted);
        float alpha       = Container.Alpha;
        Color textColor   = Disabled ? Color.DarkSlateGray : ((highlighted ? Container.HighlightColor : Color.White) * alpha);
        Color strokeColor = Color.Black * (alpha * alpha * alpha);
        float swatchLeft  = position.X + Container.Width - rightPadding - swatchSize;
        ActiveFont.DrawOutline(HexValue, new Vector2(swatchLeft - valueGap, position.Y), new Vector2(1f, 0.5f),
                               Vector2.One * valueScale, textColor, 2f, strokeColor);
        Draw.Rect(swatchLeft - swatchBorder, position.Y - swatchSize / 2f - swatchBorder,
                  swatchSize + swatchBorder * 2f, swatchSize + swatchBorder * 2f, strokeColor);
        Draw.Rect(swatchLeft, position.Y - swatchSize / 2f, swatchSize, swatchSize,
                  Value * (Disabled ? 0.5f * alpha : alpha));
    }
}
