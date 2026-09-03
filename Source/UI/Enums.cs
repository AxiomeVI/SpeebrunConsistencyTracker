using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Enums {

    public enum MetricOutputChoice {
        Off,
        Overlay,
        Export,
        Both
    }

    public enum ExportChoice {
        Clipboard,
        File,
        // Dead: kept only so YamlDotNet does not throw on an old settings file saying "Sheet",
        // which would reset every setting declared after ExportMode. OnLoadSettings rewrites it
        // to Clipboard, so this can go once players have launched once.
        Sheet
    }

    [Flags]
    public enum MetricOutput
    {
        Off = 0,
        Overlay = 1,
        Export = 2
    }

    public enum PercentileChoice {
        P10,
        P20,
        P30,
        P40,
        P60,
        P70,
        P80,
        P90
    }

    public enum StatTextPosition {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    public enum StatTextOrientation
    {
        Horizontal,
        Vertical
    }

    public enum ColorChoice
    {
        Cyan,
        Orange,
        Blue,
        Gold,
        Purple,
        Green,
        Turquoise,
        Coral,
        Indigo,
        Pink,
        LightGreen,
        Yellow,
        MadelineRed,
        BadelinePurple
    }
}