#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A window onto content longer than it along one axis: a wall of charts taller than the screen, or a row of
///     bands wider than the chart. Fed the <see cref="Pointer"/> each frame, it scrolls on the wheel, on a drag of
///     the content, and on its scrollbar (drag the thumb, or click the track for a page), easing towards where it
///     was sent.
/// </summary>
// Vertical unless built horizontal. Content is built once in its own coordinates, from 0 on the scroll axis, and
// drawn moved by -Offset and cut to the window (GameCanvas.Draw(sketch, 0, -Offset, Window) for a vertical view,
// GameCanvas.Draw(sketch, ScreenX(0), 0, Window) for a horizontal one), so scrolling rebuilds nothing.
// Hit-testing the content converts the mouse with ContentY or ContentX.
//
// A drag of the content captures the pointer, so a drag that starts on a chart scrolls rather than hovering
// whatever passes under the mouse; a click (no drag) is left for the charts. Pure and allocation-free.
internal sealed class ScrollView {
    private float _target;
    private bool _draggingThumb, _draggingContent;
    private float _grabOffset;   // content drag: the offset at the press plus the press's position along the axis
    private float _grabAt;       // thumb drag: where along the thumb it was grabbed

    /// <param name="id">The id this view captures the pointer with while dragging; not 0.</param>
    /// <param name="horizontal">
    ///     Scroll along x: the wheel (its ordinary vertical delta), the content drag, the thumb and the track all work
    ///     on x, and the track covers the window's bottom <see cref="BarWidth"/> pixels, so the window must include them.
    ///     Off, the view scrolls along y and the track covers the window's right edge.
    /// </param>
    public ScrollView(int id, bool horizontal = false) {
        if (id == 0) throw new ArgumentOutOfRangeException(nameof(id), "0 means nothing has captured the pointer");
        Id = id;
        Horizontal = horizontal;
    }

    /// <summary>Whether the view scrolls along x.</summary>
    public bool Horizontal { get; }

    /// <summary>The window's extent along the scroll axis.</summary>
    private float WindowLength => Horizontal ? Window.Width : Window.Height;

    /// <summary>The id this view captures the pointer with while dragging.</summary>
    public int Id { get; }

    /// <summary>Where the content is seen, on screen.</summary>
    public Area Window { get; private set; }

    /// <summary>The content's extent along the scroll axis: its height in a vertical view, its width in a horizontal one.</summary>
    public float ContentLength { get; private set; }

    /// <summary>How far the content is scrolled along the axis: 0 at the start, <see cref="MaxOffset"/> at the end.</summary>
    public float Offset { get; private set; }

    /// <summary>The offset to draw with: whole pixels, so text and one-pixel lines stay crisp.</summary>
    public float DrawOffset => MathF.Round(Offset);

    /// <summary>
    ///     The furthest offset. 0 when the content overflows the window by less than half a pixel: that is rounding in the
    ///     layout (bands whose float widths sum a hair over the window), not something to scroll.
    /// </summary>
    public float MaxOffset => ContentLength - WindowLength < 0.5f ? 0f : ContentLength - WindowLength;

    /// <summary>Whether there is anything to scroll.</summary>
    public bool Scrolls => MaxOffset > 0f;

    /// <summary>Pixels one wheel notch scrolls.</summary>
    public float WheelStep { get; set; } = 120f;

    /// <summary>How quickly the offset reaches its target: the share of the way covered per second, as a rate.</summary>
    public float Easing { get; set; } = 18f;

    /// <summary>The scrollbar's thickness: it sits inside the window's right edge, or its bottom edge in a horizontal view.</summary>
    public float BarWidth { get; set; } = 8f;

    /// <summary>Whether the mouse is over the thumb, or dragging it.</summary>
    public bool ThumbHot { get; private set; }

    /// <summary>Whether the content is being dragged.</summary>
    public bool DraggingContent => _draggingContent;

    /// <summary>
    ///     Sets the window and the content's extent along the scroll axis, keeping the offset inside the new range.
    ///     Call it when either changes (a rebuild), not every frame.
    /// </summary>
    public void Resize(Area window, float contentLength) {
        Window = window;
        ContentLength = Math.Max(0f, contentLength);
        _target = Math.Clamp(_target, 0f, MaxOffset);
        Offset = Math.Clamp(Offset, 0f, MaxOffset);
    }

    /// <summary>The scrollbar's track, inside the window's right edge (its bottom edge, in a horizontal view).</summary>
    public Area Track => Horizontal ? new(Window.X, Window.Bottom - BarWidth, Window.Width, BarWidth)
                                    : new(Window.Right - BarWidth, Window.Y, BarWidth, Window.Height);

    /// <summary>The thumb: its length is the share of the content in view, at least 24 pixels.</summary>
    public Area Thumb {
        get {
            if (!Scrolls) return Track;
            float length = Math.Max(24f, WindowLength * WindowLength / ContentLength);
            float at = (WindowLength - length) * (Offset / MaxOffset);
            return Horizontal ? new Area(Window.X + at, Track.Y, length, BarWidth) : new Area(Track.X, Window.Y + at, BarWidth, length);
        }
    }

    /// <summary>A y on screen, in the content's coordinates. The offset applies in a vertical view only.</summary>
    public float ContentY(float screenY) => screenY - Window.Y + (Horizontal ? 0f : DrawOffset);

    /// <summary>A y in the content, on screen. The offset applies in a vertical view only.</summary>
    public float ScreenY(float contentY) => contentY + Window.Y - (Horizontal ? 0f : DrawOffset);

    /// <summary>An x on screen, in the content's coordinates. The offset applies in a horizontal view only.</summary>
    public float ContentX(float screenX) => screenX - Window.X + (Horizontal ? DrawOffset : 0f);

    /// <summary>An x in the content, on screen. The offset applies in a horizontal view only.</summary>
    public float ScreenX(float contentX) => contentX + Window.X - (Horizontal ? DrawOffset : 0f);

    /// <summary>Scrolls (easing) to <paramref name="offset"/>, kept in range.</summary>
    public void ScrollTo(float offset) => _target = Math.Clamp(offset, 0f, MaxOffset);

    /// <summary>Jumps to <paramref name="offset"/> at once.</summary>
    public void JumpTo(float offset) => Offset = _target = Math.Clamp(offset, 0f, MaxOffset);

    /// <summary>Scrolls just enough that the content from <paramref name="from"/> to <paramref name="to"/> (along the axis) is in view.</summary>
    public void Reveal(float from, float to) {
        if (from < _target) ScrollTo(from);
        else if (to > _target + WindowLength) ScrollTo(to - WindowLength);
    }

    /// <summary>Reads this frame's input and moves the offset.</summary>
    public void Update(Pointer pointer) {
        float seconds = pointer.Input.Seconds;
        bool inside = Window.Contains(pointer.X, pointer.Y);
        Area thumb = Thumb;
        float along = Horizontal ? pointer.X : pointer.Y;

        if (Scrolls && pointer.FreeFor(Id)) {
            if (inside && pointer.Wheel != 0 && !_draggingThumb) _target = Math.Clamp(_target - pointer.Wheel * WheelStep, 0f, MaxOffset);

            if (pointer.PressedIn(thumb) && pointer.Capture(Id)) {
                _draggingThumb = true;
                _grabAt = along - (Horizontal ? thumb.X : thumb.Y);
            } else if (pointer.PressedIn(Track) && pointer.Capture(Id)) {
                // A click on the track pages towards it, as a system scrollbar does.
                _target = Math.Clamp(_target + (along < (Horizontal ? thumb.X : thumb.Y) ? -1 : 1) * WindowLength * 0.9f, 0f, MaxOffset);
            } else if (!_draggingContent && pointer.DraggingFrom(Window) && !Track.Contains(pointer.PressedAt().X, pointer.PressedAt().Y)
                       && pointer.Capture(Id)) {
                // The content follows the mouse from where it was pressed: offset = offset then + press position - mouse position.
                _draggingContent = true;
                _grabOffset = Offset + (Horizontal ? pointer.PressedAt().X : pointer.PressedAt().Y);
            }
        }

        if (_draggingThumb) {
            if (pointer.Held()) {
                float travel = WindowLength - (Horizontal ? thumb.Width : thumb.Height);
                float origin = Horizontal ? Window.X : Window.Y;
                _target = Offset = travel <= 0f ? 0f : Math.Clamp((along - _grabAt - origin) / travel * MaxOffset, 0f, MaxOffset);
            } else {
                _draggingThumb = false;
            }
        }
        if (_draggingContent) {
            if (pointer.Held()) _target = Offset = Math.Clamp(_grabOffset - along, 0f, MaxOffset);
            else _draggingContent = false;
        }

        // Ease towards the target; within half a pixel, land on it.
        if (Offset != _target) {
            Offset += (_target - Offset) * (1f - MathF.Exp(-Easing * seconds));
            if (Math.Abs(_target - Offset) < 0.5f) Offset = _target;
        }

        ThumbHot = Scrolls && (_draggingThumb || thumb.Contains(pointer.X, pointer.Y));
    }
}
