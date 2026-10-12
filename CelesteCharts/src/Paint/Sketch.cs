#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A list of marks to draw, built when the data or the size changes and drawn from every frame.
///     Painters (<see cref="Paint"/>) and a mod's own code add to it; the drawing layer draws it.
/// </summary>
// The buffer is kept across Clear, so a sketch rebuilt on every hover change (a highlight, a tooltip)
// allocates nothing once it has grown to its size. Text is measured as it is added, through the
// metrics given at construction: measuring is the costly part, and a frame never does it.
internal sealed class Sketch {
    private Mark[] _marks = new Mark[32];

    public Sketch(TextMetrics metrics) => Metrics = metrics;

    public TextMetrics Metrics { get; }

    public int Count { get; private set; }

    public ReadOnlySpan<Mark> Marks => new(_marks, 0, Count);

    /// <summary>Empties the sketch and keeps its buffer.</summary>
    public void Clear() => Count = 0;

    public void Add(in Mark mark) {
        if (Count == _marks.Length) Array.Resize(ref _marks, _marks.Length * 2);
        _marks[Count++] = mark;
    }

    /// <summary>Every mark of another sketch, moved by (dx, dy).</summary>
    public void Add(Sketch other, float dx = 0f, float dy = 0f) {
        foreach (Mark mark in other.Marks) Add(mark.Offset(dx, dy));
    }

    public void Fill(Area area, Rgba colour) {
        if (area.IsEmpty || colour.IsInvisible) return;
        Add(new Mark(MarkKind.Fill, area.X, area.Y, area.Right, area.Bottom, colour));
    }

    /// <summary>A rectangle's border, drawn inside it as four fills.</summary>
    public void Outline(Area area, Rgba colour, float thickness = 1f) {
        if (area.IsEmpty) return;
        float t = Math.Min(thickness, Math.Min(area.Width, area.Height) / 2f);
        Fill(area with { Height = t }, colour);
        Fill(new Area(area.X, area.Bottom - t, area.Width, t), colour);
        Fill(new Area(area.X, area.Y + t, t, area.Height - 2 * t), colour);
        Fill(new Area(area.Right - t, area.Y + t, t, area.Height - 2 * t), colour);
    }

    /// <summary>A line of any slope. Horizontal and vertical ones are better as fills, which stay crisp.</summary>
    public void Line(float x1, float y1, float x2, float y2, Rgba colour, float thickness = 1f) {
        if (colour.IsInvisible) return;
        Add(new Mark(MarkKind.Line, x1, y1, x2, y2, colour, thickness));
    }

    /// <summary>
    ///     One line of text anchored at (x, y). <paramref name="alignX"/> and <paramref name="alignY"/>
    ///     say which point of the text sits on the anchor, as the game's justify does: (0, 0) its
    ///     top-left, (0.5, 0.5) its centre, (1, 0) its top-right. Returns the box the text covers.
    /// </summary>
    public Area Text(string text, float x, float y, TextStyle style, float alignX = 0f, float alignY = 0f) {
        float width = Metrics.WidthAt(text, style.Scale), height = Metrics.HeightAt(style.Scale);
        Area box = new(x - alignX * width, y - alignY * height, width, height);
        if (text.Length > 0)
            Add(new Mark(MarkKind.Text, box.X, box.Y, box.Right, box.Bottom, style.Colour,
                         Text: text, Scale: style.Scale, Stroke: style.Stroke, StrokeColour: style.StrokeColour));
        return box;
    }

    /// <summary>How wide a text would be in a style.</summary>
    public float Width(string text, TextStyle style) => Metrics.WidthAt(text, style.Scale);

    /// <summary>How tall a line of text is in a style.</summary>
    public float LineHeight(TextStyle style) => Metrics.HeightAt(style.Scale);
}
