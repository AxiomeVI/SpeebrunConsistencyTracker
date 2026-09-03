using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

// The Metrics class shares its name with its namespace; the alias keeps the call sites readable.
using MetricFunctions = Celeste.Mod.SpeebrunConsistencyTracker.Metrics.Metrics;

namespace SpeebrunConsistencyTracker.UnitTests;

public class MetricsTests
{
    private static TimeTicks T(long ticks) => new(ticks);

    // One-room attempts, so each recorded time is a whole segment time. The trailing attempt
    // opened by the last StartNewAttempt stays incomplete and is ignored.
    private static PracticeSession SessionOfSegments(params long[] segmentTimes)
    {
        PracticeSession session = new(new StubSegmentShape(roomCount: 1));
        foreach (long t in segmentTimes)
        {
            session.CompleteRoom(T(t));
            session.StartNewAttempt();
        }
        return session;
    }

    private static MetricContext Context(long targetTicks = 0, int percentile = 50)
        => new(T(targetTicks), percentile);

    [Fact]
    public void SuccessRate_counts_a_run_below_the_target_as_a_success()
    {
        PracticeSession session = SessionOfSegments(100);

        MetricResult result = MetricFunctions.SuccessRate(session, Context(targetTicks: 200), isExport: false);

        Assert.Equal(MetricHelper.FormatPercent(1.0), result.SegmentValue);
    }

    [Fact]
    public void SuccessRate_does_not_count_a_run_above_the_target()
    {
        PracticeSession session = SessionOfSegments(300);

        MetricResult result = MetricFunctions.SuccessRate(session, Context(targetTicks: 200), isExport: false);

        Assert.Equal(MetricHelper.FormatPercent(0.0), result.SegmentValue);
    }

    // The comparison is s <= target, so a run landing exactly on the target is a success.
    [Fact]
    public void SuccessRate_counts_a_run_equal_to_the_target_as_a_success()
    {
        PracticeSession session = SessionOfSegments(200);

        MetricResult result = MetricFunctions.SuccessRate(session, Context(targetTicks: 200), isExport: false);

        Assert.Equal(MetricHelper.FormatPercent(1.0), result.SegmentValue);
    }

    [Fact]
    public void SuccessRate_is_the_share_of_runs_at_or_under_the_target()
    {
        PracticeSession session = SessionOfSegments(100, 200, 300, 400);

        MetricResult result = MetricFunctions.SuccessRate(session, Context(targetTicks: 200), isExport: false);

        Assert.Equal(MetricHelper.FormatPercent(0.5), result.SegmentValue);
    }

    [Fact]
    public void SuccessRate_is_empty_when_no_run_has_been_completed()
    {
        PracticeSession session = new(new StubSegmentShape(roomCount: 3));

        MetricResult result = MetricFunctions.SuccessRate(session, Context(targetTicks: 200), isExport: false);

        Assert.Equal("", result.SegmentValue);
        Assert.Empty(result.RoomValues);
    }

    [Fact]
    public void SuccessRate_reads_the_target_from_the_context()
    {
        PracticeSession session = SessionOfSegments(100, 300);

        Assert.Equal(MetricHelper.FormatPercent(0.5), MetricFunctions.SuccessRate(session, Context(targetTicks: 200), isExport: false).SegmentValue);
        Assert.Equal(MetricHelper.FormatPercent(1.0), MetricFunctions.SuccessRate(session, Context(targetTicks: 400), isExport: false).SegmentValue);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(50, 30)]
    [InlineData(100, 50)]
    public void Percentile_follows_the_percentile_carried_by_the_context(int percentile, long expectedTicks)
    {
        PracticeSession session = SessionOfSegments(10, 20, 30, 40, 50);

        MetricResult result = MetricFunctions.Percentile(session, Context(percentile: percentile), isExport: false);

        Assert.Equal(T(expectedTicks).ToString(), result.SegmentValue);
    }

    [Fact]
    public void Percentile_interpolates_between_two_runs()
    {
        PracticeSession session = SessionOfSegments(10, 20, 30, 40);

        // position = 0.25 * 3 = 0.75 -> 10 + 0.75 * (20 - 10)
        MetricResult result = MetricFunctions.Percentile(session, Context(percentile: 25), isExport: false);

        Assert.Equal(T(18).ToString(), result.SegmentValue);
    }

    [Fact]
    public void Percentile_is_zero_when_no_run_has_been_completed()
    {
        PracticeSession session = new(new StubSegmentShape(roomCount: 3));

        MetricResult result = MetricFunctions.Percentile(session, Context(percentile: 90), isExport: false);

        Assert.Equal(TimeTicks.Zero.ToString(), result.SegmentValue);
    }

    [Fact]
    public void Percentile_fills_one_room_column_per_room_on_export()
    {
        PracticeSession session = SessionOfSegments(10, 20, 30);

        MetricResult result = MetricFunctions.Percentile(session, Context(percentile: 50), isExport: true);

        Assert.Single(result.RoomValues);
        Assert.Equal(T(20).ToString(), result.RoomValues[0]);
    }
}
