using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

// One row per chart, and the row is the whole declaration: enabling, cycling, building, caching,
// clearing and listing in the settings menu all read from here. A chart left out of one of those
// compiles and fails at run time, as a null overlay or as a toggle the menu never shows.
//
// Get/Set take the settings object rather than closing over the singleton, so ChartDefinitionsTests
// can write to a throwaway instance and prove no row reads or writes another row's property.
//
// Build takes the visible room count.
public sealed record ChartDefinition(
    GraphType Type,
    string LabelKey,
    Func<SpeebrunConsistencyTrackerModuleSettings, bool> Get,
    Action<SpeebrunConsistencyTrackerModuleSettings, bool> Set,
    Func<int, BaseChartOverlay> Build);

public static partial class GraphManager
{
    private static SpeebrunConsistencyTrackerModuleSettings Settings
        => SpeebrunConsistencyTrackerModule.Settings;

    // Members of GraphType kept only so an old settings file still loads: no row, no chart.
    public static readonly GraphType[] RetiredTypes = [GraphType.RoomHistogram, GraphType.SegmentHistogram];

    // Table order is the Next/Previous cycle order AND the settings menu order.
    //
    // Every delegate here is a lambda or a static method group, never an instance method group like
    // `_someCache.Clear`: that form reads the field when the delegate is built, and the caches used
    // to live in another part of this partial class, whose initialiser order is unspecified.
    private static readonly List<ChartDefinition> _chartDefinitions =
    [
        new(GraphType.Scatter,          DialogIds.GraphScatterId,
            s => s.GraphScatter,          (s, v) => s.GraphScatter = v,
            BuildScatter),

        new(GraphType.BoxPlot,          DialogIds.GraphBoxPlotId,
            s => s.GraphBoxPlot,          (s, v) => s.GraphBoxPlot = v,
            BuildBoxPlotChart),

        new(GraphType.HistogramWall,    DialogIds.GraphHistogramWallId,
            s => s.GraphHistogramWall,    (s, v) => s.GraphHistogramWall = v,
            BuildHistogramWall),

        new(GraphType.DnfPercent,       DialogIds.GraphDnfPercentId,
            s => s.GraphDnfPercent,       (s, v) => s.GraphDnfPercent = v,
            BuildDnfPctChart),

        new(GraphType.ProblemRooms,     DialogIds.GraphProblemRoomsId,
            s => s.GraphProblemRooms,     (s, v) => s.GraphProblemRooms = v,
            BuildProblemRoomsChart),

        new(GraphType.TimeLoss,         DialogIds.GraphTimeLossId,
            s => s.GraphTimeLoss,         (s, v) => s.GraphTimeLoss = v,
            BuildTimeLossChart),

        new(GraphType.RunTrajectory,    DialogIds.GraphRunTrajectoryId,
            s => s.GraphRunTrajectory,    (s, v) => s.GraphRunTrajectory = v,
            BuildRunTrajectoryChart),
    ];

    // Declared after _chartDefinitions on purpose: static initialisers run in textual order within
    // one part of a partial class, and this one reads the list.
    private static readonly Dictionary<GraphType, ChartDefinition> _chartsByType =
        _chartDefinitions.ToDictionary(d => d.Type);

    private static readonly Dictionary<GraphType, ChartCache<BaseChartOverlay>> _caches = [];

    public static IReadOnlyList<ChartDefinition> ChartDefinitions => _chartDefinitions;

    // ShowCurrentSlot runs every frame, so this lookup must not allocate: a dictionary read on an
    // enum key, no LINQ, no closure. The cache read it feeds is in GetOrCreate below.
    private static ChartDefinition DefinitionFor(GraphType type)
        => _chartsByType.GetValueOrDefault(type);

    private static BaseChartOverlay GetOrCreate(ChartDefinition chart)
    {
        if (!_caches.TryGetValue(chart.Type, out ChartCache<BaseChartOverlay> cache))
        {
            // A room finished under an open chart rebuilds it: the player's view of it stays.
            cache = new ChartCache<BaseChartOverlay>(chart.Build, static (previous, fresh) => fresh.TakeViewFrom(previous));
            _caches[chart.Type] = cache;
        }

        return cache.Get();
    }

    // Dropping the cache entry is what ChartCache.Clear() does and a little more: the next Get
    // rebuilds both the cache and the chart.
    public static void ClearChart(GraphType type) => _caches.Remove(type);

    private static void ClearAllCharts() => _caches.Clear();
}
