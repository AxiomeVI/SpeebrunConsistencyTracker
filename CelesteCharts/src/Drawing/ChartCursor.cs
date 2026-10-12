#nullable enable

using Monocle;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Draws the mouse cursor over the HUD while a chart wants one. Celeste hides the system pointer, so
///     a chart that answers the mouse calls <see cref="Request"/> every frame it is on screen; a mod adds
///     one <see cref="ChartCursor"/> to each level beside its charts.
/// </summary>
// Drawn last of the HUD (the lowest depth), so it is over every chart. When several mods show charts, each
// has its own ChartCursor and CursorClaim lets exactly one of them draw per frame, in the shape the charts
// asked for: one cursor on screen, whichever mods are open. It is shown whenever a chart asked, controller
// players included: they point with the mouse like everyone else.
internal sealed class ChartCursor : Entity {
    private readonly Sketch _sketch = new(new TextMetrics(_ => 0f, 0f));

    /// <param name="depth">
    ///     Where the cursor draws: lower than every chart of the mod. A mod that puts its charts at depths of
    ///     its own passes one here too.
    /// </param>
    // One cursor draws per frame across mods (CursorClaim), at the depth of the mod that won the claim. A cursor in
    // the late pass draws again over that one: each mod's late pass is a layer of its own, and the cursor of the
    // mod drawn last is the only one over every chart.
    public ChartCursor(int depth = ChartDepth.Cursor) {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.FrozenUpdate | Tags.TransitionUpdate;
        Depth = depth;
    }

    /// <summary>A chart wants the cursor this frame, in this shape, in the style's cursor colours.</summary>
    public static void Request(CursorShape shape, ChartStyle style) =>
        CursorClaim.Shared.Request(GameInput.Frame, shape, style.Cursor, style.CursorOutline);

    public override void Render() {
        base.Render();
        if (!CursorClaim.Shared.TryDraw(GameInput.Frame, out CursorShape shape, out Rgba colour, out Rgba outline, again: ChartLayer.Drawing)) return;
        Pointer pointer = GameInput.Current();
        _sketch.Clear();
        CursorArt.Paint(_sketch, shape, pointer.X, pointer.Y, colour, outline);
        GameCanvas.Draw(_sketch);
    }
}
