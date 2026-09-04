using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

// One row per chart, and the row is the whole declaration: enabling, cycling, building, caching,
// clearing and listing in the settings menu all read from here. Before this table the same eight
// charts were spelled out in four hand-kept enumerations — BuildSlots, the ShowCurrentSlot switch,
// and two independently ordered blocks in the settings menu — plus a cache field, a GetOrCreate
// and a Clear apiece. A chart missing from one of them compiled fine and failed at run time, as a
// null overlay or as a toggle the menu never showed.
//
// Get/Set take the settings object rather than closing over the singleton, which is what lets
// ChartDefinitionsTests write to a throwaway instance and prove that no row reads or writes
// another row's property. That cross-wiring is the failure this shape invites: it compiles, and
// nothing else would catch it.
//
// Slots: the room indices this chart contributes when enabled. Almost every chart is one slot with
// no room (-1); the room histogram is one per room, which is why this is a function and not a bool.
public sealed record ChartDefinition(
    GraphType Type,
    string LabelKey,
    Func<SpeebrunConsistencyTrackerModuleSettings, bool> Get,
    Action<SpeebrunConsistencyTrackerModuleSettings, bool> Set,
    Func<int, int, BaseChartOverlay> Build,
    Func<IEnumerable<int>> Slots,
    bool KeyOnRoomCount = true,
    Func<int> ExtraKey = null);

public static partial class GraphManager
{
    private static SpeebrunConsistencyTrackerModuleSettings Settings
        => SpeebrunConsistencyTrackerModule.Settings;

    private static readonly int[] NoRoom = [-1];
    private static IEnumerable<int> SingleSlot() => NoRoom;

    private static IEnumerable<int> RoomHistogramSlots()
    {
        if (SessionManager.CurrentSession == null) return [];
        return Enumerable.Range(0, SessionManager.RoomCount);
    }

    // Table order is the Next/Previous cycle order AND the settings menu order. The two used to be
    // separate lists and had drifted: the box plot cycled second and was listed last.
    //
    // Build takes (roomCount, roomIndex). Only the room histogram reads the second argument; only
    // the scatter needs an ExtraKey, and only the room histogram sets KeyOnRoomCount false — one
    // matrix column is unaffected by how many rooms exist.
    //
    // Every delegate here is a lambda or a static method group, never an instance method group like
    // `_someCache.Clear`: that form reads the field when the delegate is built, and the caches used
    // to live in another part of this partial class, whose initialiser order is unspecified.
    private static readonly List<ChartDefinition> _chartDefinitions =
    [
        new(GraphType.Scatter,          DialogIds.GraphScatterId,
            s => s.GraphScatter,          (s, v) => s.GraphScatter = v,
            (roomCount, _) => BuildScatter(roomCount),          SingleSlot,
            ExtraKey: ScatterTargetKey),

        new(GraphType.BoxPlot,          DialogIds.GraphBoxPlotId,
            s => s.GraphBoxPlot,          (s, v) => s.GraphBoxPlot = v,
            (roomCount, _) => BuildBoxPlotChart(roomCount),     SingleSlot),

        new(GraphType.RoomHistogram,    DialogIds.GraphRoomHistogramId,
            s => s.GraphRoomHistogram,    (s, v) => s.GraphRoomHistogram = v,
            (_, roomIndex) => BuildRoomHistogram(roomIndex),    RoomHistogramSlots,
            KeyOnRoomCount: false),

        new(GraphType.SegmentHistogram, DialogIds.GraphSegmentHistogramId,
            s => s.GraphSegmentHistogram, (s, v) => s.GraphSegmentHistogram = v,
            (roomCount, _) => BuildSegmentHistogram(roomCount), SingleSlot),

        new(GraphType.DnfPercent,       DialogIds.GraphDnfPercentId,
            s => s.GraphDnfPercent,       (s, v) => s.GraphDnfPercent = v,
            (roomCount, _) => BuildDnfPctChart(roomCount),      SingleSlot),

        new(GraphType.ProblemRooms,     DialogIds.GraphProblemRoomsId,
            s => s.GraphProblemRooms,     (s, v) => s.GraphProblemRooms = v,
            (roomCount, _) => BuildProblemRoomsChart(roomCount), SingleSlot),

        new(GraphType.TimeLoss,         DialogIds.GraphTimeLossId,
            s => s.GraphTimeLoss,         (s, v) => s.GraphTimeLoss = v,
            (roomCount, _) => BuildTimeLossChart(roomCount),    SingleSlot),

        new(GraphType.RunTrajectory,    DialogIds.GraphRunTrajectoryId,
            s => s.GraphRunTrajectory,    (s, v) => s.GraphRunTrajectory = v,
            (roomCount, _) => BuildRunTrajectoryChart(roomCount), SingleSlot),
    ];

    // Declared after _chartDefinitions on purpose: static initialisers run in textual order within
    // one part of a partial class, and this one reads the list.
    private static readonly Dictionary<GraphType, ChartDefinition> _chartsByType =
        _chartDefinitions.ToDictionary(d => d.Type);

    // One cache per (chart, room). Charts that are a single slot always key on room -1, so the
    // room histogram needs no separate dictionary of its own any more.
    private static readonly Dictionary<(GraphType Type, int RoomIndex), ChartCache<BaseChartOverlay>> _caches = [];

    public static IReadOnlyList<ChartDefinition> ChartDefinitions => _chartDefinitions;

    // ShowCurrentSlot runs every frame, so this lookup must not allocate: a dictionary read on an
    // enum key, no LINQ, no closure. The cache read it feeds is in GetOrCreate below.
    private static ChartDefinition DefinitionFor(GraphType type)
        => _chartsByType.GetValueOrDefault(type);

    private static BaseChartOverlay GetOrCreate(ChartDefinition chart, int roomIndex)
    {
        if (!_caches.TryGetValue((chart.Type, roomIndex), out ChartCache<BaseChartOverlay> cache))
        {
            cache = new ChartCache<BaseChartOverlay>(
                roomCount => chart.Build(roomCount, roomIndex), chart.KeyOnRoomCount, chart.ExtraKey);
            _caches[(chart.Type, roomIndex)] = cache;
        }

        return cache.Get();
    }

    // Dropping the cache entry is what ChartCache.Clear() does and a little more: the next Get
    // rebuilds both the cache and the chart. For the room histogram this clears every room at once,
    // which is what the single ClearRoomHistograms() it replaces did.
    public static void ClearChart(GraphType type)
    {
        List<(GraphType Type, int RoomIndex)> keys = [.. _caches.Keys.Where(k => k.Type == type)];
        foreach ((GraphType Type, int RoomIndex) key in keys)
            _caches.Remove(key);
    }

    private static void ClearAllCharts() => _caches.Clear();
}
