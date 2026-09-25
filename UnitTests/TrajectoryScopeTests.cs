using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class TrajectoryScopeTests
{
    private static TrajectoryScope ScopeOver(TrajectoryModel model, bool[] hidden)
    {
        TrajectoryScope scope = new(model);
        scope.Recompute(hidden);
        return scope;
    }

    private static TrajectoryScope ScopeOf(int rooms, params long[][] attempts)
    {
        TrajectoryModel model = TrajectoryModel.Build(TrajectorySessions.Of(rooms, attempts), rooms);
        return ScopeOver(model, TrajectorySessions.AllVisible(rooms));
    }

    // The drawing range is floored at one frame, so the range tests need times on that scale --
    // below it every sample would read as the floor and assert nothing about the data.
    private const long F = TrajectoryScope.MinHalfRange;

    private static long[][] InFrames(params long[][] attempts)
    {
        long[][] scaled = new long[attempts.Length][];
        for (int a = 0; a < attempts.Length; a++)
        {
            scaled[a] = new long[attempts[a].Length];
            for (int r = 0; r < attempts[a].Length; r++) scaled[a][r] = attempts[a][r] * F;
        }
        return scaled;
    }

    // ---- the best-run tie-break -------------------------------------------------------------

    [Fact]
    public void BestIdx_is_the_lowest_cumulative_deviation_at_the_last_visible_room()
    {
        // Averages 14 everywhere; deviations at room 2 are -12, +18, -6.
        TrajectoryScope scope = ScopeOf(3, [10, 10, 10], [20, 20, 20], [12, 12, 12]);

        Assert.Equal(2, scope.LastVisibleRoom);
        Assert.Equal(0, scope.BestIdx);
        Assert.False(scope.LastIsBest);
    }

    // Pinning test, not a stated requirement: OrderBy is a stable sort, so equal deviations leave
    // the candidates in attempt order and the *earliest* run wins. Nothing in the code says this
    // was intended; the test records what it does.
    [Fact]
    public void BestIdx_breaks_a_deviation_tie_towards_the_earliest_attempt()
    {
        TrajectoryScope scope = ScopeOf(2, [10, 10], [10, 10], [10, 10]);

        Assert.Equal(0, scope.BestIdx);
        Assert.False(scope.LastIsBest);
    }

    [Fact]
    public void BestIdx_falls_back_to_the_attempt_that_got_furthest_when_none_reached_the_end()
    {
        // Three rooms, so neither attempt reaches the last visible room (index 2).
        TrajectoryScope scope = ScopeOf(3, [10], [10, 10]);

        Assert.Equal(2, scope.LastVisibleRoom);
        Assert.Equal(1, scope.BestIdx);
        Assert.True(scope.LastIsBest);
        Assert.False(scope.AnyCompleted);
    }

    // Reaching further beats ending on a lower deviation: attempt 0 is far ahead of average after
    // its single room, and still loses to the attempt that survived one room longer.
    [Fact]
    public void Reaching_further_outranks_a_lower_final_deviation()
    {
        // Averages: room 0 = (1 + 10) / 2 = 5, room 1 = 10. Final deviations: -4 and +5.
        TrajectoryScope scope = ScopeOf(4, [1], [10, 10]);

        Assert.Equal(1, scope.BestIdx);
    }

    [Fact]
    public void Among_attempts_that_got_equally_far_the_lowest_final_deviation_wins()
    {
        // Averages: room 0 = 10, room 1 = 20. Final deviations: +10 for attempt 0, -10 for 1.
        TrajectoryScope scope = ScopeOf(3, [10, 30], [10, 10]);

        Assert.Equal(1, scope.BestIdx);
        Assert.True(scope.LastIsBest);
    }

    [Fact]
    public void Hiding_the_last_room_can_move_the_best_run_to_another_attempt()
    {
        // Averages 15, 15, 55. Cumulative deviations: attempt 0 = -5, -10, +35;
        // attempt 1 = +5, +10, -35.
        TrajectoryModel model = TrajectoryModel.Build(
            TrajectorySessions.Of(3, [10, 10, 100], [20, 20, 10]), 3);

        TrajectoryScope wholeRun = ScopeOver(model, TrajectorySessions.AllVisible(3));
        Assert.Equal(1, wholeRun.BestIdx);

        TrajectoryScope firstTwoRooms = ScopeOver(model, TrajectorySessions.Hiding(3, 2));
        Assert.Equal(1, firstTwoRooms.LastVisibleRoom);
        Assert.Equal(0, firstTwoRooms.BestIdx);
    }

    // ---- SoB coincidence ---------------------------------------------------------------------

    [Fact]
    public void SobIsBest_when_the_best_run_matches_the_sum_of_best_at_the_last_visible_room()
    {
        // Attempt 0 is fastest in every room, so the SoB line lands on it.
        TrajectoryScope scope = ScopeOf(3, [10, 10, 10], [20, 20, 20], [12, 12, 12]);

        Assert.True(scope.SobIsBest);
        Assert.False(scope.LastIsBest);
        Assert.Equal(LineCoincidence.SobIsBest, scope.Coincidence);
    }

    [Fact]
    public void Coincidence_is_AllThree_when_the_last_run_is_both_the_best_and_the_sob()
    {
        TrajectoryScope scope = ScopeOf(2, [20, 20], [10, 10]);

        Assert.True(scope.SobIsBest);
        Assert.True(scope.LastIsBest);
        Assert.Equal(LineCoincidence.AllThree, scope.Coincidence);
    }

    [Fact]
    public void Coincidence_is_LastIsBest_when_the_sob_beats_every_single_run()
    {
        // Averages 15 and 20. Attempt 1 ends at -5 and is the best run; the SoB ends at -15.
        TrajectoryScope scope = ScopeOf(2, [10, 30], [20, 10]);

        Assert.True(scope.LastIsBest);
        Assert.False(scope.SobIsBest);
        Assert.Equal(LineCoincidence.LastIsBest, scope.Coincidence);
    }

    [Fact]
    public void Coincidence_is_None_when_the_best_run_is_neither_the_last_nor_the_sob()
    {
        // Averages 20 and 23. Deviations at room 1: -3, -13, +17; the SoB ends at -23.
        TrajectoryScope scope = ScopeOf(2, [10, 30], [20, 10], [30, 30]);

        Assert.Equal(1, scope.BestIdx);
        Assert.False(scope.LastIsBest);
        Assert.False(scope.SobIsBest);
        Assert.Equal(LineCoincidence.None, scope.Coincidence);
    }

    // ---- the drawing range -------------------------------------------------------------------

    // A single run has every deviation at 0. The range used to floor at one tick, and half a dozen
    // axis labels all read +-0.000.
    [Fact]
    public void A_single_run_still_gets_a_frame_of_range_to_draw_against()
    {
        TrajectoryScope scope = ScopeOf(2, [10 * F, 10 * F]);

        Assert.Equal(F, scope.MaxUpwardDeviation);
        Assert.Equal(F, scope.MaxDownwardDeviation);
        Assert.Equal(2 * F, scope.TotalRange);
    }

    [Fact]
    public void The_deviation_range_spans_the_furthest_line_each_way()
    {
        // Deviations run from -12 (attempt 0, room 2) to +18 (attempt 1, room 2), in frames.
        TrajectoryScope scope = ScopeOf(3, InFrames([10, 10, 10], [20, 20, 20], [12, 12, 12]));

        Assert.Equal(12 * F, scope.MaxUpwardDeviation);
        Assert.Equal(18 * F, scope.MaxDownwardDeviation);
        Assert.Equal(30 * F, scope.TotalRange);
        Assert.Equal(42 * F, scope.RoomAveragesSum);
    }

    // A hidden room in the middle still contributes to the running deviation, so the range is not
    // recomputed from the visible rooms alone — only rooms past the last visible one drop out.
    [Fact]
    public void A_hidden_middle_room_still_counts_towards_the_range()
    {
        TrajectoryModel model = TrajectoryModel.Build(
            TrajectorySessions.Of(3, InFrames([10, 10, 10], [20, 20, 20], [12, 12, 12])), 3);

        TrajectoryScope scope = ScopeOver(model, TrajectorySessions.Hiding(3, 1));

        Assert.Equal(2, scope.LastVisibleRoom);
        Assert.Equal(12 * F, scope.MaxUpwardDeviation);
        Assert.Equal(18 * F, scope.MaxDownwardDeviation);
        Assert.Equal(42 * F, scope.RoomAveragesSum);
    }

    [Fact]
    public void Rooms_past_the_last_visible_one_drop_out_of_the_range()
    {
        TrajectoryModel model = TrajectoryModel.Build(
            TrajectorySessions.Of(3, InFrames([10, 10, 10], [20, 20, 20], [12, 12, 12])), 3);

        TrajectoryScope scope = ScopeOver(model, TrajectorySessions.Hiding(3, 2));

        Assert.Equal(1, scope.LastVisibleRoom);
        Assert.Equal(8 * F, scope.MaxUpwardDeviation);
        Assert.Equal(12 * F, scope.MaxDownwardDeviation);
        Assert.Equal(28 * F, scope.RoomAveragesSum);
    }

    // ---- degenerate scopes -------------------------------------------------------------------

    [Fact]
    public void Hiding_every_room_leaves_the_minimum_range_so_the_pixel_scale_stays_finite()
    {
        TrajectoryModel model = TrajectoryModel.Build(TrajectorySessions.Of(2, [10, 10], [20, 20]), 2);

        TrajectoryScope scope = ScopeOver(model, TrajectorySessions.Hiding(2, 0, 1));

        Assert.Equal(-1, scope.LastVisibleRoom);
        Assert.Equal(F, scope.MaxUpwardDeviation);
        Assert.Equal(F, scope.MaxDownwardDeviation);
        Assert.Equal(2 * F, scope.TotalRange);
        Assert.Equal(0, scope.RoomAveragesSum);
        Assert.False(scope.SobIsBest);
        Assert.False(scope.AnyCompleted);
        Assert.False(scope.SobReachesEnd);
        Assert.False(scope.LastReachesEnd);
        // With nothing visible the last attempt stands in for the best one.
        Assert.Equal(1, scope.BestIdx);
        Assert.True(scope.LastIsBest);
    }

    // Pinning test: with no attempt at all, BestIdx is -1 and LastIsBest compares -1 to
    // Attempts.Count - 1, which is also -1, so Coincidence reports LastIsBest over an empty
    // chart. The overlay never reaches this — every drawing path returns early on an empty
    // model — so it is recorded rather than relied on.
    [Fact]
    public void An_empty_model_reports_no_best_run_and_still_claims_LastIsBest()
    {
        PracticeSession session = TrajectorySessions.Of(2);
        TrajectoryScope scope = ScopeOver(TrajectoryModel.Build(session, 2), TrajectorySessions.AllVisible(2));

        Assert.Equal(-1, scope.BestIdx);
        Assert.True(scope.LastIsBest);
        Assert.Equal(LineCoincidence.LastIsBest, scope.Coincidence);
    }

    // ---- reach flags -------------------------------------------------------------------------

    [Fact]
    public void The_reach_flags_say_which_lines_span_the_visible_rooms()
    {
        // Attempt 1 is the last one and stops in room 1, so it never reaches room 2; the SoB does
        // because attempt 0 has a time in every room.
        TrajectoryScope scope = ScopeOf(3, [10, 10, 10], [20, 20]);

        Assert.True(scope.AnyCompleted);
        Assert.True(scope.SobReachesEnd);
        Assert.False(scope.LastReachesEnd);
    }
}
