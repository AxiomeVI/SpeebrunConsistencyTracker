using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using static Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModule;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.StatsManager;

public static class StaticStatsManager {
    private static List<long> splitTimes = new([]);
    private static int DNFCount = 0;
    private static int successCount = 0;
    private static bool lockUpdate = false;
    private const long ONE_FRAME = 170000; // in ticks

    public static string FormatTime(long time) {
        TimeSpan timeSpan = TimeSpan.FromTicks(time);
        return timeSpan.ToString(timeSpan.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
    }

    public static void ExportHotkey() {
        int runCount = splitTimes.Count;
        if (runCount == 0) {
            PopupMessage("No segment stats to export");
            return;
        }
        var settings = SpeebrunConsistencyTrackerModule.Settings.StatsMenu;
        StringBuilder sb = new();
        // Header row
        if (settings.RunHistory) sb.Append("Time,");
        if (settings.SuccessRate == StatOutput.Both || settings.SuccessRate == StatOutput.Export) sb.Append($"Success Rate,");
        if (settings.TargetTime == StatOutput.Both || settings.TargetTime == StatOutput.Export) sb.Append($"Target Time,");
        if (settings.Average == StatOutput.Both || settings.Average == StatOutput.Export) sb.Append("Average,");
        if (settings.Median == StatOutput.Both || settings.Median == StatOutput.Export) sb.Append("Median,");
        if (settings.Minimum == StatOutput.Both || settings.Minimum == StatOutput.Export) sb.Append("Best,");
        if (settings.Maximum == StatOutput.Both || settings.Maximum == StatOutput.Export) sb.Append("Worst,");
        if (settings.StandardDeviation == StatOutput.Both || settings.StandardDeviation == StatOutput.Export) sb.Append("Std Dev,");
        if (settings.Percentile == StatOutput.Both || settings.Percentile == StatOutput.Export) sb.Append($"P{settings.PercentileValue.ToString()},");
        if (settings.RunCount == StatOutput.Both || settings.RunCount == StatOutput.Export) sb.Append("Run Count,");
        if (settings.CompletionRate == StatOutput.Both || settings.CompletionRate == StatOutput.Export) sb.Append("Completion Rate,");
        sb.Remove(sb.Length - 1, 1); // Remove last ","

        // First data row
        sb.Append("\n");
        if (settings.RunHistory) sb.Append($"{FormatTime(splitTimes[0])},");
        if (settings.SuccessRate == StatOutput.Both || settings.SuccessRate == StatOutput.Export) {
            string successRate = ((double)successCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"{successRate},");
        }
        if (settings.TargetTime == StatOutput.Both || settings.TargetTime == StatOutput.Export) {
            sb.Append($"{FormatTime(GetTargetTimeTicks())},");
        }
        if (settings.Average == StatOutput.Both || settings.Average == StatOutput.Export) {
            string average = FormatTime((long)Math.Round(splitTimes.Average()));
            sb.Append($"{average},");
        }
        if (settings.Median == StatOutput.Both || settings.Median == StatOutput.Export) {
            string median = FormatTime(Percentile(splitTimes, 50));
            sb.Append($"{median},");
        }
        if (settings.Minimum == StatOutput.Both || settings.Minimum == StatOutput.Export) {
            string best = FormatTime(splitTimes.Min());
            sb.Append($"{best},");
        }
        if (settings.Maximum == StatOutput.Both || settings.Maximum == StatOutput.Export) {
            string worst = FormatTime(splitTimes.Max());
            sb.Append($"{worst},");
        }
        if (settings.StandardDeviation == StatOutput.Both || settings.StandardDeviation == StatOutput.Export) {
            double average = splitTimes.Average();
            string stdDev = FormatTime((long)Math.Round(Math.Sqrt(
                splitTimes.Average(val => Math.Pow(val - average, 2))
            )));
            sb.Append($"{stdDev},");
        }
        if (settings.Percentile == StatOutput.Both || settings.Percentile == StatOutput.Export) {
            string percentile = FormatTime(Percentile(splitTimes, (int)settings.PercentileValue));
            sb.Append($"{percentile},");
        }
        if (settings.RunCount == StatOutput.Both || settings.RunCount == StatOutput.Export) sb.Append($"{runCount.ToString()},");
        if (settings.CompletionRate == StatOutput.Both || settings.CompletionRate == StatOutput.Export) {
            string completionRate = (1 - (double)DNFCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"{completionRate},");
        }
        sb.Remove(sb.Length - 1, 1); // Remove last ","
        // Remaining segment times
        if (settings.RunHistory) {
            for (int i = 1; i < runCount; i++) {
                sb.Append($"\n{FormatTime(splitTimes[i])}");
            }
        }

        TextInput.SetClipboardText(sb.ToString());
        PopupMessage("Segment stats exported");
    }

    public static string ToStringForOverlay() {
        int runCount = splitTimes.Count;
        if (runCount == 0) {
            return "";
        }
        var settings = SpeebrunConsistencyTrackerModule.Settings.StatsMenu;
        StringBuilder sb = new();
        if (settings.SuccessRate == StatOutput.Both || settings.SuccessRate == StatOutput.Overlay) {
            string successRate = ((double)successCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"success{(settings.TargetTime == StatOutput.Both || settings.TargetTime == StatOutput.Overlay ? "" : $" (<={FormatTime(GetTargetTimeTicks())})")}: {successRate} | ");
        }
        if (settings.RunCount == StatOutput.Both || settings.RunCount == StatOutput.Overlay) {
            sb.Append($"runs: {runCount.ToString()} | ");
        }
        if (settings.Average == StatOutput.Both || settings.Average == StatOutput.Overlay) {
            string average = FormatTime((long)Math.Round(splitTimes.Average()));
            sb.Append($"avg: {average} | ");
        }
        if (settings.Median == StatOutput.Both || settings.Median == StatOutput.Overlay) {
            string median = FormatTime(Percentile(splitTimes, 50));
            sb.Append($"med: {median} | ");
        }
        if (settings.Minimum == StatOutput.Both || settings.Minimum == StatOutput.Overlay) {
            string best = FormatTime(splitTimes.Min());
            sb.Append($"best: {best} | ");
        }
        if (settings.Maximum == StatOutput.Both || settings.Maximum == StatOutput.Overlay) {
            string worst = FormatTime(splitTimes.Max());
            sb.Append($"worst: {worst} | ");
        }
        if (settings.StandardDeviation == StatOutput.Both || settings.StandardDeviation == StatOutput.Overlay) {
            double average = splitTimes.Average();
            string stdDev = FormatTime((long)Math.Round(Math.Sqrt(
                splitTimes.Average(val => Math.Pow(val - average, 2))
            )));
            sb.Append($"stdev: {stdDev} | ");
        }
        if (settings.Percentile == StatOutput.Overlay || settings.Percentile == StatOutput.Both) {
            string percentile = FormatTime(Percentile(splitTimes, (int)settings.PercentileValue));
            sb.Append($"P{settings.PercentileValue.ToString()}: {percentile} | ");
        }
        if (settings.CompletionRate == StatOutput.Both || settings.CompletionRate == StatOutput.Overlay) {
            string completionRate = (1 - (double)DNFCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"completion: {completionRate} | ");
        }
        sb.Remove(sb.Length - 3, 3); // Remove last " | "
        return sb.ToString();   
    }

    public static void AddSegmentTime(long segmentTime) {
        if (lockUpdate) return;
        long adjustedTime = segmentTime - ONE_FRAME;
        splitTimes.Add(adjustedTime);
        if (isSuccessfulRun(adjustedTime)){
            successCount++;
        }
        lockUpdate = true;
    }

    static long Percentile(List<long> values, int p){
        var sorted = values.OrderBy(x => x).ToList();
        int index = (int)Math.Ceiling((double)p/100 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    static bool isSuccessfulRun(long time){
        long targetTimeInTicks = GetTargetTimeTicks();
        return time <= targetTimeInTicks;
    }

    static long GetTargetTimeTicks() {
        var settings = SpeebrunConsistencyTrackerModule.Settings.TargetTime;
        int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
        return TimeSpan.FromMilliseconds(totalMilliseconds).Ticks;
    }

    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {			
        splitTimes.Clear();
        DNFCount = 0;
        successCount = 0;
        lockUpdate = false;
    }

    public static void OnClearState() {
        splitTimes.Clear();
        DNFCount = 0;
        successCount = 0;
        lockUpdate = false;
    }

    public static void OnBeforeLoadState(Level level) {
        if(!RoomTimerIntegration.RoomTimerIsCompleted() && RoomTimerIntegration.GetRoomTime() > 0) {
            DNFCount++;
        }
    }

    public static void OnBeforeSaveState(Level level) {
        level.Entities.FindAll<TextOverlay>().ForEach(overlay => {
            overlay.SetText("");
        });    
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        lockUpdate = false;
    }
}