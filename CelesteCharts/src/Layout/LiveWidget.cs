#nullable enable

using System;
using System.Globalization;

namespace Celeste.Mod.CelesteCharts;

/// <summary>The corner or edge a widget is measured from.</summary>
internal enum WidgetAnchor {
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>
///     Where a widget sits: a corner, how far in from it, and its size. Measured from a corner, a widget
///     put in the top right stays in the top right whatever else changes.
/// </summary>
internal readonly record struct WidgetPlacement(WidgetAnchor Anchor, float InX, float InY, float Width, float Height) {
    /// <summary>The widget on <paramref name="screen"/>.</summary>
    public Area Bounds(Area screen) {
        bool right = Anchor is WidgetAnchor.TopRight or WidgetAnchor.BottomRight;
        bool bottom = Anchor is WidgetAnchor.BottomLeft or WidgetAnchor.BottomRight;
        return new Area(right ? screen.Right - InX - Width : screen.X + InX, bottom ? screen.Bottom - InY - Height : screen.Y + InY, Width, Height);
    }

    /// <summary>The placement of an area, measured from the corner of <paramref name="screen"/> nearest its centre.</summary>
    public static WidgetPlacement Nearest(Area bounds, Area screen) {
        bool right = bounds.X + bounds.Width / 2f > screen.X + screen.Width / 2f;
        bool bottom = bounds.Y + bounds.Height / 2f > screen.Y + screen.Height / 2f;
        WidgetAnchor anchor = (right, bottom) switch {
            (false, false) => WidgetAnchor.TopLeft,
            (true, false) => WidgetAnchor.TopRight,
            (false, true) => WidgetAnchor.BottomLeft,
            _ => WidgetAnchor.BottomRight,
        };
        float inX = right ? screen.Right - bounds.Right : bounds.X - screen.X;
        float inY = bottom ? screen.Bottom - bounds.Bottom : bounds.Y - screen.Y;
        return new WidgetPlacement(anchor, MathF.Round(inX), MathF.Round(inY), bounds.Width, bounds.Height);
    }

    /// <summary>One line for a mod's settings: <c>"TopRight 20 20 360 200"</c>.</summary>
    public string ToText() => string.Create(CultureInfo.InvariantCulture, $"{Anchor} {InX:0} {InY:0} {Width:0} {Height:0}");

    /// <summary>A placement read back; false, and <paramref name="placement"/> untouched, when the text is not one.</summary>
    public static bool TryParse(string? text, ref WidgetPlacement placement) {
        string[] parts = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || !Enum.TryParse(parts[0], out WidgetAnchor anchor) || !Enum.IsDefined(anchor)) return false;
        float[] v = new float[4];
        for (int i = 0; i < 4; i++)
            if (!float.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]) || v[i] < 0) return false;
        placement = new WidgetPlacement(anchor, v[0], v[1], v[2], v[3]);
        return true;
    }
}

/// <summary>
///     A chart shrunk into a corner while the player plays: faint until the mouse is on it, pulsing when
///     its data changes, and dragged anywhere to move it — on release it measures itself from the
///     nearest corner, so the layout the player made survives.
/// </summary>
// No frame and no title bar: the whole widget is the handle. It answers the mouse only while the mouse is
// on it, so it asks for the cursor only then. Pure and allocation-free; timings on real seconds.
internal sealed class LiveWidget {
    private float _opacity;
    private float _pulse;
    private long _dataVersion = long.MinValue;
    private bool _dragging;
    private float _grabX, _grabY;
    private Area _dragBounds;
    private WindowZone _dragZone;
    private Area _grabBounds;
    private float _pressX, _pressY;

    public LiveWidget(int captureId, WidgetPlacement placement) {
        if (captureId == 0) throw new ArgumentOutOfRangeException(nameof(captureId), "0 means nothing has captured the pointer");
        CaptureId = captureId;
        Placement = placement;
        _opacity = IdleOpacity;
    }

    public int CaptureId { get; }

    public WidgetPlacement Placement { get; set; }

    public Area Screen { get; set; } = new(0f, 0f, 1920f, 1080f);

    /// <summary>How visible it is with the mouse elsewhere, from 0 to 1.</summary>
    public float IdleOpacity { get; set; } = 0.55f;

    /// <summary>How quickly it fades in and out: the share of the way per second, as a rate.</summary>
    public float FadeRate { get; set; } = 10f;

    /// <summary>How long a pulse lasts, in seconds.</summary>
    public float PulseSeconds { get; set; } = 0.6f;

    /// <summary>Whether its edges and corners resize it. The middle still moves it.</summary>
    public bool Resizable { get; set; }

    /// <summary>How far in from an edge the mouse resizes; corners reach twice as far along each side.</summary>
    public float EdgeWidth { get; set; } = 8f;

    public float MinWidth { get; set; } = 120f;
    public float MinHeight { get; set; } = 60f;
    public float MaxWidth { get; set; } = 960f;
    public float MaxHeight { get; set; } = 540f;

    /// <summary>The part of it under the mouse, or the part held: Body, an edge, a corner, or None.</summary>
    public WindowZone Zone { get; private set; }

    /// <summary>Whether an edge or a corner is being dragged.</summary>
    public bool Resizing => _dragging && _dragZone != WindowZone.Body;

    /// <summary>Where it is this frame: dragged, or its placement on the screen.</summary>
    public Area Bounds => _dragging ? _dragBounds : Placement.Bounds(Screen);

    /// <summary>How visible to draw it, eased between <see cref="IdleOpacity"/> and 1.</summary>
    public float Opacity => _opacity;

    /// <summary>A new-data flash, 1 when the data has just changed and 0 once <see cref="PulseSeconds"/> have passed.</summary>
    public float Pulse => _pulse;

    public bool Hovered { get; private set; }
    /// <summary>Whether it is held: being moved or resized.</summary>
    public bool Dragging => _dragging;

    /// <summary>Whether its placement changed this frame (a drag ended): save it.</summary>
    public bool Placed { get; private set; }

    /// <summary>The cursor while it is hovered or held: the move arrows, or the resize arrows of the edge.</summary>
    public CursorShape Cursor => (_dragging ? _dragZone : Zone) switch {
        WindowZone.Left or WindowZone.Right => CursorShape.ResizeHorizontal,
        WindowZone.Top or WindowZone.Bottom => CursorShape.ResizeVertical,
        WindowZone.TopLeft or WindowZone.BottomRight => CursorShape.ResizeDiagonal,
        WindowZone.TopRight or WindowZone.BottomLeft => CursorShape.ResizeDiagonalUp,
        _ => CursorShape.Move,
    };

    /// <param name="dataVersion">The mod's version of the widget's data; a change starts a pulse (the first one seen does not).</param>
    public void Update(Pointer pointer, long dataVersion) {
        float seconds = pointer.Input.Seconds;
        Placed = false;

        if (dataVersion != _dataVersion) {
            if (_dataVersion != long.MinValue) _pulse = 1f;
            _dataVersion = dataVersion;
        } else if (_pulse > 0f) {
            _pulse = PulseSeconds <= 0f ? 0f : Math.Max(0f, _pulse - seconds / PulseSeconds);
        }

        Area bounds = Bounds;
        Hovered = bounds.Contains(pointer.X, pointer.Y);
        if (!_dragging) Zone = ZoneAt(bounds, pointer.X, pointer.Y);
        if (!_dragging && Hovered && pointer.Pressed() && pointer.Capture(CaptureId)) {
            _dragging = true;
            _dragZone = Zone;
            _dragBounds = _grabBounds = bounds;
            (_grabX, _grabY) = (pointer.X - bounds.X, pointer.Y - bounds.Y);
            (_pressX, _pressY) = (pointer.X, pointer.Y);
        }
        if (_dragging) {
            if (!pointer.Held()) {
                _dragging = false;
                Placement = WidgetPlacement.Nearest(_dragBounds, Screen);
                Placed = true;
            } else if (_dragZone == WindowZone.Body) {
                float x = Math.Clamp(pointer.X - _grabX, Screen.X, Screen.Right - _dragBounds.Width);
                float y = Math.Clamp(pointer.Y - _grabY, Screen.Y, Screen.Bottom - _dragBounds.Height);
                _dragBounds = _dragBounds with { X = MathF.Round(x), Y = MathF.Round(y) };
            } else {
                _dragBounds = Resized(_grabBounds, _dragZone, pointer.X - _pressX, pointer.Y - _pressY);
            }
        }

        float target = Hovered || _dragging ? 1f : IdleOpacity;
        _opacity += (target - _opacity) * (1f - MathF.Exp(-FadeRate * seconds));
        if (Math.Abs(target - _opacity) < 0.005f) _opacity = target;
    }

    // The same zones as a window's (Windows.ZoneAt), less its title bar: the middle is the handle.
    private WindowZone ZoneAt(Area b, float x, float y) {
        if (!b.Contains(x, y)) return WindowZone.None;
        if (!Resizable) return WindowZone.Body;
        float e = EdgeWidth, corner = 2 * EdgeWidth;
        bool left = x < b.X + e, right = x >= b.Right - e, top = y < b.Y + e, bottom = y >= b.Bottom - e;
        bool nearLeft = x < b.X + corner, nearRight = x >= b.Right - corner, nearTop = y < b.Y + corner, nearBottom = y >= b.Bottom - corner;
        if ((top && nearLeft) || (left && nearTop)) return WindowZone.TopLeft;
        if ((top && nearRight) || (right && nearTop)) return WindowZone.TopRight;
        if ((bottom && nearLeft) || (left && nearBottom)) return WindowZone.BottomLeft;
        if ((bottom && nearRight) || (right && nearBottom)) return WindowZone.BottomRight;
        if (left) return WindowZone.Left;
        if (right) return WindowZone.Right;
        if (top) return WindowZone.Top;
        if (bottom) return WindowZone.Bottom;
        return WindowZone.Body;
    }

    // The side held follows the mouse; the opposite side stays. Not Math.Clamp: a placement read from
    // a settings file can be smaller than the minimum, and Clamp throws when its bounds cross.
    private Area Resized(Area from, WindowZone zone, float dx, float dy) {
        static float Within(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));
        bool left = zone is WindowZone.Left or WindowZone.TopLeft or WindowZone.BottomLeft;
        bool right = zone is WindowZone.Right or WindowZone.TopRight or WindowZone.BottomRight;
        bool top = zone is WindowZone.Top or WindowZone.TopLeft or WindowZone.TopRight;
        bool bottom = zone is WindowZone.Bottom or WindowZone.BottomLeft or WindowZone.BottomRight;
        float x1 = from.X, y1 = from.Y, x2 = from.Right, y2 = from.Bottom;
        if (left) x1 = Within(x1 + dx, Math.Max(Screen.X, x2 - MaxWidth), x2 - MinWidth);
        if (right) x2 = Within(x2 + dx, x1 + MinWidth, Math.Min(Screen.Right, x1 + MaxWidth));
        if (top) y1 = Within(y1 + dy, Math.Max(Screen.Y, y2 - MaxHeight), y2 - MinHeight);
        if (bottom) y2 = Within(y2 + dy, y1 + MinHeight, Math.Min(Screen.Bottom, y1 + MaxHeight));
        (x1, y1, x2, y2) = (MathF.Round(x1), MathF.Round(y1), MathF.Round(x2), MathF.Round(y2));
        return new Area(x1, y1, x2 - x1, y2 - y1);
    }
}
