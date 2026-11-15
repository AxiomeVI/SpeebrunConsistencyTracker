#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Draws one item of many so that a throw stops that item and not the others: how the chart layer keeps one
///     failing chart from ending the level.
/// </summary>
// The failed flag is the caller's and lives with the item (the chart layer keeps it on a component of the
// entity), so nothing here holds an item or a level alive, and nothing allocates unless an item throws.
internal static class DrawGuard {
    /// <summary>
    ///     Draws <paramref name="item"/> unless <paramref name="failed"/> is set, and says whether it drew. A throw
    ///     sets <paramref name="failed"/> and is handed to <paramref name="report"/>: once, since a failed item is
    ///     never drawn again.
    /// </summary>
    public static bool TryDraw<T>(T item, ref bool failed, Action<T> draw, Action<T, Exception> report) {
        if (failed) return false;
        try {
            draw(item);
            return true;
        } catch (Exception e) {
            failed = true;
            report(item, e);
            return false;
        }
    }
}
