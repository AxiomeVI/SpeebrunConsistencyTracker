#nullable enable

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Where a mod's chart entities sit among the HUD's unless the mod decides otherwise: in front of nearly
///     everything, in a fixed order among themselves. A lower number draws later, so nearer the player.
/// </summary>
// These are defaults, not rules. The entities are the mod's, so it gives each any Depth it likes; the one
// entity this library owns that draws, ChartCursor, takes its depth in its constructor. A mod that overrides
// keeps the order: surfaces, then what stays over them, then the cursor.
//
// Celeste's own HUD goes down to -100,000 (its overlays; the speedrun timer is at -100, a menu at 0), so a
// chart is over all of it, the pause menu and Mod Options included: a mod hides its chart when a menu must
// show. Only the save icon and the cursor, at -1,000,000, are in front of a chart.
internal static class ChartDepth {
    /// <summary>A chart, a panel of charts, a window.</summary>
    public const int Surface = -900_000;

    /// <summary>What stays over every surface: page tabs, a control fixed to the screen.</summary>
    public const int Control = -900_100;

    /// <summary>The mouse cursor, over everything a chart draws.</summary>
    public const int Cursor = -1_000_000;
}
