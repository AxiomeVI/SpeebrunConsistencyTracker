using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public class GraphManager(List<List<TimeTicks>> rooms, List<TimeTicks> segment, TimeTicks? target = null)
{
    private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

    private readonly List<List<TimeTicks>> roomTimes = rooms;
    private readonly List<TimeTicks> segmentTimes = segment;
    private readonly TimeTicks? targetTime = target;
    
    // Cache for overlays
    private GraphOverlay scatterGraph;
    private readonly Dictionary<int, HistogramOverlay> roomHistograms = [];
    private HistogramOverlay segmentHistogram;
    
    // Current state
    private int currentIndex = -1; // -1 = scatter, 0+ = room histogram, Count = segment histogram
    private Entity currentOverlay;

    public void NextGraph(Level level)
    {
        // Remove current overlay
        currentOverlay?.RemoveSelf();
        currentOverlay = null;
        
        // Cycle: scatter -> room1 -> room2 -> ... -> segment -> scatter
        if (currentIndex > roomTimes.Count)
        {
            currentIndex = -1; // Back to scatter
        } else if (currentIndex < -1)
        {
            currentIndex = roomTimes.Count; // Goes to segment histogram
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
        else
        {
            // Show segment histogram
            segmentHistogram ??= new HistogramOverlay(
                    "Segment",
                    segmentTimes,
                    GraphOverlay.ToColor(_settings.SegmentColor)
                );
            currentOverlay = segmentHistogram;
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