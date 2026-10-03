using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

// A button showing a value on its right, where an option shows its own, without the arrows. The
// value is read at every frame, so it follows the setting with nothing to refresh.
internal sealed class ValueButton : TextMenu.Button
{
    private readonly Func<string> _value;

    public ValueButton(string label, Func<string> value) : base(label)
    {
        _value = value;
    }

    // An option's geometry: the value centred in a column 120 px wider than itself, so this lines
    // up with the On/Off above it.
    public override float RightWidth() => ActiveFont.Measure(_value()).X * 0.8f + 120f;

    public override void Render(Vector2 position, bool highlighted)
    {
        base.Render(position, highlighted);
        float alpha  = Container.Alpha;
        Color color  = Disabled ? Color.DarkSlateGray : (highlighted ? Container.HighlightColor : Color.White) * alpha;
        Color stroke = Color.Black * (alpha * alpha * alpha);
        ActiveFont.DrawOutline(_value(), position + new Vector2(Container.Width - RightWidth() * 0.5f, 0f),
            new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, color, 2f, stroke);
    }
}
