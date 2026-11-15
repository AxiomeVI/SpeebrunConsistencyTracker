#nullable enable

using System;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     The mouse and modifier keys, read from the game once per update into this mod's
///     <see cref="Pointer"/>. Call <see cref="Current"/> from anything that answers the mouse.
/// </summary>
// The mouse-to-HUD mapping is SpeebrunConsistencyTracker's: undo the viewport's letterboxing, then scale
// to 1920x1080, the space HUD entities draw in. Frames are Engine.FrameCounter.
//
// Fed from a hook on Scene.BeforeUpdate, installed by Load (ChartLayer.Load calls it): the engine calls it
// before every scene update, in any scene, paused, frozen or in a room transition, and also while SpeedrunTool
// holds the level after a load state. It is not called on a freeze frame (Engine.FreezeTimer), where no entity
// updates either: that frame's press or release is seen on the next update instead of consumed unread.
//
// One Pointer and one hook per mod (a static here is per mod): each mod sees clicks and drags on its own,
// and two mods read the same mouse the same way.
//
// While the game ignores its own input (the debug console open, the window not active), the pointer gets no
// button, modifier or wheel: a chart takes no click the game would not. The mouse still moves it.
internal static class GameInput {
    private static readonly PointerFeed _feed = new();
    private static Hook? _hook;

    /// <summary>The game's frame number, for <see cref="CursorClaim"/>.</summary>
    public static long Frame => (long) Engine.FrameCounter;

    /// <summary>Whether the pointer is fed from the game's update: false until Load.</summary>
    public static bool Loaded => _hook != null;

    /// <summary>This update's pointer. Reading it advances nothing, from an update or a render.</summary>
    // ⚠️ Without Load, the first call of a frame feeds it, from wherever that call is: the old way, kept so a
    // mod that never loads still answers the mouse. Called from a Render then, a frame drawn without an
    // update consumes a click before any update reads it.
    public static Pointer Current() {
        if (_hook == null) Feed();
        return _feed.Pointer;
    }

    /// <summary>Where the mouse is, in HUD pixels, read now: for drawing the cursor where it is this instant.</summary>
    public static (float X, float Y) Position() => ToHud(Mouse.GetState());

    public static void Load() {
        if (_hook != null) return;
        MethodInfo? before = typeof(Scene).GetMethod(nameof(Scene.BeforeUpdate), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (before == null) {
            Logger.Log(LogLevel.Warn, "CelesteCharts", "Scene.BeforeUpdate() not found: the pointer advances on its first read of a frame.");
            return;
        }
        _hook = new Hook(before, (Action<Action<Scene>, Scene>) BeforeUpdate, new DetourConfig("CelesteCharts.Input/" + typeof(GameInput).Assembly.GetName().Name));
    }

    public static void Unload() {
        _hook?.Dispose();
        _hook = null;
    }

    private static void BeforeUpdate(Action<Scene> orig, Scene scene) {
        Feed();
        orig(scene);
    }

    private static void Feed() {
        long frame = Frame;
        if (!_feed.Due(frame)) return;
        MouseState mouse = Mouse.GetState();
        (float x, float y) = ToHud(mouse);
        if (Blanked) {
            _feed.Blank(frame, new PointerInput(x, y, Seconds: Engine.RawDeltaTime), mouse.ScrollWheelValue);
            return;
        }
        KeyboardState keys = MInput.Keyboard.CurrentState;
        _feed.Feed(frame, new PointerInput(
            x, y,
            mouse.LeftButton == ButtonState.Pressed, mouse.RightButton == ButtonState.Pressed, mouse.MiddleButton == ButtonState.Pressed,
            0,
            keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift),
            keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl),
            keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt),
            Engine.RawDeltaTime), mouse.ScrollWheelValue);
    }

    // MInput.Update's own test: the game reads no mouse or keyboard while its window is not active, input is
    // switched off, or the debug console is open. The mouse is read from the device, which knows none of that.
    private static bool Blanked => !Engine.Instance.IsActive || !MInput.Active || Engine.Commands?.Open == true;

    private static (float X, float Y) ToHud(MouseState mouse) {
        Viewport viewport = Engine.Viewport;
        return ((mouse.X - viewport.X) * (1920f / viewport.Width), (mouse.Y - viewport.Y) * (1080f / viewport.Height));
    }
}
