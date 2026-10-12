#nullable enable

namespace Celeste.Mod.CelesteCharts;

/// <summary>What a mark draws.</summary>
internal enum MarkKind {
    /// <summary>A filled rectangle, from (X1, Y1) to (X2, Y2).</summary>
    Fill,

    /// <summary>A line from (X1, Y1) to (X2, Y2), <see cref="Mark.Thickness"/> pixels wide.</summary>
    Line,

    /// <summary>One line of text whose box is (X1, Y1) to (X2, Y2), drawn at <see cref="Mark.Scale"/>.</summary>
    Text,
}

/// <summary>
///     One thing to draw, in HUD pixels. Every mark is two points and a colour, so moving or clipping
///     one is the same arithmetic for every kind. Build marks through a <see cref="Sketch"/>.
/// </summary>
// A struct in an array the sketch reuses: drawing a sketch every frame walks memory it already holds.
// A text mark's box is already measured and justified, so the drawing layer draws it at its top-left
// corner and never measures during a frame.
internal readonly record struct Mark(
    MarkKind Kind,
    float X1, float Y1, float X2, float Y2,
    Rgba Colour,
    float Thickness = 1f,
    string? Text = null,
    float Scale = 1f,
    float Stroke = 0f,
    Rgba StrokeColour = default) {
    /// <summary>The rectangle a fill or a text covers, or the box a line's two ends span.</summary>
    public Area Box => new(System.Math.Min(X1, X2), System.Math.Min(Y1, Y2), System.Math.Abs(X2 - X1), System.Math.Abs(Y2 - Y1));

    /// <summary>The same mark moved by (dx, dy).</summary>
    public Mark Offset(float dx, float dy) => this with { X1 = X1 + dx, Y1 = Y1 + dy, X2 = X2 + dx, Y2 = Y2 + dy };
}
