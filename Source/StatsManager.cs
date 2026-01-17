using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using static Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModule;

namespace Celeste.Mod.SpeebrunConsistencyTracker.StatsManager;

public static class StaticStatsManager {
    private static List<long> splitTimes = new([]);
    private static int numberOfDNF = 0;
    private static int numberOfSuccess = 0;
    private static bool lockUpdate = false;
    private const long ONE_FRAME = 170000; // in ticks

    public static string FormatTime(long time) {
        TimeSpan timeSpan = TimeSpan.FromTicks(time);
        return timeSpan.ToString(timeSpan.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
    }

    public static void ExportHotkey() {
        int numberOfCompletedSegment = splitTimes.Count;
        if (numberOfCompletedSegment == 0) {
            PopupMessage("Unable to export segment stats");
            return;
        }
        StringBuilder sb = new();

        // Header row
        sb.Append("Time,Average,Median,Best,Worst,Std Dev,P90,Completed Run,Completion Rate,Success Rate,Target Time");
        
        for (int i = 0; i < numberOfCompletedSegment; i++) {
            sb.Append($"\n{FormatTime(splitTimes[i])}");
            if (i == 0) {
                double mean = splitTimes.Average();
                string avg = FormatTime((long)mean);
                string med = FormatTime(Percentile(splitTimes, 0.5));
                string min = FormatTime(splitTimes.Min());
                string max = FormatTime(splitTimes.Max());
                string stdDev = FormatTime((long)Math.Round(Math.Sqrt(
                    splitTimes.Average(val => Math.Pow(val - mean, 2))
                )));
                string p90 = FormatTime(Percentile(splitTimes, 0.9));
                string completionRate = (1 - (double)numberOfDNF / numberOfCompletedSegment).ToString("P2").Replace(" ", "");
                string successRate = ((double)numberOfSuccess / numberOfCompletedSegment).ToString("P2").Replace(" ", "");
                string targetTime = FormatTime(GetTargetTimeTicks());
                sb.Append($",{avg},{med},{min},{max},{stdDev},{p90},{numberOfCompletedSegment},{completionRate},{successRate},{targetTime}");
            }
        }

        TextInput.SetClipboardText(sb.ToString());
        PopupMessage("Segment stats exported");
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {			
        splitTimes.Clear();
        numberOfDNF = 0;
        numberOfSuccess = 0;
        lockUpdate = false;
        if (!SpeebrunConsistencyTrackerModule.Settings.IngameOverlay.VisibleDuringRun) level.Entities.FindFirst<TextOverlay>()?.SetTextVisible(false);
    }

    public static string GetStats() {
        int numberOfCompletedSegment = splitTimes.Count;
        StringBuilder sb = new();
        string lastSegment = FormatTime(splitTimes.Last());
        string avg = FormatTime((long)splitTimes.Average());
        string med = FormatTime(Percentile(splitTimes, 0.5));
        string max = FormatTime(splitTimes.Max());
        string stdDev = FormatTime((long)Math.Round(Math.Sqrt(
            splitTimes.Average(val => Math.Pow(val - splitTimes.Average(), 2))
        )));
        string p90 = FormatTime(Percentile(splitTimes, 0.9));
        string completionRate = (1 - (double)numberOfDNF / numberOfCompletedSegment).ToString("P2").Replace(" ", "");
        string successRate = ((double)numberOfSuccess / numberOfCompletedSegment).ToString("P2").Replace(" ", "");
        string targetTime = FormatTime(GetTargetTimeTicks());
        sb.Append($"Average: {avg}, Median: {med}, Nb of completed runs: {numberOfCompletedSegment}, Success Rate: {successRate}");
        return sb.ToString();   
    }

    public static void OnClearState() {
        splitTimes.Clear();
        numberOfDNF = 0;
        numberOfSuccess = 0;
        lockUpdate = false;
    }

    public static void OnBeforeLoadState(Level level) {
        if(!RoomTimerIntegration.RoomTimerIsCompleted()) {
            numberOfDNF++;
        }
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        lockUpdate = false;
        if (!SpeebrunConsistencyTrackerModule.Settings.IngameOverlay.VisibleDuringRun) level.Entities.FindFirst<TextOverlay>()?.SetTextVisible(false);
    }

    public static void AddSegmentTime(long segmentTime) {
        if (lockUpdate) return;
        long adjustedTime = segmentTime - ONE_FRAME;
        splitTimes.Add(adjustedTime);
        if (isSuccessfulRun(adjustedTime)){
            numberOfSuccess++;
        }
        lockUpdate = true;
    }

    static long Percentile(List<long> values, double p){
        var sorted = values.OrderBy(x => x).ToList();
        int index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    static bool isSuccessfulRun(long time){
        long targetTicks = GetTargetTimeTicks();
        return time <= targetTicks;
    }

    static long GetTargetTimeTicks() {
        var settings = SpeebrunConsistencyTrackerModule.Settings.TargetTime;
        int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
        return TimeSpan.FromMilliseconds(totalMilliseconds).Ticks;
    }
}