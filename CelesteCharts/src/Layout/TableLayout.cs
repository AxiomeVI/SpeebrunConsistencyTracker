#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Where a column's text sits in its cell.</summary>
internal enum ColumnAlign {
    Left,
    Right,
    Centre,
}

/// <summary>
///     One column of a table: its header, how its cells align (numbers right, so their digits line up), and
///     whether it gives up width when the table is too wide — and takes the spare when it is not.
/// </summary>
internal readonly record struct TableColumn(string Header, ColumnAlign Align = ColumnAlign.Left, bool Flexible = false);

/// <summary>
///     A table laid out: each column's place and width, each cell's text fitted to it, and the row heights.
///     Coordinates from the table's top-left corner, so it is drawn anywhere (and scrolled) without being
///     planned again.
/// </summary>
internal sealed class TablePlan {
    internal TablePlan(IReadOnlyList<TableColumn> columns, float[] x, float[] width, string[] headers, string[][] cells,
                       float headerHeight, float rowHeight, float padding) {
        Columns = columns;
        X = x;
        Width = width;
        Headers = headers;
        Cells = cells;
        HeaderHeight = headerHeight;
        RowHeight = rowHeight;
        Padding = padding;
    }

    public IReadOnlyList<TableColumn> Columns { get; }

    /// <summary>Each column's left edge.</summary>
    public IReadOnlyList<float> X { get; }

    /// <summary>Each column's width, padding included.</summary>
    public IReadOnlyList<float> Width { get; }

    /// <summary>The headers, shortened to their columns.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>Every cell's text by row then column, shortened with an ellipsis where it did not fit.</summary>
    public IReadOnlyList<string[]> Cells { get; }

    public float HeaderHeight { get; }
    public float RowHeight { get; }

    /// <summary>Space inside a cell either side of its text.</summary>
    public float Padding { get; }

    public int RowCount => Cells.Count;

    /// <summary>The whole table's width.</summary>
    public float TotalWidth => X.Count == 0 ? 0f : X[^1] + Width[^1];

    /// <summary>The whole table's height, header included: what a scroll view needs.</summary>
    public float ContentHeight => HeaderHeight + RowCount * RowHeight;

    /// <summary>Row <paramref name="row"/>'s top.</summary>
    public float RowTop(int row) => HeaderHeight + row * RowHeight;

    /// <summary>The row at a height from the table's top: -1 in the header or past the last row.</summary>
    public int RowAt(float y) {
        if (y < HeaderHeight) return -1;
        int row = (int) ((y - HeaderHeight) / RowHeight);
        return row < RowCount ? row : -1;
    }

    /// <summary>The column at a distance from the table's left: -1 outside every column.</summary>
    public int ColumnAt(float x) {
        for (int i = 0; i < X.Count; i++)
            if (x >= X[i] && x < X[i] + Width[i]) return i;
        return -1;
    }
}

/// <summary>
///     Lays out a table of text: rows of stats per room, a run's splits. Columns are as wide as their widest
///     text; a table too wide for its box takes the difference from its flexible columns (a room name),
///     whose texts are then shortened with an ellipsis (with none, from its widest column), and a narrower one
///     gives them the spare.
/// </summary>
// Every text is measured once, here: call it when the data or the width changes and keep the plan. The
// texts are the mod's, already formatted — a table writes no number itself (the maths boundary).
internal static class TableLayout {
    /// <param name="rows">Each row's cells, one per column; a missing cell is empty.</param>
    /// <param name="width">The box's width. The table fills it when it has a flexible column, else takes what it needs up to it.</param>
    public static TablePlan Plan(TextMetrics metrics, IReadOnlyList<TableColumn> columns, IReadOnlyList<string[]> rows, float width,
                                 ChartStyle style) {
        int n = columns.Count;
        float pad = style.LabelGap;
        Func<string, float> cellWidth = s => metrics.WidthAt(s, style.Label.Scale);
        Func<string, float> headerWidth = s => metrics.WidthAt(s, style.Header.Scale);

        float[] natural = new float[n];
        for (int c = 0; c < n; c++) {
            float most = headerWidth(columns[c].Header);
            foreach (string[] row in rows)
                if (c < row.Length) most = Math.Max(most, cellWidth(row[c] ?? ""));
            natural[c] = MathF.Ceiling(most) + 2 * pad;
        }

        float[] widths = (float[]) natural.Clone();
        float total = 0f, flexibleTotal = 0f;
        int flexibleCount = 0;
        for (int c = 0; c < n; c++) {
            total += natural[c];
            if (columns[c].Flexible) {
                flexibleTotal += natural[c];
                flexibleCount++;
            }
        }
        // With no flexible column, the widest gives up width when the box is too narrow (a name, more often
        // than not): a table runs out of its box only when not even an ellipsis fits.
        int widest = 0;
        for (int c = 1; c < n; c++)
            if (natural[c] > natural[widest]) widest = c;
        bool[] flexible = new bool[n];
        for (int c = 0; c < n; c++) flexible[c] = columns[c].Flexible || flexibleCount == 0 && c == widest;
        if (flexibleCount == 0 && n > 0) flexibleTotal = natural[widest];

        float spare = width - total;
        if (n > 0 && (spare < 0f || flexibleCount > 0)) {
            // Shared in proportion to the flexible columns' own widths, each keeping room for an ellipsis.
            float least = cellWidth(TextFit.Ellipsis) + 2 * pad;
            for (int c = 0; c < n; c++) {
                if (!flexible[c]) continue;
                float share = flexibleTotal > 0f ? natural[c] / flexibleTotal : 1f;
                widths[c] = MathF.Floor(Math.Max(least, natural[c] + spare * share));
            }
        }

        float[] x = new float[n];
        for (int c = 1; c < n; c++) x[c] = x[c - 1] + widths[c - 1];

        string[] headers = new string[n];
        for (int c = 0; c < n; c++) headers[c] = TextFit.Truncate(columns[c].Header, widths[c] - 2 * pad, headerWidth, keepFrom: "");
        string[][] cells = new string[rows.Count][];
        for (int r = 0; r < rows.Count; r++) {
            cells[r] = new string[n];
            for (int c = 0; c < n; c++) {
                string text = c < rows[r].Length ? rows[r][c] ?? "" : "";
                cells[r][c] = cellWidth(text) <= widths[c] - 2 * pad ? text : TextFit.Truncate(text, widths[c] - 2 * pad, cellWidth, keepFrom: "");
            }
        }

        float line = metrics.HeightAt(style.Label.Scale);
        return new TablePlan(columns, x, widths, headers, cells, MathF.Ceiling(metrics.HeightAt(style.Header.Scale) + pad),
                             MathF.Ceiling(line + pad / 2f), pad);
    }
}
