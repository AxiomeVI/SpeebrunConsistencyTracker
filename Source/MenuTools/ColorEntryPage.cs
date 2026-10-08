// MenuTools requires: StringEntryPage.cs ColorHex.cs MenuToolsDialog.cs IInputHoldingItem.cs
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// A <see cref="StringEntryPage"/> for entering a color as a six-digit hex code, shown after a <c>#</c> with a
/// swatch of the color once it is complete. The player can accept only a complete code, and can paste one from the
/// clipboard with the Paste option or with Ctrl+V. The page is opaque-only: it ignores the alpha of
/// the color it is entered with, and the color it reports has alpha 255. Like every <see cref="StringEntryPage"/>,
/// it runs no <c>OnCancel</c>, <c>OnESC</c> or <c>OnPause</c> handler and ignores items added to it.
/// </summary>
public class ColorEntryPage : StringEntryPage {
    private const int colorStringLength = 6;
    private const string letters = "7 8 9 e f\n" +
                                   "4 5 6 c d\n" +
                                   "1 2 3 a b\n" +
                                   "  0      ";
    private const string prompt = "#";

    private const float swatchSize    = 72f;
    private const float swatchBorder  = 4f;
    private const float swatchSpacing = 48f;

    /// <summary>
    /// Invoked with the entered color when the player accepts it, after <see cref="StringEntryPage.OnAccepted"/>
    /// (which receives the six digits). The page has returned by then.
    /// </summary>
    public System.Action<Color> OnColorAccepted;

    private protected override bool OffersPaste => true;

    /// <param name="parent">The menu to return to. Must not be null.</param>
    /// <param name="subMenuParent">The recursive submenu the page is entered from, if any</param>
    /// <param name="header">Text shown at the top of the page. Default: none.</param>
    public ColorEntryPage(TextMenu parent, IInputHoldingItem subMenuParent = null, string header = "")
            : base(parent, subMenuParent, letters, prompt, header, colorStringLength, colorStringLength, false) {}

    /// <summary>Enter the page with a color already entered. Does nothing if the page is already entered.</summary>
    /// <param name="initialColor">The color whose code is entered; its alpha is ignored</param>
    public void Enter(Color initialColor) {
        Enter(ColorHex.Format(initialColor));
    }

    /// <summary>
    /// Takes a pasted hex code, as <see cref="ColorHex.Parse"/> reads it, in place of the whole value. Anything else
    /// is refused: filtered down to its hex digits, a word would become a color nobody asked for.
    /// </summary>
    /// <param name="text">The pasted text</param>
    /// <returns>Whether the text was a hex code</returns>
    protected override bool Paste(string text) {
        if (ColorHex.Parse(text) is not Color color) {
            return false;
        }
        SetValue(ColorHex.Format(color));
        return true;
    }

    /// <summary>That a code has six hex digits</summary>
    /// <returns>The text to draw, in the player's language</returns>
    protected override string RequirementHint() {
        return MenuToolsDialog.Get("ENTRY_HEX", "6 hex digits");
    }

    /// <summary>
    /// Invokes <see cref="OnColorAccepted"/> with the color of the accepted code. An override must call the base
    /// method.
    /// </summary>
    /// <param name="acceptedValue">The six hex digits that were accepted</param>
    protected override void Accepted(string acceptedValue) {
        OnColorAccepted?.Invoke(Calc.HexToColor(acceptedValue));
    }

    /// <summary>Draws the page, and a swatch of the color once all six digits are entered</summary>
    public override void Render() {
        base.Render();
        if (Value.Length == colorStringLength) {
            Vector2 topLeft = new(ValueRight + swatchSpacing, 300f - swatchSize / 2f);
            Draw.Rect(topLeft - Vector2.One * swatchBorder, swatchSize + swatchBorder * 2f,
                      swatchSize + swatchBorder * 2f, Color.Black);
            Draw.Rect(topLeft, swatchSize, swatchSize, Calc.HexToColor(Value));
        }
    }
}
