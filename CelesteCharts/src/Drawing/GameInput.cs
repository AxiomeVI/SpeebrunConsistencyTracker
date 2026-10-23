#nullable enable

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     The mouse and modifier keys, read from the game once per frame into this mod's
///     <see cref="Pointer"/>. Call <see cref="Current"/> from anything that answers the mouse.
/// </summary>
// The mouse-to-HUD mapping is SpeebrunConsistencyTracker's: undo the viewport's letterboxing, then scale
// to 1920x1080, the space HUD entities draw in. The wheel is read as a change from the last frame's
// total. Frames are Engine.FrameCounter, which advances on every update, paused or not.
//
// One Pointer per mod (a static here is per mod): each mod sees clicks and drags on its own, and two
// mods read the same mouse the same way.
internal static class GameInput {
    private static readonly Pointer _pointer = new();
    private static long _frame = long.MinValue;
    private static int _wheelTotal;
    private static bool _wheelKnown;

    /// <summary>The game's frame number, for <see cref="CursorClaim"/>.</summary>
    public static long Frame => (long) Engine.FrameCounter;

    /// <summary>This frame's pointer, updated on the first call of the frame.</summary>
    public static Pointer Current() {
        if (_frame != Frame) {
            _frame = Frame;
            _pointer.Update(Read());
        }
        return _pointer;
    }

    /// <summary>Where the mouse is, in HUD pixels, without advancing the pointer: for drawing.</summary>
    // ⚠️ Not Current() from a Render. Current() advances the pointer on the first call of a frame, and on
    // a frame no update ran (a freeze frame: Engine.FrameCounter still advances) a Render's call was the
    // first, consuming that frame's press, release or wheel before the mod's update could read it.
    public static (float X, float Y) Position() => ToHud(Mouse.GetState());

    private static (float X, float Y) ToHud(MouseState mouse) {
        Viewport viewport = Engine.Viewport;
        return ((mouse.X - viewport.X) * (1920f / viewport.Width), (mouse.Y - viewport.Y) * (1080f / viewport.Height));
    }

    // Private: the wheel is read as a change, so a second read in one frame would lose it.
    private static PointerInput Read() {
        MouseState mouse = Mouse.GetState();
        (float x, float y) = ToHud(mouse);

        // 120 per notch; a part of a notch (a smooth-scrolling wheel) is kept for the next frame.
        int wheel = 0;
        if (_wheelKnown) {
            wheel = (mouse.ScrollWheelValue - _wheelTotal) / 120;
            _wheelTotal += wheel * 120;
        } else {
            _wheelTotal = mouse.ScrollWheelValue;
            _wheelKnown = true;
        }

        KeyboardState keys = MInput.Keyboard.CurrentState;
        return new PointerInput(
            x, y,
            mouse.LeftButton == ButtonState.Pressed, mouse.RightButton == ButtonState.Pressed, mouse.MiddleButton == ButtonState.Pressed,
            wheel,
            keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift),
            keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl),
            keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt),
            Engine.RawDeltaTime);
    }
}
