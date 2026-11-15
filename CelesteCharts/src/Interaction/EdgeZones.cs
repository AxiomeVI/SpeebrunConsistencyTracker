#nullable enable

namespace Celeste.Mod.CelesteCharts;

/// <summary>Which part of a window a point is on.</summary>
internal enum WindowZone {
    None,
    Body,
    TitleBar,
    Close,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,

    /// <summary>The minimise button, left of the close button.</summary>
    Minimize,
}

/// <summary>The edge and corner zones a resizable rectangle shares between windows and live widgets.</summary>
internal static class EdgeZones {
    /// <summary>
    ///     The edge or corner a point inside <paramref name="b"/> is on, or <see cref="WindowZone.Body"/> when it is on
    ///     neither. An edge is <paramref name="edge"/> deep; a corner reaches twice as far along each side.
    /// </summary>
    public static WindowZone At(Area b, float x, float y, float edge) {
        float corner = 2 * edge;
        bool left = x < b.X + edge, right = x >= b.Right - edge, top = y < b.Y + edge, bottom = y >= b.Bottom - edge;
        bool nearLeft = x < b.X + corner, nearRight = x >= b.Right - corner, nearTop = y < b.Y + corner, nearBottom = y >= b.Bottom - corner;
        if ((top && nearLeft) || (left && nearTop)) return WindowZone.TopLeft;
        if ((top && nearRight) || (right && nearTop)) return WindowZone.TopRight;
        if ((bottom && nearLeft) || (left && nearBottom)) return WindowZone.BottomLeft;
        if ((bottom && nearRight) || (right && nearBottom)) return WindowZone.BottomRight;
        if (left) return WindowZone.Left;
        if (right) return WindowZone.Right;
        if (top) return WindowZone.Top;
        if (bottom) return WindowZone.Bottom;
        return WindowZone.Body;
    }

    /// <summary>Which sides a drag on <paramref name="zone"/> moves. All false for a zone that is not an edge or corner.</summary>
    public static (bool Left, bool Right, bool Top, bool Bottom) Sides(WindowZone zone) => (
        zone is WindowZone.Left or WindowZone.TopLeft or WindowZone.BottomLeft,
        zone is WindowZone.Right or WindowZone.TopRight or WindowZone.BottomRight,
        zone is WindowZone.Top or WindowZone.TopLeft or WindowZone.TopRight,
        zone is WindowZone.Bottom or WindowZone.BottomLeft or WindowZone.BottomRight);
}
