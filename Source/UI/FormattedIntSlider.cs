using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public class FormattedIntSlider(
    string label,
    int min,
    int max,
    int initialValue,
    Func<int, string> valueToString = null) : TextMenuExt.IntSlider(label, min, max, initialValue)
{
    private readonly Func<int, string> valueFormatter = valueToString;
    private readonly int min = min;
    private readonly int max = max;
    private float sine;
    private int lastDir;

    public override void Update()
    {
        base.Update();
        sine += Engine.RawDeltaTime;
    }

    public override void LeftPressed()
    {
        int prev = Index;
        base.LeftPressed();
        if (Index != prev) lastDir = -1;
    }

    public override void RightPressed()
    {
        int prev = Index;
        base.RightPressed();
        if (Index != prev) lastDir = 1;
    }

    public override float RightWidth()
    {
        if (valueFormatter == null) return base.RightWidth();

        float maxValueWidth = Calc.Max(
            0f,
            ActiveFont.Measure(valueFormatter(min)).X,
            ActiveFont.Measure(valueFormatter(max)).X,
            ActiveFont.Measure(valueFormatter(Index)).X);

        return maxValueWidth * 0.8f + 120f;
    }

    public override void Render(Vector2 position, bool highlighted)
    {
        if (valueFormatter == null) { base.Render(position, highlighted); return; }

        float alpha       = Container.Alpha;
        Color strokeColor = Color.Black * (alpha * alpha * alpha);
        Color color       = Disabled
            ? Color.DarkSlateGray
            : ((highlighted ? Container.HighlightColor : Color.White) * alpha);

        ActiveFont.DrawOutline(Label, position, new Vector2(0f, 0.5f), Vector2.One, color, 2f, strokeColor);

        if (max - min > 0)
        {
            float rightWidth    = RightWidth();
            string displayValue = valueFormatter(Index);

            ActiveFont.DrawOutline(
                displayValue,
                position + new Vector2(Container.Width - rightWidth * 0.5f + lastDir * ValueWiggler.Value * 8f, 0f),
                new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, color, 2f, strokeColor);

            Vector2 arrowOffset = Vector2.UnitX * (highlighted ? (float)(Math.Sin(sine * 4.0) * 4.0) : 0f);

            Vector2 leftArrowPos = position + new Vector2(
                Container.Width - rightWidth + 40f + ((lastDir < 0) ? (-ValueWiggler.Value * 8f) : 0f), 0f)
                - ((Index > min) ? arrowOffset : Vector2.Zero);

            ActiveFont.DrawOutline("<", leftArrowPos, new Vector2(0.5f, 0.5f), Vector2.One,
                (Index > min) ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);

            Vector2 rightArrowPos = position + new Vector2(
                Container.Width - 40f + ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f), 0f)
                + ((Index < max) ? arrowOffset : Vector2.Zero);

            ActiveFont.DrawOutline(">", rightArrowPos, new Vector2(0.5f, 0.5f), Vector2.One,
                (Index < max) ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);
        }
    }
}
