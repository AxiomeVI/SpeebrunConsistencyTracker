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

    private static string FormatTime(long time) {
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
        if (isSettingEnabled(StatOutput.Export, settings.SuccessRate)) sb.Append($"Success Rate,");
        if (isSettingEnabled(StatOutput.Export, settings.TargetTime)) sb.Append($"Target Time,");
        if (isSettingEnabled(StatOutput.Export, settings.Average)) sb.Append("Average,");
        if (isSettingEnabled(StatOutput.Export, settings.Median)) sb.Append("Median,");
        if (isSettingEnabled(StatOutput.Export, settings.Minimum)) sb.Append("Best,");
        if (isSettingEnabled(StatOutput.Export, settings.Maximum)) sb.Append("Worst,");
        if (isSettingEnabled(StatOutput.Export, settings.StandardDeviation)) sb.Append("Std Dev,");
        if (isSettingEnabled(StatOutput.Export, settings.Percentile)) sb.Append($"{settings.PercentileValue.ToString()},");
        if (isSettingEnabled(StatOutput.Export, settings.RunCount)) sb.Append("Run Count,");
        if (isSettingEnabled(StatOutput.Export, settings.CompletionRate)) sb.Append("Completion Rate,");
        if (isSettingEnabled(StatOutput.Export, settings.LinearRegression)) sb.Append("Trend Slope,");
        if (sb.Length > 0 && sb[^1] == ',') sb.Length--; // Remove last ","

        // First data row
        sb.Append("\n");
        if (settings.RunHistory) sb.Append($"{FormatTime(splitTimes[0])},");
        if (isSettingEnabled(StatOutput.Export, settings.SuccessRate)) {
            string successRate = ((double)successCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"{successRate},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.TargetTime)) {
            sb.Append($"{FormatTime(GetTargetTimeTicks())},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Average)) {
            string average = FormatTime((long)Math.Round(splitTimes.Average()));
            sb.Append($"{average},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Median)) {
            string median = FormatTime(Percentile(splitTimes, 50));
            sb.Append($"{median},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Minimum)) {
            string best = FormatTime(splitTimes.Min());
            sb.Append($"{best},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Maximum)) {
            string worst = FormatTime(splitTimes.Max());
            sb.Append($"{worst},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.StandardDeviation)) {
            string stdDev = FormatTime(StandardDeviation(splitTimes));
            sb.Append($"{stdDev},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Percentile)) {
            string percentile = FormatTime(Percentile(splitTimes, ToInt(settings.PercentileValue)));
            sb.Append($"{percentile},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.RunCount)) sb.Append($"{runCount.ToString()},");
        if (isSettingEnabled(StatOutput.Export, settings.CompletionRate)) {
            string completionRate = (1 - (double)DNFCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"{completionRate},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.LinearRegression)) {
            double slope = LinearRegression(splitTimes);
            sb.Append($"{slope.ToString()},");
        }
        if (sb.Length > 0 && sb[^1] == ',') sb.Length--; // Remove last ","
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
        if (isSettingEnabled(StatOutput.Overlay, settings.SuccessRate)) {
            string successRate = ((double)successCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"success{(isSettingEnabled(StatOutput.Overlay, settings.TargetTime) ? $" (<={FormatTime(GetTargetTimeTicks())})" : "")}: {successRate} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.RunCount)) {
            sb.Append($"runs: {runCount.ToString()} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Average)) {
            string average = FormatTime((long)Math.Round(splitTimes.Average()));
            sb.Append($"avg: {average} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Median)) {
            string median = FormatTime(Percentile(splitTimes, 50));
            sb.Append($"med: {median} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Minimum)) {
            string best = FormatTime(splitTimes.Min());
            sb.Append($"best: {best} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Maximum)) {
            string worst = FormatTime(splitTimes.Max());
            sb.Append($"worst: {worst} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.StandardDeviation)) {
            string stdDev = FormatTime(StandardDeviation(splitTimes));
            sb.Append($"stdev: {stdDev} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Percentile)) {
            string percentile = FormatTime(Percentile(splitTimes, ToInt(settings.PercentileValue)));
            sb.Append($"{settings.PercentileValue.ToString()}: {percentile} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.CompletionRate)) {
            string completionRate = (1 - (double)DNFCount / runCount).ToString("P2").Replace(" ", "");
            sb.Append($"completion: {completionRate} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.LinearRegression)) {
            double slope = LinearRegression(splitTimes);
            sb.Append($"trend: {slope.ToString()} | ");
        }
        if (sb.Length >= 3) sb.Remove(sb.Length - 3, 3); // Remove last " | "
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

    private static bool isSettingEnabled(StatOutput output, StatOutput setting) {
        return setting == StatOutput.Both || setting == output;
    }

    private static long Percentile(List<long> values, int p){
        var sorted = values.OrderBy(x => x).ToList();
        int index = (int)Math.Ceiling((double)p/100 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static long StandardDeviation(List<long> values){
        double average = values.Average();
        double variance = values.Average(val => (val - average) * (val - average));
        return (long)Math.Round(Math.Sqrt(variance));
    }

    private static double LinearRegression(IList<long> values) {
        int n = values.Count;

        // x = indices starting from 1
        var x = Enumerable.Range(1, n).Select(i => (double)i).ToList();
        var y = values.Select(v => (double)v).ToList();

        double sumX = x.Sum();
        double sumY = y.Sum();
        double sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
        double sumX2 = x.Sum(xi => xi * xi);

        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        // double intercept = (sumY - slope * sumX) / n;

        return Math.Round(slope, 3);
    }

    private static bool isSuccessfulRun(long time){
        long targetTimeInTicks = GetTargetTimeTicks();
        return time <= targetTimeInTicks;
    }

    private static long GetTargetTimeTicks() {
        var settings = SpeebrunConsistencyTrackerModule.Settings.TargetTime;
        int totalMilliseconds = settings.Minutes * 60000 + settings.Seconds * 1000 + settings.MillisecondsFirstDigit * 100 + settings.MillisecondsSecondDigit * 10 + settings.MillisecondsThirdDigit;
        return TimeSpan.FromMilliseconds(totalMilliseconds).Ticks;
    }

    private static int ToInt(PercentileChoice choice) {
        return choice switch {
            PercentileChoice.P10 => 10,
            PercentileChoice.P20 => 20,
            PercentileChoice.P30 => 30,
            PercentileChoice.P40 => 40,
            PercentileChoice.P60 => 60,
            PercentileChoice.P70 => 70,
            PercentileChoice.P80 => 80,
            PercentileChoice.P90 => 90,
            _ => 0
        };
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