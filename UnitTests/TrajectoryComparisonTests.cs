using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// Three runs over three rooms; the third stops after two.
public class TrajectoryComparisonTests
{
    private static TrajectoryModel Model(bool againstSob = false) =>
        TrajectoryModel.Build(TrajectorySessions.Of(3, [10, 30, 20], [20, 15, 25], [12, 18]), 3,
                              againstSob ? TrajectoryBaseline.SumOfBest : TrajectoryBaseline.Average);

    [Fact]
    public void Two_runs_compare_by_their_times()
    {
        TrajectoryModel model = Model();
        TrajectoryComparison c = TrajectoryComparison.At(model, model.Attempts[0], model.Attempts[1], model.BestAmongOthers(0), 1);

        Assert.Equal(30, c.MainRoom);
        Assert.Equal(40, c.MainRunning);
        Assert.Equal(15, c.CompRoom);
        Assert.Equal(35, c.CompRunning);
        Assert.Equal(15, c.RoomDelta);
        Assert.Equal(5, c.CumulDelta);
        Assert.True(c.CompReaches && c.RoomKnown);
    }

    // The table reads the gap from the deviations and the card from the running times: one number.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_gap_at_the_end_of_a_room_is_the_difference_of_the_running_times(bool againstSob)
    {
        TrajectoryModel model = Model(againstSob);
        AttemptLine[] lines = [model.Attempts[0], model.Attempts[1], model.Attempts[2], model.SobLine, null];
        foreach (AttemptLine main in lines)
            foreach (AttemptLine comp in lines)
                for (int room = 0; room < 3; room++)
                {
                    if (main != null && room >= main.RoomsCompleted) continue;
                    TrajectoryComparison c = TrajectoryComparison.At(model, main, comp, model.BestAmongOthers(-1), room);
                    if (c.CompReaches) Assert.Equal(c.MainRunning - c.CompRunning, c.CumulDelta);
                }
    }

    [Fact]
    public void Past_the_end_of_the_second_run_nothing_is_known()
    {
        TrajectoryModel model = Model();
        TrajectoryComparison c = TrajectoryComparison.At(model, model.Attempts[0], model.Attempts[2], model.BestAmongOthers(0), 2);

        Assert.False(c.CompReaches);
        Assert.False(c.RoomKnown);
    }

    [Fact]
    public void Against_the_reference_the_gap_is_the_runs_own_deviation()
    {
        TrajectoryModel model = Model();
        TrajectoryComparison c = TrajectoryComparison.At(model, model.Attempts[0], null, model.BestAmongOthers(0), 1);

        Assert.Equal(model.Attempts[0].CumulativeDeviations[1], c.CumulDelta);
        Assert.Equal(30 - model.RoomReference[1], c.RoomDelta);
        Assert.True(c.CompReaches && c.RoomKnown);
    }

    // Run 0 is at 40 after two rooms; the best of the others there is run 2, at 30.
    [Fact]
    public void Vs_best_is_the_gap_to_the_best_of_the_other_runs()
    {
        TrajectoryModel model = Model();
        TrajectoryComparison c = TrajectoryComparison.At(model, model.Attempts[0], model.SobLine, model.BestAmongOthers(0), 1);

        Assert.True(c.HasBest);
        Assert.Equal(10, c.VsBest);
    }

    [Fact]
    public void With_no_other_run_in_the_room_there_is_no_best()
    {
        TrajectoryModel model = TrajectoryModel.Build(TrajectorySessions.Of(2, [10, 10]), 2);
        TrajectoryComparison c = TrajectoryComparison.At(model, model.Attempts[0], model.SobLine, model.BestAmongOthers(0), 0);

        Assert.False(c.HasBest);
    }
}
