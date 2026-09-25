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
        // to Clipboard on the first launch that sees it. REMOVE IN 3.0.0 -- by then no settings
        // file written before 2.1.0 can still be unread, and PersistedEnumMembersTests has to be
        // told about it in the same commit or it fails on the removal, by design.
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

    // Persisted as Settings.LastShownGraph. Member names must stay byte-identical to what a
    // settings file already holds — YamlDotNet resolves an enum by name and throws on an unknown
    // one, aborting the rest of the document (see ExportChoice.Sheet above for the incident this
    // guards against).
    public enum GraphType
    {
        Scatter,
        RoomHistogram,
        SegmentHistogram,
        DnfPercent,
        ProblemRooms,
        TimeLoss,
        RunTrajectory,
        BoxPlot
    }
}