#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A getting-started card over a chart: a title, a few lines the mod wrote, an optional tick box for "do not show this
///     again", which closes it too, and a close button. Closed, it is a "?" in the same corner that opens it again.
/// </summary>
// One card's worth of state per mod, in statics (a static here is one per mod): whether the card is open, and
// the tick box's value. Every card of a mod reads them each frame, so closing one closes them all and a tick in
// one shows in the others. Two mods do not share them, which is wanted: each has its own text and its own flag.
//
// The first Update of a run decides: collapsed if the mod's saved flag is set, open otherwise. After that only
// the player changes it. Nothing is saved here: the mod saves the flag when onNeverAgain runs.
//
// Close and "?" share a corner, and one click does not close and reopen: a click is a release on the bar the
// press went down on, and the bar that is not shown takes no press.
internal sealed class HintCard {
    private enum Run {
        Undecided,
        Open,
        Collapsed,
    }

    private const float Pad = 12f, Between = 8f;

    private static Run _run;
    private static bool _never;
    private static long _changedAt = -1, _tickedAt = -1;

    /// <summary>Forgets that this run decided anything: the next Update decides again. For tests; a restart does the same.</summary>
    public static void ForgetRun() {
        _run = Run.Undecided;
        _changedAt = _tickedAt = -1;
    }

    private readonly string _title;
    private readonly IReadOnlyList<string> _lines;
    private readonly bool _seed;
    private readonly Action<bool>? _onNeverAgain;
    private readonly ButtonBar _controls, _help;
    private readonly BarToggle? _tick;
    private readonly BarButton _close, _question;

    private Sketch _sketch = new(new TextMetrics(_ => 0f, 0f));
    private ChartStyle _style = ChartStyle.Default;
    private TextStyle _titleStyle, _bodyStyle;
    private Area _card;
    private bool _placed, _fits, _stale = true, _drawnOpen, _drawnPeek, _closedNow, _peeking;

    /// <param name="lines">The body, one string per line: the card does not wrap.</param>
    /// <param name="neverAgain">The mod's saved flag. It seeds the run's first decision and is not read after.</param>
    /// <param name="tickLabel">The tick box's text; null for a card with no tick box, only Close.</param>
    /// <param name="onNeverAgain">Runs once, inside Update, when the player ticks or unticks the box: save the value. Never called, and may be null, with no tick box.</param>
    public HintCard(string title, IReadOnlyList<string> lines, string? tickLabel, string closeLabel, bool neverAgain, Action<bool>? onNeverAgain,
                    int captureId) {
        _title = title;
        _lines = lines;
        _seed = neverAgain;
        _onNeverAgain = onNeverAgain;
        CaptureId = captureId;
        _controls = new ButtonBar(captureId);
        _help = new ButtonBar(captureId);
        _tick = tickLabel == null ? null : _controls.Toggle(tickLabel);
        _close = _controls.Action(closeLabel);
        _question = _help.Action("?");
    }

    /// <summary>
    ///     A card with no tick box, only Close, for an owner that stores the open state itself: seed it with
    ///     <see cref="StartsOpen"/> and save it from <see cref="OnOpenChanged"/>.
    /// </summary>
    public HintCard(string title, IReadOnlyList<string> lines, string closeLabel, int captureId)
        : this(title, lines, null, closeLabel, false, null, captureId) {
    }

    public int CaptureId { get; }

    /// <summary>The title's text style; the chart style's title when not set.</summary>
    public TextStyle? TitleStyle { get; init; }

    /// <summary>The body's text style; the chart style's label when not set.</summary>
    public TextStyle? BodyStyle { get; init; }

    /// <summary>
    ///     Whether the run's first decision opens the card, instead of the <c>neverAgain</c> flag deciding. For a card with no
    ///     tick box whose open state the mod saves itself. The state is shared by the mod's cards, so only the first card
    ///     updated in a run applies it. Null leaves the flag to decide.
    /// </summary>
    public bool? StartsOpen { get; init; }

    /// <summary>
    ///     Runs once, inside Update, when the player opens (true) or closes (false) the card: a click on the "?", on Close, or
    ///     a tick that closes it. A hover peek is not a change. Save the value here.
    /// </summary>
    public Action<bool>? OnOpenChanged { get; init; }

    /// <summary>Whether a collapsed card also shows while the pointer is on its "?". Off, only a click opens it.</summary>
    public bool OpensOnHover { get; init; }

    /// <summary>Whether the card is drawn only because the pointer is on the "?". It takes no input then.</summary>
    // A peek leaves the shared state alone: the other cards of the mod stay collapsed, and a click still opens for good.
    public bool Peeking => _peeking;

    private bool Open => _run == Run.Open && _fits;

    /// <summary>The card when open, the "?" when not.</summary>
    public Area Bounds => !_placed ? default : Open ? _card : _help.Bounds;

    /// <summary>Whether a point is on the card or the "?": the chart under it leaves that hover alone.</summary>
    // The click that closes the card is still showing on the pointer when the chart reads it, later in the
    // same frame: until the next Update the whole card stays covered, or that click would land on the chart.
    // That lasts until this card's next Update, which a page calls before it asks again.
    public bool Contains(float x, float y) =>
        _placed && (Open || _closedNow ? _card.Contains(x, y) || _help.Contains(x, y) : _help.Contains(x, y));

    /// <summary>Whether a control that is switched on is under the mouse.</summary>
    public bool Hovering => _placed && (Open ? _controls.Hovering : _help.Hovering);

    /// <summary>
    ///     Sizes the card to its text and puts it, and its "?", against a corner of <paramref name="within"/>. Call
    ///     it on the first frame a chart shows (it measures text) and again when the style or the area changes.
    /// </summary>
    public void PlaceIn(TextMetrics metrics, ChartStyle style, Area within, WidgetAnchor anchor, float insetX = 0f, float insetY = 0f) {
        if (!ReferenceEquals(metrics, _sketch.Metrics)) _sketch = new Sketch(metrics);
        _style = style;
        _titleStyle = TitleStyle ?? style.Title;
        _bodyStyle = BodyStyle ?? style.Label;

        // Once to measure the controls, then again where they go.
        _controls.Place(metrics, style, 0f, 0f, 0f, 0f, within);
        float width = Math.Max(metrics.WidthAt(_title, _titleStyle.Scale), _controls.Bounds.Width);
        for (int i = 0; i < _lines.Count; i++) width = Math.Max(width, metrics.WidthAt(_lines[i], _bodyStyle.Scale));
        width = MathF.Ceiling(width) + 2 * Pad;
        float body = _lines.Count == 0 ? 0f : _lines.Count * metrics.HeightAt(_bodyStyle.Scale) + Between;
        float height = MathF.Ceiling(Pad + metrics.HeightAt(_titleStyle.Scale) + Between + body + _controls.Bounds.Height + Pad);

        bool right = anchor is WidgetAnchor.TopRight or WidgetAnchor.BottomRight;
        bool bottom = anchor is WidgetAnchor.BottomLeft or WidgetAnchor.BottomRight;
        _card = new Area(MathF.Round(right ? within.Right - insetX - width : within.X + insetX),
                         MathF.Round(bottom ? within.Bottom - insetY - height : within.Y + insetY), width, height);
        _fits = within.Holds(_card);
        _controls.Place(metrics, style, _card.Right - Pad, _card.Bottom - Pad, 1f, 1f, within);
        _help.PlaceIn(metrics, style, within, anchor, insetX, insetY);
        _placed = true;
        _stale = true;
    }

    /// <summary>Reads this frame's mouse. Call it where the chart reads the pointer, before the chart does.</summary>
    public void Update(Pointer pointer) {
        if (!_placed) return;
        if (_run == Run.Undecided) {
            _never = _seed;
            _run = StartsOpen ?? !_never ? Run.Open : Run.Collapsed;
        }
        _question.Enabled = _fits;
        if (_tick != null) _tick.On = _never;
        UpdateControls(pointer);
        _closedNow = _fits && _run == Run.Collapsed && pointer.Updates == _changedAt;
    }

    // The bar that is not shown rests: the state can change after this, on this frame, and it is then drawn
    // without an Update of its own.
    private void UpdateControls(Pointer pointer) {
        if (Open) {
            _peeking = false;
            _help.Rest();
            _controls.Update(pointer);
            if (pointer.Pressed() && _card.Contains(pointer.X, pointer.Y)) pointer.Capture(CaptureId);
            // Once per click across cards: two of them under one press would tick and untick.
            if (_tick is { Clicked: true } && pointer.Updates != _tickedAt) {
                _tickedAt = pointer.Updates;
                _never = _tick.On;
                _onNeverAgain?.Invoke(_never);
                // "Never again" is also "not now". Unticking, on a card opened from the "?", leaves it open.
                if (_never) Change(Run.Collapsed, pointer);
            }
            if (_tick != null) _tick.On = _never;
            if (_close.Clicked) Change(Run.Collapsed, pointer);
        } else {
            _controls.Rest();
            _help.Update(pointer);
            if (_question.Clicked) Change(Run.Open, pointer);
            // The bar reads a pointer taken by something else as no hover, so a captured pointer starts no peek.
            _peeking = OpensOnHover && _fits && _run == Run.Collapsed && _help.Hovering;
        }
    }

    private void Change(Run to, Pointer pointer) {
        _run = to;
        _changedAt = pointer.Updates;
        OnOpenChanged?.Invoke(to == Run.Open);
    }

    /// <summary>The card or the "?". Rebuilt here only when the state, the hover or a control changed.</summary>
    public Sketch Sketch {
        get {
            bool open = Open, peek = _peeking;
            ButtonBar bar = open ? _controls : _help;
            if (!_stale && !bar.Stale && open == _drawnOpen && peek == _drawnPeek) return _sketch;
            _stale = false;
            _drawnOpen = open;
            _drawnPeek = peek;
            _sketch.Clear();
            // A peek is the card's text under the "?", without the controls: they take no input.
            if (open || peek) Paint.HintCard(_sketch, _card, _title, _lines, _titleStyle, _bodyStyle, Pad, Between, _style);
            _sketch.Add(bar.Sketch);
            return _sketch;
        }
    }
}
