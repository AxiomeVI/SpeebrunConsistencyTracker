using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class MetricHelperTests
{
    private static List<TimeTicks> Ticks(params long[] values)
        => [.. values.Select(v => new TimeTicks(v))];

    [Fact]
    public void ComputePercentile_returns_zero_for_an_empty_list()
    {
        Assert.Equal(TimeTicks.Zero, MetricHelper.ComputePercentile([], 50));
    }

    [Fact]
    public void ComputePercentile_returns_the_only_value_for_a_single_element()
    {
        Assert.Equal(42, MetricHelper.ComputePercentile(Ticks(42), 90).Ticks);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(50, 30)]
    [InlineData(100, 50)]
    public void ComputePercentile_hits_the_exact_value_when_the_position_lands_on_an_index(
        int percentile, long expected)
    {
        Assert.Equal(expected, MetricHelper.ComputePercentile(Ticks(10, 20, 30, 40, 50), percentile).Ticks);
    }

    [Fact]
    public void ComputePercentile_interpolates_between_neighbours()
    {
        // position = 0.25 * 3 = 0.75 -> 10 + 0.75 * (20 - 10)
        Assert.Equal(18, MetricHelper.ComputePercentile(Ticks(10, 20, 30, 40), 25).Ticks);
    }

    [Fact]
    public void ComputePercentile_clamps_out_of_range_percentiles()
    {
        Assert.Equal(10, MetricHelper.ComputePercentile(Ticks(10, 20, 30), -5).Ticks);
        Assert.Equal(30, MetricHelper.ComputePercentile(Ticks(10, 20, 30), 150).Ticks);
    }

    [Fact]
    public void ComputeStdDev_is_zero_below_two_samples()
    {
        Assert.Equal(0.0, MetricHelper.ComputeStdDev([], 0));
        Assert.Equal(0.0, MetricHelper.ComputeStdDev(Ticks(10), 10));
    }

    [Fact]
    public void ComputeStdDev_is_the_sample_standard_deviation()
    {
        // values 10,20,30,40 -> mean 25, sum of squares 500, /(n-1)=3 -> sqrt(166.66..)
        double sd = MetricHelper.ComputeStdDev(Ticks(10, 20, 30, 40), 25);
        Assert.Equal(12.909944, sd, 5);
    }

    [Fact]
    public void ComputeStdDev_is_zero_when_every_value_is_identical()
    {
        Assert.Equal(0.0, MetricHelper.ComputeStdDev(Ticks(7, 7, 7, 7), 7));
    }

    [Fact]
    public void ComputeMAD_is_zero_for_an_empty_list()
    {
        Assert.Equal(TimeTicks.Zero, MetricHelper.ComputeMAD([]));
    }

    [Fact]
    public void ComputeMAD_is_the_median_of_the_absolute_deviations()
    {
        // median 30; deviations 20,10,0,10,20 sorted 0,10,10,20,20 -> median 10
        Assert.Equal(10, MetricHelper.ComputeMAD(Ticks(10, 20, 30, 40, 50)).Ticks);
    }

    // The sample correction divides by (n-2)(n-3), zero at n=2 and n=3. Pinned so the callers'
    // n >= 4 guard is not dropped.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void CalculateBC_collapses_to_zero_below_four_samples(int count)
    {
        List<TimeTicks> values = Ticks([.. Enumerable.Range(1, count).Select(i => (long)(i * 10))]);
        double mean = values.Average(v => (double)v.Ticks);

        Assert.Equal(0.0, MetricHelper.CalculateBC(values, mean));
    }

    [Fact]
    public void CalculateBC_returns_a_finite_positive_value_from_four_samples_up()
    {
        List<TimeTicks> values = Ticks(10, 20, 30, 100);
        double mean = values.Average(v => (double)v.Ticks);

        double bc = MetricHelper.CalculateBC(values, mean);

        Assert.True(double.IsFinite(bc));
        Assert.True(bc > 0);
    }

    [Fact]
    public void CalculateBC_is_zero_when_every_value_is_identical()
    {
        Assert.Equal(0.0, MetricHelper.CalculateBC(Ticks(5, 5, 5, 5, 5), 5));
    }

    [Fact]
    public void DetectSignificantGap_is_true_when_a_gap_exceeds_1_2_standard_deviations()
    {
        Assert.True(MetricHelper.DetectSignificantGap(Ticks(10, 12, 14, 100), 10));
    }

    [Fact]
    public void DetectSignificantGap_is_false_for_evenly_spread_values()
    {
        Assert.False(MetricHelper.DetectSignificantGap(Ticks(10, 20, 30, 40), 100));
    }

    [Fact]
    public void DetectSignificantGap_is_false_for_fewer_than_two_values()
    {
        Assert.False(MetricHelper.DetectSignificantGap(Ticks(10), 1));
        Assert.False(MetricHelper.DetectSignificantGap([], 1));
    }

    [Fact]
    public void FormatPercent_scales_by_a_hundred_and_appends_the_sign_once()
    {
        Assert.Equal("45.00%", MetricHelper.FormatPercent(0.45));
        Assert.Equal("0.00%", MetricHelper.FormatPercent(0));
        Assert.Equal("100.00%", MetricHelper.FormatPercent(1));
    }

    [Fact]
    public void FormatPercent_uses_the_invariant_culture_decimal_point()
    {
        Assert.Contains(".", MetricHelper.FormatPercent(0.125));
        Assert.DoesNotContain(",", MetricHelper.FormatPercent(0.125));
    }

    [Fact]
    public void ComputeConsistencyScore_is_zero_for_a_non_positive_median()
    {
        Assert.Equal(0, MetricHelper.ComputeConsistencyScore(0, TimeTicks.Zero, 0, 0, 0));
        Assert.Equal(0, MetricHelper.ComputeConsistencyScore(-1, TimeTicks.Zero, 0, 0, 0));
    }

    [Fact]
    public void ComputeConsistencyScore_is_one_for_a_perfectly_repeated_run()
    {
        double score = MetricHelper.ComputeConsistencyScore(100, new TimeTicks(100), 0, 0, 0);
        Assert.Equal(1.0, score, 10);
    }

    [Fact]
    public void ComputeConsistencyScore_collapses_when_every_run_is_a_reset()
    {
        Assert.Equal(0, MetricHelper.ComputeConsistencyScore(100, new TimeTicks(100), 0, 1.0, 0));
    }

    [Fact]
    public void ComputeConsistencyScore_stays_within_zero_and_one()
    {
        double score = MetricHelper.ComputeConsistencyScore(100, new TimeTicks(10), 0.5, 0.3, 0.4);
        Assert.InRange(score, 0.0, 1.0);
    }

    [Fact]
    public void LinearRegression_reports_the_per_attempt_slope()
    {
        Assert.Equal(10, MetricHelper.LinearRegression(Ticks(10, 20, 30, 40)).Ticks);
    }

    [Fact]
    public void LinearRegression_is_zero_for_a_flat_series()
    {
        Assert.Equal(0, MetricHelper.LinearRegression(Ticks(50, 50, 50)).Ticks);
    }

    [Fact]
    public void GetFullPeakAnalysis_finds_the_peak_at_the_last_bin_for_a_monotonically_rising_sample()
    {
        // Bin counts rise monotonically (1,2,3,4,10): the true mode sits at the last bin, which
        // the pre-fix scan could never record — it only looked at interior bins and required a
        // drop-off after the hill, and the last bin has nothing to drop off into.
        const long baseTicks = 1_000_000;
        const long binWidth = 127_500; // range/(5-1), see below
        int[] counts = [1, 2, 3, 4, 10];

        var times = new List<TimeTicks>();
        for (int bin = 0; bin < counts.Length; bin++)
            for (int sample = 0; sample < counts[bin]; sample++)
                times.Add(new TimeTicks(baseTicks + bin * binWidth + sample * 1000));

        var min = new TimeTicks(baseTicks);
        // A small range with a zero IQR floors the bin width at one frame (170_000), which
        // pushes the raw bin count under the 5-bin floor, forcing the clamp to exactly 5 bins.
        var max = new TimeTicks(baseTicks + 3 * 170_000);

        var report = MetricHelper.GetFullPeakAnalysis(times, min, max, TimeTicks.Zero, bimodalDetected: false);

        Assert.False(report.NoDominantPeak);
        Assert.False(report.IsBimodal);
        // The peak must land near the tall last bin, not near the first bin the old
        // FirstOrDefault-on-empty bug would have defaulted to.
        Assert.True(report.FastPeak.Value.Ticks > baseTicks + 300_000);
        Assert.StartsWith("Single peak at", report.Summary);
    }

    [Fact]
    public void GetFullPeakAnalysis_reports_no_dominant_peak_when_every_bin_is_below_the_noise_floor()
    {
        // 50 bins (forced via a large range with a zero IQR, which floors the bin width at one
        // frame), 4 samples evenly in each: 4/200 = 2% of the total, under the 3% noise floor
        // everywhere, so no bin ever qualifies as a hill.
        const long baseTicks = 1_000_000;
        const long binWidth = 1_000_000;

        var times = new List<TimeTicks>();
        for (int bin = 0; bin < 50; bin++)
            for (int sample = 0; sample < 4; sample++)
                times.Add(new TimeTicks(baseTicks + bin * binWidth + sample * 250_000));

        var min = new TimeTicks(baseTicks);
        var max = new TimeTicks(baseTicks + 49 * binWidth);

        var report = MetricHelper.GetFullPeakAnalysis(times, min, max, TimeTicks.Zero, bimodalDetected: false);

        Assert.True(report.NoDominantPeak);
        Assert.False(report.IsBimodal);
        Assert.Equal("No dominant peak detected.", report.Summary);
    }
}
