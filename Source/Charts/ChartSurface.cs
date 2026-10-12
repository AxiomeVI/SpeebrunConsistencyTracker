using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// What draws the chart that is showing. An entity, so CelesteCharts' late pass can draw it after
// the level and over other mods' overlays; it holds nothing, so a save state that copies a level's
// entities copies nothing of the charts.
internal sealed class ChartSurface : Entity
{
    public ChartSurface()
    {
        Tag   = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.FrozenUpdate;
        Depth = ChartDepth.Surface;
    }

    public override void Render()
    {
        if (!SpeebrunConsistencyTrackerModule.Settings.Enabled || SessionManager.CurrentSession == null) return;
        // While the widget is being placed the chart is not drawn, so the widget reads as it will
        // over the game. The widget's controls are: TrajectoryChart draws them at full strength.
        GameCanvas.Fade = TrajectoryWidget.ChartFade;
        GraphManager.Render();
        GameCanvas.Fade = 1f;
        // After the chart: the widget lies over its panel.
        TrajectoryWidget.Render();
    }
}
