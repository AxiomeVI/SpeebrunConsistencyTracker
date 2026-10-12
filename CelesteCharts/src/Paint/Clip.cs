#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Cuts marks to a window, for a sketch seen through a smaller area: a scrolled wall, a chart in a
///     box. Pure arithmetic on the marks, run as they are drawn, so scrolling rebuilds nothing.
/// </summary>
// No scissor rectangle: that would mean ending and restarting the game's sprite batch, whose settings
// belong to whoever is rendering the HUD. Fills and lines are cut exactly; text is drawn only when its
// whole box is inside, because a glyph cannot be cut without one.
internal static class Clip {
    /// <summary>
    ///     The part of <paramref name="mark"/>, moved by (dx, dy), that lies inside
    ///     <paramref name="window"/>. False when none of it does.
    /// </summary>
    // A thick line is cut along its centre, so it may reach up to half its thickness past the window.
    public static bool To(Area window, in Mark mark, float dx, float dy, out Mark shown) {
        shown = mark.Offset(dx, dy);
        switch (mark.Kind) {
            case MarkKind.Fill: {
                Area cut = shown.Box.Intersect(window);
                if (cut.IsEmpty) return false;
                shown = shown with { X1 = cut.X, Y1 = cut.Y, X2 = cut.Right, Y2 = cut.Bottom };
                return true;
            }
            case MarkKind.Text:
                return window.Holds(shown.Box);
            case MarkKind.Line:
                return Segment(window, ref shown);
            default:
                return false;
        }
    }

    // Liang-Barsky: the segment as start + t * (end - start), t in [0, 1], narrowed by each edge.
    private static bool Segment(Area window, ref Mark line) {
        float x = line.X1, y = line.Y1, dx = line.X2 - x, dy = line.Y2 - y;
        float enter = 0f, leave = 1f;
        if (!Edge(-dx, x - window.X, ref enter, ref leave)) return false;
        if (!Edge(dx, window.Right - x, ref enter, ref leave)) return false;
        if (!Edge(-dy, y - window.Y, ref enter, ref leave)) return false;
        if (!Edge(dy, window.Bottom - y, ref enter, ref leave)) return false;
        line = line with { X1 = x + enter * dx, Y1 = y + enter * dy, X2 = x + leave * dx, Y2 = y + leave * dy };
        return true;
    }

    private static bool Edge(float p, float q, ref float enter, ref float leave) {
        if (p == 0f) return q >= 0f;   // parallel to this edge: inside it or never
        float t = q / p;
        if (p < 0f) {
            if (t > leave) return false;
            enter = Math.Max(enter, t);
        } else {
            if (t < enter) return false;
            leave = Math.Min(leave, t);
        }
        return true;
    }
}
