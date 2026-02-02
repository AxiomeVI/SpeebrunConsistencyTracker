using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static class ModMenuOptions
{
    private static SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;
    private static SpeebrunConsistencyTrackerModule _instance = SpeebrunConsistencyTrackerModule.Instance;

    public static void CreateMenu(TextMenu menu, bool inGame)
    {
        TextMenu.OnOff exportWithSRT = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.SrtExportId),
            _settings.ExportWithSRT).Change(b => _settings.ExportWithSRT = b);
        exportWithSRT.Visible = _settings.Enabled;

        TextMenu.Button exportStatsButton = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.KeyStatsExportId))
            .Pressed(SpeebrunConsistencyTrackerModule.ExportDataToCsv);
        exportStatsButton.Visible = _settings.Enabled && inGame;

        TextMenuExt.SubMenu targetTimeSubMenu = CreateTargetTimeSubMenu(menu);
        TextMenuExt.SubMenu overlaySubMenu = CreateOverlaySubMenu(menu);
        TextMenuExt.SubMenu metricsSubMenu = CreateMetricsSubMenu(menu);
        
        // Master switch
        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled).Change(
            value =>
            {
                _settings.Enabled = value;
                exportWithSRT.Visible = value;
                exportStatsButton.Visible = value && inGame;
                targetTimeSubMenu.Visible = value;
                overlaySubMenu.Visible = value;
                metricsSubMenu.Visible = value;
                if (value) SpeebrunConsistencyTrackerModule.Init();
                else SpeebrunConsistencyTrackerModule.Reset();
            }
        ));

        menu.Add(targetTimeSubMenu);
        menu.Add(metricsSubMenu);
        menu.Add(overlaySubMenu);
        menu.Add(exportStatsButton);
        menu.Add(exportWithSRT);
    }

    private static TextMenuExt.SubMenu CreateTargetTimeSubMenu(TextMenu menu)
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
        millisecondsFirstDigit.Change(v => _settings.MillisecondsSecondDigit = v);
        millisecondsFirstDigit.Change(v => _settings.MillisecondsThirdDigit = v);

        TextMenu.Button setTargetTimeButton = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.KeyImportTargetTimeId))
            .Pressed(() =>
            {
                _instance.ImportTargetTimeFromClipboard();
                minutes.Index = _settings.Minutes;
                seconds.Index = _settings.Seconds;
                millisecondsFirstDigit.Index = _settings.MillisecondsFirstDigit;
                millisecondsSecondDigit.Index = _settings.MillisecondsSecondDigit;
                millisecondsThirdDigit.Index = _settings.MillisecondsThirdDigit;
            });

        TextMenu.Button resetButton = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ResetTargetTimeId))
            .Pressed(() =>
            {
                minutes.Index = _settings.Minutes = 0;
                seconds.Index = _settings.Seconds = 0;
                millisecondsFirstDigit.Index = _settings.MillisecondsFirstDigit = 0;
                millisecondsSecondDigit.Index = _settings.MillisecondsSecondDigit = 0;
                millisecondsThirdDigit.Index = _settings.MillisecondsThirdDigit = 0;
                _instance.SaveSettings();
            });

        targetTimeSubMenu.Add(setTargetTimeButton);
        targetTimeSubMenu.Add(resetButton);
        targetTimeSubMenu.Add(minutes);
        targetTimeSubMenu.Add(seconds);
        targetTimeSubMenu.Add(millisecondsFirstDigit);
        targetTimeSubMenu.Add(millisecondsSecondDigit);
        targetTimeSubMenu.Add(millisecondsThirdDigit);

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

        TextMenuExt.SubMenu overlaySubMenu = new(
            Dialog.Clean(DialogIds.IngameOverlayId), 
            false
        );

        TextMenu.Slider textSize = new(Dialog.Clean(DialogIds.TextSizeId), i => i.ToString(), 0, 100, _settings.TextSize);
        TextMenu.Slider textPosition = new(Dialog.Clean(DialogIds.TextPositionId), i => enumPositionValues[i].ToString(), 0, 8, Array.IndexOf(enumPositionValues, _settings.TextPosition));
        TextMenu.Slider textOrientation = new(Dialog.Clean(DialogIds.TextOrientationId), i => enumOrientationValues[i].ToString(), 0, 1, Array.IndexOf(enumOrientationValues, _settings.TextOrientation));

        textSize.Change(v => {
            _settings.TextSize = v;
        });
        textPosition.Change(v => {
            _settings.TextPosition = enumPositionValues[v];
        });
        textOrientation.Change(v => {
            _settings.TextOrientation = enumOrientationValues[v];
        });

        TextMenu.OnOff overlayEnabled = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.OverlayEnabledId), _settings.OverlayEnabled).Change(
            value =>
            {
                _settings.OverlayEnabled = value;
                textSize.Visible = value;
                textPosition.Visible = value;
                textOrientation.Visible = value;
            }
        );

        overlaySubMenu.Add(overlayEnabled);
        overlaySubMenu.Add(textSize);
        overlaySubMenu.Add(textPosition);
        overlaySubMenu.Add(textOrientation);

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
        TextMenu.OnOff ConsistencyScore = (TextMenu.OnOff)new TextMenu.OnOff(Dialog.Clean(DialogIds.ConsistencyScoreId), _settings.ConsistencyScore).Change(b => _settings.ConsistencyScore = b);

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
        TextMenu.Slider PercentileValue = new(Dialog.Clean(DialogIds.PercentileValueId), i => enumPercentileValues[i].ToString(), 0, 7, Array.IndexOf(enumPercentileValues, _settings.PercentileValue));
        TextMenu.Slider InterquartileRange = new(Dialog.Clean(DialogIds.InterquartileRangeId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.InterquartileRange));
        TextMenu.Slider LinearRegression = new(Dialog.Clean(DialogIds.LinearRegressionId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.LinearRegression));
        LinearRegression.AddDescription(metricsSubMenu, menu, Dialog.Clean(DialogIds.LinearRegressionSubTextId));
        TextMenu.Slider SoB = new(Dialog.Clean(DialogIds.SoBId), i => enumOutputChoiceValues[i].ToString(), 0, 3, Array.IndexOf(enumOutputChoiceValues, _settings.SoB));

        SuccessRate.Change(v => _settings.SuccessRate = enumOutputChoiceValues[v]);
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
        Percentile.Change(v => _settings.Percentile = enumOutputChoiceValues[v]);
        PercentileValue.Change(v => _settings.PercentileValue = enumPercentileValues[v]);
        InterquartileRange.Change(v => _settings.InterquartileRange = enumOutputChoiceValues[v]);
        LinearRegression.Change(v => _settings.LinearRegression = enumOutputChoiceValues[v]);
        SoB.Change(v => _settings.SoB = enumOutputChoiceValues[v]);

        TextMenu.Button turnAllOff = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ButtonAllOffId))
            .Pressed(() =>
            {
                History.Index = 1;
                _settings.History = false;
                ResetShare.Index = 1;
                _settings.ResetShare = false;
                ConsistencyScore.Index = 1;
                _settings.ConsistencyScore = false;
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
                PercentileValue.Index = 7;
                _settings.PercentileValue = PercentileChoice.P90;
                _instance.SaveSettings();
            });

        TextMenu.Button turnAllOn = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ButtonAllOnId))
            .Pressed(() =>
            {
                History.Index = 0;
                _settings.History = true;
                ResetShare.Index = 0;
                _settings.ResetShare = true;
                ConsistencyScore.Index = 0;
                _settings.ConsistencyScore = true;
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
                PercentileValue.Index = 7;
                _settings.PercentileValue = PercentileChoice.P90;
                _instance.SaveSettings();
            });

        TextMenu.Button resetAll = (TextMenu.Button)new TextMenu.Button(
            Dialog.Clean(DialogIds.ButtonResetId))
            .Pressed(() =>
            {
                History.Index = 0;
                _settings.History = true;
                ResetShare.Index = 0;
                _settings.ResetShare = true;
                ConsistencyScore.Index = 0;
                _settings.ConsistencyScore = true;
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
        metricsSubMenu.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.ExportOnlyId), false));
        metricsSubMenu.Add(History);
        metricsSubMenu.Add(ResetShare);
        metricsSubMenu.Add(ConsistencyScore);

        metricsSubMenu.Visible = _settings.Enabled;
        return metricsSubMenu;
    }
}