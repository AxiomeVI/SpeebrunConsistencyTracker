using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// GetCurrentSlot() cannot express "nothing is selected" — it answers (Scatter, -1) for that and for
// a selected scatter alike. CurrentSlotName carries the distinction, and the in-game test harness
// reads it by reflection, which resolves a property but cannot compare a null. So the sentinel is
// the contract: if it stops reading "None" on an unselected cursor, every harness assertion about
// a hidden overlay silently starts passing for the wrong reason.
public class CurrentSlotTests
{
    [Fact]
    public void An_unselected_cursor_reads_as_the_no_slot_sentinel()
    {
        // No session has been started in this host, so the cursor is at its initial -1.
        Assert.Equal("None", GraphManager.NoSlotName);
        Assert.Equal(GraphManager.NoSlotName, GraphManager.CurrentSlotName);
        Assert.Equal(-1, GraphManager.CurrentSlotRoom);
    }

    [Fact]
    public void The_sentinel_is_not_a_graph_type_name()
    {
        // A GraphType.None member would have been the other design. It would give
        // Enum.GetValues<GraphType>() a value with no ChartDefinition row — and GraphType is
        // persisted, so the member could not simply be withdrawn later.
        Assert.DoesNotContain(GraphManager.NoSlotName,
            System.Enum.GetNames<Celeste.Mod.SpeebrunConsistencyTracker.Enums.GraphType>());
    }
}
