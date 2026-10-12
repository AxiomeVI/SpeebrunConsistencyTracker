#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A number that eases towards a target instead of jumping to it: a divider sliding as its slider
///     moves, a frame growing into place. Set <see cref="Target"/>, call <see cref="Update"/> every frame
///     with the real seconds that passed, and draw at <see cref="Value"/>.
/// </summary>
// Exponential: each second closes the same share of what is left (Rate), so a target that moves
// mid-way is followed smoothly from wherever the value is, and it lands within Precision rather than
// creeping for ever. Time is real seconds (PointerInput.Seconds, Engine.RawDeltaTime), so it runs in a
// paused or frozen level. Pure and allocation-free.
internal sealed class EasedValue {
    public EasedValue(float value = 0f) => Value = Target = value;

    public float Value { get; private set; }
    public float Target { get; set; }

    /// <summary>How fast it closes in: the rate per second of an exponential. 14 is most of the way in a fifth of a second.</summary>
    public float Rate { get; set; } = 14f;

    /// <summary>How near the target counts as there; it then lands exactly.</summary>
    public float Precision { get; set; } = 0.25f;

    /// <summary>Whether it is still on its way.</summary>
    public bool Moving => Value != Target;

    /// <summary>Goes straight to <paramref name="value"/>: the first placement, which should not slide in from 0.</summary>
    public void Snap(float value) => Value = Target = value;

    /// <summary>Moves one frame on; returns whether the value changed.</summary>
    public bool Update(float seconds) {
        if (Value == Target) return false;
        Value += (Target - Value) * (1f - MathF.Exp(-Rate * Math.Max(0f, seconds)));
        if (Math.Abs(Target - Value) <= Precision) Value = Target;
        return true;
    }
}
