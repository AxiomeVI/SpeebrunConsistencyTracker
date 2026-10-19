using Celeste.Mod.SpeebrunConsistencyTracker.Charts;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

// Four sample gaps in the four delta shades, as the trajectory table prints them: ahead, ahead but
// lost time in the room, behind but gained, behind. Not selectable. The palette is read every
// frame, so the row follows the Ahead and Behind wheels once a colour is confirmed.
internal sealed class DeltaColourPreview : TextMenu.Item
{
    private static readonly long[] Samples = [-12_000_000L, -3_000_000L, 4_000_000L, 15_000_000L];
    private static readonly string[] Texts = [.. System.Array.ConvertAll(Samples, TrajectoryChart.FormatDev)];

    // An option's value scale, so the samples read at the size the colours are chosen next to.
    private const float ValueScale   = 0.8f;
    private const float Gap          = 32f;
    private const float LabelGap     = 40f;
    // ColorSwatchButton's: the last sample ends where the swatches above it end.
    private const float RightPadding = 28f;

    private readonly string _label;

    public DeltaColourPreview(string label)
    {
        _label     = label;
        Selectable = false;
    }

    // The whole row is claimed on the left, though the samples draw at the right edge. A page is as
    // wide as its widest left plus its widest right, and as the widest right this row made the
    // Charts page 1877 px of the screen's 1920; its descriptions keep the left column wide anyway.
    public override float LeftWidth() => ActiveFont.Measure(_label).X + LabelGap + SamplesWidth() + RightPadding;

    private static float SamplesWidth()
    {
        float width = Gap * (Texts.Length - 1);
        foreach (string text in Texts) width += ActiveFont.Measure(text).X * ValueScale;
        return width;
    }

    public override float Height() => ActiveFont.LineHeight;

    public override void Render(Vector2 position, bool highlighted)
    {
        float alpha  = Container.Alpha;
        Color stroke = Color.Black * (alpha * alpha * alpha);
        ActiveFont.DrawOutline(_label, position, new Vector2(0f, 0.5f), Vector2.One, Color.Gray * alpha, 2f, stroke);

        ChartPalette palette = ChartPalette.Current;
        Color[] colours = [palette.Ahead, palette.AheadPale, palette.BehindPale, palette.Behind];
        float x = position.X + Container.Width - RightPadding - SamplesWidth();
        for (int i = 0; i < Texts.Length; i++)
        {
            ActiveFont.DrawOutline(Texts[i], new Vector2(x, position.Y), new Vector2(0f, 0.5f),
                Vector2.One * ValueScale, colours[i] * alpha, 2f, stroke);
            x += ActiveFont.Measure(Texts[i]).X * ValueScale + Gap;
        }
    }
}
