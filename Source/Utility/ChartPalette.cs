using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Microsoft.Xna.Framework;
using SctSettings = Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModuleSettings;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

// The chart colours the settings hold, at full colour and faded by Fill opacity.
//
// ⚠️ Fill opacity is for filled shapes only: scatter dots, histogram and grouped bars, the box of a
// box plot, and the legend marks that stand for them. Lines, whiskers, the target line and all text
// use the full colour, or the labels fade with the bars.
public sealed class ChartPalette
{
    public const string DefaultPrimaryHex   = "cd5c5c"; // IndianRed
    public const string DefaultSecondaryHex = "6495ed"; // CornflowerBlue
    public const string DefaultBestHex      = "ffd700"; // Gold
    public const string DefaultLastHex      = "ba55d3"; // MediumOrchid
    public const string DefaultSobHex       = "40e0d0"; // Turquoise

    public Color Room { get; }
    public Color Segment { get; }
    public Color Primary { get; }
    public Color Secondary { get; }
    public Color Best { get; }
    public Color Last { get; }
    public Color Sob { get; }

    public Color RoomFill { get; }
    public Color SegmentFill { get; }
    public Color PrimaryFill { get; }
    public Color SecondaryFill { get; }

    public ChartPalette(SctSettings s)
    {
        Room      = Read(s.RoomColorHex,           ColorHelper.ToColor(s.RoomColor));
        Segment   = Read(s.SegmentColorHex,        ColorHelper.ToColor(s.SegmentColor));
        Primary   = Read(s.PrimaryChartColorHex,   DefaultPrimaryHex);
        Secondary = Read(s.SecondaryChartColorHex, DefaultSecondaryHex);
        Best      = Read(s.TrajectoryBestColorHex, DefaultBestHex);
        Last      = Read(s.TrajectoryLastColorHex, DefaultLastHex);
        Sob       = Read(s.TrajectorySobColorHex,  DefaultSobHex);

        float fill = System.Math.Clamp(s.ChartOpacity, 0, 100) / 100f;
        RoomFill      = Room * fill;
        SegmentFill   = Segment * fill;
        PrimaryFill   = Primary * fill;
        SecondaryFill = Secondary * fill;
    }

    private static Color Read(string hex, string fallbackHex)
    {
        ColorHelper.TryParseHex(fallbackHex, out Color fallback);
        return Read(hex, fallback);
    }

    private static Color Read(string hex, Color fallback) =>
        ColorHelper.TryParseHex(hex, out Color color) ? color : fallback;

    // Rebuilt whenever an input differs from the last build, so a menu change shows on the next
    // frame with nothing to invalidate by hand. TasTestSuite swaps the settings object per test,
    // which is why the instance is part of the key.
    private static ChartPalette _cached;
    private static SctSettings _cachedFor;
    private static string _room, _segment, _primary, _secondary, _best, _last, _sob;
    private static ColorChoice _roomChoice, _segmentChoice;
    private static int _opacity;

    public static ChartPalette Current => For(SpeebrunConsistencyTrackerModule.Settings);

    public static ChartPalette For(SctSettings s)
    {
        if (_cached == null || !ReferenceEquals(s, _cachedFor)
            || s.RoomColorHex != _room || s.SegmentColorHex != _segment
            || s.PrimaryChartColorHex != _primary || s.SecondaryChartColorHex != _secondary
            || s.TrajectoryBestColorHex != _best || s.TrajectoryLastColorHex != _last
            || s.TrajectorySobColorHex != _sob
            || s.RoomColor != _roomChoice || s.SegmentColor != _segmentChoice
            || s.ChartOpacity != _opacity)
        {
            _cached = new ChartPalette(s);
            _cachedFor = s;
            _room = s.RoomColorHex; _segment = s.SegmentColorHex;
            _primary = s.PrimaryChartColorHex; _secondary = s.SecondaryChartColorHex;
            _best = s.TrajectoryBestColorHex; _last = s.TrajectoryLastColorHex; _sob = s.TrajectorySobColorHex;
            _roomChoice = s.RoomColor; _segmentChoice = s.SegmentColor;
            _opacity = s.ChartOpacity;
        }
        return _cached;
    }
}
