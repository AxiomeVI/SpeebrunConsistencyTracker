#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>An opacity every mark drawn is faded to, 0 to 1: the drawing layer's <c>GameCanvas.Fade</c>.</summary>
internal sealed class FadeSetting {
    private float _value = 1f;

    /// <summary>Clamped to 0 to 1; NaN reads as 1.</summary>
    public float Value {
        get => _value;
        set => _value = float.IsNaN(value) ? 1f : Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Sets the opacity until the returned scope is disposed, which puts back what was there: use it in a <c>using</c>.</summary>
    public FadeScope To(float opacity) {
        FadeScope scope = new(this, _value);
        Value = opacity;
        return scope;
    }
}

/// <summary>Puts a <see cref="FadeSetting"/> back to what it was when disposed. A struct: a <c>using</c> allocates nothing.</summary>
internal readonly struct FadeScope : IDisposable {
    private readonly FadeSetting? _setting;
    private readonly float _previous;

    internal FadeScope(FadeSetting setting, float previous) {
        _setting = setting;
        _previous = previous;
    }

    public void Dispose() {
        if (_setting != null) _setting.Value = _previous;
    }
}
