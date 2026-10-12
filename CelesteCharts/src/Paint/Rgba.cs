#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     A colour, without the game's <c>Color</c> type, so the layers that decide what to draw run in
///     tests. The drawing layer turns it into the game's colour when it draws.
/// </summary>
// Premultiplied, as the game draws: fading scales all four channels, as FNA's Color * float does, so
// Rgba.White.Times(0.08f) is the same faint white a mod gets from Color.White * 0.08f.
internal readonly record struct Rgba(byte R, byte G, byte B, byte A) {
    public static readonly Rgba White = new(255, 255, 255);
    public static readonly Rgba Black = new(0, 0, 0);
    public static readonly Rgba LightGray = new(211, 211, 211);
    public static readonly Rgba Yellow = new(255, 255, 0);
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    /// <summary>A colour from channels of 0 to 255; values outside are clamped.</summary>
    public Rgba(int r, int g, int b, int a = 255) : this(Channel(r), Channel(g), Channel(b), Channel(a)) { }

    /// <summary>Every channel scaled by <paramref name="alpha"/>, from 0 (gone) to 1 (unchanged).</summary>
    public Rgba Times(float alpha) {
        alpha = Math.Clamp(alpha, 0f, 1f);
        return new Rgba((byte) (R * alpha + 0.5f), (byte) (G * alpha + 0.5f), (byte) (B * alpha + 0.5f), (byte) (A * alpha + 0.5f));
    }

    /// <summary>
    ///     A colour from a hex code as a mod saves it in its settings: <c>rrggbb</c> or <c>rrggbbaa</c>,
    ///     with or without a leading <c>#</c>. Anything else, a hand-edited setting say, reads as false.
    /// </summary>
    // Straight alpha in, premultiplied out: "ffffff80" is half-transparent white, as the game would draw it.
    public static bool TryParseHex(string? hex, out Rgba colour) {
        colour = default;
        if (hex == null) return false;
        ReadOnlySpan<char> digits = hex.AsSpan().Trim();
        if (digits.Length > 0 && digits[0] == '#') digits = digits[1..];
        if (digits.Length != 6 && digits.Length != 8) return false;
        foreach (char c in digits)
            if (!Uri.IsHexDigit(c)) return false;
        int r = Convert.ToInt32(digits[..2].ToString(), 16), g = Convert.ToInt32(digits[2..4].ToString(), 16);
        int b = Convert.ToInt32(digits[4..6].ToString(), 16), a = digits.Length == 8 ? Convert.ToInt32(digits[6..8].ToString(), 16) : 255;
        colour = new Rgba(r, g, b).Times(a / 255f);
        return true;
    }

    /// <summary>The colour of a hex code (<see cref="TryParseHex"/>), or <paramref name="fallback"/> when it is not one.</summary>
    public static Rgba FromHex(string? hex, Rgba fallback) => TryParseHex(hex, out Rgba colour) ? colour : fallback;

    /// <summary>
    ///     The <c>rrggbb</c> hex code of an opaque colour, lower case and without <c>#</c>, the form
    ///     CelesteMenuTools' colour buttons save; <c>rrggbbaa</c> when it is not opaque.
    /// </summary>
    public string ToHex() {
        if (A == 255) return $"{R:x2}{G:x2}{B:x2}";
        if (A == 0) return "00000000";
        // Back to straight alpha, the form a hex code is written in.
        float unpremultiply = 255f / A;
        return $"{Channel((int) (R * unpremultiply + 0.5f)):x2}{Channel((int) (G * unpremultiply + 0.5f)):x2}" +
               $"{Channel((int) (B * unpremultiply + 0.5f)):x2}{A:x2}";
    }

    /// <summary>
    ///     How bright it looks, from 0 to 1 (Rec. 709 weights on the channels as stored). For choosing
    ///     text that reads on it, and for checking that a ramp's brightness moves one way.
    /// </summary>
    public float Luma => (0.2126f * R + 0.7152f * G + 0.0722f * B) / 255f;

    /// <summary>Black or white, whichever reads better on this colour: for a value written inside a cell.</summary>
    public Rgba ContrastingText => Luma > 0.5f ? Black : White;

    // Its hex code. Also keeps the record's printer away from ContrastingText, an Rgba itself, which it
    // would print for ever.
    public override string ToString() => "#" + ToHex();

    /// <summary>Whether drawing it would change nothing on screen.</summary>
    public bool IsInvisible => A == 0 && R == 0 && G == 0 && B == 0;

    private static byte Channel(int value) => (byte) Math.Clamp(value, 0, 255);
}
