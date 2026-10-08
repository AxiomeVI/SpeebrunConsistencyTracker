// MenuTools requires: ColorSwatchButton.cs ColorWheelPage.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A menu button showing a color, as its hex code and a swatch, on the right of its label. Pressing it opens a
/// <see cref="ColorWheelPage"/> to pick a new color. The page previews changes live: the handler given to
/// <see cref="Change"/> receives every color the player moves through. If the player leaves the page without
/// confirming, the handler is invoked once more, with the color the button had when the page opened.
/// </summary>
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
    /// Set <see cref="ColorSwatchButton.OnValueChange"/>, invoked on every change the player makes on the page, and
    /// with the original color when the player leaves without confirming. The color is opaque.
    /// </summary>
    /// <param name="onValueChange">The handler; it replaces the one set before</param>
    /// <returns>This button</returns>
    public new ColorWheelButton Change(Action<Color> onValueChange) {
        base.Change(onValueChange);
        return this;
    }

    /// <summary>
    /// Open the color wheel page for this button, as pressing it does. The button must be in a menu: the page
    /// returns to that menu.
    /// </summary>
    /// <returns>
    ///     The page, a new one on every call, already entered. Its <see cref="ColorWheelPage.OnColorChange"/> is
    ///     what updates the button, so a caller that sets it must also update the button.
    /// </returns>
    public override ColorWheelPage OpenPage() {
        ColorWheelPage page = new(Container, null, PageHeader, presets);
        page.OnColorChange = SetValueFromPage;
        page.Enter(Value);
        return page;
    }
}
