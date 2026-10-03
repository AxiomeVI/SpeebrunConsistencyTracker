// MenuTools requires: ColorSwatchButton.cs ColorWheelPage.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A menu button showing a color, as its hex code and a swatch, on the right of its label. Pressing it opens a
/// <see cref="ColorWheelPage"/> to pick a new color. The page applies changes live: the handler given to
/// <see cref="Change"/> receives every color the player moves through, and leaving the page keeps the last one.
/// </summary>
/// <remarks>
/// Pressing the button runs <see cref="TextMenu.Item.OnPressed"/>, which opens the page: replacing it (for example
/// with <see cref="TextMenu.Item.Pressed"/>) stops the button from opening the page.
/// </remarks>
public class ColorWheelButton : ColorSwatchButton {
    private readonly IReadOnlyList<(string name, Color color)> presets;

    /// <param name="label">Label of the button</param>
    /// <param name="value">Color shown initially</param>
    /// <param name="pageHeader">Header of the color wheel page; the label if null</param>
    /// <param name="presets">Named colors the page offers in its Preset row; no Preset row if null or empty</param>
    public ColorWheelButton(string label, Color value, string pageHeader = null,
                            IReadOnlyList<(string name, Color color)> presets = null)
            : base(label, value, pageHeader) {
        this.presets = presets;
    }

    /// <summary>
    /// Set <see cref="ColorSwatchButton.OnValueChange"/>, invoked on every change the player makes on the page
    /// </summary>
    public new ColorWheelButton Change(Action<Color> onValueChange) {
        base.Change(onValueChange);
        return this;
    }

    /// <summary>
    /// Open the color wheel page for this button, as pressing it does
    /// </summary>
    /// <returns>The page, already entered</returns>
    public override ColorWheelPage OpenPage() {
        ColorWheelPage page = new(Container, null, PageHeader, presets);
        page.OnColorChange = ChangeValue;
        page.Enter(Value);
        return page;
    }
}
