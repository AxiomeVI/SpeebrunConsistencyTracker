#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A rectangle in HUD pixels (1920x1080, y downward): a chart's tile, its plot, a label's box, the
///     window a scrolled wall is seen through.
/// </summary>
internal readonly record struct Area(float X, float Y, float Width, float Height) {
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool IsEmpty => Width <= 0f || Height <= 0f;

    /// <summary>Whether a point is inside: the left and top edges are, the right and bottom are not.</summary>
    public bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>Whether <paramref name="other"/> lies entirely inside this one.</summary>
    public bool Holds(Area other) => other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>The part both share; empty when they do not meet.</summary>
    public Area Intersect(Area other) {
        float left = Math.Max(X, other.X), top = Math.Max(Y, other.Y);
        float right = Math.Min(Right, other.Right), bottom = Math.Min(Bottom, other.Bottom);
        return new Area(left, top, Math.Max(0f, right - left), Math.Max(0f, bottom - top));
    }

    /// <summary>Shrunk by a margin on each side; never below zero size.</summary>
    public Area Inset(float left, float top, float right, float bottom) =>
        new(X + left, Y + top, Math.Max(0f, Width - left - right), Math.Max(0f, Height - top - bottom));

    /// <summary>Shrunk by the same margin on every side.</summary>
    public Area Inset(float all) => Inset(all, all, all, all);

    /// <summary>Moved by (dx, dy).</summary>
    public Area Offset(float dx, float dy) => this with { X = X + dx, Y = Y + dy };
}
