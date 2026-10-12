#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>What the cursor looks like, which says what a press would do.</summary>
// ⚠️ The numbers are shared between mods through CursorClaim: never renumber, only append.
internal enum CursorShape {
    /// <summary>SpeebrunConsistencyTracker's crosshair: pointing at data.</summary>
    Crosshair = 0,

    // 1 is unused: never reuse it. One that arrives through CursorClaim from an older copy draws as the crosshair.

    /// <summary>Over something to drag around: a window's title bar.</summary>
    Move = 2,

    /// <summary>On a corner: resizes both ways.</summary>
    ResizeDiagonal = 3,

    /// <summary>On a left or right edge.</summary>
    ResizeHorizontal = 4,

    /// <summary>On a top or bottom edge.</summary>
    ResizeVertical = 5,

    /// <summary>On a top-right or bottom-left corner: resizes both ways, the other diagonal.</summary>
    ResizeDiagonalUp = 6,
}

/// <summary>The marks of each cursor shape around its hotspot, built from fills and lines so it runs in tests.</summary>
internal static class CursorArt {
    /// <summary>
    ///     Adds <paramref name="shape"/> at (x, y) to <paramref name="sketch"/>, in
    ///     <paramref name="colour"/>, over a one-pixel outline in <paramref name="outline"/> unless that
    ///     is transparent.
    /// </summary>
    // A sketch cleared and refilled each frame allocates nothing once it has grown: about forty marks.
    public static void Paint(Sketch sketch, CursorShape shape, float x, float y, Rgba colour, Rgba outline) {
        x = MathF.Round(x);
        y = MathF.Round(y);
        if (!outline.IsInvisible) Shape(sketch, shape, x, y, outline, grow: 1f);
        Shape(sketch, shape, x, y, colour, grow: 0f);
    }

    private static void Shape(Sketch sketch, CursorShape shape, float x, float y, Rgba c, float grow) {
        switch (shape) {
            case CursorShape.Crosshair:
                // SCT's: arms 8 long and 3 thick, 7 from the centre, and a 3x3 centre.
                const float gap = 7f, arm = 8f;
                Rect(sketch, x - gap - arm, y - 1f, arm, 3f, c, grow);
                Rect(sketch, x + gap + 1f, y - 1f, arm, 3f, c, grow);
                Rect(sketch, x - 1f, y - gap - arm, 3f, arm, c, grow);
                Rect(sketch, x - 1f, y + gap + 1f, 3f, arm, c, grow);
                Rect(sketch, x - 1f, y - 1f, 3f, 3f, c, grow);
                break;
            case CursorShape.Move:
                Rect(sketch, x - 17f, y - 1f, 35f, 3f, c, grow);
                Rect(sketch, x - 1f, y - 17f, 3f, 35f, c, grow);
                Head(sketch, x - 17f, y, -1, 0, c, grow);
                Head(sketch, x + 17f, y, 1, 0, c, grow);
                Head(sketch, x, y - 17f, 0, -1, c, grow);
                Head(sketch, x, y + 17f, 0, 1, c, grow);
                break;
            case CursorShape.ResizeHorizontal:
                Rect(sketch, x - 11f, y - 1f, 23f, 3f, c, grow);
                SmallHead(sketch, x - 11f, y, -1, 0, c, grow);
                SmallHead(sketch, x + 11f, y, 1, 0, c, grow);
                break;
            case CursorShape.ResizeVertical:
                Rect(sketch, x - 1f, y - 11f, 3f, 23f, c, grow);
                SmallHead(sketch, x, y - 11f, 0, -1, c, grow);
                SmallHead(sketch, x, y + 11f, 0, 1, c, grow);
                break;
            case CursorShape.ResizeDiagonal:
                Diagonal(sketch, x, y, 1f, c, grow);
                break;
            case CursorShape.ResizeDiagonalUp:
                Diagonal(sketch, x, y, -1f, c, grow);
                break;
        }
    }

    // A double arrow along a diagonal, top-left to bottom-right (flip 1) or bottom-left to top-right
    // (flip -1), with a head of two short strokes at each end.
    private static void Diagonal(Sketch sketch, float x, float y, float flip, Rgba c, float grow) {
        float t = 3f + 2f * grow;
        sketch.Line(x - 9f, y - 9f * flip, x + 9f, y + 9f * flip, c, t);
        sketch.Line(x - 10f, y - 10f * flip, x - 3f, y - 10f * flip, c, t);
        sketch.Line(x - 10f, y - 10f * flip, x - 10f, y - 3f * flip, c, t);
        sketch.Line(x + 10f, y + 10f * flip, x + 3f, y + 10f * flip, c, t);
        sketch.Line(x + 10f, y + 10f * flip, x + 10f, y + 3f * flip, c, t);
    }

    // An arrowhead pointing (dx, dy) whose tip is at (x, y): three rows narrowing towards the tip. The straight
    // resize arrows keep this one, small on purpose.
    private static void SmallHead(Sketch sketch, float x, float y, int dx, int dy, Rgba c, float grow) {
        for (int row = 0; row < 3; row++) {
            float half = 2f + 2f * row, back = row + 1;
            if (dx != 0) Rect(sketch, x - dx * back, y - half, 1f, 2 * half + 1f, c, grow);
            else Rect(sketch, x - half, y - dy * back, 2 * half + 1f, 1f, c, grow);
        }
    }

    // The move arrow's head: four steps of two pixels, 3 wide at the tip and 15 at the back. With the small
    // head its four arrows read as blobs on the ends of a cross.
    private static void Head(Sketch sketch, float x, float y, int dx, int dy, Rgba c, float grow) {
        for (int step = 0; step < 4; step++) {
            float half = 1f + 2f * step, back = 2f * step;
            if (dx != 0) Rect(sketch, dx > 0 ? x - back - 1f : x + back, y - half, 2f, 2 * half + 1f, c, grow);
            else Rect(sketch, x - half, dy > 0 ? y - back - 1f : y + back, 2 * half + 1f, 2f, c, grow);
        }
    }

    private static void Rect(Sketch sketch, float x, float y, float w, float h, Rgba c, float grow) =>
        sketch.Fill(new Area(x - grow, y - grow, w + 2 * grow, h + 2 * grow), c);
}

/// <summary>
///     Keeps one cursor on screen when several mods show charts at once. Each mod's charts ask for a
///     cursor every frame they want one; each mod's cursor entity draws only if no other has drawn this
///     frame, with the shape the charts asked for.
/// </summary>
// ⚠️ SHIPPED CONTRACT between mods at different commits: the AppContext key and the meaning of each slot
// never change; new slots are only appended, and readers accept a longer array.
//   [0] the frame a cursor was last drawn (it may be drawn again that frame, the same, over later layers)
//   [1] the frame the last request was made
//   [2] the requested CursorShape (as long)
//   [3] the requested colour, packed as R << 24 | G << 16 | B << 8 | A
//   [4] the requested outline colour, packed the same way
//   [5..7] reserved
// A long[] is held in AppContext and written in place, so asking and drawing allocate nothing.
internal sealed class CursorClaim {
    public const string Key = "CelesteCharts.Cursor";
    private const int Slots = 8;

    private readonly long[] _slots;

    public CursorClaim(long[] slots) {
        if (slots.Length < Slots) throw new ArgumentException($"a cursor claim needs {Slots} slots", nameof(slots));
        _slots = slots;
    }

    /// <summary>The claim every mod in this game shares, made by the first one to ask.</summary>
    public static CursorClaim Shared {
        get {
            // Two mods reaching here at once on the main thread cannot happen: rendering is single-threaded.
            if (AppContext.GetData(Key) is not long[] slots || slots.Length < Slots) {
                slots = new long[Slots];
                slots[0] = slots[1] = long.MinValue;
                AppContext.SetData(Key, slots);
            }
            return _shared is { } claim && ReferenceEquals(claim._slots, slots) ? claim : _shared = new CursorClaim(slots);
        }
    }

    private static CursorClaim? _shared;

    /// <summary>
    ///     A chart wants the cursor this frame. Within a frame, a shape other than the crosshair wins over
    ///     the crosshair: whoever is under the mouse and has something to click says so.
    /// </summary>
    public void Request(long frame, CursorShape shape, Rgba colour, Rgba outline) {
        bool first = _slots[1] != frame;
        if (!first && shape == CursorShape.Crosshair && _slots[2] != (long) CursorShape.Crosshair) return;
        _slots[1] = frame;
        _slots[2] = (long) shape;
        _slots[3] = Pack(colour);
        _slots[4] = Pack(outline);
    }

    /// <summary>
    ///     Whether to draw the cursor this frame, and how: true once per frame across every mod, and only
    ///     when a chart asked this frame or the one before (a chart drawn after the cursor asks a frame
    ///     late). <paramref name="again"/> draws even where one was drawn this frame: for a cursor drawn after
    ///     the level, which another mod's charts, drawn after it too, would otherwise cover.
    /// </summary>
    public bool TryDraw(long frame, out CursorShape shape, out Rgba colour, out Rgba outline, bool again = false) {
        shape = CursorShape.Crosshair;
        colour = outline = default;
        if (_slots[0] == frame && !again) return false;
        if (_slots[1] != frame && _slots[1] != frame - 1) return false;
        _slots[0] = frame;
        // A shape a newer mod appended, unknown to this one, draws as the crosshair.
        // So does 1, a shape that was removed.
        shape = _slots[2] is >= 2 and <= (long) CursorShape.ResizeDiagonalUp ? (CursorShape) (int) _slots[2] : CursorShape.Crosshair;
        colour = Unpack(_slots[3]);
        outline = Unpack(_slots[4]);
        return true;
    }

    private static long Pack(Rgba c) => ((long) c.R << 24) | ((long) c.G << 16) | ((long) c.B << 8) | c.A;

    private static Rgba Unpack(long v) => new((byte) (v >> 24), (byte) (v >> 16), (byte) (v >> 8), (byte) v);
}
