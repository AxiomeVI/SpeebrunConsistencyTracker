#nullable enable

using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Draws a mod's chart entities after the game has drawn everything else, other mods' overlays included.
///     A depth only orders the game's own HUD pass, and many mods draw from a hook on <c>Level.Render</c> or on
///     the engine's own render, once that pass is over: nothing an entity sets puts it in front of those.
/// </summary>
// Call Load from the module's Load and Unload from its Unload. Hand each chart entity to Add as it goes into a
// level, the cursor too. An entity given here is made invisible, so the game's HUD pass skips it, and its
// Render is called from this pass instead, in depth order. Its Update and its tags are unchanged.
//
// Visible belongs to this class from then on: it is written here and not read. Hide an entity with Show,
// which works in either pass.
//
// The hook is on the engine's render (Engine.RenderCore), which holds the whole frame, and asks for the
// outermost place there: what it draws afterwards comes after what every hook on the level and on the engine
// drew. Another mod that asks for the same place can still be in front; so is the debug console.
//
// What it costs: a chart drawn here is over the pause menu and over screen wipes. Set Enabled to false while
// a menu must be read; the entities then draw in the HUD pass again, at their depths. While the player holds
// the button that hides the HUD in the pause menu, this pass draws nothing either.
//
// Which entities: the ones carrying a mark, found in the level each frame. No list is kept. A save state
// replaces a level's entities with copies, and a list would go on drawing the ones that were replaced.
//
// In a window under 1920x1080 the charts are drawn at full size into a buffer before the frame renders, and
// the buffer is drawn scaled down after it: what the game does for its HUD. Scaled quad by quad, a line one
// pixel wide falls between the window's pixels and is lost.
//
// A static here is one per mod, as the hooks and the mark's type are: each mod draws its own entities.
internal static class ChartLayer {
    private sealed class Mark : Component {
        public bool Shown = true;

        public Mark() : base(active: false, visible: false) { }
    }

    private static Hook? _hook;
    private static VirtualRenderTarget? _buffer;
    private static bool _enabled = true;

    /// <summary>Whether the pass is installed: false until Load, and on a build of the game where it could not be.</summary>
    public static bool Loaded => _hook != null;

    /// <summary>Whether the pass is drawing now: a cursor drawn in it draws over one already drawn this frame.</summary>
    public static bool Drawing { get; private set; }

    /// <summary>False puts the entities back in the game's HUD pass, under whatever is in front of their depth.</summary>
    public static bool Enabled {
        get => _enabled;
        set {
            if (_enabled == value) return;
            _enabled = value;
            ShowAll(Engine.Scene, !Late);
        }
    }

    private static bool Late => _enabled && _hook != null;

    public static void Load() {
        if (_hook != null) return;
        // Protected, so by name: reflection needs no access the mod's build does not have.
        MethodInfo? render = typeof(Engine).GetMethod("RenderCore", BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes);
        if (render == null) {
            Logger.Log(LogLevel.Warn, "CelesteCharts", "Engine.RenderCore() not found: charts draw in the HUD pass, under other mods' overlays.");
            return;
        }
        // Highest priority is outermost: this hook's code after the frame runs after every other hook's.
        string id = "CelesteCharts.Layer/" + typeof(ChartLayer).Assembly.GetName().Name;
        _hook = new Hook(render, (Action<Action<Engine>, Engine>) AroundFrame, new DetourConfig(id, priority: int.MaxValue, before: null, after: null, subPriority: int.MaxValue));
        ShowAll(Engine.Scene, !Late);
    }

    public static void Unload() {
        _hook?.Dispose();
        _hook = null;
        _buffer?.Dispose();
        _buffer = null;
        ShowAll(Engine.Scene, true);
    }

    /// <summary>Draws this entity in the late pass from now on. Returns it, to pass straight to <c>Scene.Add</c>.</summary>
    public static T Add<T>(T entity) where T : Entity {
        if (entity.Get<Mark>() == null) entity.Add(new Mark());
        entity.Visible = !Late;
        return entity;
    }

    /// <summary>Hides or shows an entity given to <see cref="Add"/>, in whichever pass draws it.</summary>
    public static void Show(Entity entity, bool shown) {
        Mark? mark = entity.Get<Mark>();
        if (mark != null) mark.Shown = shown;
        entity.Visible = shown && (mark == null || !Late);
    }

    private static void ShowAll(Scene? scene, bool visible) {
        if (scene == null) return;
        EntityList entities = scene.Entities;
        for (int i = 0; i < entities.Count; i++)
            if (entities[i].Get<Mark>() is { } mark) entities[i].Visible = visible && mark.Shown;
    }

    private static void AroundFrame(Action<Engine> orig, Engine engine) {
        if (Engine.Scene is not Level level) {
            orig(engine);
            return;
        }
        // The buffer first: the frame ends on the screen, and a render target set after it would lose it.
        bool buffered = Late && (Engine.ViewWidth < 1920 || Engine.ViewHeight < 1080) && Pass(level, toBuffer: true);
        orig(engine);
        if (!buffered) {
            Pass(level, toBuffer: false);
            return;
        }
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None,
                               RasterizerState.CullNone, null, Engine.ScreenMatrix);
        Draw.SpriteBatch.Draw(_buffer!.Target, Vector2.Zero, Color.White);
        Draw.SpriteBatch.End();
    }

    // Draws the marked entities, and says whether there were any to draw.
    private static bool Pass(Level level, bool toBuffer) {
        // The level keeps its entities highest depth first, as it draws them. An entity that joined since
        // Enabled last changed (it was added while waiting to enter the level) is put right here.
        bool late = Late, drew = false;
        // The game's own test for hiding its HUD, less the second it lingers after a pause ends.
        bool hudHidden = level.Paused && level.PauseMainMenuOpen && level.AllowHudHide && Input.MenuJournal?.Check == true;
        EntityList entities = level.Entities;
        try {
            for (int i = 0; i < entities.Count; i++) {
                Entity entity = entities[i];
                if (entity.Get<Mark>() is not { } mark) continue;
                bool inHud = mark.Shown && !late;
                if (entity.Visible != inHud) entity.Visible = inHud;
                if (!late || !mark.Shown || hudHidden) continue;
                if (!Drawing) {
                    Begin(toBuffer);
                    Drawing = drew = true;
                }
                entity.Render();
            }
        } finally {
            if (Drawing) {
                Drawing = false;
                Draw.SpriteBatch.End();
            }
        }
        return drew;
    }

    // The HUD's own batch: 1920x1080, scaled to the window, or as it is into the buffer.
    private static void Begin(bool toBuffer) {
        if (toBuffer) {
            _buffer ??= VirtualContent.CreateRenderTarget("celestecharts-late/" + typeof(ChartLayer).Assembly.GetName().Name, 1920, 1080);
            Engine.Graphics.GraphicsDevice.SetRenderTarget(_buffer.Target);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
        }
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None,
                               RasterizerState.CullNone, null, toBuffer ? Matrix.Identity : Engine.ScreenMatrix);
    }
}
