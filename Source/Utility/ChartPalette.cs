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
    public const string DefaultAheadHex     = "29cc54"; // LiveSplit's ahead green
    public const string DefaultBehindHex    = "cc3729"; // LiveSplit's behind red
    public const string DefaultTargetHex    = "ff0000"; // Red

    // The default pair keeps LiveSplit's own pale shades. Any other colour is paled by blending it
    // toward white, since a player who picks one cannot also pick its shade.
    private static readonly Color DefaultAheadPale  = new(150, 220, 170);
    private static readonly Color DefaultBehindPale = new(204, 120, 112);
    private const float PaleBlend = 0.4f;

    public Color Room { get; }
    public Color Segment { get; }
    public Color Primary { get; }
    public Color Secondary { get; }
    public Color Best { get; }
    public Color Last { get; }
    public Color Sob { get; }
    public Color Target { get; }

    // Ahead or behind overall. The pale shade marks a room that went the other way: ahead but lost
    // time in it, or behind but gained.
    public Color Ahead { get; }
    public Color AheadPale { get; }
    public Color Behind { get; }
    public Color BehindPale { get; }

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
        Target    = Read(s.TargetColorHex,         DefaultTargetHex);
        Ahead     = Read(s.AheadColorHex,          DefaultAheadHex);
        Behind    = Read(s.BehindColorHex,         DefaultBehindHex);
        AheadPale  = AheadPaleOf(Ahead);
        BehindPale = BehindPaleOf(Behind);

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

    // Public for the Mod Options page, which previews the shade of a colour not yet saved.
    public static Color AheadPaleOf(Color ahead)   => Pale(ahead,  DefaultAheadHex,  DefaultAheadPale);
    public static Color BehindPaleOf(Color behind) => Pale(behind, DefaultBehindHex, DefaultBehindPale);

    private static Color Pale(Color strong, string defaultHex, Color defaultPale)
    {
        ColorHelper.TryParseHex(defaultHex, out Color defaultStrong);
        return strong == defaultStrong ? defaultPale : Color.Lerp(strong, Color.White, PaleBlend);
    }

    // Rebuilt whenever an input differs from the last build, so a menu change shows on the next
    // frame with nothing to invalidate by hand. TasTestSuite swaps the settings object per test,
    // which is why the instance is part of the key.
    private static ChartPalette _cached;
    private static SctSettings _cachedFor;
    private static string _room, _segment, _primary, _secondary, _best, _last, _sob, _target, _ahead, _behind;
    private static ColorChoice _roomChoice, _segmentChoice;
    private static int _opacity;

    public static ChartPalette Current => For(SpeebrunConsistencyTrackerModule.Settings);

    public static ChartPalette For(SctSettings s)
    {
        if (_cached == null || !ReferenceEquals(s, _cachedFor)
            || s.RoomColorHex != _room || s.SegmentColorHex != _segment
            || s.PrimaryChartColorHex != _primary || s.SecondaryChartColorHex != _secondary
            || s.TrajectoryBestColorHex != _best || s.TrajectoryLastColorHex != _last
            || s.TrajectorySobColorHex != _sob || s.TargetColorHex != _target
            || s.AheadColorHex != _ahead || s.BehindColorHex != _behind
            || s.RoomColor != _roomChoice || s.SegmentColor != _segmentChoice
            || s.ChartOpacity != _opacity)
        {
            _cached = new ChartPalette(s);
            _cachedFor = s;
            _room = s.RoomColorHex; _segment = s.SegmentColorHex;
            _primary = s.PrimaryChartColorHex; _secondary = s.SecondaryChartColorHex;
            _best = s.TrajectoryBestColorHex; _last = s.TrajectoryLastColorHex; _sob = s.TrajectorySobColorHex;
            _target = s.TargetColorHex;
            _ahead = s.AheadColorHex; _behind = s.BehindColorHex;
            _roomChoice = s.RoomColor; _segmentChoice = s.SegmentColor;
            _opacity = s.ChartOpacity;
        }
        return _cached;
    }
}
