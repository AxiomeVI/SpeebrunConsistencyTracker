#nullable enable

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A chart saved as a PNG: an area of the HUD drawn again, layer by layer as the chart draws itself, into
///     an image of its own at HUD pixels, then written to a file.
/// </summary>
// ⚠️ From an Update, never from a Render: it opens its own sprite batch on its own render target, and a
// Render runs inside the HUD's batch, which cannot be nested. The render targets bound before are put back.
// It allocates an image and a file; that is what exporting is.
//
//     using (ChartPng png = new(panel, style.Panel)) {
//         png.Draw(chart);
//         png.Draw(data, 0f, 0f, plot);
//         png.Save(path);
//     }
internal sealed class ChartPng : IDisposable {
    private readonly GraphicsDevice _device;
    private readonly RenderTarget2D _target;
    private readonly RenderTargetBinding[] _previous;
    private readonly Area _area;
    private bool _open;

    /// <param name="area">The part of the HUD to save, in HUD pixels.</param>
    /// <param name="background">What shows where nothing is drawn: a panel's colour, since a PNG of a half-transparent panel reads badly.</param>
    public ChartPng(Area area, Rgba background) {
        _area = new Area(MathF.Round(area.X), MathF.Round(area.Y), Math.Max(1f, MathF.Round(area.Width)), Math.Max(1f, MathF.Round(area.Height)));
        _device = Engine.Graphics.GraphicsDevice;
        _target = new RenderTarget2D(_device, (int) _area.Width, (int) _area.Height);
        _previous = _device.GetRenderTargets();
        _device.SetRenderTarget(_target);
        // A see-through background is laid over black, as a panel over a dark level would read.
        _device.Clear(Color.Black);
        Monocle.Draw.SpriteBatch.Begin();
        Monocle.Draw.Rect(0f, 0f, _area.Width, _area.Height, GameCanvas.ToColor(background));
        _open = true;
    }

    /// <summary>A sketch where it was built, as <c>GameCanvas.Draw(sketch)</c> draws it.</summary>
    public void Draw(Sketch sketch) => Draw(sketch, 0f, 0f, _area);

    /// <summary>A sketch moved and cut, as <c>GameCanvas.Draw(sketch, dx, dy, window)</c> draws it on the HUD.</summary>
    public void Draw(Sketch sketch, float dx, float dy, Area window) {
        if (_open) GameCanvas.Draw(sketch, dx - _area.X, dy - _area.Y, window.Offset(-_area.X, -_area.Y));
    }

    /// <summary>Writes the image as a PNG, making its folder if needed.</summary>
    public void Save(string path) {
        End();
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        using FileStream file = File.Create(path);
        _target.SaveAsPng(file, _target.Width, _target.Height);
    }

    public void Dispose() {
        End();
        _target.Dispose();
    }

    private void End() {
        if (!_open) return;
        _open = false;
        Monocle.Draw.SpriteBatch.End();
        _device.SetRenderTargets(_previous);
    }
}
