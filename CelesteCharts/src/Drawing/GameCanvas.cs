#nullable enable

using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Draws sketches with the game: Monocle's <c>Draw</c> for fills and lines, <c>ActiveFont</c> for
///     text. Call it from a HUD entity's <c>Render</c>, which draws in 1920x1080 pixels.
/// </summary>
// The one file that names game types, and so the only one tests cannot run: it is compiled against the
// real game by the example's build, and what it draws is checked by a person running the example.
// Everything it draws was decided by pure code; keep it that thin.
//
// ⚠️ Draws only: nothing here measures text or allocates, so it can run every frame.
internal static partial class GameCanvas {
    /// <summary>
    ///     The game font's measurements, for building sketches. Call it when building, not in a static
    ///     initialiser: the fonts are not loaded until the game has started. The same instance comes back until
    ///     the font changes (a language with another font face), so asking every frame allocates nothing.
    /// </summary>
    public static TextMetrics FontMetrics() => _fontMetrics.For(ActiveFont.Font, ActiveFont.BaseSize, MeasureActiveFont);

    /// <summary>
    ///     Moves when the game's font changes. A chart that keeps its sketches across a language change (one held
    ///     past a level) remembers it when it builds and rebuilds, with new sketches, when it moved.
    /// </summary>
    public static int FontVersion {
        get {
            FontMetrics();
            return _fontMetrics.Version;
        }
    }

    private static readonly TextMetricsCache _fontMetrics = new();

    // Measures with the font current when built, so metrics kept past a font change stay what they were.
    private static readonly System.Func<TextMetrics> MeasureActiveFont = () => {
        PixelFontSize size = ActiveFont.FontSize;
        return new TextMetrics(text => size.Measure(text).X, size.LineHeight);
    };

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
    ///     Every mark drawn while this is under 1 has its colours faded to that opacity (0 to 1): a whole chart
    ///     dims without an opacity passed to each <c>Draw</c>. Clamped; NaN reads as 1. Prefer
    ///     <see cref="FadeTo"/>: a value set here stays set, on a throw too, until something sets it back.
    /// </summary>
    public static float Fade {
        get => _fade.Value;
        set => _fade.Value = value;
    }

    /// <summary>
    ///     Fades every mark drawn until the scope is disposed, then puts back the fade that was there:
    ///     <c>using (GameCanvas.FadeTo(0.4f)) { ... }</c>. A throw inside cannot leave later draws faded.
    /// </summary>
    public static FadeScope FadeTo(float opacity) => _fade.To(opacity);

    private static readonly FadeSetting _fade = new();

    public static void Draw(in Mark mark) {
        float fade = _fade.Value;
        if (fade < 1f) {
            if (fade <= 0f) return;
            DrawMark(mark with { Colour = mark.Colour.Times(fade), StrokeColour = mark.StrokeColour.Times(fade) });
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
