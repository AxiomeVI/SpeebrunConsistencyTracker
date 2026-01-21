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
        string sign = timeSpan < TimeSpan.Zero ? "-" : "";
        return sign + timeSpan.ToString(timeSpan.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
    }

    public static void ExportHotkey() {
        int runCount = splitTimes.Count;
        if (runCount == 0) {
            PopupMessage("No segment stats to export");
            return;
        }
        var settings = SpeebrunConsistencyTrackerModule.Settings.StatsMenu;
        StringBuilder headerRow = new();
        StringBuilder firstRow = new();
        // Header row
        if (settings.RunHistory) {
            headerRow.Append("Time,");
            firstRow.Append($"{FormatTime(splitTimes[0])},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.SuccessRate)) {
            headerRow.Append($"Success Rate,");
            string successRate = ((double)successCount / runCount).ToString("P2").Replace(" ", "");
            firstRow.Append($"{successRate},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.TargetTime)) {
            headerRow.Append($"Target Time,");
            firstRow.Append($"{FormatTime(GetTargetTimeTicks())},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Average)) {
            headerRow.Append("Average,");
            string average = FormatTime((long)Math.Round(splitTimes.Average()));
            firstRow.Append($"{average},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Median)) {
            headerRow.Append("Median,");
            string median = FormatTime(Percentile(splitTimes, 50));
            firstRow.Append($"{median},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Minimum)) {
            headerRow.Append("Best,");
            string best = FormatTime(splitTimes.Min());
            firstRow.Append($"{best},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Maximum)) {
            headerRow.Append("Worst,");
            string worst = FormatTime(splitTimes.Max());
            firstRow.Append($"{worst},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.StandardDeviation)) {
            headerRow.Append("Std Dev,");
            string stdDev = FormatTime(StandardDeviation(splitTimes));
            firstRow.Append($"{stdDev},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Percentile)) {
            headerRow.Append($"{settings.PercentileValue.ToString()},");
            string percentile = FormatTime(Percentile(splitTimes, ToInt(settings.PercentileValue)));
            firstRow.Append($"{percentile},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.RunCount)) {
            headerRow.Append("Run Count,");
            firstRow.Append($"{runCount.ToString()},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.CompletionRate)) {
            headerRow.Append("Completion Rate,");
            string completionRate = (1 - (double)DNFCount / runCount).ToString("P2").Replace(" ", "");
            firstRow.Append($"{completionRate},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.LinearRegression)) {
            headerRow.Append("Trend Slope,");
            string slope = FormatTime(LinearRegression(splitTimes));
            firstRow.Append($"{slope},");
        }
        if (headerRow.Length > 0 && headerRow[^1] == ',') headerRow.Length--; // Remove last ","

        // First data row
        headerRow.Append("\n");
        if (firstRow.Length > 0 && firstRow[^1] == ',') firstRow.Length--; // Remove last ","
        headerRow.Append(firstRow.ToString());

        // Remaining segment times
        if (settings.RunHistory) {
            for (int i = 1; i < runCount; i++) {
                headerRow.Append($"\n{FormatTime(splitTimes[i])}");
            }
        }

        TextInput.SetClipboardText(headerRow.ToString());
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
            string slope = FormatTime(LinearRegression(splitTimes));
            sb.Append($"trend: {slope} | ");
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

    private static long LinearRegression(List<long> values) {
        int n = values.Count;
        if (n <= 1) return 0;
        // x = indices starting from 1
        List<double> x = Enumerable.Range(1, n).Select(i => (double)i).ToList();
        List<double> y = values.Select(v => (double)v).ToList();

        double sumX = x.Sum();
        double sumY = y.Sum();
        double sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
        double sumX2 = x.Sum(xi => xi * xi);

        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        // double intercept = (sumY - slope * sumX) / n;
        bool slopeIsNegative = slope < 0;
        long slopeAsLong = (long)Math.Round(slope);
        // Fix sign indication loss due to rounding errors
        if (slopeIsNegative && slopeAsLong > 0) slopeAsLong = -slopeAsLong;
        return slopeAsLong;
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