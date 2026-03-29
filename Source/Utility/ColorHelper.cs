using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker;

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
}
