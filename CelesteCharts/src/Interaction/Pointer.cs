#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>A mouse button.</summary>
internal enum PointerButton {
    Left,
    Right,
    Middle,
}

/// <summary>
///     One frame of mouse and modifier input, in HUD pixels (1920x1080). The drawing layer reads it
///     from the game (<c>GameInput</c>); tests write it by hand.
/// </summary>
/// <param name="Wheel">Wheel notches this frame: positive away from the player (scroll up).</param>
/// <param name="Seconds">Real time since the last frame, unscaled and unfrozen.</param>
internal readonly record struct PointerInput(
    float X, float Y,
    bool Left = false, bool Right = false, bool Middle = false,
    int Wheel = 0,
    bool Shift = false, bool Ctrl = false, bool Alt = false,
    float Seconds = 1f / 60f) {
    public bool Holds(PointerButton button) => button switch {
        PointerButton.Left => Left,
        PointerButton.Right => Right,
        _ => Middle,
    };
}

/// <summary>
///     The mouse, frame to frame: where it is, which buttons went down or up, clicks and double clicks,
///     drags, the wheel. Fed one <see cref="PointerInput"/> per frame with <see cref="Update"/>, it
///     answers questions about that frame; charts and windows ask it instead of reading the mouse.
/// </summary>
// A press becomes a drag once the mouse has moved DragThreshold pixels from where it went down; a
// release before that is a click. So a slightly shaky click still clicks, and a drag never also clicks.
//
// Capture: whatever handles a press (a scrollbar, a window's title bar) captures the pointer with an id,
// and until the button comes up everything else can see it is taken. That stops a drag that leaves the
// scrollbar from hovering the charts it passes over.
//
// Pure and allocation-free: Update is called every frame.
internal sealed class Pointer {
    /// <summary>How far the mouse must move from a press before it is a drag, in pixels.</summary>
    public float DragThreshold { get; init; } = 4f;

    /// <summary>The longest time between two clicks that makes a double click, in seconds.</summary>
    public float DoubleClickSeconds { get; init; } = 0.35f;

    /// <summary>How far apart two clicks may be and still make a double click, in pixels.</summary>
    public float DoubleClickDistance { get; init; } = 6f;

    private PointerInput _now, _before;
    private bool _started;
    private readonly float[] _pressX = new float[3], _pressY = new float[3];
    private readonly bool[] _dragging = new bool[3], _clicked = new bool[3], _doubleClicked = new bool[3], _dragEnded = new bool[3];
    private readonly float[] _sinceClick = { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
    private readonly float[] _lastClickX = new float[3], _lastClickY = new float[3];

    /// <summary>This frame's input.</summary>
    public PointerInput Input => _now;

    public float X => _now.X;
    public float Y => _now.Y;

    /// <summary>Whether the mouse moved since the last frame.</summary>
    public bool Moved => _started && (_now.X != _before.X || _now.Y != _before.Y);

    /// <summary>Wheel notches this frame; positive scrolls up.</summary>
    public int Wheel => _now.Wheel;

    /// <summary>The id that captured the pointer, or 0 when nothing has.</summary>
    public int CapturedBy { get; private set; }

    /// <summary>How many times <see cref="Update"/> has run: what reads a click remembers it, to answer one click once.</summary>
    // The game can draw several times on one update of its own. Everything drawn then sees the same
    // pointer, a click still showing.
    public long Updates { get; private set; }

    public void Update(in PointerInput input) {
        Updates++;
        _before = _started ? _now : input;
        _now = input;
        _started = true;

        for (int b = 0; b < 3; b++) {
            PointerButton button = (PointerButton) b;
            _clicked[b] = _doubleClicked[b] = _dragEnded[b] = false;
            _sinceClick[b] += input.Seconds;

            if (Pressed(button)) {
                _pressX[b] = input.X;
                _pressY[b] = input.Y;
                _dragging[b] = false;
            } else if (Held(button) && !_dragging[b]) {
                float dx = input.X - _pressX[b], dy = input.Y - _pressY[b];
                _dragging[b] = dx * dx + dy * dy >= DragThreshold * DragThreshold;
            } else if (Released(button)) {
                if (_dragging[b]) {
                    _dragEnded[b] = true;
                } else {
                    _clicked[b] = true;
                    float cx = input.X - _lastClickX[b], cy = input.Y - _lastClickY[b];
                    _doubleClicked[b] = _sinceClick[b] <= DoubleClickSeconds &&
                                        cx * cx + cy * cy <= DoubleClickDistance * DoubleClickDistance;
                    // A double click's second click starts no new pair: a triple click is one double.
                    _sinceClick[b] = _doubleClicked[b] ? float.PositiveInfinity : 0f;
                    _lastClickX[b] = input.X;
                    _lastClickY[b] = input.Y;
                }
            }
        }
        if (CapturedBy != 0 && !_now.Left && !_now.Right && !_now.Middle) CapturedBy = 0;
    }

    /// <summary>Whether the button went down this frame.</summary>
    // On the first frame "before" is the same input, so a button already held then is no press.
    public bool Pressed(PointerButton button = PointerButton.Left) => _now.Holds(button) && !_before.Holds(button);

    /// <summary>Whether the button is down.</summary>
    public bool Held(PointerButton button = PointerButton.Left) => _now.Holds(button);

    /// <summary>Whether the button came up this frame.</summary>
    public bool Released(PointerButton button = PointerButton.Left) => !_now.Holds(button) && _before.Holds(button);

    /// <summary>Whether the button came up this frame without having dragged.</summary>
    public bool Clicked(PointerButton button = PointerButton.Left) => _clicked[(int) button];

    /// <summary>Whether this frame's click was the second of a pair close in time and place.</summary>
    public bool DoubleClicked(PointerButton button = PointerButton.Left) => _doubleClicked[(int) button];

    /// <summary>Whether the button is down and has moved past the drag threshold since it went down.</summary>
    public bool Dragging(PointerButton button = PointerButton.Left) => Held(button) && _dragging[(int) button];

    /// <summary>Whether a drag ended this frame (the button came up after dragging).</summary>
    public bool DragEnded(PointerButton button = PointerButton.Left) => _dragEnded[(int) button];

    /// <summary>Where the button last went down.</summary>
    public (float X, float Y) PressedAt(PointerButton button = PointerButton.Left) => (_pressX[(int) button], _pressY[(int) button]);

    /// <summary>How far the mouse moved this frame.</summary>
    public (float X, float Y) Delta => (_now.X - _before.X, _now.Y - _before.Y);

    /// <summary>Whether the button went down this frame inside <paramref name="area"/>.</summary>
    public bool PressedIn(Area area, PointerButton button = PointerButton.Left) => Pressed(button) && area.Contains(X, Y);

    /// <summary>Whether a drag is under way that started inside <paramref name="area"/>.</summary>
    public bool DraggingFrom(Area area, PointerButton button = PointerButton.Left) {
        (float x, float y) = PressedAt(button);
        return Dragging(button) && area.Contains(x, y);
    }

    /// <summary>
    ///     Takes the pointer for <paramref name="id"/> (not 0) until every button is up. Returns false
    ///     when something else holds it.
    /// </summary>
    public bool Capture(int id) {
        if (id == 0) throw new ArgumentOutOfRangeException(nameof(id), "0 means nothing has captured the pointer");
        if (CapturedBy != 0 && CapturedBy != id) return false;
        CapturedBy = id;
        return true;
    }

    /// <summary>Whether the pointer is free for <paramref name="id"/>: nothing captured it, or <paramref name="id"/> did.</summary>
    public bool FreeFor(int id) => CapturedBy == 0 || CapturedBy == id;
}
