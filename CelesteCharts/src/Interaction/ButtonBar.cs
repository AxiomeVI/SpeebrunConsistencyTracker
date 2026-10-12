#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A bar of buttons that answer the mouse themselves: actions, toggles that stay on, switches of joined
///     choices (SCT's Absolute | Relative) and sliders that pick a whole step, left to right in the order added. It owns its sketch and redraws it
///     only when something a player can see changed.
/// </summary>
// Add every item, then place the bar; place it again when the style, the scale or its area changes. Each frame:
// Update, read the handles, draw Sketch, and draw Tooltip last so it is over the chart.
//
// A caption is an item that is never under the mouse; the bar still contains its area.
//
// A slider is dragged, not clicked: the press sets the step nearest the mouse, and the value follows the mouse's x
// until the release, however far it strays; it never reports through WasClicked. Rest() and a missed update end
// the drag without a release.
//
// The bar holds every item's state; BarButton, BarToggle, BarSwitch, BarSlider and BarCaption are views onto it. A choice of a switch
// is an item like any other that shares its switch's number.
//
// A press inside an item takes the pointer, so a chart under the bar sees neither the press nor a drag. The
// bar remembers which item the press went down on, and never asks the pointer afterwards: the pointer lets go
// of its capture on the release frame, before anything reads it. A click is a release on that same item,
// however far the mouse moved inside it.
//
// The game can draw several times on one update of its own; the pointer is the same on each. A second Update
// then finds no press left to release, so a toggle flips once.
//
// A bar that is not updated (its page is hidden) does not see what the button does meanwhile. It counts the
// pointer's updates, and drops the press it remembers when it missed one.
//
// Allocation: adding items and the first placement allocate. Update, placing again, a new label and the sketch
// rebuilds do not.
internal sealed class ButtonBar {
    private const float Gap = 6f;
    private static readonly TextMetrics NoMetrics = new(_ => 0f, 0f);

    private sealed class Item {
        public string Label = "";
        public string? Tooltip;
        public Area Area;
        public bool Enabled = true, On, Toggle;
        public Rgba? Colour;
        public int Switch = -1;
        public float Space, Track;
        public bool Caption, Slider;
        public int Min, Max, Value;
    }

    private readonly List<Item> _items = new();
    private Sketch _sketch = new(NoMetrics), _tooltip = new(NoMetrics);
    private ChartStyle _style = ChartStyle.Default;
    private Area _keepIn;
    private float _x, _y, _alignX, _alignY, _space;
    private int _switches, _hovered = -1, _pressed = -1, _clicked = -1, _changedSwitch = -1, _tooltipFor = -2, _changedSlider = -1, _releasedSlider = -1;
    private long _seen = -1;
    private bool _placed, _held, _layoutStale, _dirty;

    /// <param name="captureId">This bar's id for <see cref="Pointer.Capture"/>: not 0, and no other surface's.</param>
    public ButtonBar(int captureId) {
        if (captureId == 0) throw new ArgumentOutOfRangeException(nameof(captureId), "0 means nothing has captured the pointer");
        CaptureId = captureId;
    }

    public int CaptureId { get; }

    public int Count => _items.Count;

    /// <summary>The bar's area, without its tooltip: for placing something else beside it.</summary>
    public Area Bounds { get; private set; }

    /// <summary>Whether an item that is switched on is under the mouse, for a mod that marks it some way of its own.</summary>
    public bool Hovering => _hovered >= 0;

    /// <summary>Whether <see cref="Sketch"/> will be rebuilt when next read.</summary>
    public bool Stale => _dirty;

    /// <summary>A button that reports a click.</summary>
    public BarButton Action(string label, string? tooltip = null) => new(this, Add(label, tooltip, toggle: false, inSwitch: -1));

    /// <summary>A button that stays on until clicked again.</summary>
    public BarToggle Toggle(string label, string? tooltip = null) => new(this, Add(label, tooltip, toggle: true, inSwitch: -1));

    /// <summary>Joined choices of which one is chosen, the first to begin with.</summary>
    public BarSwitch Switch(IReadOnlyList<string> labels, IReadOnlyList<string>? tooltips = null) {
        if (labels.Count == 0) throw new ArgumentException("a switch needs a choice", nameof(labels));
        if (tooltips != null && tooltips.Count != labels.Count) throw new ArgumentException("one tooltip per label", nameof(tooltips));
        int first = _items.Count, number = _switches++;
        for (int i = 0; i < labels.Count; i++) Add(labels[i], tooltips?[i], toggle: false, number);
        _items[first].On = true;
        return new BarSwitch(this, first, labels.Count);
    }

    /// <summary>
    ///     A track <paramref name="width"/> pixels wide with a knob that picks a whole step from <paramref name="min"/>
    ///     to <paramref name="max"/>, starting at <paramref name="min"/>.
    /// </summary>
    public BarSlider Slider(int min, int max, float width, string? tooltip = null) {
        if (max <= min) throw new ArgumentException("a slider needs a range", nameof(max));
        if (!(width > 0f)) throw new ArgumentException("a slider needs a width", nameof(width));
        int index = Add("", tooltip, toggle: false, inSwitch: -1);
        Item item = _items[index];
        (item.Slider, item.Min, item.Max, item.Value, item.Track) = (true, min, max, min, width);
        return new BarSlider(this, index);
    }

    /// <summary>
    ///     Plain text between controls: <c>Median band:</c> before a switch. It takes room in the row, answers no
    ///     mouse and has no tooltip, but <see cref="Contains"/> is true over it so a chart under the bar stays quiet.
    /// </summary>
    public BarCaption Caption(string text) {
        int index = Add(text, null, toggle: false, inSwitch: -1);
        _items[index].Caption = true;
        return new BarCaption(this, index);
    }

    /// <summary>More room before the next item, on top of the gap.</summary>
    public void Space(float pixels) => _space += pixels;

    private int Add(string label, string? tooltip, bool toggle, int inSwitch) {
        if (_placed) throw new InvalidOperationException("add every item before the bar is placed");
        _items.Add(new Item { Label = label, Tooltip = tooltip, Toggle = toggle, Switch = inSwitch, Space = _space });
        _space = 0f;
        return _items.Count - 1;
    }

    /// <summary>
    ///     Lays the bar out at a point: <paramref name="alignX"/> 0 starts it at x, 0.5 centres it, 1 ends it there,
    ///     and <paramref name="alignY"/> likewise. <paramref name="keepIn"/> bounds the tooltip only.
    /// </summary>
    public void Place(TextMetrics metrics, ChartStyle style, float x, float y, float alignX, float alignY, Area keepIn) {
        if (!ReferenceEquals(metrics, _sketch.Metrics)) {
            _sketch = new Sketch(metrics);
            _tooltip = new Sketch(metrics);
        }
        _style = style;
        (_x, _y, _alignX, _alignY, _keepIn) = (x, y, alignX, alignY, keepIn);
        _placed = true;
        Layout();
    }

    /// <summary>Lays the bar out against a corner of <paramref name="within"/>; the insets point inward and may be negative.</summary>
    public void PlaceIn(TextMetrics metrics, ChartStyle style, Area within, WidgetAnchor anchor, float insetX = 0f, float insetY = 0f) {
        bool right = anchor is WidgetAnchor.TopRight or WidgetAnchor.BottomRight;
        bool bottom = anchor is WidgetAnchor.BottomLeft or WidgetAnchor.BottomRight;
        Place(metrics, style, right ? within.Right - insetX : within.X + insetX, bottom ? within.Bottom - insetY : within.Y + insetY,
              right ? 1f : 0f, bottom ? 1f : 0f, within);
    }

    private void Layout() {
        TextMetrics metrics = _sketch.Metrics;
        float scale = _style.Label.Scale, pad = _style.LabelGap;
        float height = MathF.Ceiling(metrics.HeightAt(scale) + 6f), total = 0f;
        for (int i = 0; i < _items.Count; i++) {
            Item item = _items[i];
            if (i > 0) total += item.Space + (JoinsPrevious(i) ? 0f : _items[i - 1].Caption ? Gap / 3f : Gap);
            float width = item.Slider ? item.Track : MathF.Ceiling(metrics.WidthAt(item.Label, scale) + (item.Caption ? 0f : 2 * pad));
            item.Area = new Area(total, 0f, width, height);
            total += width;
        }
        float left = MathF.Round(_x - _alignX * total), top = MathF.Round(_y - _alignY * height);
        for (int i = 0; i < _items.Count; i++) _items[i].Area = _items[i].Area.Offset(left, top);
        Bounds = _items.Count == 0 ? default : new Area(left, top, total, height);
        _layoutStale = false;
        _dirty = true;
        _tooltipFor = -2;
    }

    /// <summary>Reads this frame's mouse: hover, press and click. Call it once a frame, before reading the handles.</summary>
    public void Update(Pointer pointer) {
        if (!_placed) return;
        if (_layoutStale) Layout();
        _clicked = -1;
        _changedSwitch = _changedSlider = _releasedSlider = -1;
        if (_pressed >= 0 && !_items[_pressed].Enabled) EndPress();
        if (pointer.Updates - _seen > 1) EndPress();
        _seen = pointer.Updates;

        int under = -1;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Enabled && !_items[i].Caption && _items[i].Area.Contains(pointer.X, pointer.Y)) under = i;

        if (pointer.Pressed()) {
            _pressed = under >= 0 && pointer.Capture(CaptureId) ? under : -1;
            if (_pressed >= 0 && _items[_pressed].Slider) {
                Drag(_pressed, pointer.X);
                _dirty = true;                          // lit for the drag
            }
        } else if (_pressed >= 0 && _items[_pressed].Slider) {
            // The release frame's position counts: the mouse may have moved on the frame it let go.
            Drag(_pressed, pointer.X);
            if (pointer.Released()) {
                _releasedSlider = _pressed;
                EndPress();
            }
        } else if (_pressed >= 0 && pointer.Released()) {
            if (under == _pressed) Click(_pressed);
            _pressed = -1;
        }

        bool held = _pressed >= 0 && under == _pressed;
        int hovered = _pressed >= 0 ? (held ? under : -1) : pointer.FreeFor(CaptureId) ? under : -1;
        if (hovered == _hovered && held == _held) return;
        (_hovered, _held) = (hovered, held);
        _dirty = true;
    }

    /// <summary>Drops the hover and the press: for a bar that is not shown this frame, in place of <see cref="Update"/>.</summary>
    public void Rest() {
        _clicked = _changedSwitch = _changedSlider = _releasedSlider = -1;
        EndPress();
        if (_hovered < 0 && !_held) return;
        (_hovered, _held) = (-1, false);
        _dirty = true;
    }

    // Drops the press; a drag in progress changes how the slider is drawn.
    private void EndPress() {
        if (_pressed >= 0 && _items[_pressed].Slider) _dirty = true;
        _pressed = -1;
    }

    private void Drag(int index, float x) {
        Item item = _items[index];
        int step = SliderStep(x, item.Area, KnobWidth, item.Min, item.Max);
        if (step == item.Value) return;
        item.Value = step;
        _changedSlider = index;
        _dirty = true;
    }

    /// <summary>The knob's width in pixels: its centre travels the track inset by half of it.</summary>
    public const float KnobWidth = 8f;
    public float SliderKnobWidth => KnobWidth;

    /// <summary>
    ///     The step a mouse at <paramref name="x"/> picks on a track in <paramref name="track"/>: the knob's centre
    ///     runs between the track's ends inset by half <paramref name="knobWidth"/>, and the nearest step wins.
    /// </summary>
    public static int SliderStep(float x, Area track, float knobWidth, int min, int max) {
        float span = track.Width - knobWidth;
        if (!(span > 0f)) return min;
        float t = Math.Clamp((x - track.X - knobWidth / 2f) / span, 0f, 1f);
        return min + (int) MathF.Round(t * (max - min), MidpointRounding.AwayFromZero);
    }

    private void Click(int index) {
        Item item = _items[index];
        _clicked = index;
        if (item.Toggle) {
            item.On = !item.On;
        } else if (item.Switch >= 0 && !item.On) {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Switch == item.Switch) _items[i].On = i == index;
            _changedSwitch = item.Switch;
        }
        _dirty = true;
    }

    /// <summary>The buttons. Rebuilt here when anything visible changed, so a value set after Update shows this frame.</summary>
    public Sketch Sketch {
        get {
            if (_placed && _layoutStale) Layout();
            if (_dirty) {
                _dirty = false;
                _sketch.Clear();
                Paint.ButtonBar(_sketch, this, _style);
            }
            return _sketch;
        }
    }

    /// <summary>The hovered item's tooltip, or nothing. Its own sketch, to be drawn over the chart and over other bars.</summary>
    public Sketch Tooltip {
        get {
            int want = _hovered >= 0 && !string.IsNullOrEmpty(_items[_hovered].Tooltip) ? _hovered : -1;
            if (want == _tooltipFor) return _tooltip;
            _tooltipFor = want;
            _tooltip.Clear();
            if (want < 0) return _tooltip;
            string text = _items[want].Tooltip!;
            (float width, float height) = Paint.TooltipSize(_tooltip, text, _style);
            Paint.TooltipBox(_tooltip, text, TooltipLayout.Beside(_items[want].Area, width, height, _keepIn), _style);
            return _tooltip;
        }
    }

    /// <summary>Whether a point is on an item, switched on or not: a chart under the bar leaves that hover alone.</summary>
    public bool Contains(float x, float y) {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Area.Contains(x, y)) return true;
        return false;
    }

    public Area AreaOf(int index) => _items[index].Area;
    public string LabelOf(int index) => _items[index].Label;
    public bool IsEnabled(int index) => _items[index].Enabled;
    public bool IsCaption(int index) => _items[index].Caption;
    public bool IsOn(int index) => _items[index].On;
    public bool IsSlider(int index) => _items[index].Slider;

    /// <summary>Where a slider's value sits in its range, 0 at the minimum and 1 at the maximum.</summary>
    public float SliderFraction(int index) {
        Item item = _items[index];
        return (item.Value - item.Min) / (float) (item.Max - item.Min);
    }

    /// <summary>Whether the press that went down on a slider is still held.</summary>
    public bool IsDragging(int index) => index == _pressed && _items[index].Slider;

    /// <summary>The colour an item was given in place of the style's, or null.</summary>
    public Rgba? ColourOf(int index) => _items[index].Colour;
    public bool IsHovered(int index) => index == _hovered;
    public bool IsPressed(int index) => _held && index == _pressed;

    /// <summary>Whether an item is a choice of the same switch as the one before it: they touch.</summary>
    public bool JoinsPrevious(int index) => index > 0 && _items[index].Switch >= 0 && _items[index].Switch == _items[index - 1].Switch;

    internal bool WasClicked(int index) => index == _clicked;
    internal bool SliderChanged(int index) => index == _changedSlider;
    internal bool SliderReleased(int index) => index == _releasedSlider;
    internal int SliderMin(int index) => _items[index].Min;
    internal int SliderMax(int index) => _items[index].Max;
    internal int SliderValue(int index) => _items[index].Value;

    internal void SetSliderValue(int index, int value) {
        Item item = _items[index];
        value = Math.Clamp(value, item.Min, item.Max);
        if (item.Value == value) return;
        item.Value = value;
        _dirty = true;
    }

    internal bool SwitchChanged(int first) => _changedSwitch >= 0 && _changedSwitch == _items[first].Switch;

    internal void SetEnabled(int first, int count, bool enabled) {
        for (int i = first; i < first + count; i++) {
            if (_items[i].Enabled == enabled) continue;
            _items[i].Enabled = enabled;
            _dirty = true;
        }
    }

    internal void SetColour(int index, Rgba? colour) {
        if (_items[index].Colour == colour) return;
        _items[index].Colour = colour;
        _dirty = true;
    }

    internal void SetOn(int index, bool on) {
        if (_items[index].On == on) return;
        _items[index].On = on;
        _dirty = true;
    }

    internal int ChoiceOf(int first, int count) {
        for (int i = 0; i < count; i++)
            if (_items[first + i].On) return i;
        return 0;
    }

    internal void Choose(int first, int count, int choice) {
        if (choice < 0 || choice >= count) throw new ArgumentOutOfRangeException(nameof(choice));
        for (int i = 0; i < count; i++) SetOn(first + i, i == choice);
    }

    internal void SetLabel(int index, string label) {
        if (_items[index].Label == label) return;
        _items[index].Label = label;
        _layoutStale = true;
    }
}

/// <summary>A button of a <see cref="ButtonBar"/> that reports a click.</summary>
internal sealed class BarButton {
    private readonly ButtonBar _bar;
    private readonly int _index;

    internal BarButton(ButtonBar bar, int index) {
        _bar = bar;
        _index = index;
    }

    /// <summary>Whether it was clicked on this update of the bar.</summary>
    public bool Clicked => _bar.WasClicked(_index);

    /// <summary>Off draws it faint and takes no hover or click.</summary>
    public bool Enabled { get => _bar.IsEnabled(_index); set => _bar.SetEnabled(_index, 1, value); }

    /// <summary>A new label lays the bar out again; the same one does nothing.</summary>
    public string Label { get => _bar.LabelOf(_index); set => _bar.SetLabel(_index, value); }

    /// <summary>
    ///     A colour of its own for its outline, its label and its fill when lit, in place of the style's: what sets
    ///     a destructive action apart. Null (the default) is the style's.
    /// </summary>
    public Rgba? Colour { get => _bar.ColourOf(_index); set => _bar.SetColour(_index, value); }
}

/// <summary>A caption of a <see cref="ButtonBar"/>.</summary>
internal sealed class BarCaption {
    private readonly ButtonBar _bar;
    private readonly int _index;

    internal BarCaption(ButtonBar bar, int index) {
        _bar = bar;
        _index = index;
    }

    /// <summary>A new text lays the bar out again; the same one does nothing.</summary>
    public string Text { get => _bar.LabelOf(_index); set => _bar.SetLabel(_index, value); }
}

/// <summary>A button of a <see cref="ButtonBar"/> that stays on until clicked again.</summary>
internal sealed class BarToggle {
    private readonly ButtonBar _bar;
    private readonly int _index;

    internal BarToggle(ButtonBar bar, int index) {
        _bar = bar;
        _index = index;
    }

    public bool On { get => _bar.IsOn(_index); set => _bar.SetOn(_index, value); }

    /// <summary>Whether a click flipped it on this update of the bar.</summary>
    public bool Clicked => _bar.WasClicked(_index);

    public bool Enabled { get => _bar.IsEnabled(_index); set => _bar.SetEnabled(_index, 1, value); }

    public string Label { get => _bar.LabelOf(_index); set => _bar.SetLabel(_index, value); }

    /// <summary>
    ///     A colour of its own for its outline, its label and its fill when lit, in place of the style's: what sets
    ///     a destructive action apart. Null (the default) is the style's.
    /// </summary>
    public Rgba? Colour { get => _bar.ColourOf(_index); set => _bar.SetColour(_index, value); }
}

/// <summary>Joined choices of a <see cref="ButtonBar"/>, one of them chosen.</summary>
internal sealed class BarSwitch {
    private readonly ButtonBar _bar;
    private readonly int _first;

    internal BarSwitch(ButtonBar bar, int first, int count) {
        _bar = bar;
        _first = first;
        Count = count;
    }

    public int Count { get; }

    public int Choice { get => _bar.ChoiceOf(_first, Count); set => _bar.Choose(_first, Count, value); }

    /// <summary>Whether a click chose another choice on this update of the bar.</summary>
    public bool Changed => _bar.SwitchChanged(_first);

    /// <summary>A switch is on or off whole.</summary>
    public bool Enabled { get => _bar.IsEnabled(_first); set => _bar.SetEnabled(_first, Count, value); }
}

/// <summary>A slider of a <see cref="ButtonBar"/>: a whole step from its minimum to its maximum, dragged with the mouse.</summary>
internal sealed class BarSlider {
    private readonly ButtonBar _bar;
    private readonly int _index;

    internal BarSlider(ButtonBar bar, int index) {
        _bar = bar;
        _index = index;
    }

    public int Min => _bar.SliderMin(_index);
    public int Max => _bar.SliderMax(_index);

    /// <summary>Clamped to the range. Setting it from code raises nothing, and a value that is already there redraws nothing.</summary>
    public int Value { get => _bar.SliderValue(_index); set => _bar.SetSliderValue(_index, value); }

    /// <summary>Whether the mouse moved the value on this update of the bar.</summary>
    public bool Changed => _bar.SliderChanged(_index);

    /// <summary>Whether the press that went down on it is still held.</summary>
    public bool Dragging => _bar.IsDragging(_index);

    /// <summary>Whether the drag ended on this update: save here, once. A bar that rested or missed an update ends it without this.</summary>
    public bool Released => _bar.SliderReleased(_index);

    /// <summary>Off draws it faint and takes no press; a drag in progress ends.</summary>
    public bool Enabled { get => _bar.IsEnabled(_index); set => _bar.SetEnabled(_index, 1, value); }
}
