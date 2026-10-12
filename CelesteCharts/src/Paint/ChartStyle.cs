#nullable enable

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     How charts look: colours, text sizes and spacing, for every painter at once. A mod starts from
///     <see cref="Default"/> and changes what it wants with <c>with { ... }</c>.
/// </summary>
// The default look: a dark
// panel, faint grid lines, grey axes and labels, cornflower-blue data, gold medians.
internal sealed record ChartStyle {
    public static readonly ChartStyle Default = new();

    /// <summary>
    ///     For players who find the default faint: an opaque panel, white labels, stronger grid and axes, thicker
    ///     lines and bigger points.
    /// </summary>
    public static readonly ChartStyle HighContrast = new() {
        Panel = Rgba.Black.Times(0.95f),
        Grid = Rgba.White.Times(0.16f),
        Axis = new Rgba(180, 180, 180),
        Data = new Rgba(120, 180, 255),
        Label = new TextStyle(0.35f, Rgba.White),
        LineThickness = 3f,
        PointSize = 8f,
    };

    /// <summary>
    ///     Every colour from the Okabe-Ito palette, which stays apart with every common colour blindness: sky blue
    ///     data, orange for the accent and medians, bluish green references, vermillion outliers.
    /// </summary>
    public static readonly ChartStyle ColourBlind = new() {
        Data = new Rgba(86, 180, 233),
        BoxFill = new Rgba(86, 180, 233).Times(0.5f),
        Accent = new Rgba(230, 159, 0),
        Median = new Rgba(230, 159, 0),
        Reference = new Rgba(0, 158, 115),
        Outlier = new Rgba(213, 94, 0),
    };

    /// <summary>
    ///     The same style at another size: every text, gap, tick, line and point times <paramref name="factor"/>
    ///     (1.25 for a player who sits far from the screen). Layouts that measure their labels follow; sizes a mod
    ///     fixes itself (a tile's height) are the mod's to scale.
    /// </summary>
    public ChartStyle Scaled(float factor) {
        if (factor <= 0f || factor == 1f) return this;
        return this with {
            Title = Title with { Scale = Title.Scale * factor },
            Label = Label with { Scale = Label.Scale * factor },
            Tooltip = Tooltip with { Scale = Tooltip.Scale * factor },
            Header = Header with { Scale = Header.Scale * factor },
            Stat = Stat with { Scale = Stat.Scale * factor },
            LineThickness = System.MathF.Max(1f, System.MathF.Round(LineThickness * factor)),
            AxisThickness = System.MathF.Max(1f, System.MathF.Round(AxisThickness * factor)),
            ReferenceThickness = System.MathF.Max(1f, System.MathF.Round(ReferenceThickness * factor)),
            PointSize = System.MathF.Round(PointSize * factor),
            Dash = System.MathF.Round(Dash * factor),
            LabelGap = System.MathF.Round(LabelGap * factor),
            TickLength = System.MathF.Round(TickLength * factor),
            LabelSpacing = System.MathF.Round(LabelSpacing * factor),
        };
    }

    /// <summary>
    ///     The same style with every text outlined: readable over a busy level with a faint panel or none. An
    ///     outline costs several extra quads per glyph, every frame.
    /// </summary>
    public ChartStyle Outlined(float stroke = 2f, Rgba? colour = null) {
        Rgba c = colour ?? Rgba.Black;
        return this with {
            Title = Title with { Stroke = stroke, StrokeColour = c },
            Label = Label with { Stroke = stroke, StrokeColour = c },
            Tooltip = Tooltip with { Stroke = stroke, StrokeColour = c },
            Header = Header with { Stroke = stroke, StrokeColour = c },
            Stat = Stat with { Stroke = stroke, StrokeColour = c },
        };
    }

    public Rgba Panel { get; init; } = Rgba.Black.Times(0.8f);
    public Rgba Grid { get; init; } = Rgba.White.Times(0.08f);
    public Rgba Axis { get; init; } = new(110, 110, 110);

    /// <summary>A data mark: a bar, a box, a sample.</summary>
    public Rgba Data { get; init; } = new(100, 149, 237);

    /// <summary>The data mark under the mouse.</summary>
    public Rgba Hovered { get; init; } = Rgba.White;

    /// <summary>A second series drawn over the data: a rolling median, a trend.</summary>
    public Rgba Accent { get; init; } = new(255, 215, 0);

    /// <summary>A reference line and its label: a target, a threshold, a personal best.</summary>
    public Rgba Reference { get; init; } = new(64, 224, 208);

    /// <summary>
    ///     Colours for several series side by side, in order: the Okabe-Ito palette (less its black),
    ///     chosen to stay apart with every common colour blindness.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Rgba> Series { get; init; } = [
        new(86, 180, 233), new(230, 159, 0), new(0, 158, 115), new(240, 228, 66),
        new(0, 114, 178), new(213, 94, 0), new(204, 121, 167),
    ];

    /// <summary>
    ///     Colours for ordered categories, mildest first (a report's bands: fine, late, very late), each darker than
    ///     the one before, so the order still reads in greyscale and with any colour blindness. A test holds
    ///     it for every built-in style.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Rgba> Ordered { get; init; } = [
        new(255, 237, 160), new(254, 178, 76), new(240, 59, 32), new(160, 0, 38),
    ];

    public Rgba BoxFill { get; init; } = new Rgba(100, 149, 237).Times(0.5f);
    public Rgba Median { get; init; } = new(255, 215, 0);
    public Rgba Outlier { get; init; } = new(255, 69, 0);

    /// <summary>The mouse cursor: SpeebrunConsistencyTracker's yellow.</summary>
    public Rgba Cursor { get; init; } = Rgba.Yellow;

    /// <summary>A one-pixel edge round the cursor; transparent (none) by default, as SCT draws it.</summary>
    public Rgba CursorOutline { get; init; } = Rgba.Transparent;

    /// <summary>A tooltip's backing, dark enough that a bar behind it does not show through.</summary>
    public Rgba TooltipBack { get; init; } = Rgba.Black.Times(0.9f);

    /// <summary>Pixels between a tooltip's text and its backing's left and right edges.</summary>
    public float TooltipPadX { get; init; } = 4f;

    /// <summary>Pixels between a tooltip's text and its backing's top and bottom edges.</summary>
    public float TooltipPadY { get; init; }

    /// <summary>Pixels between two columns of a tooltip (a tab in its text starts a column).</summary>
    public float TooltipColumnGap { get; init; } = 12f;

    /// <summary>Where a tooltip's lines sit when it has several: 0 against the left, 0.5 centred, 1 against the right.</summary>
    public float TooltipAlign { get; init; }

    public TextStyle Title { get; init; } = new(0.45f, Rgba.White);
    public TextStyle Label { get; init; } = new(0.35f, Rgba.LightGray);
    public TextStyle Tooltip { get; init; } = new(0.35f, Rgba.Yellow);

    /// <summary>A table's header row.</summary>
    public TextStyle Header { get; init; } = new(0.35f, Rgba.White);

    /// <summary>The largest a stat tile's number grows, and its colour; it shrinks from this scale to fit its tile.</summary>
    public TextStyle Stat { get; init; } = new(1.4f, Rgba.White);

    /// <summary>
    ///     An axis line's width in pixels, for <c>Paint.YAxis</c>, <c>XAxis</c> and <c>YAxisRight</c>. One pixel can
    ///     vanish in a window smaller than 1920x1080 unless the chart is drawn through <c>ChartLayer</c>.
    /// </summary>
    public float AxisThickness { get; init; } = 1f;

    /// <summary>
    ///     A reference line's thickness in pixels (rounded, at least 1), dashed or solid, and a reference band's edges. It
    ///     stays centred on its pixel: an odd thickness covers that pixel and an equal number either side, an even one
    ///     straddles its left or top edge. A <c>hot</c> line is one pixel thicker.
    /// </summary>
    public float ReferenceThickness { get; init; } = 1f;

    /// <summary>A line series' width in pixels.</summary>
    public float LineThickness { get; init; } = 2f;

    /// <summary>A point's side in pixels.</summary>
    public float PointSize { get; init; } = 6f;

    /// <summary>A reference line's dash and gap length; 0 draws it solid.</summary>
    public float Dash { get; init; } = 8f;

    /// <summary>Pixels between a label and the axis or tick it names.</summary>
    public float LabelGap { get; init; } = 8f;

    /// <summary>How far a tick mark reaches out of the plot.</summary>
    public float TickLength { get; init; } = 6f;

    /// <summary>The least space between two labels in a row; closer ones are thinned out.</summary>
    public float LabelSpacing { get; init; } = 6f;
}
