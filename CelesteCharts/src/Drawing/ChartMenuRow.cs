#nullable enable

using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A chart as a row of a menu, under the slider it illustrates: a threshold slider with its
///     histogram, the divider sliding as the slider moves. Not selectable; the menu steps over it.
/// </summary>
// Two sketches, both in the row's own coordinates from (0, 0): the chart, built by the mod's function
// when the row is first drawn and again after Invalidate or a change of width; and a moving part, cleared
// and handed to the mod's function every frame with the real seconds that passed (an EasedValue's step), for a
// divider that slides without rebuilding the chart. Both fade with the menu.
//
// A width of 0 takes the menu's own width and leaves the row out of the menu's measuring, so a chart
// never widens the menu it sits in.
internal sealed class ChartMenuRow : TextMenu.Item {
    private readonly float _width;
    private readonly float _height;
    private readonly Action<Sketch, float, float> _build;
    private readonly Action<Sketch, float>? _animate;
    private Sketch? _chart, _moving;
    private float _builtWidth = -1f;

    /// <param name="width">The chart's width in pixels; 0 for the menu's width.</param>
    /// <param name="height">The row's height in pixels.</param>
    /// <param name="build">Paints the chart into a cleared sketch, given its width and height.</param>
    /// <param name="animate">Paints the moving part into a cleared sketch every frame, given the seconds since the last.</param>
    public ChartMenuRow(float width, float height, Action<Sketch, float, float> build, Action<Sketch, float>? animate = null) {
        _width = width;
        _height = height;
        _build = build;
        _animate = animate;
        Selectable = false;
        IncludeWidthInMeasurement = width > 0f;
    }

    /// <summary>Rebuilds the chart on the next frame it is drawn: its data or its look changed.</summary>
    public void Invalidate() => _builtWidth = -1f;

    public override float LeftWidth() => _width;

    public override float Height() => _height;

    public override void Render(Vector2 position, bool highlighted) {
        float width = _width > 0f ? _width : Container?.Width ?? 0f;
        if (width <= 0f) return;
        // Here, not in the constructor: measuring text needs the game's fonts, and a menu may be built first.
        _chart ??= new Sketch(GameCanvas.FontMetrics());
        _moving ??= new Sketch(_chart.Metrics);
        if (width != _builtWidth) {
            _builtWidth = width;
            _chart.Clear();
            _build(_chart, width, _height);
        }

        float x = MathF.Round(position.X), y = MathF.Round(position.Y - _height / 2f);
        Area area = new(x, y, width, _height);
        float alpha = Container?.Alpha ?? 1f;
        GameCanvas.Draw(_chart, x, y, area, alpha);
        if (_animate == null) return;
        _moving.Clear();
        _animate(_moving, Engine.RawDeltaTime);
        GameCanvas.Draw(_moving, x, y, area, alpha);
    }
}
