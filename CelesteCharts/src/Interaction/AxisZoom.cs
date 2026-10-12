#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Zoom and pan along one axis: the wheel zooms around the mouse, a drag pans, a double-click shows
///     everything again. It keeps which part of the full range shows, as fractions (<see cref="From"/>,
///     <see cref="To"/>); the mod reads the values there (<see cref="Range{T}"/>) and rebuilds its chart when
///     <see cref="Changed"/> says so.
/// </summary>
// A pan does not rebuild the chart every frame: while the drag lasts, the chart as it was built is drawn
// moved by PanPixels and cut to the plot, like a scrolled wall; the new range is taken on release and the
// chart rebuilt once. So the ticks and labels are not re-chosen until the drag ends. A wheel notch is one
// rebuild. The pan stops where the full range ends, and the zoom at MinSpan of it.
//
// Pure and allocation-free.
internal sealed class AxisZoom {
    private float _grabbed;
    private bool _panning;

    public AxisZoom(int captureId, bool vertical = false) {
        if (captureId == 0) throw new ArgumentOutOfRangeException(nameof(captureId), "0 means nothing has captured the pointer");
        CaptureId = captureId;
        Vertical = vertical;
    }

    public int CaptureId { get; }

    /// <summary>False for an x axis; true for a y axis, whose fractions count up from the plot's bottom.</summary>
    public bool Vertical { get; }

    /// <summary>Where the part shown starts, as a fraction of the full range.</summary>
    public double From { get; private set; }

    /// <summary>Where the part shown ends, as a fraction of the full range.</summary>
    public double To { get; private set; } = 1.0;

    /// <summary>The smallest part of the full range that may show.</summary>
    public double MinSpan { get; set; } = 0.02;

    /// <summary>How much one wheel notch zooms: the part shown is divided (in) or multiplied (out) by it.</summary>
    public double WheelFactor { get; set; } = 1.25;

    /// <summary>Whether the part shown changed this frame (a notch, the end of a pan, a reset): rebuild the chart.</summary>
    public bool Changed { get; private set; }

    /// <summary>Whether a pan is under way.</summary>
    public bool Panning => _panning;

    /// <summary>How far to draw the chart as built, along the axis, while a pan is under way; 0 otherwise.</summary>
    public float PanPixels { get; private set; }

    /// <summary>Whether any zoom is on: the whole range is not showing.</summary>
    public bool Zoomed => From > 0.0 || To < 1.0;

    /// <summary>The cursor to ask for while panning: the move arrows.</summary>
    public CursorShape Cursor => CursorShape.Move;

    /// <summary>The values at the ends of the part shown, in the full range's own kind.</summary>
    public (T Min, T Max) Range<T>(IValueKind<T> kind, T min, T max) => (kind.Between(min, max, From), kind.Between(min, max, To));

    /// <summary>Shows the whole range.</summary>
    public void Reset() {
        if (!Zoomed) return;
        (From, To) = (0.0, 1.0);
        Changed = true;
    }

    /// <param name="plot">The plot the axis spans: the wheel and a drag act inside it.</param>
    public void Update(Pointer pointer, Area plot) {
        Changed = false;
        float length = Vertical ? plot.Height : plot.Width;
        if (length <= 0f) return;
        float along = Vertical ? plot.Bottom - pointer.Y : pointer.X - plot.X;   // pixels from the axis' low end
        double span = To - From;

        if (_panning) {
            if (pointer.Held()) {
                float moved = along - _grabbed;
                // Kept to the full range: the part shown can move by no more than what lies beyond it.
                float most = (float) (From / span * length), least = -(float) ((1.0 - To) / span * length);
                PanPixels = Math.Clamp(moved, least, most) * (Vertical ? -1f : 1f);
                return;
            }
            float pixels = Vertical ? -PanPixels : PanPixels;
            double shift = -pixels / length * span;
            (From, To) = (From + shift, To + shift);
            Clamp(span);
            _panning = false;
            PanPixels = 0f;
            Changed = pixels != 0f;
            return;
        }

        bool inside = plot.Contains(pointer.X, pointer.Y);
        if (!inside || !pointer.FreeFor(CaptureId)) return;

        if (pointer.DoubleClicked()) {
            Reset();
            return;
        }
        if (pointer.Wheel != 0) {
            double at = From + Math.Clamp(along / length, 0f, 1f) * span;
            double wanted = span * Math.Pow(WheelFactor, -pointer.Wheel);
            double next = Math.Clamp(wanted, Math.Min(MinSpan, 1.0), 1.0);
            if (next != span) {
                double share = (at - From) / span;
                (From, To) = (at - share * next, at - share * next + next);
                Clamp(next);
                Changed = true;
            }
            return;
        }
        // A drag pans only while zoomed: with the whole range showing there is nowhere to go, and a click
        // stays a click for the chart.
        if (Zoomed && pointer.Dragging() && pointer.DraggingFrom(plot) && pointer.Capture(CaptureId)) {
            _panning = true;
            (float px, float py) = pointer.PressedAt();
            _grabbed = Vertical ? plot.Bottom - py : px - plot.X;
            PanPixels = 0f;
            Update(pointer, plot);
        }
    }

    private void Clamp(double span) {
        if (From < 0.0) (From, To) = (0.0, span);
        if (To > 1.0) (From, To) = (1.0 - span, 1.0);
        From = Math.Max(0.0, From);
        To = Math.Min(1.0, To);
    }
}
