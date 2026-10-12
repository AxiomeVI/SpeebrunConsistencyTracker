#nullable enable

using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteCharts;

// Motion: bars growing in, a flash when data changes, tiles easing to a new place. Every timer takes real
// seconds (PointerInput.Seconds, Engine.RawDeltaTime), so it runs the same in a paused or frozen level.
// None of it rebuilds a sketch: a sketch is built once and drawn moved, faded or grown each frame.

/// <summary>
///     Bars growing up from their baseline when a chart first shows, left to right in a wave. The chart is
///     built once, at full height; each frame it is drawn grown (<c>GameCanvas.DrawGrown</c>).
/// </summary>
internal sealed class GrowIn {
    private float _elapsed = float.PositiveInfinity;

    /// <summary>How long one bar takes to grow, in seconds.</summary>
    public float Seconds { get; set; } = 0.35f;

    /// <summary>How much later the rightmost bar starts than the leftmost, in seconds.</summary>
    public float Stagger { get; set; } = 0.25f;

    /// <summary>Whether every bar is at full height: draw the sketch plainly.</summary>
    public bool Done => _elapsed >= Seconds + Stagger;

    /// <summary>Starts growing from nothing: when the chart is first shown, or its data is replaced.</summary>
    public void Start() => _elapsed = 0f;

    /// <summary>Jumps to the end: a chart that should not animate this time.</summary>
    public void Finish() => _elapsed = float.PositiveInfinity;

    public void Update(float seconds) {
        if (!Done) _elapsed += Math.Max(0f, seconds);
    }

    /// <summary>
    ///     A mark of the chart as drawn this frame: a fill or a line pulled towards the plot's baseline (its
    ///     bottom edge) by how grown it is where it stands across the plot. Text does not grow — scaled text
    ///     would be blurred — it fades in instead.
    /// </summary>
    public Mark Apply(in Mark mark, Area plot) {
        float amount = Amount(plot.Width <= 0f ? 1f : ((mark.X1 + mark.X2) / 2f - plot.X) / plot.Width);
        if (amount >= 1f) return mark;
        if (mark.Kind == MarkKind.Text) return mark with { Colour = mark.Colour.Times(amount), StrokeColour = mark.StrokeColour.Times(amount) };
        float baseline = plot.Bottom;
        return mark with { Y1 = baseline + (mark.Y1 - baseline) * amount, Y2 = baseline + (mark.Y2 - baseline) * amount };
    }

    /// <summary>How grown a mark is, from 0 to 1, <paramref name="along"/> the plot from 0 (left) to 1 (right); eased out.</summary>
    public float Amount(float along) {
        if (Done) return 1f;
        float t = Seconds <= 0f ? 1f : Math.Clamp((_elapsed - Stagger * Math.Clamp(along, 0f, 1f)) / Seconds, 0f, 1f);
        float rest = 1f - t;
        return 1f - rest * rest * rest;
    }
}

/// <summary>
///     A flash that fades: 1 when started, eased down to 0 over <see cref="Seconds"/>. A tile lights up when
///     a new rep lands in it; a mod draws something at <see cref="Value"/>'s strength.
/// </summary>
internal sealed class Pulse {
    private float _left;

    public float Seconds { get; set; } = 0.6f;

    /// <summary>The flash's strength, from 1 down to 0.</summary>
    public float Value => Seconds <= 0f ? 0f : _left / Seconds * (_left / Seconds);

    public bool Active => _left > 0f;

    public void Start() => _left = Seconds;

    public void Update(float seconds) => _left = Math.Max(0f, _left - Math.Max(0f, seconds));
}

/// <summary>A rectangle easing to a new place and size, as <see cref="EasedValue"/> does for one number.</summary>
internal sealed class EasedArea {
    private readonly EasedValue _x = new(), _y = new(), _width = new(), _height = new();

    public EasedArea(Area area) => Snap(area);

    public Area Value => new(_x.Value, _y.Value, _width.Value, _height.Value);

    public Area Target {
        get => new(_x.Target, _y.Target, _width.Target, _height.Target);
        set {
            _x.Target = value.X;
            _y.Target = value.Y;
            _width.Target = value.Width;
            _height.Target = value.Height;
        }
    }

    /// <summary>The share of the way closed per second, as a rate: 12 is most of the way in a quarter second.</summary>
    public float Rate {
        get => _x.Rate;
        set => _x.Rate = _y.Rate = _width.Rate = _height.Rate = value;
    }

    public bool Moving => _x.Moving || _y.Moving || _width.Moving || _height.Moving;

    public void Snap(Area area) {
        _x.Snap(area.X);
        _y.Snap(area.Y);
        _width.Snap(area.Width);
        _height.Snap(area.Height);
    }

    public void Update(float seconds) {
        _x.Update(seconds);
        _y.Update(seconds);
        _width.Update(seconds);
        _height.Update(seconds);
    }
}

/// <summary>
///     Tiles that move to their new places when a wall is sorted or filtered, instead of jumping: the mod
///     says which tiles show and in what order (its sort keys are its statistics), a <see cref="GridPlan"/>
///     says where each place is, and every tile eases there. A tile filtered out fades where it stands; one
///     let back in fades in at its place.
/// </summary>
// Each tile is its own sketch in its own coordinates, drawn at Bounds(i) (moved, not rebuilt), so a sort
// rebuilds nothing. The first Arrange puts every tile in place at once.
internal sealed class ArrangedTiles {
    private readonly EasedArea[] _areas;
    private readonly EasedValue[] _opacity;
    private readonly int[] _place;
    private bool _placed;

    public ArrangedTiles(int count) {
        _areas = new EasedArea[count];
        _opacity = new EasedValue[count];
        _place = new int[count];
        for (int i = 0; i < count; i++) {
            _areas[i] = new EasedArea(default) { Rate = 12f };
            _opacity[i] = new EasedValue(0f) { Rate = 12f, Precision = 0.01f };
            _place[i] = -1;
        }
    }

    public int Count => _areas.Length;

    /// <summary>Where tile <paramref name="tile"/> is drawn this frame.</summary>
    public Area Bounds(int tile) => _areas[tile].Value;

    /// <summary>How visible tile <paramref name="tile"/> is: 0 once filtered out, 1 when shown.</summary>
    public float Opacity(int tile) => _opacity[tile].Value;

    /// <summary>Where the tile is in the order, or -1 when it is filtered out.</summary>
    public int PlaceOf(int tile) => _place[tile];

    /// <summary>Whether anything is still moving or fading.</summary>
    public bool Moving {
        get {
            for (int i = 0; i < _areas.Length; i++)
                if (_areas[i].Moving || _opacity[i].Moving) return true;
            return false;
        }
    }

    /// <summary>The tile under a point, among those shown, or -1.</summary>
    public int TileAt(float x, float y) {
        for (int i = 0; i < _areas.Length; i++)
            if (_place[i] >= 0 && _areas[i].Value.Contains(x, y)) return i;
        return -1;
    }

    /// <param name="order">The tiles to show, first place first; any tile not in it is filtered out.</param>
    /// <param name="plan">Where the places are: tile sizes and positions from <see cref="GridLayout.Plan"/>.</param>
    /// <param name="originX">The left of the grid's content.</param>
    /// <param name="originY">The top of the grid's content.</param>
    public void Arrange(IReadOnlyList<int> order, GridPlan plan, float originX, float originY) {
        for (int i = 0; i < _place.Length; i++) _place[i] = -1;
        for (int p = 0; p < order.Count; p++)
            if (order[p] >= 0 && order[p] < _place.Length) _place[order[p]] = p;

        for (int i = 0; i < _areas.Length; i++) {
            int place = _place[i];
            if (place < 0) {
                _opacity[i].Target = 0f;
                if (!_placed) _opacity[i].Snap(0f);
                continue;
            }
            (float x, float y) = plan.TileAt(place);
            Area target = new(originX + x, originY + y, plan.TileWidth, plan.TileHeight);
            // A tile coming back from hidden appears at its new place rather than flying from its old one.
            if (!_placed || _opacity[i].Value == 0f) _areas[i].Snap(target);
            else _areas[i].Target = target;
            _opacity[i].Target = 1f;
            if (!_placed) _opacity[i].Snap(1f);
        }
        _placed = true;
    }

    public void Update(float seconds) {
        for (int i = 0; i < _areas.Length; i++) {
            _areas[i].Update(seconds);
            _opacity[i].Update(seconds);
        }
    }
}

/// <summary>
///     A steady blink for something current, such as the room the player is in: <see cref="Value"/> goes from
///     1 down to <see cref="Low"/> and back every <see cref="Period"/> seconds, smoothly.
/// </summary>
internal sealed class Blink {
    private float _time;

    public float Period { get; set; } = 1f;

    /// <summary>The faintest it gets.</summary>
    public float Low { get; set; } = 0.25f;

    public float Value => Period <= 0f ? 1f : Low + (1f - Low) * (0.5f + 0.5f * MathF.Cos(2f * MathF.PI * _time / Period));

    /// <summary>Back to full strength: the current thing changed.</summary>
    public void Restart() => _time = 0f;

    public void Update(float seconds) {
        _time += Math.Max(0f, seconds);
        if (Period > 0f && _time >= Period) _time %= Period;
    }
}
