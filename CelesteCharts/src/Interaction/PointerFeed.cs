#nullable enable

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Feeds a <see cref="Pointer"/> at most once a frame, with the wheel as a change of the device's running
///     total. The drawing layer feeds it from every update of the game (<c>GameInput</c>); tests feed it by hand.
/// </summary>
// Fed on every update, charts shown or not: a button held while they were hidden is then no fresh press when
// they show, a click before hiding does not pair with one after, and no wheel notch made while hidden
// arrives. A frame nobody feeds (no update ran) is skipped, not lost: the next feed compares with the last.
internal sealed class PointerFeed {
    private long _frame = long.MinValue;
    private int _wheelTotal;
    private bool _wheelKnown;

    public Pointer Pointer { get; } = new();

    /// <summary>Whether <paramref name="frame"/> has not been fed yet.</summary>
    public bool Due(long frame) => frame != _frame;

    /// <summary>
    ///     Feeds one frame: a second call with the same <paramref name="frame"/> does nothing.
    ///     <paramref name="wheelTotal"/> is the wheel's running total, 120 a notch; the input's own wheel is ignored.
    /// </summary>
    public void Feed(long frame, in PointerInput input, int wheelTotal) {
        if (!Due(frame)) return;
        _frame = frame;
        // A part of a notch (a smooth-scrolling wheel) is kept for the next frame.
        int notches = 0;
        if (_wheelKnown) {
            notches = (wheelTotal - _wheelTotal) / 120;
            _wheelTotal += notches * 120;
        } else {
            _wheelTotal = wheelTotal;
            _wheelKnown = true;
        }
        Pointer.Update(input with { Wheel = notches });
    }

    /// <summary>
    ///     Feeds one frame the game blanks its own input on (its debug console open, its window not active): no
    ///     button, no modifier and no wheel, the mouse where <paramref name="input"/> puts it. Notches turned
    ///     meanwhile never arrive: <paramref name="wheelTotal"/> becomes the total the next feed compares with.
    /// </summary>
    // As the game's own mouse does then, a button held when the blank starts reads as released.
    public void Blank(long frame, in PointerInput input, int wheelTotal) {
        if (!Due(frame)) return;
        _frame = frame;
        _wheelTotal = wheelTotal;
        _wheelKnown = true;
        Pointer.Update(new PointerInput(input.X, input.Y, Seconds: input.Seconds));
    }
}
