#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>One section of a wall: a checkpoint's name, how many tiles it has, and whether it is folded to its header.</summary>
internal readonly record struct WallSection(string Name, int Tiles, bool Folded = false);

/// <summary>
///     A wall in sections, each under a header a click folds and opens: a chapter's rooms under their
///     checkpoints. Every tile has the shape a <see cref="GridPlan"/> gave (plan it for the whole wall), each
///     section starts a new row, and a folded section is its header alone. In the wall's own coordinates, from
///     (0, 0), for a <see cref="ScrollView"/>.
/// </summary>
// Planned when a section folds or the wall's size changes, then kept; hit-testing reads the plan.
internal sealed class SectionedWall {
    private readonly Area[] _headers;
    private readonly float[] _firstRowTop;
    private readonly int[] _firstTile;

    private SectionedWall(IReadOnlyList<WallSection> sections, GridPlan shape, Area[] headers, float[] firstRowTop, int[] firstTile,
                          float contentHeight) {
        Sections = sections;
        Shape = shape;
        _headers = headers;
        _firstRowTop = firstRowTop;
        _firstTile = firstTile;
        ContentHeight = contentHeight;
    }

    public IReadOnlyList<WallSection> Sections { get; }

    /// <summary>The tiles' shape and the columns: the grid every section is laid out on.</summary>
    public GridPlan Shape { get; }

    /// <summary>The whole wall's height, for a scroll view.</summary>
    public float ContentHeight { get; }

    /// <param name="shape">The tile shape and columns, from <see cref="GridLayout.Plan"/> over every tile of the wall.</param>
    /// <param name="headerHeight">A header's height; the gutter separates it from the tiles above and below.</param>
    public static SectionedWall Plan(IReadOnlyList<WallSection> sections, GridPlan shape, float headerHeight) {
        Area[] headers = new Area[sections.Count];
        float[] firstRowTop = new float[sections.Count];
        int[] firstTile = new int[sections.Count];
        float y = 0f;
        int tile = 0;
        for (int s = 0; s < sections.Count; s++) {
            if (s > 0) y += shape.Gutter;
            headers[s] = new Area(0f, y, shape.ContentWidth, headerHeight);
            y += headerHeight;
            firstRowTop[s] = y + shape.Gutter;
            firstTile[s] = tile;
            tile += Math.Max(0, sections[s].Tiles);
            if (sections[s].Folded || sections[s].Tiles <= 0 || shape.Columns <= 0) continue;
            int rows = (sections[s].Tiles + shape.Columns - 1) / shape.Columns;
            y = firstRowTop[s] + rows * shape.TileHeight + (rows - 1) * shape.Gutter;
        }
        return new SectionedWall(sections, shape, headers, firstRowTop, firstTile, y);
    }

    /// <summary>Section <paramref name="section"/>'s header.</summary>
    public Area Header(int section) => _headers[section];

    /// <summary>Tile <paramref name="index"/> of a section, which must be open.</summary>
    public Area Tile(int section, int index) {
        (float x, float y) = Shape.TileAt(index);
        return new Area(x, _firstRowTop[section] + y, Shape.TileWidth, Shape.TileHeight);
    }

    /// <summary>A tile's number across the whole wall, counting folded sections' tiles too: the mod's index for its data.</summary>
    public int WallIndex(int section, int index) => _firstTile[section] + index;

    /// <summary>The header under a point, or -1.</summary>
    public int HeaderAt(float x, float y) {
        for (int s = 0; s < _headers.Length; s++)
            if (_headers[s].Contains(x, y)) return s;
        return -1;
    }

    /// <summary>The tile under a point, as its section and its index there; (-1, -1) off every open tile.</summary>
    public (int Section, int Index) TileAt(float x, float y) {
        for (int s = 0; s < Sections.Count; s++) {
            if (Sections[s].Folded) continue;
            for (int i = 0; i < Sections[s].Tiles; i++)
                if (Tile(s, i).Contains(x, y)) return (s, i);
        }
        return (-1, -1);
    }
}
