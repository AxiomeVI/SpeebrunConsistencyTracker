using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Enums {

    public enum MetricOutputChoice {
        Off,
        Overlay,
        Export,
        Both
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
}