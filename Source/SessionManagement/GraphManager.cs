using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public class GraphManager(List<List<TimeTicks>> rooms, List<TimeTicks> segment, TimeTicks? target = null)
{
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
                    Color.Cyan
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
                    Color.Orange
                );
            currentOverlay = segmentHistogram;
        }
        
        level.Add(currentOverlay);
        // Move to next index
        currentIndex++;
        Logger.Log(LogLevel.Info, "NextGraph", currentIndex.ToString());
    }

    public void PreviousGraph(Level level)
    {
        currentIndex -= 2;
        if (currentIndex == -2)
            currentIndex = roomTimes.Count - 1;
        Logger.Log(LogLevel.Info, "PreviousGraph", currentIndex.ToString());
        NextGraph(level);
    }

    public void CurrentGraph(Level level)
    {
        currentIndex -= 1;
        Logger.Log(LogLevel.Info, "CurrentGraph", currentIndex.ToString());
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
}