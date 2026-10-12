#nullable enable

using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Draws sketches with the game: Monocle's <c>Draw</c> for fills and lines, <c>ActiveFont</c> for
///     text. Call it from a HUD entity's <c>Render</c>, which draws in 1920x1080 pixels.
/// </summary>
// The one file that names game types, and so the only one tests cannot run: it is compiled against the
// real game by tools/build-example.sh, and what it draws is checked by a person running the example.
// Everything it draws was decided by pure code; keep it that thin.
//
// ⚠️ Draws only: nothing here measures text or allocates, so it can run every frame.
internal static class GameCanvas {
    /// <summary>
    ///     The game font's measurements, for building sketches. Call it when building, not in a static
    ///     initialiser: the fonts are not loaded until the game has started, and a language change swaps them.
    /// </summary>
    public static TextMetrics FontMetrics() => new(text => ActiveFont.Measure(text).X, ActiveFont.LineHeight);

    /// <summary>Every mark of the sketch, where it was built.</summary>
    public static void Draw(Sketch sketch) {
        foreach (Mark mark in sketch.Marks) Draw(mark);
    }

    /// <summary>
    ///     Every mark of the sketch moved by (dx, dy) and cut to <paramref name="window"/>: a scrolled
    ///     sketch, or one seen through a smaller area. Text partly outside the window is not drawn.
    /// </summary>
    public static void Draw(Sketch sketch, float dx, float dy, Area window) {
        foreach (Mark mark in sketch.Marks)
            if (Clip.To(window, mark, dx, dy, out Mark shown)) Draw(shown);
    }

    /// <summary>
    ///     As <see cref="Draw(Sketch, float, float, Area)"/>, every colour faded to
    ///     <paramref name="opacity"/> (0 to 1): a widget drawn faint without rebuilding it.
    /// </summary>
    public static void Draw(Sketch sketch, float dx, float dy, Area window, float opacity) {
        if (opacity >= 1f) {
            Draw(sketch, dx, dy, window);
            return;
        }
        if (opacity <= 0f) return;
        foreach (Mark mark in sketch.Marks)
            if (Clip.To(window, mark, dx, dy, out Mark shown))
                Draw(shown with { Colour = shown.Colour.Times(opacity), StrokeColour = shown.StrokeColour.Times(opacity) });
    }

    /// <summary>
    ///     A sketch of bars drawn growing in (<see cref="GrowIn"/>): each mark pulled towards the plot's
    ///     baseline by how far it has grown, then moved and cut as <see cref="Draw(Sketch, float, float, Area)"/>
    ///     does. Put only what grows in the sketch — the bars — and draw the axes plainly.
    /// </summary>
    public static void DrawGrown(Sketch sketch, GrowIn grow, Area plot, float dx, float dy, Area window) {
        if (grow.Done) {
            Draw(sketch, dx, dy, window);
            return;
        }
        foreach (Mark mark in sketch.Marks)
            if (Clip.To(window, grow.Apply(mark, plot), dx, dy, out Mark shown)) Draw(shown);
    }

    /// <summary>
    ///     Every mark drawn while this is under 1 has its colours faded to that opacity (0 to 1). A caller
    ///     sets it around the draws it wants faint and puts it back to 1: a whole chart dims without an
    ///     opacity passed to each <c>Draw</c>. Values outside 0 to 1 are clamped.
    /// </summary>
    public static float Fade {
        get => _fade;
        set => _fade = value < 0f ? 0f : value > 1f ? 1f : value;
    }

    private static float _fade = 1f;

    public static void Draw(in Mark mark) {
        if (_fade < 1f) {
            if (_fade <= 0f) return;
            DrawMark(mark with { Colour = mark.Colour.Times(_fade), StrokeColour = mark.StrokeColour.Times(_fade) });
            return;
        }
        DrawMark(mark);
    }

    private static void DrawMark(in Mark mark) {
        Color colour = ToColor(mark.Colour);
        switch (mark.Kind) {
            case MarkKind.Fill:
                Monocle.Draw.Rect(mark.X1, mark.Y1, mark.X2 - mark.X1, mark.Y2 - mark.Y1, colour);
                break;
            case MarkKind.Line:
                Monocle.Draw.Line(new Vector2(mark.X1, mark.Y1), new Vector2(mark.X2, mark.Y2), colour, mark.Thickness);
                break;
            case MarkKind.Text when mark.Text != null:
                if (mark.Stroke > 0f)
                    ActiveFont.DrawOutline(mark.Text, new Vector2(mark.X1, mark.Y1), Vector2.Zero, Vector2.One * mark.Scale,
                                           colour, mark.Stroke, ToColor(mark.StrokeColour));
                else
                    ActiveFont.Draw(mark.Text, new Vector2(mark.X1, mark.Y1), Vector2.Zero, Vector2.One * mark.Scale, colour);
                break;
        }
    }

    public static Color ToColor(Rgba colour) => new(colour.R, colour.G, colour.B, colour.A);

    /// <summary>
    ///     A game colour as the library's, for a <see cref="ChartStyle"/> built from what a colour picker
    ///     (CelesteMenuTools' colour wheel, say) hands the mod.
    /// </summary>
    public static Rgba ToRgba(Color colour) => new(colour.R, colour.G, colour.B, colour.A);
}
