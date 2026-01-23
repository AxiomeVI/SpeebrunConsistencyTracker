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
    private static List<long> segmentTimes = new([]);
    private static int DNFCount = 0;
    private static int successCount = 0;
    private static bool lockUpdate = false;
    private const long ONE_FRAME = 170000; // in ticks

    public static void Reset(bool fullReset = true) {
        segmentTimes.Clear();
        DNFCount = 0;
        successCount = 0;
        if (fullReset) lockUpdate = false;
    }

    private static string FormatTime(long time) {
        TimeSpan timeSpan = TimeSpan.FromTicks(time);
        string sign = timeSpan < TimeSpan.Zero ? "-" : "";
        return sign + timeSpan.ToString(timeSpan.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
    }

    public static void ExportHotkey() {
        int runCount = segmentTimes.Count;
        if (runCount == 0) {
            PopupMessage("No segment stats to export");
            return;
        }
        var settings = SpeebrunConsistencyTrackerModule.Settings.StatsMenu;
        StringBuilder headerRow = new();
        StringBuilder firstRow = new();

        double average = -1;
        double standardDeviation = -1;
        
        if (settings.RunHistory) {
            headerRow.Append("Time,");
            firstRow.Append($"{FormatTime(segmentTimes[0])},");
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
            average = segmentTimes.Average();
            string averageString = FormatTime((long)Math.Round(average));
            firstRow.Append($"{averageString},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Median)) {
            headerRow.Append("Median,");
            string median = FormatTime(Percentile(segmentTimes, 50));
            firstRow.Append($"{median},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Minimum)) {
            headerRow.Append("Best,");
            string best = FormatTime(segmentTimes.Min());
            firstRow.Append($"{best},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Maximum)) {
            headerRow.Append("Worst,");
            string worst = FormatTime(segmentTimes.Max());
            firstRow.Append($"{worst},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.StandardDeviation)) {
            headerRow.Append("Std Dev,");
            standardDeviation = StandardDeviation(segmentTimes, average);
            string stdDev = FormatTime((long)Math.Round(standardDeviation));
            firstRow.Append($"{stdDev},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.CoefficientOfVariation)) {
            headerRow.Append("Coef of Variation,");
            string cv = CoefficientOfVariation(segmentTimes, average, standardDeviation).ToString("P2").Replace(" ", "");
            firstRow.Append($"{cv},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Percentile)) {
            headerRow.Append($"{settings.PercentileValue.ToString()},");
            string percentile = FormatTime(Percentile(segmentTimes, ToInt(settings.PercentileValue)));
            firstRow.Append($"{percentile},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.RunCount)) {
            headerRow.Append("Run Count,");
            firstRow.Append($"{runCount.ToString()},");
        }
        if (isSettingEnabled(StatOutput.Export, settings.CompletionRate)) {
            headerRow.Append("Completion Rate,");
            string completionRate = (1 - (double)DNFCount / (DNFCount + runCount)).ToString("P2").Replace(" ", "");
            firstRow.Append($"{completionRate},");
        }
        if (settings.LinearRegression) {
            headerRow.Append("Trend Slope,");
            string slope = FormatTime(LinearRegression(segmentTimes));
            firstRow.Append($"{slope},");
        }
        if (headerRow.Length > 0 && headerRow[^1] == ',') headerRow.Length--; // Remove last ","
        if (firstRow.Length > 0 && firstRow[^1] == ',') firstRow.Length--; // Remove last ","

        headerRow.Append("\n");
        headerRow.Append(firstRow.ToString());

        // Remaining segment times
        if (settings.RunHistory) {
            for (int i = 1; i < runCount; i++) {
                headerRow.Append($"\n{FormatTime(segmentTimes[i])}");
            }
        }

        TextInput.SetClipboardText(headerRow.ToString());
        PopupMessage("Segment stats exported");
    }

    public static string ToStringForOverlay() {
        int runCount = segmentTimes.Count;
        if (runCount == 0) {
            return "";
        }

        double average = -1;
        double standardDeviation = -1;
    
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
            average = segmentTimes.Average();
            string averageString = FormatTime((long)Math.Round(average));
            sb.Append($"avg: {averageString} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Median)) {
            string median = FormatTime(Percentile(segmentTimes, 50));
            sb.Append($"med: {median} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Minimum)) {
            string best = FormatTime(segmentTimes.Min());
            sb.Append($"best: {best} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Maximum)) {
            string worst = FormatTime(segmentTimes.Max());
            sb.Append($"worst: {worst} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.StandardDeviation)) {
            standardDeviation = StandardDeviation(segmentTimes, average);
            string stdDev = FormatTime((long)Math.Round(standardDeviation));
            sb.Append($"stdev: {stdDev} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.CoefficientOfVariation)) {
            string cv = CoefficientOfVariation(segmentTimes, average, standardDeviation).ToString("P2").Replace(" ", "");
            sb.Append($"cv: {cv} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Percentile)) {
            string percentile = FormatTime(Percentile(segmentTimes, ToInt(settings.PercentileValue)));
            sb.Append($"{settings.PercentileValue.ToString()}: {percentile} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.CompletionRate)) {
            string completionRate = (1 - (double)DNFCount / (DNFCount + runCount)).ToString("P2").Replace(" ", "");
            sb.Append($"completion: {completionRate} | ");
        }
        if (sb.Length >= 3) sb.Remove(sb.Length - 3, 3); // Remove last " | "
        return sb.ToString();   
    }

    public static void AddSegmentTime(long segmentTime) {
        if (lockUpdate) return;
        long adjustedTime = segmentTime - ONE_FRAME;
        segmentTimes.Add(adjustedTime);
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

    private static double StandardDeviation(List<long> values, double average){
        average = average != -1 ? average : values.Average();
        double variance = values.Average(val => (val - average) * (val - average));
        return Math.Sqrt(variance);
    }

    public static double CoefficientOfVariation(List<long> values, double average, double standardDeviation) {
        average = average != -1 ? average : values.Average();
        standardDeviation = standardDeviation != -1 ? standardDeviation : StandardDeviation(values, average);
        return standardDeviation / average;
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
        return (long)Math.Round(slope);
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
        Reset();
    }

    public static void OnClearState() {
        Reset();
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