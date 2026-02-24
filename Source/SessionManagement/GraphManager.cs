using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public class GraphManager(List<List<TimeTicks>> rooms, List<TimeTicks> segment, IReadOnlyDictionary<int, int> dnfPerRoom, int roomCount, TimeTicks? target = null)
{
    private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

    private readonly List<List<TimeTicks>> roomTimes = rooms;
    private readonly List<TimeTicks> segmentTimes = segment;
    private readonly IReadOnlyDictionary<int, int> dnfData = dnfPerRoom;
    private readonly int totalRooms = roomCount;
    private readonly TimeTicks? targetTime = target;
    
    // Cache for overlays
    private GraphOverlay scatterGraph;
    private readonly Dictionary<int, HistogramOverlay> roomHistograms = [];
    private HistogramOverlay segmentHistogram;
    private BarChartOverlay dnfChart;
    
    // Current state
    // -1 = scatter, 0..N-1 = room histogram, N = segment histogram, N+1 = DNF chart
    private int currentIndex = -1;
    private Entity currentOverlay;

    public void NextGraph(Level level)
    {
        // Remove current overlay
        currentOverlay?.RemoveSelf();
        currentOverlay = null;
        
        // Cycle: scatter -> room1 -> room2 -> ... -> segment -> dnf chart -> scatter
        if (currentIndex > roomTimes.Count + 1)
        {
            currentIndex = -1; // Back to scatter
        } else if (currentIndex < -1)
        {
            currentIndex = roomTimes.Count + 1; // Goes to DNF chart
        }
        
        // Show appropriate graph
        if (currentIndex == -1)
        {
            // Show scatter plot
            scatterGraph ??= new GraphOverlay(roomTimes, segmentTimes, null, targetTime);
            currentOverlay = scatterGraph;
        }
        else if (currentIndex < roomTimes.Count)
        {
            // Show room histogram
            if (!roomHistograms.TryGetValue(currentIndex, out HistogramOverlay value))
            {
                value = new HistogramOverlay(
                    $"Room {currentIndex + 1}",
                    roomTimes[currentIndex],
                    GraphOverlay.ToColor(_settings.RoomColor)
                );
                roomHistograms[currentIndex] = value;
            }
            currentOverlay = value;
        }
        else if (currentIndex == roomTimes.Count)
        {
            // Show segment histogram
            segmentHistogram ??= new HistogramOverlay(
                    "Segment",
                    segmentTimes,
                    GraphOverlay.ToColor(_settings.SegmentColor)
                );
            currentOverlay = segmentHistogram;
        }
        else
        {
            // Show DNF bar chart
            if (dnfChart == null)
            {
                var labels = Enumerable.Range(1, totalRooms).Select(i => $"R{i}").ToList();
                var values = Enumerable.Range(0, totalRooms).Select(i => dnfData.GetValueOrDefault(i)).ToList();
                dnfChart = new BarChartOverlay("DNF Count by Room", labels, values, Color.IndianRed);
            }
            currentOverlay = dnfChart;
        }
        
        level.Add(currentOverlay);
        currentIndex++;;
    }

    public void PreviousGraph(Level level)
    {
        currentIndex -= 2;
        NextGraph(level);
    }

    public void CurrentGraph(Level level)
    {
        currentIndex -= 1;
        NextGraph(level);
    }
    
    public void HideGraph()
    {
        currentOverlay?.RemoveSelf();
        currentOverlay = null;
    }
    
    public bool IsShowing()
    {
        return currentOverlay != null;
    }

    public void RemoveGraphs()
    {
        currentOverlay?.RemoveSelf();
        scatterGraph?.RemoveSelf();
        segmentHistogram?.RemoveSelf();
        dnfChart?.RemoveSelf();
        foreach(HistogramOverlay graph in roomHistograms.Values)
        {
            graph?.RemoveSelf();
        }
    }

    public void Dispose()
    {
        RemoveGraphs();
        currentOverlay = null;
        scatterGraph = null;
        segmentHistogram = null;
        dnfChart = null;
        roomHistograms.Clear();
    }

    public void ClearScatterGraph()
    {
        scatterGraph = null;
    }

    public void ClearHistrogram()
    {
        roomHistograms.Clear();
        segmentHistogram = null;
    }
}