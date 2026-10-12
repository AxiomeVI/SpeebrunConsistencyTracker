#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Which part of a window a point is on.</summary>
internal enum WindowZone {
    None,
    Body,
    TitleBar,
    Close,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,

    /// <summary>The minimise button, left of the close button.</summary>
    Minimize,
}

/// <summary>
///     A chart in a window the player can move by its title bar, resize by its edges and corners, and
///     bring to the front. Its content is built in its own coordinates, from (0, 0) at the body's
///     top-left, and drawn moved to <see cref="Body"/>: moving rebuilds nothing, resizing rebuilds the
///     content for the new size.
/// </summary>
internal sealed class ChartWindow {
    public ChartWindow(int id, string title, Area bounds) {
        Id = id;
        Title = title;
        Bounds = bounds;
    }

    /// <summary>The mod's own number for this window, used in the saved layout. Keep it stable.</summary>
    public int Id { get; }

    public string Title { get; set; }

    /// <summary>The whole window, title bar included, on screen.</summary>
    public Area Bounds { get; internal set; }

    public float MinWidth { get; set; } = 240f;
    public float MinHeight { get; set; } = 160f;
    public bool Movable { get; set; } = true;
    public bool Resizable { get; set; } = true;

    /// <summary>Whether its title bar has a close button; what closing means (hide the chart, say) is the mod's.</summary>
    public bool Closable { get; set; }

    /// <summary>Whether its title bar has a minimise button, which sends it to the dock.</summary>
    public bool Minimizable { get; set; }

    /// <summary>
    ///     Hidden windows are neither drawn nor hit; the mod shows and hides them. A minimised window is
    ///     still visible — it waits in the dock — so check <see cref="Shown"/> to draw.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>Whether it is in the dock: not drawn and not hit until it is restored.</summary>
    public bool Minimized { get; internal set; }

    /// <summary>Whether to draw it and hit-test it: visible and not minimised.</summary>
    public bool Shown => Visible && !Minimized;

    /// <summary>Whether it moved this frame: redraw at the new place, nothing to rebuild.</summary>
    public bool Moved { get; internal set; }

    /// <summary>Whether its size changed this frame: rebuild its content for <see cref="Body"/>'s new size.</summary>
    public bool Resized { get; internal set; }

    /// <summary>Whether its close button was clicked this frame.</summary>
    public bool CloseClicked { get; internal set; }

    /// <summary>The part under the title bar, where its chart goes; set by the manager's title height.</summary>
    public Area Body { get; internal set; }
}

/// <summary>
///     Windows on the HUD, back to front: which one and which part of it the mouse is on, pressing to
///     bring one to the front, dragging its title bar to move it and its edges or corners to resize it,
///     snapping to the screen's edges, to other windows' edges and to a grid, and the cursor shape for
///     all of it.
/// </summary>
// Pure: fed the Pointer each frame, it only changes bounds and flags. Drawing a frame is Paint.Window;
// saving where the windows are is WindowLayout. A drag captures the pointer with this manager's id, so
// whatever lies under a window, or under the mouse mid-drag, does not answer it.
internal sealed class WindowManager {
    private readonly List<ChartWindow> _windows = new();
    private readonly List<ChartWindow> _docked = new();
    private ChartWindow? _dragged;
    private WindowZone _dragZone;
    private Area _dragFrom;
    private float _pressX, _pressY;

    public WindowManager(int captureId) {
        if (captureId == 0) throw new ArgumentOutOfRangeException(nameof(captureId), "0 means nothing has captured the pointer");
        CaptureId = captureId;
    }

    public int CaptureId { get; }

    /// <summary>Where windows may go: the HUD by default.</summary>
    public Area Screen { get; set; } = new(0f, 0f, 1920f, 1080f);

    public float TitleHeight { get; set; } = 30f;

    /// <summary>How far inside a window's border a press resizes it; corners take twice as much.</summary>
    public float EdgeWidth { get; set; } = 6f;

    /// <summary>How close an edge must come to another to jump onto it, in pixels; 0 for no snapping.</summary>
    public float SnapDistance { get; set; } = 12f;

    /// <summary>A grid positions round to when no edge is near; 0 for none.</summary>
    public float Grid { get; set; }

    /// <summary>Back to front: draw in this order.</summary>
    public IReadOnlyList<ChartWindow> BackToFront => _windows;

    /// <summary>Minimised windows, in the order they went to the dock: its buttons, left to right.</summary>
    public IReadOnlyList<ChartWindow> Docked => _docked;

    /// <summary>The dock's height, along the bottom of <see cref="Screen"/>.</summary>
    public float DockHeight { get; set; } = 30f;

    /// <summary>One dock button's width; its title is shortened to it.</summary>
    public float DockButtonWidth { get; set; } = 220f;

    /// <summary>The dock button under the mouse this frame, as an index into <see cref="Docked"/>, or -1.</summary>
    public int HoveredDock { get; private set; } = -1;

    /// <summary>The window and part under the mouse this frame (the front one wins), or (null, None).</summary>
    public (ChartWindow? Window, WindowZone Zone) Hovered { get; private set; }

    /// <summary>Whether a window is being moved or resized.</summary>
    public bool Dragging => _dragged != null;

    /// <summary>The cursor for this frame: move on a title bar, a resize shape on an edge, the crosshair elsewhere.</summary>
    public CursorShape Cursor => _dragged == null && HoveredDock >= 0 ? CursorShape.Crosshair : CursorFor(_dragged != null ? _dragZone : Hovered.Zone);

    public void Add(ChartWindow window) {
        _windows.Remove(window);
        _windows.Add(window);
        window.Body = BodyOf(window.Bounds);
    }

    public void Remove(ChartWindow window) {
        _windows.Remove(window);
        _docked.Remove(window);
        if (_dragged == window) _dragged = null;
    }

    /// <summary>Sends a window to the dock.</summary>
    public void Minimize(ChartWindow window) {
        if (window.Minimized) return;
        if (_dragged == window) _dragged = null;
        window.Minimized = true;
        _docked.Add(window);
    }

    /// <summary>Brings a window back from the dock, in front, where it was.</summary>
    public void Restore(ChartWindow window) {
        if (!window.Minimized) return;
        window.Minimized = false;
        _docked.Remove(window);
        BringToFront(window);
    }

    /// <summary>The dock's button for <see cref="Docked"/>[<paramref name="index"/>], along the bottom of the screen.</summary>
    public Area DockButton(int index) =>
        new(Screen.X + 4f + index * (DockButtonWidth + 4f), Screen.Bottom - DockHeight, DockButtonWidth, DockHeight - 4f);

    public ChartWindow? Find(int id) => _windows.Find(w => w.Id == id);

    public void BringToFront(ChartWindow window) {
        if (_windows.Remove(window)) _windows.Add(window);
    }

    /// <summary>Puts <paramref name="window"/> at <paramref name="bounds"/>, kept to its minimum size and on screen.</summary>
    public void Place(ChartWindow window, Area bounds) {
        Area placed = KeepOnScreen(AtLeast(window, bounds));
        window.Resized |= placed.Width != window.Bounds.Width || placed.Height != window.Bounds.Height;
        window.Moved |= placed.X != window.Bounds.X || placed.Y != window.Bounds.Y;
        window.Bounds = placed;
        window.Body = BodyOf(placed);
    }

    /// <summary>The part of a window a point is on.</summary>
    public WindowZone ZoneAt(ChartWindow window, float x, float y) {
        Area b = window.Bounds;
        if (!window.Shown || !b.Contains(x, y)) return WindowZone.None;
        if (window.Resizable) {
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
        }
        if (y < b.Y + TitleHeight) {
            if (window.Closable && CloseButton(window).Contains(x, y)) return WindowZone.Close;
            if (window.Minimizable && MinimizeButton(window).Contains(x, y)) return WindowZone.Minimize;
            return WindowZone.TitleBar;
        }
        return WindowZone.Body;
    }

    /// <summary>A window's close button: a square at the right of its title bar.</summary>
    public Area CloseButton(ChartWindow window) {
        float size = TitleHeight - 8f;
        return new Area(window.Bounds.Right - size - 4f - EdgeWidth, window.Bounds.Y + 4f, size, size);
    }

    /// <summary>A window's minimise button: left of its close button, or where that would be.</summary>
    public Area MinimizeButton(ChartWindow window) {
        Area close = CloseButton(window);
        return window.Closable ? close.Offset(-close.Width - 4f, 0f) : close;
    }

    public void Update(Pointer pointer) {
        for (int i = 0; i < _windows.Count; i++) _windows[i].Moved = _windows[i].Resized = _windows[i].CloseClicked = false;

        Hovered = (null, WindowZone.None);
        HoveredDock = -1;
        // The dock is over every window.
        for (int i = 0; i < _docked.Count && _dragged == null; i++)
            if (DockButton(i).Contains(pointer.X, pointer.Y)) HoveredDock = i;
        if (HoveredDock >= 0) {
            if (pointer.Clicked() && pointer.FreeFor(CaptureId)) Restore(_docked[HoveredDock]);
            return;
        }
        for (int i = _windows.Count - 1; i >= 0; i--) {
            WindowZone zone = ZoneAt(_windows[i], pointer.X, pointer.Y);
            if (zone == WindowZone.None) continue;
            Hovered = (_windows[i], zone);
            break;
        }

        if (_dragged != null) {
            if (pointer.Held()) Drag(pointer);
            else _dragged = null;
            return;
        }

        (ChartWindow? window, WindowZone hoveredZone) = Hovered;
        if (window == null || !pointer.FreeFor(CaptureId)) return;
        if (pointer.Pressed()) {
            BringToFront(window);
            bool moves = hoveredZone == WindowZone.TitleBar && window.Movable;
            bool resizes = hoveredZone is not (WindowZone.TitleBar or WindowZone.Body or WindowZone.Close or WindowZone.Minimize) && window.Resizable;
            if ((moves || resizes) && pointer.Capture(CaptureId)) {
                _dragged = window;
                _dragZone = hoveredZone;
                _dragFrom = window.Bounds;
                (_pressX, _pressY) = (pointer.X, pointer.Y);
            }
        }
        if (hoveredZone == WindowZone.Close && pointer.Clicked()) window.CloseClicked = true;
        if (hoveredZone == WindowZone.Minimize && pointer.Clicked()) Minimize(window);
    }

    private void Drag(Pointer pointer) {
        ChartWindow w = _dragged!;
        float dx = pointer.X - _pressX, dy = pointer.Y - _pressY;
        Area from = _dragFrom;
        if (_dragZone == WindowZone.TitleBar) {
            Area moved = from.Offset(dx, dy);
            float sx = SnapShift(moved.X, moved.Right, horizontal: true, w), sy = SnapShift(moved.Y, moved.Bottom, horizontal: false, w);
            Place(w, moved.Offset(sx, sy));
            return;
        }

        float left = from.X, top = from.Y, right = from.Right, bottom = from.Bottom;
        if (_dragZone is WindowZone.Left or WindowZone.TopLeft or WindowZone.BottomLeft) left = Snapped(left + dx, true, w);
        if (_dragZone is WindowZone.Right or WindowZone.TopRight or WindowZone.BottomRight) right = Snapped(right + dx, true, w);
        if (_dragZone is WindowZone.Top or WindowZone.TopLeft or WindowZone.TopRight) top = Snapped(top + dy, false, w);
        if (_dragZone is WindowZone.Bottom or WindowZone.BottomLeft or WindowZone.BottomRight) bottom = Snapped(bottom + dy, false, w);
        // A dragged edge stops at the screen's edge rather than pushing the window along.
        left = Math.Max(left, Screen.X);
        top = Math.Max(top, Screen.Y);
        right = Math.Min(right, Screen.Right);
        bottom = Math.Min(bottom, Screen.Bottom);

        // Held to the minimum by the edge that moves, so the other edge stays where it was.
        if (right - left < w.MinWidth) {
            if (left != from.X) left = right - w.MinWidth;
            else right = left + w.MinWidth;
        }
        if (bottom - top < w.MinHeight) {
            if (top != from.Y) top = bottom - w.MinHeight;
            else bottom = top + w.MinHeight;
        }
        Place(w, new Area(left, top, right - left, bottom - top));
    }

    // The smallest shift that lands either edge of a moving window on a snap line; else onto the grid.
    private float SnapShift(float a, float b, bool horizontal, ChartWindow moving) {
        float best = float.PositiveInfinity;
        for (int i = 0; i < LineCount(); i++) {
            if (!Line(i, horizontal, moving, out float line)) continue;
            if (Math.Abs(line - a) < Math.Abs(best)) best = line - a;
            if (Math.Abs(line - b) < Math.Abs(best)) best = line - b;
        }
        if (Math.Abs(best) <= SnapDistance) return best;
        return Grid > 0f ? MathF.Round(a / Grid) * Grid - a : 0f;
    }

    // One moving edge onto the nearest snap line, else the grid.
    private float Snapped(float edge, bool horizontal, ChartWindow moving) {
        float best = float.PositiveInfinity;
        for (int i = 0; i < LineCount(); i++)
            if (Line(i, horizontal, moving, out float line) && Math.Abs(line - edge) < Math.Abs(best - edge)) best = line;
        if (Math.Abs(best - edge) <= SnapDistance) return best;
        return Grid > 0f ? MathF.Round(edge / Grid) * Grid : edge;
    }

    // Snap lines: the screen's edges, and every other shown window's edges, on one axis. Indexed rather than
    // an iterator, which would allocate on every frame of a drag.
    private int LineCount() => SnapDistance <= 0f ? 0 : 2 + 2 * _windows.Count;

    private bool Line(int i, bool horizontal, ChartWindow moving, out float line) {
        if (i < 2) {
            line = horizontal ? (i == 0 ? Screen.X : Screen.Right) : (i == 0 ? Screen.Y : Screen.Bottom);
            return true;
        }
        ChartWindow other = _windows[(i - 2) / 2];
        bool low = i % 2 == 0;
        line = horizontal ? (low ? other.Bounds.X : other.Bounds.Right) : (low ? other.Bounds.Y : other.Bounds.Bottom);
        return other != moving && other.Shown;
    }

    private static Area AtLeast(ChartWindow w, Area a) => a with { Width = Math.Max(a.Width, w.MinWidth), Height = Math.Max(a.Height, w.MinHeight) };

    // A window stays wholly on screen, so its title bar can always be grabbed again.
    private Area KeepOnScreen(Area a) {
        float width = Math.Min(a.Width, Screen.Width), height = Math.Min(a.Height, Screen.Height);
        return new Area(Math.Clamp(a.X, Screen.X, Screen.Right - width), Math.Clamp(a.Y, Screen.Y, Screen.Bottom - height), width, height);
    }

    private Area BodyOf(Area bounds) => bounds.Inset(0f, TitleHeight, 0f, 0f);

    private static CursorShape CursorFor(WindowZone zone) => zone switch {
        WindowZone.TitleBar => CursorShape.Move,
        WindowZone.Left or WindowZone.Right => CursorShape.ResizeHorizontal,
        WindowZone.Top or WindowZone.Bottom => CursorShape.ResizeVertical,
        WindowZone.TopLeft or WindowZone.BottomRight => CursorShape.ResizeDiagonal,
        WindowZone.TopRight or WindowZone.BottomLeft => CursorShape.ResizeDiagonalUp,
        _ => CursorShape.Crosshair,
    };
}

/// <summary>
///     Where a mod's windows are, as one line of text for its settings or save data: each window's id,
///     position and size, back to front, so the order comes back too.
/// </summary>
// "3 100 80 600 400;1 700 80 500 300 m": invariant culture, whole pixels, "m" for a window in the dock (in
// the dock's order). A window missing from the text keeps its place; an id the mod no longer has is
// skipped; text that does not parse changes nothing. Text written before the dock existed still reads.
internal static class WindowLayout {
    public static string Write(WindowManager windows) {
        StringBuilder text = new();
        foreach (ChartWindow w in windows.BackToFront)
            if (!w.Minimized) Append(text, w);
        // Docked windows last, in the dock's order, so they come back in it.
        foreach (ChartWindow w in windows.Docked) {
            Append(text, w);
            text.Append(" m");
        }
        return text.ToString();
    }

    private static void Append(StringBuilder text, ChartWindow w) {
        if (text.Length > 0) text.Append(';');
        text.Append(CultureInfo.InvariantCulture, $"{w.Id} {w.Bounds.X:0} {w.Bounds.Y:0} {w.Bounds.Width:0} {w.Bounds.Height:0}");
    }

    /// <summary>Puts the windows back where the text says. False, and nothing changed, when it does not parse.</summary>
    public static bool Apply(WindowManager windows, string? text) {
        if (string.IsNullOrWhiteSpace(text)) return false;
        List<(int Id, Area Bounds, bool Minimized)> read = new();
        foreach (string entry in text.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            string[] parts = entry.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is not (5 or 6) || parts.Length == 6 && parts[5] != "m") return false;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) return false;
            float[] v = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
            read.Add((id, new Area(v[0], v[1], v[2], v[3]), parts.Length == 6));
        }
        foreach ((int id, Area bounds, bool minimized) in read) {
            if (windows.Find(id) is not { } window) continue;
            windows.Place(window, bounds);
            windows.Restore(window);
            windows.BringToFront(window);
            if (minimized) windows.Minimize(window);
        }
        return true;
    }
}
