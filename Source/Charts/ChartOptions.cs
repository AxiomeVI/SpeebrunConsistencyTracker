using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// What the player switched on a chart. Kept in the settings and not on the chart: every run builds
// new charts, and the choice is still there the next time the game starts.
internal static class ChartOptions
{
    private static SpeebrunConsistencyTrackerModuleSettings Settings => SpeebrunConsistencyTrackerModule.Settings;

    public static bool ScatterRelative
    {
        get => Settings.ChartScatterRelative;
        set => Save(Settings.ChartScatterRelative != value, () => Settings.ChartScatterRelative = value);
    }

    public static bool BoxPlotRelative
    {
        get => Settings.ChartBoxPlotRelative;
        set => Save(Settings.ChartBoxPlotRelative != value, () => Settings.ChartBoxPlotRelative = value);
    }

    public static bool ScatterSpread
    {
        get => Settings.ChartScatterSpread;
        set => Save(Settings.ChartScatterSpread != value, () => Settings.ChartScatterSpread = value);
    }

    public static bool HistogramMedian
    {
        get => Settings.ChartHistogramMedian;
        set => Save(Settings.ChartHistogramMedian != value, () => Settings.ChartHistogramMedian = value);
    }

    public static TrajectoryBaseline TrajectoryBaseline
    {
        get => Settings.ChartTrajectoryBaseline;
        set => Save(Settings.ChartTrajectoryBaseline != value, () => Settings.ChartTrajectoryBaseline = value);
    }

    // What an old settings file's "against the sum of bests" becomes.
    internal static TrajectoryBaseline MigratedBaseline(bool againstSob, TrajectoryBaseline current) =>
        againstSob ? TrajectoryBaseline.SumOfBest : current;

    public static bool TrajectoryWidget
    {
        get => Settings.TrajectoryWidget;
        set => Save(Settings.TrajectoryWidget != value, () => Settings.TrajectoryWidget = value);
    }

    public static WallScale WallScale
    {
        get => Settings.ChartWallScale;
        set => Save(Settings.ChartWallScale != value, () => Settings.ChartWallScale = value);
    }

    public static bool HelpOpen
    {
        get => Settings.ChartHelpOpen;
        set => Save(Settings.ChartHelpOpen != value, () => Settings.ChartHelpOpen = value);
    }

    // Everest writes the settings file when Mod Options closes, not when the game does.
    internal static void Save(bool changed, System.Action change)
    {
        if (!changed) return;
        change();
        SpeebrunConsistencyTrackerModule.Instance.SaveSettings();
    }
}
