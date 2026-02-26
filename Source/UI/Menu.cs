using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static class ModMenuOptions
{
    private static readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;
    private static readonly SpeebrunConsistencyTrackerModule _instance = SpeebrunConsistencyTrackerModule.Instance;

    private const string ConfirmSfx = "event:/ui/main/button_select";

    private static readonly MetricOutputChoice[] AllChoices = Enum.GetValues<MetricOutputChoice>();

    // ---------------------------------------------------------------------------
    // Metric definitions — drives sliders AND the Turn All Off / On / Reset buttons
    // ---------------------------------------------------------------------------

    private record MetricDef(
        string LabelKey,
        MetricOutputChoice[] Choices,
        Func<MetricOutputChoice> Get,
        Action<MetricOutputChoice> Set,
        MetricOutputChoice DefaultValue);

    private static List<MetricDef> BuildMetricDefs() =>
    [
        new(DialogIds.ConsistencyScoreId,       AllChoices, () => _settings.ConsistencyScore,        v => _settings.ConsistencyScore = v,        MetricOutputChoice.Export),
        new(DialogIds.SuccessRateId,            AllChoices, () => _settings.SuccessRate,             v => _settings.SuccessRate = v,             MetricOutputChoice.Both),
        new(DialogIds.TargetTimeStatId,         AllChoices, () => _settings.TargetTime,              v => _settings.TargetTime = v,              MetricOutputChoice.Export),
        new(DialogIds.CompletedRunCountId,      AllChoices, () => _settings.CompletedRunCount,       v => _settings.CompletedRunCount = v,       MetricOutputChoice.Both),
        new(DialogIds.TotalRunCountId,          AllChoices, () => _settings.TotalRunCount,           v => _settings.TotalRunCount = v,           MetricOutputChoice.Both),
        new(DialogIds.DnfCountId,               AllChoices, () => _settings.DnfCount,                v => _settings.DnfCount = v,                MetricOutputChoice.Export),
        new(DialogIds.AverageId,                AllChoices, () => _settings.Average,                 v => _settings.Average = v,                 MetricOutputChoice.Both),
        new(DialogIds.MedianId,                 AllChoices, () => _settings.Median,                  v => _settings.Median = v,                  MetricOutputChoice.Both),
        new(DialogIds.MadID,                    AllChoices, () => _settings.MedianAbsoluteDeviation, v => _settings.MedianAbsoluteDeviation = v, MetricOutputChoice.Export),
        new(DialogIds.RelMadID,                 AllChoices, () => _settings.RelativeMAD,             v => _settings.RelativeMAD = v,             MetricOutputChoice.Export),
        new(DialogIds.ResetRateId,              AllChoices, () => _settings.ResetRate,               v => _settings.ResetRate = v,               MetricOutputChoice.Export),
        new(DialogIds.MinimumId,                AllChoices, () => _settings.Minimum,                 v => _settings.Minimum = v,                 MetricOutputChoice.Export),
        new(DialogIds.MaximumId,                AllChoices, () => _settings.Maximum,                 v => _settings.Maximum = v,                 MetricOutputChoice.Export),
        new(DialogIds.StandardDeviationId,      AllChoices, () => _settings.StandardDeviation,       v => _settings.StandardDeviation = v,       MetricOutputChoice.Both),
        new(DialogIds.CoefficientOfVariationId, AllChoices, () => _settings.CoefficientOfVariation,  v => _settings.CoefficientOfVariation = v,  MetricOutputChoice.Export),
        new(DialogIds.PercentileId,             AllChoices, () => _settings.Percentile,              v => _settings.Percentile = v,              MetricOutputChoice.Export),
        new(DialogIds.InterquartileRangeId,     AllChoices, () => _settings.InterquartileRange,      v => _settings.InterquartileRange = v,      MetricOutputChoice.Export),
        new(DialogIds.LinearRegressionId,       AllChoices, () => _settings.LinearRegression,        v => _settings.LinearRegression = v,        MetricOutputChoice.Export),
        new(DialogIds.SoBId,                    AllChoices, () => _settings.SoB,                     v => _settings.SoB = v,                     MetricOutputChoice.Both),
    ];

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TextMenu.Slider MetricSlider(MetricDef def)
    {
        var slider = new TextMenu.Slider(
            Dialog.Clean(def.LabelKey),
            i => def.Choices[i].ToString(),
            0,
            def.Choices.Length - 1,
            Array.IndexOf(def.Choices, def.Get()));
        slider.Change(v => def.Set(def.Choices[v]));
        return slider;
    }

    private static string GetTargetTime() =>
        $"{_settings.Minutes}:{_settings.Seconds:D2}.{_settings.MillisecondsFirstDigit}{_settings.MillisecondsSecondDigit}{_settings.MillisecondsThirdDigit}";

    // ---------------------------------------------------------------------------
    // Public entry point
    // ---------------------------------------------------------------------------

    public static void CreateMenu(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu targetTimeSubMenu  = CreateTargetTimeSubMenu(menu, inGame);
        TextMenuExt.SubMenu textOverlaySubMenu  = CreateTextOverlaySubMenu(menu);
        TextMenuExt.SubMenu graphOverlaySubMenu = CreateGraphOverlaySubMenu(menu);
        TextMenuExt.SubMenu metricsSubMenu      = CreateMetricsSubMenu(menu);
        TextMenuExt.SubMenu exportSubMenu       = CreateExportSubMenu(menu, inGame);

        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled).Change(value =>
        {
            _settings.Enabled = value;
            exportSubMenu.Visible      = value;
            targetTimeSubMenu.Visible  = value;
            textOverlaySubMenu.Visible = value;
            graphOverlaySubMenu.Visible = value;
            metricsSubMenu.Visible     = value;
            if (!value) SpeebrunConsistencyTrackerModule.Clear();
        }));

        menu.Add(targetTimeSubMenu);
        menu.Add(exportSubMenu);
        menu.Add(metricsSubMenu);
        menu.Add(textOverlaySubMenu);
        menu.Add(graphOverlaySubMenu);
    }

    // ---------------------------------------------------------------------------
    // Target Time
    // ---------------------------------------------------------------------------

    private static TextMenuExt.SubMenu CreateTargetTimeSubMenu(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.TargetTimeId), false);

        // Sliders
        TextMenu.Slider minutes = new(
            Dialog.Clean(DialogIds.Minutes),
            i => i.ToString(),
            0, 30,
            _settings.Minutes);

        FormattedIntSlider seconds = new(
            Dialog.Clean(DialogIds.Seconds),
            0, 59,
            _settings.Seconds,
            v => v.ToString("D2"));

        TextMenu.Slider ms1 = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsFirstDigit);
        TextMenu.Slider ms2 = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsSecondDigit);
        TextMenu.Slider ms3 = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsThirdDigit);

        minutes.Change(v => _settings.Minutes = v);
        seconds.Change(v => _settings.Seconds = v);
        ms1.Change(v => _settings.MillisecondsFirstDigit = v);
        ms2.Change(v => _settings.MillisecondsSecondDigit = v);
        ms3.Change(v => _settings.MillisecondsThirdDigit = v);

        // Buttons — declare first so SyncSlidersFromSettings can close over it
        TextMenu.Button inputTimeButton = new(Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime());

        void SyncSlidersFromSettings()
        {
            minutes.Index = _settings.Minutes;
            seconds.Index = _settings.Seconds;
            ms1.Index     = _settings.MillisecondsFirstDigit;
            ms2.Index     = _settings.MillisecondsSecondDigit;
            ms3.Index     = _settings.MillisecondsThirdDigit;
            inputTimeButton.Label = Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime();
        }

        inputTimeButton.Pressed(() =>
        {
            Audio.Play(SFX.ui_main_savefile_rename_start);
            menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                GetTargetTime(),
                v =>
                {
                    if (SpeebrunConsistencyTrackerModule.TryParseTime(v, out TimeSpan result))
                    {
                        _settings.Minutes                 = result.Minutes;
                        _settings.Seconds                 = result.Seconds;
                        _settings.MillisecondsFirstDigit  = result.Milliseconds / 100;
                        _settings.MillisecondsSecondDigit = result.Milliseconds / 10 % 10;
                        _settings.MillisecondsThirdDigit  = result.Milliseconds % 10;
                        SyncSlidersFromSettings();
                        SpeebrunConsistencyTrackerModule.PopupMessage(
                            $"{Dialog.Clean(DialogIds.PopupTargetTimeSetid)} {result:mm\\:ss\\.fff}");
                        _instance.SaveSettings();
                    }
                    else
                    {
                        SpeebrunConsistencyTrackerModule.PopupMessage(
                            Dialog.Clean(DialogIds.PopupInvalidTargetTimeid));
                    }
                },
                9, 3);
        });

        TextMenu.Button importButton = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.KeyImportTargetTimeId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                SpeebrunConsistencyTrackerModule.ImportTargetTimeFromClipboard();
                SyncSlidersFromSettings();
            });

        TextMenu.Button resetButton = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ResetTargetTimeId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                _settings.Minutes = _settings.Seconds = 0;
                _settings.MillisecondsFirstDigit = _settings.MillisecondsSecondDigit = _settings.MillisecondsThirdDigit = 0;
                SyncSlidersFromSettings();
                _instance.SaveSettings();
            });

        sub.Add(inputTimeButton);
        sub.Add(importButton);
        sub.Add(resetButton);
        sub.Add(minutes);
        sub.Add(seconds);
        sub.Add(ms1);
        sub.Add(ms2);
        sub.Add(ms3);

        minutes.Visible = inGame;
        seconds.Visible = inGame;
        ms1.Visible     = inGame;
        ms2.Visible     = inGame;
        ms3.Visible     = inGame;
        inputTimeButton.Visible = !inGame;

        importButton.AddDescription(sub, menu, Dialog.Clean(DialogIds.TargetTimeFormatId));
        ms1.AddDescription(sub, menu, Dialog.Clean(DialogIds.MillisecondsFirst));
        ms2.AddDescription(sub, menu, Dialog.Clean(DialogIds.MillisecondsSecond));
        ms3.AddDescription(sub, menu, Dialog.Clean(DialogIds.MillisecondsThird));

        sub.Visible = _settings.Enabled;
        return sub;
    }

    // ---------------------------------------------------------------------------
    // Overlay
    // ---------------------------------------------------------------------------

    private static TextMenuExt.SubMenu CreateTextOverlaySubMenu(TextMenu menu)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.TextOverlayId), false);

        StatTextPosition[]    enumPositions    = Enum.GetValues<StatTextPosition>();
        StatTextOrientation[] enumOrientations = Enum.GetValues<StatTextOrientation>();

        TextMenu.Slider textSize = new(
            Dialog.Clean(DialogIds.TextSizeId),
            i => i.ToString(), 0, 100, _settings.TextSize);

        TextMenu.Slider textAlpha = new(
            Dialog.Clean(DialogIds.TextAlphaId),
            i => (i / 100f).ToString("0.00"), 0, 100, _settings._textAlpha);

        TextMenu.Slider textPosition = new(
            Dialog.Clean(DialogIds.TextPositionId),
            i => enumPositions[i].ToString(), 0, enumPositions.Length - 1,
            Array.IndexOf(enumPositions, _settings.TextPosition));

        TextMenu.Slider textOrientation = new(
            Dialog.Clean(DialogIds.TextOrientationId),
            i => enumOrientations[i].ToString(), 0, enumOrientations.Length - 1,
            Array.IndexOf(enumOrientations, _settings.TextOrientation));

        textSize.Change(v => { _settings.TextSize = v; _instance.textOverlay?.SetTextSize(v); });
        textAlpha.Change(v => { _settings._textAlpha = v; _instance.textOverlay?.SetTextAlpha(_settings.TextAlpha); });
        textPosition.Change(v => { _settings.TextPosition = enumPositions[v]; _instance.textOverlay?.SetTextPosition(enumPositions[v]); });
        textOrientation.Change(v => { _settings.TextOrientation = enumOrientations[v]; _instance.textOverlay?.SetTextOrientation(enumOrientations[v]); });

        textAlpha.Disabled = true; // TODO: debug

        TextMenu.OnOff overlayEnabled = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.OverlayEnabledId), _settings.OverlayEnabled)
            .Change(value =>
            {
                _settings.OverlayEnabled = value;
                textSize.Visible        = value;
                textAlpha.Visible       = value;
                textPosition.Visible    = value;
                textOrientation.Visible = value;
            });

        sub.Add(overlayEnabled);
        sub.Add(textSize);
        sub.Add(textAlpha);
        sub.Add(textPosition);
        sub.Add(textOrientation);

        sub.Visible = _settings.Enabled;
        return sub;
    }

    private static TextMenuExt.SubMenu CreateGraphOverlaySubMenu(TextMenu menu)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.GraphOverlayId), false);

        ColorChoice[] enumColors = Enum.GetValues<ColorChoice>();

        TextMenu.Slider roomColor = new(
            Dialog.Clean(DialogIds.RoomColorId),
            i => enumColors[i].ToString(), 0, enumColors.Length - 1,
            Array.IndexOf(enumColors, _settings.RoomColor));

        TextMenu.Slider segmentColor = new(
            Dialog.Clean(DialogIds.SegmentColorId),
            i => enumColors[i].ToString(), 0, enumColors.Length - 1,
            Array.IndexOf(enumColors, _settings.SegmentColor));

        FormattedIntSlider timeLossThreshold = new(
            Dialog.Clean(DialogIds.TimeLossThresholdId),
            1, 118,
            (int)Math.Round(_settings.TimeLossThresholdMs / 17.0),
            v => $"{v * 17}ms");

        roomColor.Change(v =>
        {
            _settings.RoomColor = enumColors[v];
            _instance.graphManager?.ClearHistogram();
            _instance.graphManager?.ClearScatterGraph();
        });
        segmentColor.Change(v =>
        {
            _settings.SegmentColor = enumColors[v];
            _instance.graphManager?.ClearScatterGraph();
            _instance.graphManager?.ClearHistogram();
        });
        timeLossThreshold.Change(v => _settings.TimeLossThresholdMs = v * 17);

        // Per-graph enable/disable toggles
        TextMenu.OnOff graphScatter = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphScatterId), _settings.GraphScatter)
            .Change(v => { _settings.GraphScatter = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphRoomHistogram = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphRoomHistogramId), _settings.GraphRoomHistogram)
            .Change(v => { _settings.GraphRoomHistogram = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphSegmentHistogram = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphSegmentHistogramId), _settings.GraphSegmentHistogram)
            .Change(v => { _settings.GraphSegmentHistogram = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphDnfPercent = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphDnfPercentId), _settings.GraphDnfPercent)
            .Change(v => { _settings.GraphDnfPercent = v; RebuildGraphSlots(); });

        TextMenu.OnOff graphProblemRooms = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphProblemRoomsId), _settings.GraphProblemRooms)
            .Change(v => { _settings.GraphProblemRooms = v; RebuildGraphSlots(); });
        
        TextMenu.OnOff graphInconsistentRooms = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.GraphInconsistentRoomsId), _settings.GraphInconsistentRooms)
            .Change(v => { _settings.GraphInconsistentRooms = v; RebuildGraphSlots(); });

        sub.Add(roomColor);
        sub.Add(segmentColor);
        sub.Add(timeLossThreshold);
        sub.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.GraphEnabledId), false));
        sub.Add(graphScatter);
        sub.Add(graphRoomHistogram);
        sub.Add(graphSegmentHistogram);
        sub.Add(graphDnfPercent);
        sub.Add(graphProblemRooms);
        sub.Add(graphInconsistentRooms);

        sub.Visible = _settings.Enabled;
        return sub;
    }

    private static void RebuildGraphSlots()
    {
        if (_instance.graphManager == null) return;
        Level level = Engine.Scene as Level;
        _instance.graphManager.RebuildEnabledSlots(level);
    }

    // ---------------------------------------------------------------------------
    // Metrics
    // ---------------------------------------------------------------------------

    private static TextMenuExt.SubMenu CreateMetricsSubMenu(TextMenu menu)
    {
        PercentileChoice[] enumPercentileValues = Enum.GetValues<PercentileChoice>();

        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.StatsSubMenuId), false);

        // Boolean options
        TextMenu.OnOff history        = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RunHistoryId),    _settings.History).Change(b => _settings.History = b);
        TextMenu.OnOff resetShare     = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.ResetShareId),    _settings.ResetShare).Change(b => _settings.ResetShare = b);
        TextMenu.OnOff multimodalTest = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.MultimodalTestId),_settings.MultimodalTest).Change(b => _settings.MultimodalTest = b);
        TextMenu.OnOff roomDependency = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RoomDependencyId),_settings.RoomDependency).Change(b => _settings.RoomDependency = b);

        // Percentile value (special: depends on Percentile slider)
        TextMenu.Slider percentileValue = new(
            Dialog.Clean(DialogIds.PercentileValueId),
            i => enumPercentileValues[i].ToString(),
            0, enumPercentileValues.Length - 1,
            Array.IndexOf(enumPercentileValues, _settings.PercentileValue))
        {
            Disabled = _settings.Percentile == MetricOutputChoice.Off
        };
        percentileValue.Change(v => _settings.PercentileValue = enumPercentileValues[v]);

        // Build all metric sliders from definitions
        List<MetricDef> defs = BuildMetricDefs();
        var sliders = new Dictionary<string, TextMenu.Slider>();
        foreach (MetricDef def in defs)
        {
            TextMenu.Slider slider = MetricSlider(def);
            // Wire percentile enable/disable
            if (def.LabelKey == DialogIds.PercentileId)
                slider.Change(v => percentileValue.Disabled = def.Choices[v] == MetricOutputChoice.Off);
            sliders[def.LabelKey] = slider;
        }

        // --- Bulk action buttons ---

        TextMenu.Button turnAllOff = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonAllOffId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                history.Index        = 0; _settings.History        = false;
                resetShare.Index     = 0; _settings.ResetShare     = false;
                multimodalTest.Index = 0; _settings.MultimodalTest = false;
                roomDependency.Index = 0; _settings.RoomDependency = false;
                foreach (MetricDef def in defs)
                {
                    def.Set(MetricOutputChoice.Off);
                    sliders[def.LabelKey].Index = 0;
                }
                percentileValue.Disabled = true;
                _instance.SaveSettings();
            });

        TextMenu.Button turnAllOn = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonAllOnId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                history.Index        = 1; _settings.History        = true;
                resetShare.Index     = 1; _settings.ResetShare     = true;
                multimodalTest.Index = 1; _settings.MultimodalTest = true;
                roomDependency.Index = 1; _settings.RoomDependency = true;
                foreach (MetricDef def in defs)
                {
                    // Pick the highest available choice (Both if available, else Export)
                    MetricOutputChoice best = def.Choices[def.Choices.Length - 1];
                    def.Set(best);
                    sliders[def.LabelKey].Index = def.Choices.Length - 1;
                }
                percentileValue.Disabled = false;
                _instance.SaveSettings();
            });

        TextMenu.Button resetAll = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.ButtonResetId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                history.Index        = 1; _settings.History        = true;
                resetShare.Index     = 1; _settings.ResetShare     = true;
                multimodalTest.Index = 1; _settings.MultimodalTest = true;
                roomDependency.Index = 1; _settings.RoomDependency = true;
                foreach (MetricDef def in defs)
                {
                    def.Set(def.DefaultValue);
                    sliders[def.LabelKey].Index = Array.IndexOf(def.Choices, def.DefaultValue);
                }
                percentileValue.Index    = Array.IndexOf(enumPercentileValues, PercentileChoice.P90);
                percentileValue.Disabled = _settings.Percentile == MetricOutputChoice.Off;
                _instance.SaveSettings();
            });

        sub.Add(turnAllOff);
        sub.Add(turnAllOn);
        sub.Add(resetAll);
        sub.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.MetricsSubHeaderId), false));

        foreach (MetricDef def in defs)
        {
            sub.Add(sliders[def.LabelKey]);
            if (def.LabelKey == DialogIds.PercentileId)
                sub.Add(percentileValue);
        }

        // Success rate description
        sliders[DialogIds.SuccessRateId].AddDescription(sub, menu, Dialog.Clean(DialogIds.SuccessRateSubTextId));

        sub.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.ExportOnlyId), false));
        sub.Add(history);
        sub.Add(resetShare);
        sub.Add(multimodalTest);
        sub.Add(roomDependency);

        sub.Visible = _settings.Enabled;
        return sub;
    }

    // ---------------------------------------------------------------------------
    // Export
    // ---------------------------------------------------------------------------

    private static TextMenuExt.SubMenu CreateExportSubMenu(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.ExportSubMenu), false);

        ExportChoice[] enumExportChoices = Enum.GetValues<ExportChoice>();

        TextMenu.Slider exportMode = new(
            Dialog.Clean(DialogIds.ExportModeId),
            i => enumExportChoices[i].ToString(),
            0, enumExportChoices.Length - 1,
            Array.IndexOf(enumExportChoices, _settings.ExportMode));
        exportMode.Change(v => _settings.ExportMode = enumExportChoices[v]);

        TextMenu.OnOff exportWithSRT = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.SrtExportId), _settings.ExportWithSRT)
            .Change(b => _settings.ExportWithSRT = b);

        TextMenu.Button exportStatsButton = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.KeyStatsExportId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                if (_settings.ExportMode == ExportChoice.Clipboard)
                    SpeebrunConsistencyTrackerModule.ExportDataToClipboard();
                else
                    SpeebrunConsistencyTrackerModule.ExportDataToFiles();
            });
        exportStatsButton.Disabled = !inGame;

        sub.Add(exportStatsButton);
        sub.Add(exportMode);
        sub.Add(exportWithSRT);

        exportMode.AddDescription(sub, menu, Dialog.Clean(DialogIds.ExportPathId));

        sub.Visible = _settings.Enabled;
        return sub;
    }

    // ---------------------------------------------------------------------------
    // FormattedIntSlider
    // ---------------------------------------------------------------------------

    public class FormattedIntSlider(
        string label,
        int min,
        int max,
        int initialValue,
        Func<int, string> valueToString = null) : TextMenuExt.IntSlider(label, min, max, initialValue)
    {
        private readonly Func<int, string> valueFormatter = valueToString;
        private readonly int min = min;
        private readonly int max = max;
        private float sine;
        private int lastDir;

        public override void Update()
        {
            base.Update();
            sine += Engine.RawDeltaTime;
        }

        public override void LeftPressed()
        {
            int prev = Index;
            base.LeftPressed();
            if (Index != prev) lastDir = -1;
        }

        public override void RightPressed()
        {
            int prev = Index;
            base.RightPressed();
            if (Index != prev) lastDir = 1;
        }

        public override float RightWidth()
        {
            if (valueFormatter == null) return base.RightWidth();

            float maxValueWidth = Calc.Max(
                0f,
                ActiveFont.Measure(valueFormatter(min)).X,
                ActiveFont.Measure(valueFormatter(max)).X,
                ActiveFont.Measure(valueFormatter(Index)).X);

            return maxValueWidth * 0.8f + 120f;
        }

        public override void Render(Vector2 position, bool highlighted)
        {
            if (valueFormatter == null) { base.Render(position, highlighted); return; }

            float alpha       = Container.Alpha;
            Color strokeColor = Color.Black * (alpha * alpha * alpha);
            Color color       = Disabled
                ? Color.DarkSlateGray
                : ((highlighted ? Container.HighlightColor : Color.White) * alpha);

            ActiveFont.DrawOutline(Label, position, new Vector2(0f, 0.5f), Vector2.One, color, 2f, strokeColor);

            if (max - min > 0)
            {
                float rightWidth   = RightWidth();
                string displayValue = valueFormatter(Index);

                ActiveFont.DrawOutline(
                    displayValue,
                    position + new Vector2(Container.Width - rightWidth * 0.5f + lastDir * ValueWiggler.Value * 8f, 0f),
                    new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, color, 2f, strokeColor);

                Vector2 arrowOffset = Vector2.UnitX * (highlighted ? (float)(Math.Sin(sine * 4.0) * 4.0) : 0f);

                Vector2 leftArrowPos = position + new Vector2(
                    Container.Width - rightWidth + 40f + ((lastDir < 0) ? (-ValueWiggler.Value * 8f) : 0f), 0f)
                    - ((Index > min) ? arrowOffset : Vector2.Zero);

                ActiveFont.DrawOutline("<", leftArrowPos, new Vector2(0.5f, 0.5f), Vector2.One,
                    (Index > min) ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);

                Vector2 rightArrowPos = position + new Vector2(
                    Container.Width - 40f + ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f), 0f)
                    + ((Index < max) ? arrowOffset : Vector2.Zero);

                ActiveFont.DrawOutline(">", rightArrowPos, new Vector2(0.5f, 0.5f), Vector2.One,
                    (Index < max) ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);
            }
        }
    }
}