#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A small picture of a vertically scrolled wall beside it (a horizontal <see cref="ScrollView"/> is refused): every tile as a dot of colour, and a frame for the part on
///     screen. A press on it scrolls there, and a drag moves the view with the mouse, for a wall far taller
///     than the screen.
/// </summary>
// The whole content is shrunk by one factor to fit the map's bounds, keeping its shape, and set at its top.
// The tiles' picture is built once (Paint.MiniMap) from the same content areas the wall draws; the frame is
// a few marks redrawn each frame. Pure and allocation-free.
internal sealed class MiniMap {
    private bool _dragging;

    public MiniMap(int captureId) {
        if (captureId == 0) throw new ArgumentOutOfRangeException(nameof(captureId), "0 means nothing has captured the pointer");
        CaptureId = captureId;
    }

    public int CaptureId { get; }

    /// <summary>Where the map is on screen.</summary>
    public Area Bounds { get; private set; }

    /// <summary>The content's left edge, in the coordinates its tiles are given in.</summary>
    public float ContentLeft { get; private set; }

    public float ContentWidth { get; private set; }
    public float ContentHeight { get; private set; }

    /// <summary>How much the content is shrunk on the map.</summary>
    public float Factor { get; private set; }

    public bool Hovered { get; private set; }
    public bool Dragging => _dragging;

    /// <summary>The cursor to ask for while hovered: move while it is dragged, the crosshair otherwise.</summary>
    public CursorShape Cursor => _dragging ? CursorShape.Move : CursorShape.Crosshair;

    public void Resize(Area bounds, float contentLeft, float contentWidth, float contentHeight) {
        Bounds = bounds;
        ContentLeft = contentLeft;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        Factor = contentWidth <= 0f || contentHeight <= 0f ? 0f : Math.Min(bounds.Width / contentWidth, bounds.Height / contentHeight);
    }

    /// <summary>An area of the content (x as the tiles have it, y from the content's top) on the map.</summary>
    public Area ToMap(Area content) =>
        new(Bounds.X + (content.X - ContentLeft) * Factor, Bounds.Y + content.Y * Factor, content.Width * Factor, content.Height * Factor);

    /// <summary>The part of the content on screen, framed on the map.</summary>
    public Area Viewport(ScrollView view) =>
        RequireVertical(view) ? new(Bounds.X, Bounds.Y + view.Offset * Factor, ContentWidth * Factor, Math.Min(view.Window.Height, ContentHeight) * Factor) : default;

    /// <summary>Reads this frame's mouse. <paramref name="view"/> must be vertical: the map shows a tall wall, top to bottom.</summary>
    public void Update(Pointer pointer, ScrollView view) {
        RequireVertical(view);
        Area used = new(Bounds.X, Bounds.Y, ContentWidth * Factor, ContentHeight * Factor);
        Hovered = _dragging || used.Contains(pointer.X, pointer.Y) && pointer.FreeFor(CaptureId);
        if (Factor <= 0f) return;
        if (!_dragging && Hovered && pointer.Pressed() && pointer.Capture(CaptureId)) {
            _dragging = true;
            // A press centres the view on it, easing there.
            view.ScrollTo(Centred(pointer.Y, view));
            return;
        }
        if (!_dragging) return;
        if (!pointer.Held()) {
            _dragging = false;
            return;
        }
        // A drag follows the mouse at once.
        if (pointer.Dragging()) view.JumpTo(Centred(pointer.Y, view));
    }

    private static bool RequireVertical(ScrollView view) =>
        !view.Horizontal ? true : throw new ArgumentException("A mini-map shows a vertical view; this one scrolls along x.", nameof(view));

    private float Centred(float y, ScrollView view) => (y - Bounds.Y) / Factor - view.Window.Height / 2f;
}
