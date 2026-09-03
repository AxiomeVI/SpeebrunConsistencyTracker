using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

public class AttemptMatrixTests
{
    [Fact]
    public void A_new_matrix_has_no_rows_and_no_columns()
    {
        AttemptMatrix matrix = new();
        Assert.Equal(0, matrix.RowCount);
        Assert.Equal(0, matrix.ColumnCount);
    }

    // NotReached is deliberately the zero value so a freshly allocated row costs nothing.
    [Fact]
    public void Cells_start_as_NotReached()
    {
        AttemptMatrix matrix = new();
        matrix.AddRow();
        matrix.EnsureColumns(3);

        Assert.All(matrix.GetRow(0).ToArray(), c => Assert.Equal(RoomCellState.NotReached, c.State));
        Assert.Equal(RoomCellState.NotReached, default(RoomCell).State);
    }

    [Fact]
    public void Reading_past_the_last_column_yields_NotReached_instead_of_throwing()
    {
        AttemptMatrix matrix = new();
        matrix.AddRow();
        matrix.EnsureColumns(2);

        Assert.Equal(RoomCellState.NotReached, matrix[0, 99].State);
    }

    [Fact]
    public void EnsureColumns_never_shrinks_the_matrix()
    {
        AttemptMatrix matrix = new();
        matrix.EnsureColumns(5);
        matrix.EnsureColumns(2);

        Assert.Equal(5, matrix.ColumnCount);
    }

    // The growth path re-lays out every row, so this is where data would be lost.
    [Fact]
    public void Growing_past_the_column_capacity_preserves_existing_cells()
    {
        AttemptMatrix matrix = new(initialColumnCapacity: 2, initialRowCapacity: 4);
        matrix.AddRow();
        matrix.AddRow();
        matrix.EnsureColumns(2);
        matrix.SetCell(0, 0, RoomCell.Completed(new TimeTicks(11)));
        matrix.SetCell(0, 1, RoomCell.Completed(new TimeTicks(12)));
        matrix.SetCell(1, 0, RoomCell.Completed(new TimeTicks(21)));

        matrix.EnsureColumns(6);

        Assert.Equal(11, matrix[0, 0].Time.Ticks);
        Assert.Equal(12, matrix[0, 1].Time.Ticks);
        Assert.Equal(21, matrix[1, 0].Time.Ticks);
        Assert.Equal(RoomCellState.NotReached, matrix[1, 1].State);
        Assert.Equal(6, matrix.ColumnCount);
    }

    [Fact]
    public void Growing_past_the_row_capacity_preserves_existing_cells()
    {
        AttemptMatrix matrix = new(initialColumnCapacity: 4, initialRowCapacity: 1);
        matrix.AddRow();
        matrix.EnsureColumns(2);
        matrix.SetCell(0, 1, RoomCell.Completed(new TimeTicks(7)));

        matrix.AddRow();
        matrix.AddRow();

        Assert.Equal(3, matrix.RowCount);
        Assert.Equal(7, matrix[0, 1].Time.Ticks);
    }

    [Fact]
    public void GetColumn_walks_one_room_across_every_attempt()
    {
        AttemptMatrix matrix = new();
        matrix.AddRow();
        matrix.AddRow();
        matrix.AddRow();
        matrix.EnsureColumns(2);
        matrix.SetCell(0, 1, RoomCell.Completed(new TimeTicks(10)));
        matrix.SetCell(1, 1, RoomCell.DNF);
        matrix.SetCell(2, 1, RoomCell.Completed(new TimeTicks(30)));

        RoomCell[] column = matrix.GetColumn(1).ToArray();

        Assert.Equal(3, column.Length);
        Assert.Equal(10, column[0].Time.Ticks);
        Assert.Equal(RoomCellState.DNF, column[1].State);
        Assert.Equal(30, column[2].Time.Ticks);
    }

    [Fact]
    public void HasTime_is_true_only_for_a_completed_cell()
    {
        Assert.True(RoomCell.Completed(new TimeTicks(1)).HasTime);
        Assert.False(RoomCell.DNF.HasTime);
        Assert.False(RoomCell.Deleted.HasTime);
        Assert.False(RoomCell.NotReached.HasTime);
    }
}
