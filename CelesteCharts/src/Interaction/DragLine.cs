#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A reference line the player drags to set its value: a target time, a mute divider. It
///     knows pixels only; the mod turns <see cref="DragPixel"/> into a value (<c>Scale.ValueAt</c>, then
///     its own snap: a whole frame, a band edge), draws the line at that value, and saves it when
///     <see cref="Released"/> says the drag is over.
/// </summary>
// A line is one pixel wide, so it is grabbed within GrabDistance of it. A press there captures the
// pointer, so the drag keeps the line wherever the mouse goes; the line stays inside the plot. A right
// click during the drag cancels it (Cancelled), and the mod goes back to the value it had.
//
// Not Escape to cancel: it is Celeste's pause key. Pure and allocation-free.
internal sealed class DragLine {
    public DragLine(int captureId, bool vertical) {
        if (captureId == 0) throw new ArgumentOutOfRangeException(nameof(captureId), "0 means nothing has captured the pointer");
        CaptureId = captureId;
        Vertical = vertical;
    }

    public int CaptureId { get; }

    /// <summary>True for a line down the plot at an x, dragged left and right; false for one across it at a y.</summary>
    public bool Vertical { get; }

    /// <summary>How near the line the mouse must be to grab it, in pixels.</summary>
    public float GrabDistance { get; set; } = 6f;

    /// <summary>Whether the mouse is near enough to grab it (always while dragging).</summary>
    public bool Hovered { get; private set; }

    public bool Dragging { get; private set; }

    /// <summary>Where the drag has the line this frame, kept inside the plot. Meaningful while <see cref="Dragging"/> and on the frame it is <see cref="Released"/>.</summary>
    public float DragPixel { get; private set; }

    /// <summary>Whether a drag ended this frame with the button let go: the mod keeps the value it has shown and saves it.</summary>
    public bool Released { get; private set; }

    /// <summary>Whether a drag was given up this frame (a right click): the mod shows the value it had before.</summary>
    public bool Cancelled { get; private set; }

    /// <summary>The cursor to ask for while hovered or dragging: arrows along the axis the line moves on.</summary>
    public CursorShape Cursor => Vertical ? CursorShape.ResizeHorizontal : CursorShape.ResizeVertical;

    /// <param name="plot">The plot the line spans and stays inside.</param>
    /// <param name="linePixel">Where the line is drawn now: its value's pixel on the plot's axis.</param>
    public void Update(Pointer pointer, Area plot, float linePixel) {
        Released = Cancelled = false;
        float along = Vertical ? pointer.X : pointer.Y;
        float low = Vertical ? plot.X : plot.Y, high = Vertical ? plot.Right : plot.Bottom;

        if (Dragging) {
            if (pointer.Pressed(PointerButton.Right)) {
                Dragging = false;
                Cancelled = true;
            } else {
                DragPixel = Math.Clamp(along, low, high);
                if (!pointer.Held()) {
                    Dragging = false;
                    Released = true;
                }
            }
            Hovered = Dragging;
            if (Dragging || Released) return;
        }

        // A line off the plot is not drawn (the reference painters skip it), so it cannot be grabbed.
        Hovered = linePixel >= low && linePixel <= high && Near(along, linePixel) && Across(pointer, plot) && pointer.FreeFor(CaptureId);
        if (Hovered && pointer.Pressed() && pointer.Capture(CaptureId)) {
            Dragging = true;
            DragPixel = Math.Clamp(linePixel, low, high);
        }
    }

    private bool Near(float along, float linePixel) => Math.Abs(along - linePixel) <= GrabDistance;

    private bool Across(Pointer pointer, Area plot) =>
        Vertical ? pointer.Y >= plot.Y && pointer.Y <= plot.Bottom : pointer.X >= plot.X && pointer.X <= plot.Right;
}

/// <summary>
///     The edges between bands, for a line that sits between two columns rather than on a value: a mute
///     divider falls between two bins. Edge <c>i</c> is where band <c>i</c> starts; edge <c>Count</c> is
///     where the last one ends.
/// </summary>
internal static class BandEdges {
    /// <summary>The pixel of edge <paramref name="index"/>, from 0 to <c>Count</c>.</summary>
    // Midway between one band's bar and the next, so the line never covers either: with wholePixels and
    // a bar narrower than its band that is the middle of the gap, and in a route axis's group gap it is
    // the middle of that gap.
    public static float At<TBands>(TBands bands, int index) where TBands : IBandLayout {
        if (bands.Count == 0) return 0f;
        index = Math.Clamp(index, 0, bands.Count);
        if (index == 0) return bands.BandStart(0);
        if (index == bands.Count) return bands.BandStart(bands.Count - 1) + bands.WidthOf(bands.Count - 1);
        float previousEnd = bands.BarStart(index - 1) + bands.BarWidthOf(index - 1), nextStart = bands.BarStart(index);
        return MathF.Round((previousEnd + nextStart) / 2f);
    }

    /// <summary>The edge nearest a pixel.</summary>
    public static int Nearest<TBands>(TBands bands, float pixel) where TBands : IBandLayout {
        int best = 0;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i <= bands.Count; i++) {
            float distance = Math.Abs(At(bands, i) - pixel);
            if (distance < bestDistance) (best, bestDistance) = (i, distance);
        }
        return best;
    }
}
