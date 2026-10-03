using System.Globalization;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

public static class ColorHelper
{
    public static Color ToColor(ColorChoice choice) => choice switch
    {
        ColorChoice.BadelinePurple => new Color(197, 80, 128),
        ColorChoice.MadelineRed    => new Color(255, 89, 99),
        ColorChoice.Blue           => new Color(100, 149, 237),
        ColorChoice.Coral          => new Color(255, 127, 80),
        ColorChoice.Cyan           => new Color(0, 255, 255),
        ColorChoice.Gold           => new Color(255, 215, 0),
        ColorChoice.Green          => new Color(50, 205, 50),
        ColorChoice.Indigo         => new Color(75, 0, 130),
        ColorChoice.LightGreen     => new Color(124, 252, 0),
        ColorChoice.Orange         => new Color(255, 165, 0),
        ColorChoice.Pink           => new Color(255, 105, 180),
        ColorChoice.Purple         => new Color(147, 112, 219),
        ColorChoice.Turquoise      => new Color(72, 209, 204),
        ColorChoice.Yellow         => new Color(240, 228, 66),
        _ => Color.White,
    };

    public static Color ToFinalColor(ColorChoice choice, int opacity) =>
        ToColor(choice) * (opacity / 100f);

    // Six hex digits, no '#': how the colour settings are saved. Alpha is dropped.
    public static string ToHex(Color color) => $"{color.R:x2}{color.G:x2}{color.B:x2}";

    // Accepts "rrggbb" or "#rrggbb", any case; anything else is false. A hand-edited settings file
    // can hold anything, and Calc.HexToColor reads garbage as some colour instead of failing.
    public static bool TryParseHex(string text, out Color color)
    {
        color = Color.White;
        if (text == null) return false;
        string hex = text.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
            return false;
        color = new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        return true;
    }
}
