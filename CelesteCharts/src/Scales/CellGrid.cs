#nullable enable

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Rows and columns of cells, for a heatmap or a calendar: bands across for the columns, bands down
///     for the rows. The same object gives a cell's rectangle and the cell under a pixel, so drawing and
///     hover agree, as with <see cref="Bands"/>.
/// </summary>
// A cell is the bar of its column band by the bar of its row band: a fill below 1 leaves a gap between
// cells, which is what separates two cells of nearly the same colour.
internal readonly struct CellGrid {
    public Bands Columns { get; }
    public Bands Rows { get; }

    public CellGrid(Bands columns, Bands rows) {
        Columns = columns;
        Rows = rows;
    }

    /// <summary>A grid filling <paramref name="area"/>, on whole pixels.</summary>
    public CellGrid(int columns, int rows, Area area, float fill = 0.9f)
        : this(new Bands(columns, area.X, area.Width, fill, wholePixels: true),
               new Bands(rows, area.Y, area.Height, fill, wholePixels: true)) { }

    public int ColumnCount => Columns.Count;
    public int RowCount => Rows.Count;

    /// <summary>The rectangle of cell (<paramref name="column"/>, <paramref name="row"/>).</summary>
    public Area Cell(int column, int row) => new(Columns.BarStart(column), Rows.BarStart(row), Columns.BarWidth, Rows.BarWidth);

    /// <summary>The whole grid, from the first cell's corner to the last's.</summary>
    public Area Bounds => new(Columns.BandStart(0), Rows.BandStart(0), Columns.Width * Columns.Count, Rows.Width * Rows.Count);

    /// <summary>The cell whose band holds the pixel, gaps included, or (-1, -1) outside the grid.</summary>
    public (int Column, int Row) IndexAt(float x, float y) {
        int column = Columns.IndexAt(x), row = Rows.IndexAt(y);
        return column < 0 || row < 0 ? (-1, -1) : (column, row);
    }
}
