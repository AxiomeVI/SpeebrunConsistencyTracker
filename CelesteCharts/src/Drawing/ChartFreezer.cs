#nullable enable

using Monocle;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Freezes the level while a chart asks it to (<see cref="Request"/>), and unfreezes it when none has
///     for two frames. A mod that lets any chart freeze the game adds one to each level, beside its
///     <see cref="ChartCursor"/>.
/// </summary>
// Level.Frozen: Level.Update keeps running and updates only entities tagged FrozenUpdate, so Madeline,
// the camera and the timer stop, while hotkey polling, the pause menu's own update and these entities
// go on. Engine.FreezeTimer would stop the charts too. Whether this is right in every case (SpeedrunTool's
// timer, save states, CelesteTAS, a button held through the freeze) is checked in the game, not here.
internal sealed class ChartFreezer : Entity {
    public ChartFreezer() {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.FrozenUpdate | Tags.TransitionUpdate;
    }

    /// <summary>A chart wants the level frozen this frame.</summary>
    public static void Request() => FreezeClaim.Shared.Request(GameInput.Frame);

    /// <summary>Whether the level is frozen for charts, by this mod or another.</summary>
    public static bool Holding => FreezeClaim.Shared.Holding;

    public override void Update() {
        base.Update();
        if (Scene is not Level level) return;
        switch (FreezeClaim.Shared.Decide(GameInput.Frame, level.Frozen)) {
            case FreezeAction.Freeze:
                level.Frozen = true;
                break;
            case FreezeAction.Unfreeze:
                level.Frozen = false;
                break;
        }
    }
}
