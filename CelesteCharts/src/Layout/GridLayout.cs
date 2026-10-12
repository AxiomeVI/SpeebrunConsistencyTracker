#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Where a grid's tiles go.</summary>
/// <param name="TileWidth">One tile's width, gutter excluded.</param>
/// <param name="ContentHeight">
///     The height every row needs. More than the box's height when a minimum tile height made the
///     tiles overflow it: the caller scrolls, or accepts that the last rows are cut.
/// </param>
internal readonly record struct GridPlan(int Columns, int Rows, float TileWidth, float TileHeight, float Gutter,
                                         float ContentHeight, bool Overflows) {
    /// <summary>A tile's top-left corner, relative to the grid's own top-left.</summary>
    public (float X, float Y) TileAt(int index) {
        if (Columns <= 0) return (0f, 0f);
        return (index % Columns * (TileWidth + Gutter), index / Columns * (TileHeight + Gutter));
    }

    /// <summary>The grid's own width: what to centre in the box.</summary>
    public float ContentWidth => Columns <= 0 ? 0f : Columns * TileWidth + (Columns - 1) * Gutter;
}

/// <summary>
///     Lays out N tiles in a box: how many columns, and how big each tile is. Every tile has the same
///     shape (width over height); the column count that gives the biggest tiles wins.
/// </summary>
internal static class GridLayout {
    /// <param name="tiles">How many tiles.</param>
    /// <param name="width">The box's width.</param>
    /// <param name="height">The box's height.</param>
    /// <param name="gutter">The space between two tiles.</param>
    /// <param name="aspect">Every tile's shape, width over height.</param>
    /// <param name="minTileHeight">
    ///     0 fits every tile in the box however small they get. Above 0, tiles never shrink below it and
    ///     the rows that no longer fit overflow the box — the choice between "every chart at once" and
    ///     "legible charts, scrolled".
    /// </param>
    /// <param name="maxTileHeight">A tile never grows past it, so four charts do not each fill a quarter screen.</param>
    public static GridPlan Plan(int tiles, float width, float height, float gutter, float aspect = 1.45f,
                                float minTileHeight = 0f, float maxTileHeight = float.PositiveInfinity) {
        if (tiles <= 0 || width <= 0f || height <= 0f || aspect <= 0f)
            return new GridPlan(0, 0, 0f, 0f, gutter, 0f, false);

        GridPlan? best = null;
        for (int columns = 1; columns <= tiles; columns++) {
            int rows = (tiles + columns - 1) / columns;
            float roomWidth = (width - (columns - 1) * gutter) / columns;
            float roomHeight = (height - (rows - 1) * gutter) / rows;
            if (roomWidth <= 0f) break;   // more columns only leave less room

            // The biggest tile of the shape that fits its share of the box...
            float tileHeight = Math.Min(Math.Min(roomHeight, maxTileHeight), roomWidth / aspect);
            // ...unless that is below the minimum: then the minimum, and the rows overflow the box —
            // provided a tile that tall still fits across, or this column count is out.
            if (tileHeight < minTileHeight) {
                if (minTileHeight * aspect > roomWidth + 1e-3f) continue;
                tileHeight = minTileHeight;
            }
            if (tileHeight <= 0f) continue;

            float content = rows * tileHeight + (rows - 1) * gutter;
            GridPlan plan = new(columns, rows, tileHeight * aspect, tileHeight, gutter, content, content > height + 1e-3f);
            // Bigger tiles win; at equal size (every candidate at the minimum), less to scroll; then
            // fewer columns, by loop order.
            if (best is not GridPlan b || plan.TileHeight > b.TileHeight + 1e-3f
                || (Math.Abs(plan.TileHeight - b.TileHeight) <= 1e-3f && plan.ContentHeight < b.ContentHeight - 1e-3f))
                best = plan;
        }

        // Not even one column fits a tile of the minimum height across: one column, as wide as the box.
        if (best is not GridPlan found) {
            float tileHeight = Math.Max(minTileHeight, 1f);
            float content = tiles * tileHeight + (tiles - 1) * gutter;
            return new GridPlan(1, tiles, width, tileHeight, gutter, content, content > height + 1e-3f);
        }
        return found;
    }
}
