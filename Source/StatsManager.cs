using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using static Celeste.Mod.SpeebrunConsistencyTracker.SpeebrunConsistencyTrackerModule;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeedrunTool.SaveLoad;

namespace Celeste.Mod.SpeebrunConsistencyTracker.StatsManager;

public static class StaticStatsManager {

    public class TimeData {
        public List<long> times = new List<long>();
        public int DNFCount = 0;

        public TimeData() { }

        public TimeData(List<long> times) {
            this.times = times;
        }

        public TimeData(int DNFCount) {
            this.DNFCount = DNFCount;
        }
    }
    private static TimeData segmentData = new TimeData();
    private static List<TimeData> roomData = new List<TimeData>();

    public static bool lockUpdate = false;
    public static int successCount = 0;
    public static int roomIndex = 0;
    public static long currentSegmentTime = 0;

    public static string previousRoom = "";

    private const long ONE_FRAME = 170000; // in ticks

    public static void Reset(bool fullReset = true) {
        successCount = 0;
        roomIndex = 0;
        previousRoom = "";
        currentSegmentTime = 0;
        roomData.Clear();
        segmentData = new TimeData();
        if (fullReset) lockUpdate = false;
    }

    private static string FormatTime(long time) {
        TimeSpan timeSpan = TimeSpan.FromTicks(time);
        string sign = timeSpan < TimeSpan.Zero ? "-" : "";
        return sign + timeSpan.ToString(timeSpan.TotalSeconds < 60 ? "s\\.fff" : "m\\:ss\\.fff");
    }

    public static void ExportHotkey() {
        int runCount = segmentData.times.Count;
        if (runCount == 0) {
            PopupMessage("No segment stats to export");
            return;
        }
        TextInput.SetClipboardText(BuildStatsCSV(runCount) + "\n" + BuildHistoryCSV(runCount));
        PopupMessage("Segment stats exported to clipboard");
    }

    public static string BuildHistoryCSV(int runCount) {
        var settings = SpeebrunConsistencyTrackerModule.Settings.StatsMenu;
        if (settings.History) {
            StringBuilder csvBuilder = new();
            csvBuilder.Append("\n\n");
            for (int i = 0; i < roomData.Count; i++) {
                csvBuilder.Append($"R{i+1},");
            }
            csvBuilder.Append("Segment,\n");
            for (int i = 0; i < runCount + segmentData.DNFCount; i++) {
                for (int j = 0; j < roomData.Count; j++) {
                    long roomTime = roomData[j].times.ElementAtOrDefault(i);
                    csvBuilder.Append($"{(roomTime == 0 ? "" : FormatTime(roomTime))},");
                }
                long time = segmentData.times.ElementAtOrDefault(i);
                csvBuilder.Append($"{(time == 0 ? "" : FormatTime(time))},");
                csvBuilder.Append("\n");
            }
            return csvBuilder.ToString();
        }
        return "";
    }

    public static string BuildStatsCSV(int runCount) {
        var settings = SpeebrunConsistencyTrackerModule.Settings.StatsMenu;
        StringBuilder headerRow = new();
        StringBuilder segmentRow = new();
        List<StringBuilder> roomRows = Enumerable.Range(0, roomData.Count).Select(i => new StringBuilder($"Room {i+1},")).ToList();

        double segmentAverage = -1.0;
        double segmentStandardDeviation = -1.0;
        var roomAvgStd = Enumerable.Range(0, roomData.Count).Select(_ => new double[] {-1.0, -1.0}).ToList();
        List<long> roomsMin = new List<long>();

        headerRow.Append("\\,");
        segmentRow.Append("Segment,");

        if (isSettingEnabled(StatOutput.Export, settings.SuccessRate)) {
            headerRow.Append($"Success Rate{(isSettingEnabled(StatOutput.Export, settings.TargetTime) ? $" (<={FormatTime(GetTargetTimeTicks())})" : "")},");
            string successRate = ((double)successCount / runCount).ToString("P2").Replace(" ", "");
            segmentRow.Append($"{successRate},");
            foreach (StringBuilder roomRow in roomRows) roomRow.Append(",");
        }
        if (isSettingEnabled(StatOutput.Export, settings.Average)) {
            headerRow.Append("Average,");
            segmentAverage = segmentData.times.Average();
            string averageString = FormatTime((long)Math.Round(segmentAverage));
            segmentRow.Append($"{averageString},");
            for (int i = 0; i < roomData.Count; i++) {
                double avg = roomData[i].times.Average();
                roomAvgStd[i][0] = avg;
                string formattedAvg = FormatTime((long)Math.Round(avg));
                roomRows[i].Append($"{formattedAvg},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.Median)) {
            headerRow.Append("Median,");
            string median = FormatTime(Percentile(segmentData.times, 50));
            segmentRow.Append($"{median},");
            for (int i = 0; i < roomData.Count; i++) {
                string formattedMedian = FormatTime(Percentile(roomData[i].times, 50));
                roomRows[i].Append($"{formattedMedian},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.Minimum)) {
            headerRow.Append("Best,");
            long min = segmentData.times.Min();
            string best = FormatTime(min);
            segmentRow.Append($"{best},");
            for (int i = 0; i < roomData.Count; i++) {
                long roomMin = roomData[i].times.Min();
                roomsMin.Append(roomMin);
                string formattedMin = FormatTime(roomMin);
                roomRows[i].Append($"{formattedMin},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.Maximum)) {
            headerRow.Append("Worst,");
            string worst = FormatTime(segmentData.times.Max());
            segmentRow.Append($"{worst},");
            for (int i = 0; i < roomData.Count; i++) {
                string formattedWorst = FormatTime(roomData[i].times.Max());
                roomRows[i].Append($"{formattedWorst},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.StandardDeviation)) {
            headerRow.Append("Std Dev,");
            segmentStandardDeviation = StandardDeviation(segmentData.times, segmentAverage);
            string stdDev = FormatTime((long)Math.Round(segmentStandardDeviation));
            segmentRow.Append($"{stdDev},");
            for (int i = 0; i < roomData.Count; i++) {
                double std = StandardDeviation(roomData[i].times, roomAvgStd[i][0]);
                roomAvgStd[i][1] = std;
                string formattedStd = FormatTime((long)Math.Round(std));
                roomRows[i].Append($"{formattedStd},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.CoefficientOfVariation)) {
            headerRow.Append("Coef of Variation,");
            string cv = CoefficientOfVariation(segmentData.times, segmentAverage, segmentStandardDeviation).ToString("P2").Replace(" ", "");
            segmentRow.Append($"{cv},");
            for (int i = 0; i < roomData.Count; i++) {
                string roomCV = CoefficientOfVariation(roomData[i].times, roomAvgStd[i][0], roomAvgStd[i][1]).ToString("P2").Replace(" ", "");
                roomRows[i].Append($"{roomCV},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.Percentile)) {
            headerRow.Append($"{settings.PercentileValue.ToString()},");
            string percentile = FormatTime(Percentile(segmentData.times, ToInt(settings.PercentileValue)));
            segmentRow.Append($"{percentile},");
            for (int i = 0; i < roomData.Count; i++) {
                string percentileCV = FormatTime(Percentile(roomData[i].times, ToInt(settings.PercentileValue)));
                roomRows[i].Append($"{percentileCV},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.RunCount)) {
            headerRow.Append("Run Count,");
            segmentRow.Append($"{runCount.ToString()},");
            for (int i = 0; i < roomData.Count; i++) {
                roomRows[i].Append($"{roomData[i].times.Count},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.CompletionRate)) {
            headerRow.Append("Completion Rate,");
            string completionRate = ((double)runCount / (segmentData.DNFCount + runCount)).ToString("P2").Replace(" ", "");
            segmentRow.Append($"{completionRate},");
            for (int i = 0; i < roomData.Count; i++) {
                string roomCompletionRate = ((double)roomData[i].times.Count / (roomData[i].DNFCount + roomData[i].times.Count)).ToString("P2").Replace(" ", "");
                roomRows[i].Append($"{roomCompletionRate},");
            }
        }
        if (settings.ResetShare) {
            headerRow.Append("Share of Resets,");
            bool noDNF = segmentData.DNFCount == 0;
            segmentRow.Append($"{(noDNF ? "0%" : "100%")},");
            for (int i = 0; i < roomData.Count; i++) {
                string resetShare = noDNF ? "0%" : ((double)roomData[i].DNFCount / segmentData.DNFCount).ToString("P2").Replace(" ", "");
                roomRows[i].Append($"{resetShare},");
            }
        }
        if (settings.LinearRegression) {
            headerRow.Append("Trend Slope,");
            string slope = FormatTime(LinearRegression(segmentData.times));
            segmentRow.Append($"{slope},");
            for (int i = 0; i < roomData.Count; i++) {
                string roomslope = FormatTime(LinearRegression(roomData[i].times));
                roomRows[i].Append($"{roomslope},");
            }
        }
        if (isSettingEnabled(StatOutput.Export, settings.SoB)) {
            headerRow.Append("SoB,");
            List<long> partialSums = new List<long>();
            long sum = 0;
            if (roomsMin.Count == 0) {
                for (int i = 0; i < roomData.Count; i++) {
                    double min = roomData[i].times.Min();
                    sum += (long)Math.Round(min);
                    partialSums.Add(sum);
                }
            } else {
                for (int i = 0; i < roomsMin.Count; i++) {
                    double min = roomsMin[i];
                    sum += (long)Math.Round(min);
                    partialSums.Add(sum);
                }
            }
            segmentRow.Append($"{FormatTime(partialSums[partialSums.Count - 1])},");
            for (int i = 0; i < partialSums.Count; i++) {
                roomRows[i].Append($"{FormatTime(partialSums[i])},");
            }
        }

        return headerRow + "\n" + segmentRow + "\n" + string.Join("\n", roomRows);
    }

    public static string ToStringForOverlay() {
        int runCount = segmentData.times.Count;
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
            average = segmentData.times.Average();
            string averageString = FormatTime((long)Math.Round(average));
            sb.Append($"avg: {averageString} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Median)) {
            string median = FormatTime(Percentile(segmentData.times, 50));
            sb.Append($"med: {median} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Minimum)) {
            string best = FormatTime(segmentData.times.Min());
            sb.Append($"best: {best} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Maximum)) {
            string worst = FormatTime(segmentData.times.Max());
            sb.Append($"worst: {worst} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.StandardDeviation)) {
            standardDeviation = StandardDeviation(segmentData.times, average);
            string stdDev = FormatTime((long)Math.Round(standardDeviation));
            sb.Append($"stdev: {stdDev} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.CoefficientOfVariation)) {
            string cv = CoefficientOfVariation(segmentData.times, average, standardDeviation).ToString("P2").Replace(" ", "");
            sb.Append($"cv: {cv} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.Percentile)) {
            string percentile = FormatTime(Percentile(segmentData.times, ToInt(settings.PercentileValue)));
            sb.Append($"{settings.PercentileValue.ToString()}: {percentile} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.CompletionRate)) {
            string completionRate = (1 - (double)segmentData.DNFCount / (segmentData.DNFCount + runCount)).ToString("P2").Replace(" ", "");
            sb.Append($"completion: {completionRate} | ");
        }
        if (isSettingEnabled(StatOutput.Overlay, settings.SoB)) {
            long sob = 0;
            for (int i = 0; i < roomData.Count; i++) {
                double min = roomData[i].times.Min();
                sob += (long)Math.Round(min);
            }
            sb.Append($"SoB: {FormatTime(sob)} | ");
        }
        if (sb.Length >= 3) sb.Remove(sb.Length - 3, 3); // Remove last " | "
        return sb.ToString();   
    }

    public static void AddSegmentTime(long segmentTime) {
        if (lockUpdate) return;
        long adjustedTime = segmentTime - ONE_FRAME;
        segmentData.times.Add(adjustedTime);
        if (isSuccessfulRun(adjustedTime)){
            successCount++;
        }
        if (segmentTime - currentSegmentTime > ONE_FRAME) AddRoomTime(segmentTime); // Try to detect cases where the end isn't a room transition
        lockUpdate = true;
    }

    public static void AddRoomTime(long segmentTime) {
        if (lockUpdate) return;
        long roomTime = segmentTime - currentSegmentTime;
        currentSegmentTime = segmentTime;
        if (roomData.Count > roomIndex) {
            roomData[roomIndex].times.Add(roomTime);
        } else {
            roomData.Add(new TimeData([roomTime]));
        }
        roomIndex++;
    }

    private static bool isSettingEnabled(StatOutput output, StatOutput setting) {
        return setting == StatOutput.Both || setting == output;
    }

    #region Stat Calculations
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
    #endregion

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

    #region SaveLoadIntegration
    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {			
        Reset();
    }

    public static void OnClearState() {
        Reset();
    }

    public static void OnBeforeLoadState(Level level) {
        if(!RoomTimerIntegration.RoomTimerIsCompleted() && RoomTimerIntegration.GetRoomTime() > 0) {
            segmentData.DNFCount++;
            if (roomData.Count > roomIndex) roomData[roomIndex].DNFCount++;
            else roomData.Append(new TimeData());
        }
    }

    public static void OnBeforeSaveState(Level level) {
        level.Entities.FindAll<TextOverlay>().ForEach(overlay => {
            overlay.SetText("");
        });    
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level) {
        lockUpdate = false;
        roomIndex = 0;
        currentSegmentTime = 0;
        previousRoom = "";
    }
    #endregion
}