#nullable enable

using System;
using System.Collections.Generic;
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
// Call Load from the module's Load and Unload from its Unload; they load and unload GameInput's hook too.
// Hand each chart entity to Add as it goes into a level, the cursor too. An entity given here is made invisible, so the game's HUD pass skips it, and its
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
// Which entities: the ones carrying a mark, read each frame from the level's tracker. No list of entities is
// kept from one frame to the next: a save state replaces a level's entities with copies, tracker included,
// and a kept list would go on drawing the ones that were replaced. Scanning every entity instead cost up to
// half a millisecond a frame per mod in a room of 1,700 entities. Equal depths draw in the order their marks
// joined the level. Where the tracker does not know the mark, every entity is scanned.
//
// In a window under 1920x1080 the charts are drawn at full size into a buffer before the frame renders, and
// the buffer is drawn scaled down after it: what the game does for its HUD. Scaled quad by quad, a line one
// pixel wide falls between the window's pixels and is lost. The render targets and viewport bound before the
// buffer are put back after it, on a throw too.
//
// An entity whose Render throws in this pass is logged once at Warn and hidden for the rest of its life; the
// frame and the level go on. The game's HUD pass has no such guard: while Enabled is false, or the hook could
// not be installed, a throw there ends the level as it always did.
//
// A static here is one per mod, as the hooks and the mark's type are: each mod draws its own entities.
internal static class ChartLayer {
    [Tracked]
    private sealed class Mark : Component {
        public bool Shown = true;

        // Set when its Render threw in this pass. Kept on the entity, so it goes with the entity and holds
        // nothing alive; a save state's copy keeps it.
        public bool Failed;

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

    /// <summary>Installs the pass, and feeds the pointer from the game's update (<see cref="GameInput.Load"/>).</summary>
    public static void Load() {
        GameInput.Load();
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
        GameInput.Unload();
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

    /// <summary>
    ///     Hides or shows an entity given to <see cref="Add"/>, in whichever pass draws it. One whose Render threw in
    ///     the late pass stays hidden.
    /// </summary>
    public static void Show(Entity entity, bool shown) {
        Mark? mark = entity.Get<Mark>();
        if (mark != null) mark.Shown = shown;
        entity.Visible = shown && (mark == null || (!Late && !mark.Failed));
    }

    private static void ShowAll(Scene? scene, bool visible) {
        if (scene == null) return;
        EntityList entities = scene.Entities;
        for (int i = 0; i < entities.Count; i++)
            if (entities[i].Get<Mark>() is { } mark) entities[i].Visible = visible && mark.Shown && !mark.Failed;
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
        List<Mark> marks = Collect(level);
        try {
            for (int i = 0; i < marks.Count; i++) {
                Mark mark = marks[i];
                Entity entity = mark.Entity;
                bool inHud = mark.Shown && !late && !mark.Failed;
                if (entity.Visible != inHud) entity.Visible = inHud;
                if (!late || !mark.Shown || mark.Failed || hudHidden) continue;
                if (!Drawing) {
                    Begin(toBuffer);
                    Drawing = drew = true;
                }
                if (!DrawGuard.TryDraw(entity, ref mark.Failed, RenderEntity, ReportFailure)) Restart(toBuffer);
            }
        } finally {
            try {
                if (Drawing) {
                    Drawing = false;
                    Draw.SpriteBatch.End();
                }
            } finally {
                marks.Clear();
                if (_rebind) Rebind();
            }
        }
        return drew;
    }

    // One pass's marks, highest depth first, as the level draws its entities. Emptied when the pass ends.
    private static readonly List<Mark> Marks = new();

    private static List<Mark> Collect(Level level) {
        Marks.Clear();
        if (level.Tracker.Components.TryGetValue(typeof(Mark), out List<Component>? tracked)) {
            for (int i = 0; i < tracked.Count; i++)
                if (tracked[i] is Mark { Entity: not null } mark) Marks.Add(mark);
            // Insertion sort: stable, so equal depths keep the tracker's order, and nothing allocates.
            for (int i = 1; i < Marks.Count; i++) {
                Mark mark = Marks[i];
                int depth = mark.Entity.Depth, j = i - 1;
                for (; j >= 0 && Marks[j].Entity.Depth < depth; j--) Marks[j + 1] = Marks[j];
                Marks[j + 1] = mark;
            }
        } else {
            EntityList entities = level.Entities;
            for (int i = 0; i < entities.Count; i++)
                if (entities[i].Get<Mark>() is { } mark) Marks.Add(mark);
        }
        return Marks;
    }

    // The HUD's own batch: 1920x1080, scaled to the window, or as it is into the buffer.
    private static void Begin(bool toBuffer) {
        if (toBuffer) {
            GraphicsDevice device = Engine.Graphics.GraphicsDevice;
            int count = device.GetRenderTargetsNoAllocEXT(null);
            if (_bound.Length != count) _bound = new RenderTargetBinding[count];
            device.GetRenderTargetsNoAllocEXT(_bound);
            _viewport = device.Viewport;
            _rebind = true;
            _buffer ??= VirtualContent.CreateRenderTarget("celestecharts-late/" + typeof(ChartLayer).Assembly.GetName().Name, 1920, 1080);
            device.SetRenderTarget(_buffer.Target);
            device.Clear(Color.Transparent);
        }
        BeginBatch(toBuffer);
    }

    // The buffered pass runs before the frame, inside the engine's render: the scene's BeforeRender and other
    // mods' hooks on it come next, and some draw to whatever is bound. Put back what was bound before, whether
    // the pass ended or threw.
    private static RenderTargetBinding[] _bound = Array.Empty<RenderTargetBinding>();
    private static Viewport _viewport;
    private static bool _rebind;

    private static void Rebind() {
        _rebind = false;
        GraphicsDevice device = Engine.Graphics.GraphicsDevice;
        device.SetRenderTargets(_bound.Length == 0 ? null : _bound);
        device.Viewport = _viewport;
        // Holds no render target alive until the next pass.
        Array.Clear(_bound);
    }

    // Fields, so the pass hands the guard the same two delegates every frame.
    private static readonly Action<Entity> RenderEntity = entity => entity.Render();

    private static readonly Action<Entity, Exception> ReportFailure = (entity, e) =>
        Logger.Log(LogLevel.Warn, "CelesteCharts",
                   $"{typeof(ChartLayer).Assembly.GetName().Name}: {entity.GetType().FullName} threw while drawing and is hidden from now on.\n{e}");

    // A Render that threw may have ended the batch, begun one of its own, set another render target, or left
    // GameCanvas.Fade set. What it drew before the throw is flushed with this frame.
    private static void Restart(bool toBuffer) {
        GameCanvas.Fade = 1f;
        try {
            Draw.SpriteBatch.End();
        } catch (InvalidOperationException) {
            // It had ended the batch already.
        }
        RenderTarget2D? target = toBuffer ? _buffer!.Target : null;
        RenderTargetBinding[] bound = Engine.Graphics.GraphicsDevice.GetRenderTargets();
        // Only when it changed: setting the buffer again could lose what the entities before drew into it.
        if (bound.Length != (target == null ? 0 : 1) || (target != null && bound[0].RenderTarget != target))
            Engine.Graphics.GraphicsDevice.SetRenderTarget(target);
        BeginBatch(toBuffer);
    }

    private static void BeginBatch(bool toBuffer) {
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None,
                               RasterizerState.CullNone, null, toBuffer ? Matrix.Identity : Engine.ScreenMatrix);
    }
}
