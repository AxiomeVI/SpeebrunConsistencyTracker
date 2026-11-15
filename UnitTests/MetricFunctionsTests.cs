using System;
using System.Linq;
using System.Reflection;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

// The Metrics class shares its name with its namespace; the alias keeps the call sites readable.
using MetricFunctions = Celeste.Mod.SpeebrunConsistencyTracker.Metrics.Metrics;

namespace SpeebrunConsistencyTracker.UnitTests;

// Pins what every function in Metrics.cs returns today, on one small hand-computed session.
// Where the session holds too little, every metric answers MetricHelper.NoData, "-". An empty
// string is a value the metric never has there, or a function asked outside an export.
public class MetricFunctionsTests
{
    // Times are written in milliseconds so the expected strings read as seconds.
    internal static long[] Ms(params int[] ms) => ms.Select(m => m * 10_000L).ToArray();

    // Rooms 0, 1, 2 over six attempts (seconds):
    //   A0 10 20 30   completed, 60
    //   A1 12 18 33   room 1 deleted afterwards: not completed, and not a reset
    //   A2 10         reset in room 1 (DNF)
    //   A3 11 22 27   completed, 60
    //   A4 13 19 30   completed, 62
    //   A5 10 21 34   completed, 65
    // Segment times, sorted: 60 60 62 65.
    // Room 0 sorted: 10 10 10 11 12 13.  Room 1: 19 20 21 22.  Room 2: 27 30 30 33 34.
    // Counts: 6 attempts, 4 completed, 1 DNF. Per room: attempts 6 5 5, completed 6 4 5, DNFs 0 1 0.
    internal static PracticeSession Main()
    {
        PracticeSession session = TrajectorySessions.Of(3,
            Ms(10000, 20000, 30000),
            Ms(12000, 18000, 33000),
            Ms(10000),
            Ms(11000, 22000, 27000),
            Ms(13000, 19000, 30000),
            Ms(10000, 21000, 34000));
        session.DeleteCell(1, 1);
        return session;
    }

    internal static PracticeSession Empty() => new(new StubSegmentShape(roomCount: 3));

    private delegate MetricResult Metric(PracticeSession session, MetricContext context, bool isExport);

    // A fresh context per call: results are cached in it by key.
    private static MetricResult Compute(Metric metric, PracticeSession session, bool isExport, long targetMs = 0, int percentile = 50)
        => metric(session, new MetricContext(new TimeTicks(targetMs * 10_000L), percentile), isExport);

    private static void Check(MetricResult result, string segment, params string[] rooms)
    {
        Assert.Equal(segment, result.SegmentValue);
        Assert.Equal(rooms, result.RoomValues);
    }

    // ---- Average, Median, MAD, RelativeMAD, StdDev, CoefVariation -----------------------------

    [Fact]
    public void Average()
    {
        // Segment (60+60+62+65)/4 = 61.75. Room 0 66/6 = 11, room 1 82/4 = 20.5, room 2 154/5 = 30.8.
        Check(Compute(MetricFunctions.Average, Main(), true), "1:01.750", "11.000", "20.500", "30.800");
        Check(Compute(MetricFunctions.Average, Main(), false), "1:01.750");
    }

    [Fact]
    public void Median()
    {
        // Segment: position 1.5 of 60 60 62 65 -> 61. Room 0: position 2.5 of 10 10 10 11 12 13 -> 10.5.
        Check(Compute(MetricFunctions.Median, Main(), true), "1:01.000", "10.500", "20.500", "30.000");
        Check(Compute(MetricFunctions.Median, Main(), false), "1:01.000");
    }

    [Fact]
    public void MedianAbsoluteDeviation()
    {
        // Segment: deviations from 61 are 1 1 1 4 -> median 1. Room 0 from 10.5: .5 .5 .5 .5 1.5 2.5 -> .5.
        // Room 1 from 20.5: .5 .5 1.5 1.5 -> 1. Room 2 from 30: 0 0 3 3 4 -> 3.
        Check(Compute(MetricFunctions.MedianAbsoluteDeviation, Main(), true), "1.000", "0.500", "1.000", "3.000");
        Check(Compute(MetricFunctions.MedianAbsoluteDeviation, Main(), false), "1.000");
    }

    [Fact]
    public void RelativeMAD()
    {
        // MAD over median: 1/61, .5/10.5, 1/20.5, 3/30.
        Check(Compute(MetricFunctions.RelativeMAD, Main(), true), "1.64%", "4.76%", "4.88%", "10.00%");
        Check(Compute(MetricFunctions.RelativeMAD, Main(), false), "1.64%");
    }

    [Fact]
    public void StdDev()
    {
        // Sample deviation (n-1). Segment: squares 3.0625 3.0625 .0625 10.5625 = 16.75, /3, root = 2.3629.
        // Room 0: 8/5 = 1.6 -> 1.2649. Room 1: 5/3 -> 1.2910. Room 2: 30.8/4 = 7.7 -> 2.7749.
        // The time format truncates to milliseconds.
        Check(Compute(MetricFunctions.StdDev, Main(), true), "2.362", "1.264", "1.290", "2.774");
        Check(Compute(MetricFunctions.StdDev, Main(), false), "2.362");
    }

    [Fact]
    public void CoefVariation()
    {
        // StdDev over average: 2.3629/61.75, 1.2649/11, 1.2910/20.5, 2.7749/30.8.
        Check(Compute(MetricFunctions.CoefVariation, Main(), true), "3.83%", "11.50%", "6.30%", "9.01%");
        Check(Compute(MetricFunctions.CoefVariation, Main(), false), "3.83%");
    }

    // One completed run, plus an attempt that reset in room 1 after 14 s in room 0.
    // Room 0 holds two times (10, 14), rooms 1 and 2 hold one each (20, 30).
    internal static PracticeSession OneCompletedRun() => TrajectorySessions.Of(3,
        Ms(10000, 20000, 30000),
        Ms(14000));

    [Fact]
    public void StdDev_needs_two_times_in_the_segment_and_in_a_room()
    {
        // Room 0: mean 12, deviations -2 and 2, 8/1 -> 2.828. Rooms with a single time have no data.
        Check(Compute(MetricFunctions.StdDev, OneCompletedRun(), true), "-", "2.828", "-", "-");
    }

    [Fact]
    public void CoefVariation_needs_two_times_in_the_segment_and_in_a_room()
    {
        // Room 0: 2.828 / 12.
        Check(Compute(MetricFunctions.CoefVariation, OneCompletedRun(), true), "-", "23.57%", "-", "-");
    }

    // ---- Best, Worst, SumOfBest, BestSplit -----------------------------------------------------

    [Fact]
    public void Best()
    {
        Check(Compute(MetricFunctions.Best, Main(), true), "1:00.000", "10.000", "19.000", "27.000");
        Check(Compute(MetricFunctions.Best, Main(), false), "1:00.000");
    }

    [Fact]
    public void Worst()
    {
        Check(Compute(MetricFunctions.Worst, Main(), true), "1:05.000", "13.000", "22.000", "34.000");
        Check(Compute(MetricFunctions.Worst, Main(), false), "1:05.000");
    }

    [Fact]
    public void SumOfBest_lists_the_running_total_of_the_best_room_times()
    {
        // 10, 10+19, 10+19+27. Not gated on export: the overlay gets the same room values.
        Check(Compute(MetricFunctions.SumOfBest, Main(), true), "56.000", "10.000", "29.000", "56.000");
        Check(Compute(MetricFunctions.SumOfBest, Main(), false), "56.000", "10.000", "29.000", "56.000");
    }

    [Fact]
    public void BestSplit_is_the_best_cumulative_time_at_each_room_over_the_runs_that_got_there()
    {
        // A1 stops counting at its deleted cell, so its 33 in room 2 is not used.
        // Room 0: min(10 12 10 11 13 10) = 10. Room 1: min(30 33 32 31) = 30. Room 2: min(60 60 62 65) = 60.
        // Segment value is the best segment time, as in Best.
        Check(Compute(MetricFunctions.BestSplit, Main(), true), "1:00.000", "10.000", "30.000", "1:00.000");
    }

    [Fact]
    public void BestSplit_is_empty_outside_an_export()
    {
        Check(Compute(MetricFunctions.BestSplit, Main(), false), "");
    }

    // ---- SuccessRate, Percentile, InterquartileRange --------------------------------------------

    [Fact]
    public void SuccessRate_on_the_session()
    {
        // Target 61 s: 60 and 60 qualify, 62 and 65 do not.
        Check(Compute(MetricFunctions.SuccessRate, Main(), false, targetMs: 61000), "50.00%");
        Check(Compute(MetricFunctions.SuccessRate, Main(), true, targetMs: 61000), "50.00%", "", "", "");
    }

    [Fact]
    public void Percentile_on_the_session()
    {
        // 90th over 60 60 62 65: position 2.7 -> 62 + 0.7 * 3 = 64.1.
        // Room 0 over 10 10 10 11 12 13: position 4.5 -> 12.5.
        MetricResult result = Compute(MetricFunctions.Percentile, Main(), true, percentile: 90);
        Assert.Equal("1:04.100", result.SegmentValue);
        Assert.Equal("12.500", result.RoomValues[0]);
    }

    [Fact]
    public void InterquartileRange()
    {
        // Segment: Q1 position .75 -> 60, Q3 position 2.25 -> 62.75, so 2.75.
        // Room 0: 10 and 11.75. Room 1: 19.75 and 21.25. Room 2: 30 and 33.
        Check(Compute(MetricFunctions.InterquartileRange, Main(), true), "2.750", "1.750", "1.500", "3.000");
        Check(Compute(MetricFunctions.InterquartileRange, Main(), false), "2.750");
    }

    // ---- Counts ------------------------------------------------------------------------------

    [Fact]
    public void CompletedRunCount()
    {
        // Four whole runs. Room 1 lost A1's deleted cell and A2's reset: 6 4 5.
        Check(Compute(MetricFunctions.CompletedRunCount, Main(), true), "4", "6", "4", "5");
        Check(Compute(MetricFunctions.CompletedRunCount, Main(), false), "4");
    }

    [Fact]
    public void TotalRunCount()
    {
        // Six attempts played. Room 1: A0 A2(reset) A3 A4 A5 = 5; room 2: all but A2 = 5.
        Check(Compute(MetricFunctions.TotalRunCount, Main(), true), "6", "6", "5", "5");
        Check(Compute(MetricFunctions.TotalRunCount, Main(), false), "6");
    }

    [Fact]
    public void DnfCount()
    {
        // A2 is the one reset, in room 1. The deleted cell is not a reset.
        Check(Compute(MetricFunctions.DnfCount, Main(), true), "1", "0", "1", "0");
        Check(Compute(MetricFunctions.DnfCount, Main(), false), "1");
    }

    [Fact]
    public void ResetRate()
    {
        // 1 reset in 6 attempts; in room 1, 1 reset in 5.
        Check(Compute(MetricFunctions.ResetRate, Main(), true), "16.67%", "0.00%", "20.00%", "0.00%");
        Check(Compute(MetricFunctions.ResetRate, Main(), false), "16.67%");
    }

    [Fact]
    public void ResetShare_splits_the_resets_between_rooms_and_leaves_the_segment_empty()
    {
        Check(Compute(MetricFunctions.ResetShare, Main(), true), "", "0.00%", "100.00%", "0.00%");
    }

    [Fact]
    public void ResetShare_has_no_data_when_nothing_was_reset()
    {
        PracticeSession session = TrajectorySessions.Of(3, Ms(10000, 20000, 30000));
        Check(Compute(MetricFunctions.ResetShare, session, true), "", "-", "-", "-");
    }

    [Fact]
    public void ResetShare_is_empty_outside_an_export()
    {
        Check(Compute(MetricFunctions.ResetShare, Main(), false), "");
    }

    // ---- TrendSlope --------------------------------------------------------------------------

    [Fact]
    public void TrendSlope()
    {
        // Least squares over the run number, in attempt order, x = 1..n.
        // Segment 60 60 62 65: (4*626 - 10*247) / (4*30 - 100) = 34/20 = 1.7.
        // Room 0 10 12 10 11 13 10: 12/105 = 0.114. Room 1 20 22 19 21: 0.
        // Room 2 30 33 27 30 34 (A1's room 2 is kept): 25/50 = 0.5.
        Check(Compute(MetricFunctions.TrendSlope, Main(), true), "1.700", "0.114", "0.000", "0.500");
        Check(Compute(MetricFunctions.TrendSlope, Main(), false), "1.700");
    }

    // ---- GoldRate ----------------------------------------------------------------------------

    [Fact]
    public void GoldRate_is_the_share_of_runs_that_tie_the_best()
    {
        // Segment: two of four at 60. Room 0: three of six at 10; room 1: 1 of 4; room 2: 1 of 5.
        Check(Compute(MetricFunctions.GoldRate, Main(), true), "50.00%", "50.00%", "25.00%", "20.00%");
        Check(Compute(MetricFunctions.GoldRate, Main(), false), "50.00%");
    }

    // ---- MultimodalTest ----------------------------------------------------------------------

    // A one-room session of `count` completed runs: runs from index `slowFrom` on take slowMs, the rest fastMs.
    private static PracticeSession SingleRoomRuns(int count, int fastMs, int slowMs = 0, int slowFrom = int.MaxValue)
        => TrajectorySessions.Of(1, Enumerable.Range(0, count)
            .Select(i => Ms(i >= slowFrom ? slowMs : fastMs)).ToArray());

    [Fact]
    public void MultimodalTest_is_empty_outside_an_export()
    {
        Check(Compute(MetricFunctions.MultimodalTest, Main(), false), "");
    }

    [Fact]
    public void MultimodalTest_needs_ten_completed_runs()
    {
        Check(Compute(MetricFunctions.MultimodalTest, Main(), true), "-", "-", "-", "-");
        Check(Compute(MetricFunctions.MultimodalTest, SingleRoomRuns(9, 10000), true), "-", "-");
    }

    [Fact]
    public void MultimodalTest_reads_identical_runs_as_a_single_peak()
    {
        // No variance: BC is 0, one bin holds every run.
        Check(Compute(MetricFunctions.MultimodalTest, SingleRoomRuns(10, 10000), true),
            "0.000; Single peak at 10.000.", "0.000; Single peak at 10.000.");
    }

    [Fact]
    public void MultimodalTest_does_not_call_ten_runs_in_two_equal_clusters_bimodal()
    {
        // 5 x 10 s and 5 x 20 s: skew 0, excess kurtosis -2, sample correction 3*81/(8*7) = 4.339,
        // BC = 1 / 2.339 = .427, under the .555 threshold. The peak is the centre of the lowest bin.
        Check(Compute(MetricFunctions.MultimodalTest, SingleRoomRuns(10, 10000, 20000, 5), true),
            "0.427; Single peak at 10.500.", "0.427; Single peak at 10.500.");
    }

    [Fact]
    public void MultimodalTest_reports_two_clusters_of_twenty_runs_as_bimodal()
    {
        // 10 x 10 s and 10 x 20 s: sample correction 3*361/(18*17) = 3.539, BC = 1 / 1.539 = .650.
        // The biggest gap, 10 s, exceeds 1.2 SD (5.1 s). Peaks sit at the bin centres 10.5 and 20.5,
        // each holding half the runs.
        MetricResult result = Compute(MetricFunctions.MultimodalTest, SingleRoomRuns(20, 10000, 20000, 10), true);
        Assert.StartsWith("0.650; Bimodal Detected. Fast: 10.500 (50.00% weight ", result.SegmentValue);
        Assert.Contains("Backup: 20.500 (50.00% weight ", result.SegmentValue);
        Assert.EndsWith("Time loss: +10.000.", result.SegmentValue);
    }

    // ---- RoomDependency ----------------------------------------------------------------------

    // Ten attempts, `completed` of them whole: room 1 = 2 x room 0, room 2 = 100 s - room 1.
    // The others reset in room 2 after two rooms: they still count as attempts, and as pairs for rooms 0 -> 1.
    private static PracticeSession CorrelatedRuns(int completed)
    {
        long[][] attempts = Enumerable.Range(0, 10).Select(i =>
        {
            int r0 = 10000 + i * 1000, r1 = 2 * r0;
            return i < completed ? Ms(r0, r1, 100000 - r1) : Ms(r0, r1);
        }).ToArray();
        return TrajectorySessions.Of(3, attempts);
    }

    [Fact]
    public void RoomDependency_is_empty_outside_an_export()
    {
        Check(Compute(MetricFunctions.RoomDependency, CorrelatedRuns(10), false), "");
    }

    [Fact]
    public void RoomDependency_needs_ten_attempts()
    {
        Check(Compute(MetricFunctions.RoomDependency, Main(), true), "", "", "-", "-");
    }

    [Fact]
    public void RoomDependency_is_the_pearson_correlation_of_each_room_with_the_next()
    {
        // The first room has no predecessor. Rooms 0 -> 1 are exactly linear, rooms 1 -> 2 exactly inverse.
        Check(Compute(MetricFunctions.RoomDependency, CorrelatedRuns(10), true), "", "", "1.00", "-1.00");
    }

    [Fact]
    public void RoomDependency_needs_five_pairs_for_a_room()
    {
        Check(Compute(MetricFunctions.RoomDependency, CorrelatedRuns(5), true), "", "", "1.00", "-1.00");
        Check(Compute(MetricFunctions.RoomDependency, CorrelatedRuns(4), true), "", "", "1.00", "-");
    }

    // ---- The empty session -------------------------------------------------------------------

    public static TheoryData<string, string, string[]> EmptyExpectations => new()
    {
        { nameof(MetricFunctions.Average), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.Median), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.MedianAbsoluteDeviation), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.RelativeMAD), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.StdDev), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.CoefVariation), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.Best), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.Worst), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.SumOfBest), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.BestSplit), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.SuccessRate), "-", [] },
        { nameof(MetricFunctions.Percentile), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.InterquartileRange), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.CompletedRunCount), "0", ["0", "0", "0"] },
        { nameof(MetricFunctions.TotalRunCount), "0", ["0", "0", "0"] },
        { nameof(MetricFunctions.DnfCount), "0", ["0", "0", "0"] },
        { nameof(MetricFunctions.ResetRate), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.ResetShare), "", ["-", "-", "-"] },
        { nameof(MetricFunctions.TrendSlope), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.MultimodalTest), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.GoldRate), "-", ["-", "-", "-"] },
        { nameof(MetricFunctions.RoomDependency), "", ["", "-", "-"] },
    };

    private static Metric ByName(string name)
        => (Metric)Delegate.CreateDelegate(typeof(Metric), typeof(MetricFunctions).GetMethod(name)!);

    [Theory]
    [MemberData(nameof(EmptyExpectations))]
    public void Empty_session_in_export_mode(string name, string segment, string[] rooms)
        => Check(Compute(ByName(name), Empty(), true), segment, rooms);

    // SumOfBest is the one metric that fills room values for the overlay too.
    [Theory]
    [MemberData(nameof(EmptyExpectations))]
    public void Empty_session_in_overlay_mode(string name, string segment, string[] rooms)
    {
        MetricResult result = Compute(ByName(name), Empty(), false);
        if (name == nameof(MetricFunctions.SumOfBest))
            Assert.Equal(rooms, result.RoomValues);
        else
            Assert.Empty(result.RoomValues);
    }

    // A function added to Metrics.cs without a row above fails this one.
    [Fact]
    public void Every_public_metric_function_is_in_the_empty_session_table()
    {
        string[] tested = EmptyExpectations.Select(r => (string)r.ToArray()[0]).ToArray();
        string[] declared = typeof(MetricFunctions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(MetricResult)).Select(m => m.Name).ToArray();
        Assert.Empty(declared.Except(tested));
    }
}
