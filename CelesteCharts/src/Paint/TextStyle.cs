#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>How a piece of text is drawn: its size relative to the game's font, its colour, and an optional outline.</summary>
/// <param name="Stroke">Outline width in pixels; 0 for none. An outline costs several extra quads per glyph.</param>
internal readonly record struct TextStyle(float Scale, Rgba Colour, float Stroke = 0f, Rgba StrokeColour = default);

/// <summary>
///     How wide a text is and how tall a line is, at scale 1. The drawing layer gives the game font's
///     (<c>GameCanvas.FontMetrics()</c>); tests give a fixed width per character.
/// </summary>
internal sealed record TextMetrics(Func<string, float> Width, float LineHeight) {
    public float WidthAt(string text, float scale) => Width(text) * scale;
    public float HeightAt(float scale) => LineHeight * scale;
}
