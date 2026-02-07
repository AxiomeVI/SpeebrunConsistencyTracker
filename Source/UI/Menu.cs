using System;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.UI;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static class ModMenuOptions
{
    private static readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;
    private static readonly SpeebrunConsistencyTrackerModule _instance = SpeebrunConsistencyTrackerModule.Instance;

    private const string ConfirmSfx = "event:/ui/main/button_select";

    private static string GetTargetTime()
    {
        return _settings.Minutes.ToString() + ":" + _settings.Seconds.ToString("D2") + "." + _settings.MillisecondsFirstDigit.ToString() + _settings.MillisecondsSecondDigit.ToString() + _settings.MillisecondsThirdDigit.ToString();
    }

    public static void CreateMenu(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu targetTimeSubMenu = CreateTargetTimeSubMenu(menu, inGame);
        TextMenuExt.SubMenu overlaySubMenu = CreateOverlaySubMenu(menu);
        TextMenuExt.SubMenu metricsSubMenu = CreateMetricsSubMenu(menu);
        TextMenuExt.SubMenu exportSubmenu = CreateExportSubMenu(menu, inGame);
        
        // Master switch
        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled).Change(
            value =>
            {
                _settings.Enabled = value;
                exportSubmenu.Visible = value;
                targetTimeSubMenu.Visible = value;
                overlaySubMenu.Visible = value;
                metricsSubMenu.Visible = value;
                if (!value) SpeebrunConsistencyTrackerModule.Clear();
            }
        ));

        menu.Add(targetTimeSubMenu);
        menu.Add(exportSubmenu);
        menu.Add(metricsSubMenu);
        menu.Add(overlaySubMenu);
    }

    private static TextMenuExt.SubMenu CreateExportSubMenu(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu exportSubMenu = new(
            Dialog.Clean(DialogIds.ExportSubMenu), 
            false
        );

        ExportChoice[] enumExportChoice = Enum.GetValues<ExportChoice>();

        TextMenu.Slider exportMode = new(Dialog.Clean(DialogIds.ExportModeId), i => enumExportChoice[i].ToString(), 0, 1, Array.IndexOf(enumExportChoice, _settings.ExportMode));
        exportMode.Change(v => _settings.ExportMode = enumExportChoice[v]);

        TextMenu.OnOff exportWithSRT = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.SrtExportId),
            _settings.ExportWithSRT).Change(b => _settings.ExportWithSRT = b);

        TextMenu.Button exportStatsButton = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.KeyStatsExportId))
            .Pressed(() => {
                Audio.Play(ConfirmSfx);
                if (_settings.ExportMode == ExportChoice.Clipboard)
                    SpeebrunConsistencyTrackerModule.ExportDataToClipboard();
                else
                    SpeebrunConsistencyTrackerModule.ExportDataToFiles();
            });
        exportStatsButton.Disabled = !inGame;

        exportSubMenu.Add(exportStatsButton);
        exportSubMenu.Add(exportMode);
        exportSubMenu.Add(exportWithSRT);

        exportMode.AddDescription(exportSubMenu, menu, Dialog.Clean(DialogIds.ExportPathId));

        exportSubMenu.Visible = _settings.Enabled;
        return exportSubMenu;
    }

    private static TextMenuExt.SubMenu CreateTargetTimeSubMenu(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu targetTimeSubMenu = new(
            Dialog.Clean(DialogIds.TargetTimeId), 
            false
        );

        TextMenu.Slider minutes = new(Dialog.Clean(DialogIds.Minutes), i => i.ToString(), 0, 30, _settings.Minutes);
        TextMenu.Slider seconds = new(Dialog.Clean(DialogIds.Seconds), i => i.ToString("D2"), 0, 59, _settings.Seconds);
        TextMenu.Slider millisecondsFirstDigit = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsFirstDigit);
        TextMenu.Slider millisecondsSecondDigit = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsSecondDigit);
        TextMenu.Slider millisecondsThirdDigit = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsThirdDigit);
        minutes.Change(v => _settings.Minutes = v);
        seconds.Change(v => _settings.Seconds = v);
        millisecondsFirstDigit.Change(v => _settings.MillisecondsFirstDigit = v);
        millisecondsSecondDigit.Change(v => _settings.MillisecondsSecondDigit = v);
        millisecondsThirdDigit.Change(v => _settings.MillisecondsThirdDigit = v);


        TextMenu.Button inputTimeButton = (TextMenu.Button)new TextMenu.Button(Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime())
            .Pressed(() => {
                Audio.Play(SFX.ui_main_savefile_rename_start);
                menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                    GetTargetTime(),
                    v => {
                        if (SpeebrunConsistencyTrackerModule.TryParseTime(v, out TimeSpan result))
                        {
                            _settings.Minutes = result.Minutes;
                            _settings.Seconds = result.Seconds;
                            _settings.MillisecondsFirstDigit = result.Milliseconds / 100;
                            _settings.MillisecondsSecondDigit = result.Milliseconds / 10 % 10;
                            _settings.MillisecondsThirdDigit = result.Milliseconds % 10;
                            SpeebrunConsistencyTrackerModule.PopupMessage($"{Dialog.Clean(DialogIds.PopupTargetTimeSetid)} {result:mm\\:ss\\.fff}");
                            _instance.SaveSettings();
                        } else
                        {
                            SpeebrunConsistencyTrackerModule.PopupMessage($"{Dialog.Clean(DialogIds.PopupInvalidTargetTimeid)}");
                        }
                    },
                    9,
                    3
                );
            });

        TextMenu.Button setTargetTimeButton = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.KeyImportTargetTimeId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                SpeebrunConsistencyTrackerModule.ImportTargetTimeFromClipboard();
                minutes.Index = _settings.Minutes;
                seconds.Index = _settings.Seconds;
                millisecondsFirstDigit.Index = _settings.MillisecondsFirstDigit;
                millisecondsSecondDigit.Index = _settings.MillisecondsSecondDigit;
                millisecondsThirdDigit.Index = _settings.MillisecondsThirdDigit;
                inputTimeButton.Label = Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime();
            });

        TextMenu.Button resetButton = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ResetTargetTimeId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                minutes.Index = _settings.Minutes = 0;
                seconds.Index = _settings.Seconds = 0;
                millisecondsFirstDigit.Index = _settings.MillisecondsFirstDigit = 0;
                millisecondsSecondDigit.Index = _settings.MillisecondsSecondDigit = 0;
                millisecondsThirdDigit.Index = _settings.MillisecondsThirdDigit = 0;
                _instance.SaveSettings();
                inputTimeButton.Label = Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime();
            });

        targetTimeSubMenu.Add(inputTimeButton);
        targetTimeSubMenu.Add(setTargetTimeButton);
        targetTimeSubMenu.Add(resetButton);
        targetTimeSubMenu.Add(minutes);
        targetTimeSubMenu.Add(seconds);
        targetTimeSubMenu.Add(millisecondsFirstDigit);
        targetTimeSubMenu.Add(millisecondsSecondDigit);
        targetTimeSubMenu.Add(millisecondsThirdDigit);

        minutes.Visible = inGame;
        seconds.Visible = inGame;
        millisecondsFirstDigit.Visible = inGame;
        millisecondsSecondDigit.Visible = inGame;
        millisecondsThirdDigit.Visible = inGame;
        inputTimeButton.Visible = !inGame;

        setTargetTimeButton.AddDescription(targetTimeSubMenu, menu, Dialog.Clean(DialogIds.TargetTimeFormatId));
        millisecondsFirstDigit.AddDescription(targetTimeSubMenu, menu, Dialog.Clean(DialogIds.MillisecondsFirst));
        millisecondsSecondDigit.AddDescription(targetTimeSubMenu, menu, Dialog.Clean(DialogIds.MillisecondsSecond));
        millisecondsThirdDigit.AddDescription(targetTimeSubMenu, menu, Dialog.Clean(DialogIds.MillisecondsThird));

        targetTimeSubMenu.Visible = _settings.Enabled;
        return targetTimeSubMenu;
    }

    private static TextMenuExt.SubMenu CreateOverlaySubMenu(TextMenu menu)
    {
        StatTextPosition[] enumPositionValues = Enum.GetValues<StatTextPosition>();
        StatTextOrientation[] enumOrientationValues = Enum.GetValues<StatTextOrientation>();
        ColorChoice[] enumColorValues = Enum.GetValues<ColorChoice>();

        TextMenuExt.SubMenu overlaySubMenu = new(
            Dialog.Clean(DialogIds.IngameOverlayId), 
            false
        );

        TextMenu.Slider textSize = new(Dialog.Clean(DialogIds.TextSizeId), i => i.ToString(), 0, 100, _settings.TextSize);
        TextMenu.Slider textPosition = new(Dialog.Clean(DialogIds.TextPositionId), i => enumPositionValues[i].ToString(), 0, 8, Array.IndexOf(enumPositionValues, _settings.TextPosition));
        TextMenu.Slider textOrientation = new(Dialog.Clean(DialogIds.TextOrientationId), i => enumOrientationValues[i].ToString(), 0, 1, Array.IndexOf(enumOrientationValues, _settings.TextOrientation));
        TextMenu.Slider textAlpha = new(Dialog.Clean(DialogIds.TextAlphaId), i => (i/100f).ToString("0.00"), 0, 100, _settings._textAlpha);
        TextMenu.Slider roomColor = new(Dialog.Clean(DialogIds.RoomColorId), i => enumColorValues[i].ToString(), 0, enumColorValues.Length - 1, Array.IndexOf(enumColorValues, _settings.RoomColor));
        TextMenu.Slider segmentColor = new(Dialog.Clean(DialogIds.SegmentColorId), i => enumColorValues[i].ToString(), 0, enumColorValues.Length - 1, Array.IndexOf(enumColorValues, _settings.SegmentColor));

        textAlpha.Change(v => {
            _settings._textAlpha = v;
            _instance?.textOverlay.SetTextAlpha(_settings.TextAlpha);
        });
        textSize.Change(v => {
            _settings.TextSize = v;
            _instance?.textOverlay.SetTextSize(v);
        });
        textPosition.Change(v => {
            _settings.TextPosition = enumPositionValues[v];
            _instance?.textOverlay.SetTextPosition(enumPositionValues[v]);
        });
        textOrientation.Change(v => {
            _settings.TextOrientation = enumOrientationValues[v];
            _instance?.textOverlay.SetTextOrientation(enumOrientationValues[v]);
        });
        roomColor.Change(v => {
            _settings.RoomColor = enumColorValues[v];
            _instance?.graphManager.ClearHistrogram();
            _instance?.graphManager.ClearScatterGraph();
        });
        segmentColor.Change(v => {
            _settings.SegmentColor = enumColorValues[v];
            _instance?.graphManager.ClearScatterGraph();
            _instance?.graphManager.ClearHistrogram();
        });

        TextMenu.OnOff overlayEnabled = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.OverlayEnabledId), _settings.OverlayEnabled).Change(
            value =>
            {
                _settings.OverlayEnabled = value;
                textSize.Visible = value;
                textAlpha.Visible = value;
                textPosition.Visible = value;
                textOrientation.Visible = value;
                roomColor.Visible = value;
                segmentColor.Visible = value;
            }
        );

        overlaySubMenu.Add(overlayEnabled);
        overlaySubMenu.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.TextOverlayId), false));
        overlaySubMenu.Add(textSize);
        overlaySubMenu.Add(textAlpha);
        overlaySubMenu.Add(textPosition);
        overlaySubMenu.Add(textOrientation);
        overlaySubMenu.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.GraphOverlayId), false));
        overlaySubMenu.Add(roomColor);
        overlaySubMenu.Add(segmentColor);

        textAlpha.Disabled = true; // TODO: debug

        overlaySubMenu.Visible = _settings.Enabled;
        return overlaySubMenu;
    }

    private static TextMenuExt.SubMenu CreateMetricsSubMenu(TextMenu menu)
    {
        MetricOutputChoice[] enumOutputChoiceValues = Enum.GetValues<MetricOutputChoice>();
        PercentileChoice[] enumPercentileValues = Enum.GetValues<PercentileChoice>();

        TextMenuExt.SubMenu metricsSubMenu = new(
            Dialog.Clean(DialogIds.StatsSubMenuId), 
            false
        );

        TextMenu.OnOff History = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RunHistoryId), _settings.History).Change(b => _settings.History = b);
        TextMenu.OnOff ResetShare = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.ResetShareId), _settings.ResetShare).Change(b => _settings.ResetShare = b);
        TextMenu.OnOff MultimodalTest = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.MultimodalTestId), _settings.MultimodalTest).Change(b => _settings.MultimodalTest = b);
        TextMenu.OnOff RoomDependency = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.RoomDependencyId), _settings.RoomDependency).Change(b => _settings.RoomDependency = b);

        TextMenu.Slider ConsistencyScore = new (Dialog.Clean(DialogIds.ConsistencyScoreId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.ConsistencyScore));
        TextMenu.Slider SuccessRate = new(Dialog.Clean(DialogIds.SuccessRateId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.SuccessRate));
        SuccessRate.AddDescription(metricsSubMenu, menu, Dialog.Clean(DialogIds.SuccessRateSubTextId));
        TextMenu.Slider TargetTime = new(Dialog.Clean(DialogIds.TargetTimeStatId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.TargetTime));
        TextMenu.Slider CompletedRunCount = new(Dialog.Clean(DialogIds.CompletedRunCountId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.CompletedRunCount));
        TextMenu.Slider TotalRunCount = new(Dialog.Clean(DialogIds.TotalRunCountId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.TotalRunCount));
        TextMenu.Slider DnfCount = new(Dialog.Clean(DialogIds.DnfCountId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.DnfCount));
        TextMenu.Slider Average = new(Dialog.Clean(DialogIds.AverageId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.Average));
        TextMenu.Slider Median = new(Dialog.Clean(DialogIds.MedianId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.Median));
        TextMenu.Slider MedianAbsoluteDeviation = new(Dialog.Clean(DialogIds.MadID), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.MedianAbsoluteDeviation));
        TextMenu.Slider ResetRate = new(Dialog.Clean(DialogIds.ResetRateId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.ResetRate));
        TextMenu.Slider Minimum = new(Dialog.Clean(DialogIds.MinimumId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.Minimum));
        TextMenu.Slider Maximum = new(Dialog.Clean(DialogIds.MaximumId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.Maximum));
        TextMenu.Slider StandardDeviation = new(Dialog.Clean(DialogIds.StandardDeviationId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.StandardDeviation));
        TextMenu.Slider CoefficientOfVariation = new(Dialog.Clean(DialogIds.CoefficientOfVariationId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.CoefficientOfVariation));
        TextMenu.Slider Percentile = new(Dialog.Clean(DialogIds.PercentileId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.Percentile));
        TextMenu.Slider PercentileValue = new(Dialog.Clean(DialogIds.PercentileValueId), i => enumPercentileValues[i].ToString(), 0, 7, Array.IndexOf(enumPercentileValues, _settings.PercentileValue))
        {
            Disabled = _settings.Percentile == MetricOutputChoice.Off
        };
        TextMenu.Slider InterquartileRange = new(Dialog.Clean(DialogIds.InterquartileRangeId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.InterquartileRange));
        TextMenu.Slider LinearRegression = new(Dialog.Clean(DialogIds.LinearRegressionId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.LinearRegression));
        LinearRegression.AddDescription(metricsSubMenu, menu, Dialog.Clean(DialogIds.LinearRegressionSubTextId));
        TextMenu.Slider SoB = new(Dialog.Clean(DialogIds.SoBId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.SoB));

        SuccessRate.Change(v => _settings.SuccessRate = enumOutputChoiceValues[v]);
        ConsistencyScore.Change(v => _settings.ConsistencyScore = enumOutputChoiceValues[v]);
        TargetTime.Change(v => _settings.TargetTime = enumOutputChoiceValues[v]);
        CompletedRunCount.Change(v => _settings.CompletedRunCount = enumOutputChoiceValues[v]);
        TotalRunCount.Change(v => _settings.TotalRunCount = enumOutputChoiceValues[v]);
        DnfCount.Change(v => _settings.DnfCount = enumOutputChoiceValues[v]);
        Average.Change(v => _settings.Average = enumOutputChoiceValues[v]);
        Median.Change(v => _settings.Median = enumOutputChoiceValues[v]);
        MedianAbsoluteDeviation.Change(v => _settings.MedianAbsoluteDeviation = enumOutputChoiceValues[v]);
        ResetRate.Change(v => _settings.ResetRate = enumOutputChoiceValues[v]);
        Minimum.Change(v => _settings.Minimum = enumOutputChoiceValues[v]);
        Maximum.Change(v => _settings.Maximum = enumOutputChoiceValues[v]);
        StandardDeviation.Change(v => _settings.StandardDeviation = enumOutputChoiceValues[v]);
        CoefficientOfVariation.Change(v => _settings.CoefficientOfVariation = enumOutputChoiceValues[v]);
        Percentile.Change(v => {
            _settings.Percentile = enumOutputChoiceValues[v];
            PercentileValue.Disabled = v == 0;
        });
        PercentileValue.Change(v => _settings.PercentileValue = enumPercentileValues[v]);
        InterquartileRange.Change(v => _settings.InterquartileRange = enumOutputChoiceValues[v]);
        LinearRegression.Change(v => _settings.LinearRegression = enumOutputChoiceValues[v]);
        SoB.Change(v => _settings.SoB = enumOutputChoiceValues[v]);

        TextMenu.Button turnAllOff = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ButtonAllOffId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                History.Index = 0;
                _settings.History = false;
                ResetShare.Index = 0;
                _settings.ResetShare = false;
                MultimodalTest.Index = 0;
                _settings.MultimodalTest = false;
                RoomDependency.Index = 0;
                _settings.RoomDependency = false;
                ConsistencyScore.Index = 0;
                _settings.ConsistencyScore = MetricOutputChoice.Off;
                SuccessRate.Index = 0;
                _settings.SuccessRate = MetricOutputChoice.Off;
                TargetTime.Index = 0;
                _settings.TargetTime = MetricOutputChoice.Off;
                CompletedRunCount.Index = 0;
                _settings.CompletedRunCount = MetricOutputChoice.Off;
                TotalRunCount.Index = 0;
                _settings.TotalRunCount = MetricOutputChoice.Off;
                DnfCount.Index = 0;
                _settings.DnfCount = MetricOutputChoice.Off;
                Average.Index = 0;
                _settings.Average = MetricOutputChoice.Off;
                Median.Index = 0;
                _settings.Median = MetricOutputChoice.Off;
                MedianAbsoluteDeviation.Index = 0;
                _settings.MedianAbsoluteDeviation = MetricOutputChoice.Off;
                ResetRate.Index = 0;
                _settings.ResetRate = MetricOutputChoice.Off;
                Minimum.Index = 0;
                _settings.Minimum = MetricOutputChoice.Off;
                Maximum.Index = 0;
                _settings.Maximum = MetricOutputChoice.Off;
                StandardDeviation.Index = 0;
                _settings.StandardDeviation = MetricOutputChoice.Off;
                CoefficientOfVariation.Index = 0;
                _settings.CoefficientOfVariation = MetricOutputChoice.Off;
                Percentile.Index = 0;
                _settings.Percentile = MetricOutputChoice.Off;
                InterquartileRange.Index = 0;
                _settings.InterquartileRange = MetricOutputChoice.Off;
                LinearRegression.Index = 0;
                _settings.LinearRegression = MetricOutputChoice.Off;
                SoB.Index = 0;
                _settings.SoB = MetricOutputChoice.Off;
                PercentileValue.Disabled = true;
                _instance.SaveSettings();
            });

        TextMenu.Button turnAllOn = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ButtonAllOnId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                History.Index = 1;
                _settings.History = true;
                ResetShare.Index = 1;
                _settings.ResetShare = true;
                MultimodalTest.Index = 1;
                _settings.MultimodalTest = true;
                RoomDependency.Index = 1;
                _settings.RoomDependency = true;
                ConsistencyScore.Index = 3;
                _settings.ConsistencyScore = MetricOutputChoice.Both;
                SuccessRate.Index = 3;
                _settings.SuccessRate = MetricOutputChoice.Both;
                TargetTime.Index = 3;
                _settings.TargetTime = MetricOutputChoice.Both;
                CompletedRunCount.Index = 3;
                _settings.CompletedRunCount = MetricOutputChoice.Both;
                TotalRunCount.Index = 3;
                _settings.TotalRunCount = MetricOutputChoice.Both;
                DnfCount.Index = 3;
                _settings.DnfCount = MetricOutputChoice.Both;
                Average.Index = 3;
                _settings.Average = MetricOutputChoice.Both;
                Median.Index = 3;
                _settings.Median = MetricOutputChoice.Both;
                MedianAbsoluteDeviation.Index = 3;
                _settings.MedianAbsoluteDeviation = MetricOutputChoice.Both;
                ResetRate.Index = 3;
                _settings.ResetRate = MetricOutputChoice.Both;
                Minimum.Index = 3;
                _settings.Minimum = MetricOutputChoice.Both;
                Maximum.Index = 3;
                _settings.Maximum = MetricOutputChoice.Both;
                StandardDeviation.Index = 3;
                _settings.StandardDeviation = MetricOutputChoice.Both;
                CoefficientOfVariation.Index = 3;
                _settings.CoefficientOfVariation = MetricOutputChoice.Both;
                Percentile.Index = 3;
                _settings.Percentile = MetricOutputChoice.Both;
                InterquartileRange.Index = 3;
                _settings.InterquartileRange = MetricOutputChoice.Both;
                LinearRegression.Index = 3;
                _settings.LinearRegression = MetricOutputChoice.Both;
                SoB.Index = 3;
                _settings.SoB = MetricOutputChoice.Both;
                PercentileValue.Disabled = false;
                _instance.SaveSettings();
            });

        TextMenu.Button resetAll = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ButtonResetId))
            .Pressed(() =>
            {
                Audio.Play(ConfirmSfx);
                History.Index = 1;
                _settings.History = true;
                ResetShare.Index = 1;
                _settings.ResetShare = true;
                MultimodalTest.Index = 1;
                _settings.MultimodalTest = true;
                RoomDependency.Index = 1;
                _settings.RoomDependency = true;
                ConsistencyScore.Index = 3;
                _settings.ConsistencyScore = MetricOutputChoice.Both;
                SuccessRate.Index = 3;
                _settings.SuccessRate = MetricOutputChoice.Both;
                TargetTime.Index = 3;
                _settings.TargetTime = MetricOutputChoice.Both;
                CompletedRunCount.Index = 2;
                _settings.CompletedRunCount = MetricOutputChoice.Export;
                TotalRunCount.Index = 3;
                _settings.TotalRunCount = MetricOutputChoice.Both;
                DnfCount.Index = 3;
                _settings.DnfCount = MetricOutputChoice.Both;
                Average.Index = 3;
                _settings.Average = MetricOutputChoice.Both;
                Median.Index = 3;
                _settings.Median = MetricOutputChoice.Both;
                MedianAbsoluteDeviation.Index = 2;
                _settings.MedianAbsoluteDeviation = MetricOutputChoice.Export;
                ResetRate.Index = 2;
                _settings.ResetRate = MetricOutputChoice.Export;
                Minimum.Index = 2;
                _settings.Minimum = MetricOutputChoice.Export;
                Maximum.Index = 2;
                _settings.Maximum = MetricOutputChoice.Export;
                StandardDeviation.Index = 3;
                _settings.StandardDeviation = MetricOutputChoice.Both;
                CoefficientOfVariation.Index = 2;
                _settings.CoefficientOfVariation = MetricOutputChoice.Export;
                Percentile.Index = 2;
                _settings.Percentile = MetricOutputChoice.Export;
                InterquartileRange.Index = 2;
                _settings.InterquartileRange = MetricOutputChoice.Export;
                LinearRegression.Index = 2;
                _settings.LinearRegression = MetricOutputChoice.Export;
                SoB.Index = 3;
                _settings.SoB = MetricOutputChoice.Both;
                PercentileValue.Index = 7;
                _settings.PercentileValue = PercentileChoice.P90;
                PercentileValue.Disabled = false;
                _instance.SaveSettings();
            });

        metricsSubMenu.Add(turnAllOff);
        metricsSubMenu.Add(turnAllOn);
        metricsSubMenu.Add(resetAll);
        metricsSubMenu.Add(SuccessRate);
        metricsSubMenu.Add(TargetTime);
        metricsSubMenu.Add(CompletedRunCount);
        metricsSubMenu.Add(TotalRunCount);
        metricsSubMenu.Add(DnfCount);
        metricsSubMenu.Add(Average);
        metricsSubMenu.Add(Median);
        metricsSubMenu.Add(MedianAbsoluteDeviation);
        metricsSubMenu.Add(ResetRate);
        metricsSubMenu.Add(Minimum);
        metricsSubMenu.Add(Maximum);
        metricsSubMenu.Add(StandardDeviation);
        metricsSubMenu.Add(CoefficientOfVariation);
        metricsSubMenu.Add(Percentile);
        metricsSubMenu.Add(PercentileValue);
        metricsSubMenu.Add(InterquartileRange);
        metricsSubMenu.Add(LinearRegression);
        metricsSubMenu.Add(SoB);
        metricsSubMenu.Add(ConsistencyScore);
        metricsSubMenu.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.ExportOnlyId), false));
        metricsSubMenu.Add(History);
        metricsSubMenu.Add(ResetShare);
        metricsSubMenu.Add(MultimodalTest);
        metricsSubMenu.Add(RoomDependency);

        metricsSubMenu.Visible = _settings.Enabled;
        return metricsSubMenu;
    }
}