using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// Two series per room as bars: side by side on a time axis (time loss), side by side on a percent
// axis (reset rate and runs alive), or stacked on a percent axis (problem rooms). Every value is
// computed and formatted by the caller's side of the mod; this places them.

internal sealed class BarsChart : SketchChart
{
    private enum Shape { GroupedTime, GroupedPercent, StackedPercent }

    private const float InnerFill = 0.95f;
    // A value too wide for its bar shrinks this far (0.22 against 0.3), then is left out.
    private const float ValueShrink = ChartConstants.FontScale.BarValueTiny / ChartConstants.FontScale.AxisLabelSmall;

    private readonly Shape _shape;
    private readonly IReadOnlyList<string> _labels;
    private readonly long[] _primaryTicks, _secondaryTicks;
    private readonly double[] _primary;
    private double[] _secondary;
    private readonly string[] _primaryText;
    private string[] _secondaryText;
    private readonly string _primaryLabel;
    private string _secondaryLabel;

    // Problem rooms only: the time loss threshold, in frames, set from under the chart. What the
    // chart shows for a threshold comes from the caller, as its first values did.
    internal readonly record struct Retuned(string Title, List<double> Secondary, string SecondaryLabel);
    internal const int MinThresholdFrames = 1, MaxThresholdFrames = 118, FrameMs = 17;
    private const float SliderWidth = 260f;
    private readonly ButtonBar _foot;
    private readonly BarSlider _threshold;
    private readonly BarCaption _thresholdValue;
    private readonly Func<int, Retuned> _retune;
    private bool _unsaved;

    // As Mod Options writes it: frames, then seconds.
    internal static string ThresholdText(int frames) =>
        $"{frames}f / {(frames * FrameMs / 1000.0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}s";
    internal override ChartHelp Help => _shape switch
    {
        Shape.GroupedTime    => new(DialogIds.HintSubTimeLoss, DialogIds.HintAboutTimeLoss, DialogIds.HintMouseBars),
        Shape.GroupedPercent => new(DialogIds.HintSubResets, DialogIds.HintAboutResets, DialogIds.HintMouseBars),
        _                    => new(DialogIds.HintSubProblem, DialogIds.HintAboutProblem, DialogIds.HintMouseBars),
    };

    private HideableBands _bands;
    private Area[] _primaryBar = [], _secondaryBar = [];

    private int Count => _labels.Count;

    public static BarsChart Time(string title, List<string> labels, List<long> primary, List<long> secondary,
                                 string primaryLabel, string secondaryLabel)
    {
        // No TrimStart('0'): it turned 0.250 into ".250", which reads as a missing digit.
        static string Text(long ticks) => "+" + new TimeTicks(ticks);
        return new BarsChart(Shape.GroupedTime, title, labels, primaryLabel, secondaryLabel,
            [.. primary], [.. secondary], null, null, [.. primary.Select(Text)], [.. secondary.Select(Text)]);
    }

    public static BarsChart Percent(string title, List<string> labels, List<float> primary, List<float> secondary,
                                    string primaryLabel, string secondaryLabel) =>
        new(Shape.GroupedPercent, title, labels, primaryLabel, secondaryLabel, null, null,
            [.. primary.Select(v => (double)v)], [.. secondary.Select(v => (double)v)],
            [.. primary.Select(v => $"{v:0.#}%")], [.. secondary.Select(v => $"{v:0.#}%")]);

    public static BarsChart Stacked(List<string> labels, List<double> primary, string primaryLabel,
                                    int thresholdFrames, Func<int, Retuned> retune)
    {
        Retuned first = retune(thresholdFrames);
        return new(Shape.StackedPercent, first.Title, labels, primaryLabel, first.SecondaryLabel, null, null,
            [.. primary], [.. first.Secondary],
            [.. primary.Select(Share)], [.. first.Secondary.Select(Share)], thresholdFrames, retune);
    }

    private static string Share(double percent) => $"{percent:0.#}%";

    private BarsChart(Shape shape, string title, List<string> labels, string primaryLabel, string secondaryLabel,
                      long[] primaryTicks, long[] secondaryTicks, double[] primary, double[] secondary,
                      string[] primaryText, string[] secondaryText,
                      int thresholdFrames = 0, Func<int, Retuned> retune = null) : base(title)
    {
        if (retune != null)
        {
            _retune = retune;
            _foot   = new ButtonBar(captureId: CaptureIds.ThresholdFoot);
            _foot.Caption(Dialog.Clean(DialogIds.ChartThreshold));
            _foot.Space(4f);
            _threshold = _foot.Slider(MinThresholdFrames, MaxThresholdFrames, SliderWidth);
            _threshold.Value = thresholdFrames;
            _foot.Space(4f);
            _thresholdValue = _foot.Caption(ThresholdText(_threshold.Value));
        }
        _shape          = shape;
        _labels         = labels;
        _primaryLabel   = primaryLabel;
        _secondaryLabel = secondaryLabel;
        _primaryTicks   = primaryTicks;
        _secondaryTicks = secondaryTicks;
        _primary        = primary;
        _secondary      = secondary;
        _primaryText    = primaryText;
        _secondaryText  = secondaryText;
    }

    protected override void Build(ChartStyle style)
    {
        if (_foot != null)
        {
            // Centred with its widest value, then held by its left end: the track must not move
            // under the mouse as the value's text changes width.
            _thresholdValue.Text = ThresholdText(MaxThresholdFrames);
            _foot.Place(Sketch.Metrics, style, Panel.X + Panel.Width / 2f, Panel.Bottom - 6f, 0.5f, 1f, Panel);
            float left = _foot.Bounds.X;
            _thresholdValue.Text = ThresholdText(_threshold.Value);
            _foot.Place(Sketch.Metrics, style, left, Panel.Bottom - 6f, 0f, 1f, Panel);
        }
        _primaryBar   = new Area[Count];
        _secondaryBar = new Area[Count];

        bool[] hidden = HiddenFlags(Count);
        int hiddenCount = hidden.Count(h => h), shown = Count - hiddenCount;
        const float stub = ChartConstants.Interactivity.HiddenColumnStubWidth;
        float length = Math.Min(Plot.Width, shown * ChartConstants.Layout.MaxBarWidth + hiddenCount * stub);
        _bands = new HideableBands(Count, Plot.X, length, hidden, stub,
            fill: 1f - ChartConstants.BarLayout.GroupSpacingRatio, wholePixels: false, minWidth: ChartConstants.Layout.MinColumnWidth);
        PlaceColumns(Plot.X, Plot.Width, _bands.ContentLength, wholePlot: true);

        Rgba[] colours = [GameCanvas.ToRgba(ChartPalette.Current.PrimaryFill), GameCanvas.ToRgba(ChartPalette.Current.SecondaryFill)];
        ChartStyle axis = style;
        TextStyle small = SctChartStyle.Small(style);

        if (_shape == Shape.GroupedTime)
        {
            long most = 0;
            for (int i = 0; i < Count; i++)
                if (!hidden[i]) most = Math.Max(most, Math.Max(_primaryTicks[i], _secondaryTicks[i]));
            // Headroom for the value over the tallest bar, in whole frames.
            long frame = Durations.CelesteFrameTicks;
            long top = Math.Max(frame, (most + most / 12 + frame - 1) / frame * frame);
            Scale<long> y = new(Durations.Times, 0, top, Plot.Bottom, Plot.Y);
            Paint.YAxis(Sketch, Plot, y.Ticks(ChartConstants.Axis.MaxTickMarks), axis);
            if (most > 0) Grouped(y, _primaryTicks, _secondaryTicks, 0L, colours, small);
        }
        else
        {
            Scale<double> y = new(Numbers.Percent, 0, 100, Plot.Bottom, Plot.Y);
            Paint.YAxis(Sketch, Plot, y.Ticks(ChartConstants.Axis.PercentTickCount + 1), axis);
            if (_shape == Shape.GroupedPercent) Grouped(y, _primary, _secondary, 0.0, colours, small);
            else Stacked(y, colours, small);
        }
        DrawHiddenStubs(_bands);
        DrawAxisLines();

        ChartStyle names = style;
        // One row while the columns scroll: the bar is under the names.
        Paint.BandLabels(Scrolled, _bands, _labels, Plot.Bottom, names, Columns.Scrolls ? LabelFit.Shorten : LabelFit.Stagger);
        string[] legend = [_primaryLabel, _secondaryLabel];
        Paint.Legend(Sketch, Plot.Right - Paint.LegendWidth(Sketch, legend, names),
                     Plot.Bottom + ChartConstants.Legend.LegendOffsetY, legend, colours, names);
    }

    private void Grouped<T>(Scale<T> y, T[] primary, T[] secondary, T zero, Rgba[] colours, TextStyle small)
    {
        float bottom = y.At(zero);
        Paint.GroupedBars(Scrolled, _bands, y, [primary, secondary], zero, colours, InnerFill);

        // A value over each bar; one across the pair when the two are the same, none over no bar.
        string[] one = new string[Count], first = new string[Count], second = new string[Count];
        T[] pair = new T[Count];
        for (int i = 0; i < Count; i++)
        {
            Bands inner = Bands.Within(_bands, i, 2, InnerFill);
            _primaryBar[i]   = Paint.Bar(inner, 0, bottom, y.At(primary[i]));
            _secondaryBar[i] = Paint.Bar(inner, 1, bottom, y.At(secondary[i]));
            bool hasFirst = !_primaryBar[i].IsEmpty, hasSecond = !_secondaryBar[i].IsEmpty;
            bool same = hasFirst && hasSecond && EqualityComparer<T>.Default.Equals(primary[i], secondary[i]);
            pair[i]   = primary[i];
            one[i]    = same ? _primaryText[i] : "";
            first[i]  = hasFirst && !same ? _primaryText[i] : "";
            second[i] = hasSecond && !same ? _secondaryText[i] : "";
        }
        float gap = ChartConstants.BarLayout.BarLabelOffsetY;
        Paint.GroupedBarValues(Scrolled, _bands, y, [primary, secondary], zero, [first, second], small, InnerFill, gap, ValueShrink);
        Paint.BarValues(Scrolled, _bands, y, pair, zero, one, small, gap, ValueShrink);
    }

    private void Stacked(Scale<double> y, Rgba[] colours, TextStyle small)
    {
        double[] totals = new double[Count];
        string[] texts  = new string[Count];
        float bottom = y.At(0);
        for (int i = 0; i < Count; i++)
        {
            totals[i] = _primary[i] + _secondary[i];
            Scrolled.Fill(Paint.Bar(_bands, i, bottom, y.At(_primary[i])), colours[0]);
            Scrolled.Fill(Paint.Bar(_bands, i, y.At(_primary[i]), y.At(totals[i])), colours[1]);
            _primaryBar[i] = Paint.Bar(_bands, i, bottom, y.At(totals[i]));
            texts[i] = _primaryBar[i].Height > ChartConstants.BarLayout.StackedLabelMinHeight ? $"{totals[i]:0.#}%" : "";
        }
        Paint.BarValues(Scrolled, _bands, y, totals, 0.0, texts, small, ChartConstants.BarLayout.BarLabelOffsetY);
    }

    protected override void DrawOver() => DrawStrips(_bands, Shift, Plot.X, Plot.Right);

    protected override bool UpdateOwnControls(Pointer pointer)
    {
        if (_foot == null) return UpdateColumns(pointer);
        _foot.Update(pointer);
        if (_threshold.Changed)
        {
            int frames = _threshold.Value;
            SpeebrunConsistencyTrackerModule.Settings.TimeLossThresholdMs = frames * FrameMs;
            Retuned now = _retune(frames);
            title           = now.Title;
            _secondary      = [.. now.Secondary];
            _secondaryText  = [.. now.Secondary.Select(Share)];
            _secondaryLabel = now.SecondaryLabel;
            _thresholdValue.Text = ThresholdText(frames);
            _unsaved = true;
            Invalidate();
        }
        // Once, when the drag is over however it ended: a save writes the settings file.
        if (_unsaved && !_threshold.Dragging)
        {
            _unsaved = false;
            SpeebrunConsistencyTrackerModule.Instance.SaveSettings();
        }
        return _threshold.Dragging | _foot.Contains(pointer.X, pointer.Y) | UpdateColumns(pointer);
    }

    protected override void DrawOwnControls()
    {
        if (_foot != null) GameCanvas.Draw(_foot.Sketch);
    }

    public override int? ColumnHitTest(Vector2 mousePos)
    {
        EnsureBuilt();
        return StripAt(_bands, mousePos, Shift, Plot.X, Plot.Right);
    }

    // The bars are where they were built; the mouse is on one at its x plus the scroll.
    public override HoverInfo? HitTest(Vector2 mouseHudPos)
    {
        EnsureBuilt();
        if (!Plot.Contains(mouseHudPos.X, mouseHudPos.Y)) return null;
        float x = mouseHudPos.X + Shift;
        int i = _bands.IndexAt(x);
        if (i < 0 || _bands.IsHidden(i)) return null;

        bool onPrimary   = _primaryBar[i].Contains(x, mouseHudPos.Y);
        bool onSecondary = !onPrimary && _secondaryBar[i].Contains(x, mouseHudPos.Y);
        return onPrimary || onSecondary ? InfoOf(i, onPrimary) : null;
    }

    private const string StackKey = "bar:", GroupKey = "group:";

    public override HoverInfo? Locate(string key)
    {
        if (key == null) return null;
        EnsureBuilt();
        bool stacked = key.StartsWith(StackKey);
        if (!stacked && !key.StartsWith(GroupKey)) return null;
        string[] parts = key.Split(':');
        if (!int.TryParse(parts[1], out int i) || i < 0 || i >= Count || _bands.IsHidden(i)) return null;
        bool primary = stacked || (parts.Length > 2 && parts[2] == "p");
        Area bar = primary ? _primaryBar[i] : _secondaryBar[i];
        float centre = bar.X + bar.Width / 2f - Shift;
        return bar.IsEmpty || centre < Plot.X || centre > Plot.Right ? null : InfoOf(i, primary);
    }

    private HoverInfo InfoOf(int i, bool primary)
    {
        Area bar = primary ? _primaryBar[i] : _secondaryBar[i];
        string label, key;
        if (_shape == Shape.StackedPercent)
        {
            label = DialogText.LabelValue(_secondaryLabel, _secondaryText[i]) + "\n" + DialogText.LabelValue(_primaryLabel, _primaryText[i]);
            key   = StackKey + i;
        }
        else
        {
            // Keyed on the group and which bar: two bars whose tooltips read the same text
            // ("Remaining (%): 100%") are two pins.
            label = primary ? DialogText.LabelValue(_primaryLabel, _primaryText[i]) : DialogText.LabelValue(_secondaryLabel, _secondaryText[i]);
            key   = $"{GroupKey}{i}:{(primary ? "p" : "s")}";
        }
        return new HoverInfo(label, new Vector2(bar.X + bar.Width / 2f - Shift, bar.Y), key, 2 * i + (primary ? 0 : 1));
    }

    public override void DrawHighlight(HoverInfo info)
    {
        int i = info.Mark / 2;
        if (info.Mark >= 0 && i < Count) DrawOutline(info.Mark % 2 == 0 ? _primaryBar[i] : _secondaryBar[i]);
    }
}
