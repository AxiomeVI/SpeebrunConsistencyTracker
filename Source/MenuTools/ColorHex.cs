// MenuTools requires: nothing else
using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MenuTools;

/// <summary>
/// The hex codes of colors, as the color pages read and write them. The pages are opaque-only, so a code has no alpha.
/// </summary>
public static class ColorHex {
    /// <summary>
    /// The opaque color of a hex code, <c>#rrggbb</c> or <c>rrggbb</c> in any case, ignoring surrounding whitespace
    /// </summary>
    /// <param name="text">The text to read; may be null</param>
    /// <returns>The color, or null if the text is anything else: another length, a name, a code with alpha</returns>
    public static Color? Parse(string text) {
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

    /// <summary>The hex code of a color, <c>rrggbb</c> in lower case, ignoring its alpha</summary>
    /// <param name="color">The color to write</param>
    /// <returns>Six hex digits, without a <c>#</c>; <see cref="Parse"/> reads them back</returns>
    public static string Format(Color color) {
        return $"{color.R:x2}{color.G:x2}{color.B:x2}";
    }

    private static PixelFontSize widestCodeFont;
    private static float widestCodeWidth;

    /// <summary>
    /// The width of the widest code a color can have, with its <c>#</c>, in the game's font at scale 1. The digits
    /// have different widths, so the width to reserve for a code is not that of any one code.
    /// </summary>
    internal static float WidestCodeWidth() {
        PixelFontSize font = ActiveFont.FontSize;
        if (font != widestCodeFont) {
            widestCodeFont  = font;
            widestCodeWidth = 0f;
            foreach (char digit in "0123456789abcdef") {
                widestCodeWidth = Math.Max(widestCodeWidth, font.Measure("#" + new string(digit, 6)).X);
            }
        }
        return widestCodeWidth;
    }
}
