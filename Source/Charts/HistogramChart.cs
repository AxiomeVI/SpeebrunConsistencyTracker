using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.CelesteCharts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// The time histogram of one room or of the segment.
//
// The bin width is this mod's statistic (MetricHelper.HistogramBinWidth); the library cuts the bins
// at that width and places them. Everything a player reads as a number is formatted here.
internal sealed class HistogramChart : SketchChart
{
    private const int MaxBins   = 50;
    private const int MaxYTicks = 6;

    private readonly List<TimeTicks> _times;
    private readonly bool _isSegment;
    // The target time, read live; null for a room, which has none.
    private readonly Func<TimeTicks?> _targetSetting;
    private TimeTicks? _target;
    // Under the chart, where the scatter has its own.
    private readonly ButtonBar _foot = new(captureId: CaptureIds.HistogramFoot);
    private readonly BarSwitch _medianSwitch;
    private bool MedianOn => _medianSwitch.Choice == 0;
    private long _median;

    private Bins<long> _bins;
    private BinCounts _counts;

    private Bands _bands;
    private Scale<double> _y;

    public HistogramChart(string roomName, List<TimeTicks> times, bool isSegment = false, Func<TimeTicks?> target = null)
        : base(DialogText.Format(DialogIds.ChartHistogramTitleFmt, roomName))
    {
        _times         = times;
        _isSegment     = isSegment;
        _targetSetting = target;
        _foot.Caption(Dialog.Clean(DialogIds.ChartMedianLine));
        _foot.Space(4f);
        _medianSwitch        = _foot.Switch([Dialog.Clean(DialogIds.ChartOn), Dialog.Clean(DialogIds.ChartOff)]);
        _medianSwitch.Choice = ChartOptions.HistogramMedian ? 0 : 1;
        if (_times.Count == 0) return;
        TimeTicks[] sorted = [.. _times.OrderBy(t => t)];
        _median = MetricHelper.ComputePercentile(sorted, 50).Ticks;
        (_bins, _counts) = BinsOf(sorted);
    }

    protected override string TitleNote =>
        _times.Count == 0 ? null : RunCount(_times.Count, _isSegment);

    // A segment's times are those of the runs that completed it; a room's, of every run through it.
    internal static string RunCount(int count, bool segment) => segment
        ? DialogText.Count(count, DialogIds.ChartOneCompletedRun, DialogIds.ChartCompletedRunsFmt)
        : DialogText.Count(count, DialogIds.ChartOneRun, DialogIds.ChartRunsFmt);

    internal override ChartHelp Help => new(DialogIds.HintSubHistogram, DialogIds.HintAboutHistogram, DialogIds.HintMouseHistogram);

    protected override int LiveKey
    {
        get
        {
            _target = _targetSetting?.Invoke();
            return _target?.Ticks.GetHashCode() ?? -1;
        }
    }

    protected override bool UpdateOwnControls(Pointer pointer)
    {
        _foot.Update(pointer);
        if (_medianSwitch.Changed)
        {
            ChartOptions.HistogramMedian = MedianOn;
            Invalidate();
        }
        return _foot.Contains(pointer.X, pointer.Y);
    }

    protected override void DrawOwnControls() => GameCanvas.Draw(_foot.Sketch);

    // The bins of a set of times, ascending and not empty.
    internal static (Bins<long> Bins, BinCounts Counts) BinsOf(TimeTicks[] sorted)
    {
        // Counted from the fastest time, so the first bin starts on it.
        long fastest = sorted[0].Ticks;
        Bins<long> bins = Bins.Frames(Durations.Times, fastest, sorted[^1].Ticks, MetricHelper.HistogramBinWidth(sorted), MaxBins, origin: fastest);
        return (bins, bins.Count(sorted.Select(t => t.Ticks)));
    }

    protected override void Build(ChartStyle style)
    {
        _foot.Place(Sketch.Metrics, style, Panel.X + Panel.Width / 2f, Panel.Bottom - 6f, 0.5f, 1f, Panel);
        if (_bins == null)
        {
            DrawAxisLines();
            return;
        }

        int count = _bins.All.Count;
        Color fill = _isSegment ? ChartPalette.Current.SegmentFill : ChartPalette.Current.RoomFill;
        TextStyle small = SctChartStyle.Small(style);

        _y     = new Scale<double>(Numbers.Integers, 0, _counts.Tallest, Plot.Bottom, Plot.Y);
        _bands = new Bands(count, Plot.X, Math.Min(Plot.Width, count * ChartConstants.Layout.MaxBarWidth),
                           fill: 1f - ChartConstants.BarLayout.SingleBarSpacingRatio);

        double[] values = new double[count];
        string[] labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = _counts.Counts[i];
            // A bar too short for its count to sit clear of the axis carries none.
            labels[i] = Plot.Bottom - _y.At(values[i]) > ChartConstants.BarLayout.SingleBarLabelMinHeight
                ? _counts.Counts[i].ToString() : "";
        }

        Paint.YAxis(Sketch, Plot, _y.Ticks(MaxYTicks), style);
        Paint.Bars(Sketch, _bands, _y, values, 0.0, GameCanvas.ToRgba(fill));
        Paint.BarValues(Sketch, _bands, _y, values, 0.0, labels, small, ChartConstants.BarLayout.BarLabelOffsetY);
        DrawAxisLines();

        // A label under every bin edge, on two rows so neighbours do not run into each other.
        for (int i = 0; i <= count; i++)
        {
            float x      = MathF.Round(i == count ? _bands.BandStart(count - 1) + _bands.Width : _bands.BandStart(i));
            long edge    = i == count ? _bins.All[^1].Upper : _bins.All[i].Lower;
            bool lower   = i % 2 != 0;
            float tickH  = lower ? ChartConstants.XAxisLabel.TickOddLineEndY : ChartConstants.XAxisLabel.TickEvenLineEndY;
            float labelY = lower ? ChartConstants.XAxisLabel.TickOddLabelY   : ChartConstants.XAxisLabel.TickEvenLabelY;
            Sketch.Fill(new Area(x, Plot.Bottom, 1f, tickH), style.Axis);
            Sketch.Text(new TimeTicks(edge).ToString(), x, Plot.Bottom + labelY, small, alignX: 0.5f);
        }

        DrawLines(style, count);
    }

    // The median down the bars while its button is on, and the target on the segment's while the
    // target setting shows it, named with their values in a legend under the chart: a label on a line would cover a bar's count. The bins are equal, so
    // times run evenly from the first bar's left to the last one's right.
    private void DrawLines(ChartStyle style, int count)
    {
        Scale<long> x = new(Durations.Times, _bins.All[0].Lower, _bins.All[^1].Upper,
                            _bands.BandStart(0), _bands.BandStart(count - 1) + _bands.Width);
        Rgba targetColour = GameCanvas.ToRgba(ChartPalette.Current.Target);
        List<string> names = [];
        List<Rgba> colours = [];
        if (MedianOn)
        {
            // Thin and dashed, so it does not read as a bar's edge or cover its fill.
            Paint.ReferenceX(Sketch, Plot, x, _median, "", style with { Reference = Rgba.White, ReferenceThickness = 1f });
            names.Add(DialogText.LabelValue(Dialog.Clean(DialogIds.ChartStatMedian), new TimeTicks(_median)));
            colours.Add(Rgba.White);
        }
        if (_target is { Ticks: > 0 } target)
        {
            Paint.ReferenceX(Sketch, Plot, x, target.Ticks, "", style with { Reference = targetColour, Dash = 0f });
            names.Add(DialogText.Format(DialogIds.ChartTargetFmt, target));
            colours.Add(targetColour);
        }
        if (names.Count == 0) return;
        Paint.Legend(Sketch, Plot.Right - Paint.LegendWidth(Sketch, names, style),
                     Plot.Bottom + ChartConstants.Legend.LegendOffsetY, names, colours, style);
    }

    private Area BarOf(int bin) => Paint.Bar(_bands, bin, _y.At(0), _y.At(_counts.Counts[bin]));

    public override HoverInfo? HitTest(Vector2 mouseHudPos)
    {
        if (_bins == null) return null;
        EnsureBuilt();

        int bin = _bands.IndexAt(mouseHudPos.X);
        if (bin < 0 || _counts.Counts[bin] == 0) return null;
        Area bar = BarOf(bin);
        if (!bar.Contains(mouseHudPos.X, mouseHudPos.Y)) return null;

        return InfoOf(bin);
    }

    public override HoverInfo? Locate(string key)
    {
        if (_bins == null || key == null || !key.StartsWith(BinKey) || !long.TryParse(key.AsSpan(BinKey.Length), out long lower)) return null;
        EnsureBuilt();
        for (int bin = 0; bin < _bins.All.Count; bin++)
            if (_bins.All[bin].Lower == lower) return _counts.Counts[bin] > 0 ? InfoOf(bin) : null;
        return null;
    }

    // Keyed on where the bin starts: new times move the bins, and a pin must not slide to another range.
    private const string BinKey = "bucket:";

    private HoverInfo InfoOf(int bin)
    {
        Area bar = BarOf(bin);
        int inBin  = _counts.Counts[bin];
        double pct = 100.0 * inBin / _times.Count;
        // The range stays out of the dialog file: its loader reads "[...]" as a portrait tag.
        string runs  = DialogText.Format(inBin == 1 ? DialogIds.ChartBinOneRunFmt : DialogIds.ChartBinRunsFmt, inBin, pct.ToString("F1"));
        string label = $"{runs}\n[{new TimeTicks(_bins.All[bin].Lower)}, {new TimeTicks(_bins.All[bin].Upper)})";
        return new HoverInfo(label, new Vector2(bar.X + bar.Width / 2f, bar.Y), BinKey + _bins.All[bin].Lower, bin);
    }

    public override void DrawHighlight(HoverInfo info)
    {
        if (_bins != null && info.Mark >= 0 && info.Mark < _bins.All.Count) DrawOutline(BarOf(info.Mark));
    }
}
