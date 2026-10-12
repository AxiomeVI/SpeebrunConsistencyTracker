using Celeste.Mod.SpeebrunConsistencyTracker.Charts;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class TrajectoryBaselineSettingTests
{
    [Fact]
    public void A_settings_file_that_chose_the_sum_of_bests_keeps_it()
        => Assert.Equal(TrajectoryBaseline.SumOfBest, ChartOptions.MigratedBaseline(againstSob: true, TrajectoryBaseline.Average));

    [Fact]
    public void A_settings_file_that_did_not_keeps_what_it_has()
        => Assert.Equal(TrajectoryBaseline.BestRun, ChartOptions.MigratedBaseline(againstSob: false, TrajectoryBaseline.BestRun));

    // The chart's switch stores its choice as the enum's number.
    [Fact]
    public void The_members_are_in_the_order_of_the_switch()
    {
        Assert.Equal(0, (int)TrajectoryBaseline.Average);
        Assert.Equal(1, (int)TrajectoryBaseline.BestRun);
        Assert.Equal(2, (int)TrajectoryBaseline.SumOfBest);
    }
}
