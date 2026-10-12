#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Where a focus is: closed, growing, open, or shrinking back.</summary>
internal enum FocusState {
    Closed,
    Opening,
    Open,
    Closing,
}

/// <summary>
///     One chart taken out of a wall to fill the screen, and put back: a frame that grows from the tile to
///     its full-size area, then shows the chart built for that area, then shrinks back to the tile.
/// </summary>
// Only the frame moves. The chart is built once at its full size and shown when the frame arrives, so
// nothing is rebuilt per frame and no text is drawn scaled, which the game's font does badly. Closing
// while still opening turns round from where the frame is. Pure: fed real seconds, it only computes.
internal sealed class FocusTransition {
    private float _t;   // 0 at the tile, 1 at the full-size area

    /// <summary>How long growing or shrinking takes, in seconds.</summary>
    public float Seconds { get; init; } = 0.18f;

    public FocusState State { get; private set; } = FocusState.Closed;

    /// <summary>Which item is focused (the mod's own number: a tile's index), or -1.</summary>
    public int Item { get; private set; } = -1;

    /// <summary>The tile it came from, on screen.</summary>
    public Area From { get; private set; }

    /// <summary>The area it fills when open.</summary>
    public Area To { get; private set; }

    /// <summary>Whether anything is focused, even halfway: the wall behind should not answer the mouse.</summary>
    public bool IsActive => State != FocusState.Closed;

    /// <summary>Whether the full-size chart is shown: only once the frame has arrived.</summary>
    public bool ShowsContent => State == FocusState.Open;

    /// <summary>How far open, eased, from 0 to 1: for dimming what is behind.</summary>
    public float Amount => Ease(_t);

    /// <summary>The frame this frame: between the tile and the full-size area.</summary>
    public Area Frame {
        get {
            float e = Ease(_t);
            return new Area(Lerp(From.X, To.X, e), Lerp(From.Y, To.Y, e), Lerp(From.Width, To.Width, e), Lerp(From.Height, To.Height, e));
        }
    }

    /// <summary>Starts growing <paramref name="item"/> from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public void Open(int item, Area from, Area to) {
        Item = item;
        From = from;
        To = to;
        _t = 0f;
        State = Seconds <= 0f ? FocusState.Open : FocusState.Opening;
        if (State == FocusState.Open) _t = 1f;
    }

    /// <summary>Starts shrinking back to the tile. Nothing when closed.</summary>
    public void Close() {
        if (State is FocusState.Opening or FocusState.Open) State = FocusState.Closing;
    }

    /// <summary>Moves the frame on by <paramref name="seconds"/> of real time.</summary>
    public void Update(float seconds) {
        float step = Seconds <= 0f ? 1f : seconds / Seconds;
        switch (State) {
            case FocusState.Opening:
                _t = Math.Min(1f, _t + step);
                if (_t >= 1f) State = FocusState.Open;
                break;
            case FocusState.Closing:
                _t = Math.Max(0f, _t - step);
                if (_t <= 0f) {
                    State = FocusState.Closed;
                    Item = -1;
                }
                break;
        }
    }

    // Fast out, gentle in: the frame leaves the tile quickly and settles.
    private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
