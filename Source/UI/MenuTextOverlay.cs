using System;
using System.Globalization;
using Celeste.Mod.MenuTools;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static void OpenOverlayPage(TextMenu menu)
    {
        TextMenuPage page = NewPage(menu, DialogIds.StatsOverlayPageId);

        StatTextPosition[]    enumPositions    = Enum.GetValues<StatTextPosition>();
        StatTextOrientation[] enumOrientations = Enum.GetValues<StatTextOrientation>();

        TextMenu.Slider textPosition = new(
            Dialog.Clean(DialogIds.TextPositionId),
            i => Utility.EnumLabels.For(enumPositions[i]), 0, enumPositions.Length - 1,
            Array.IndexOf(enumPositions, _settings.TextPosition));

        TextMenu.Slider textOrientation = new(
            Dialog.Clean(DialogIds.TextOrientationId),
            i => Utility.EnumLabels.For(enumOrientations[i]), 0, enumOrientations.Length - 1,
            Array.IndexOf(enumOrientations, _settings.TextOrientation));

        TextMenu.Slider textSize = new(
            Dialog.Clean(DialogIds.TextSizeId),
            i => i.ToString(), 0, 100, _settings.TextSize);

        FormattedIntSlider textAlpha = new(
            Dialog.Clean(DialogIds.TextAlphaId),
            0, 100,
            _settings.TextAlpha,
            v => (v / 100f).ToString("0.00", CultureInfo.InvariantCulture));

        textPosition.Change(v => { _settings.TextPosition = enumPositions[v]; TextOverlay.SetTextPosition(enumPositions[v]); });
        textOrientation.Change(v => { _settings.TextOrientation = enumOrientations[v]; TextOverlay.SetTextOrientation(enumOrientations[v]); });
        textSize.Change(v => { _settings.TextSize = v; TextOverlay.SetTextSize(v); });
        textAlpha.Change(v => { _settings.TextAlpha = v; TextOverlay.SetTextAlpha(_settings.TextAlpha); });

        TextMenu.Item[] lookRows = [textPosition, textOrientation, textSize, textAlpha];

        TextMenu.OnOff overlayEnabled = new(Dialog.Clean(DialogIds.OverlayEnabledId), _settings.OverlayEnabled);
        overlayEnabled.Change(value =>
        {
            _settings.OverlayEnabled = value;
            foreach (TextMenu.Item row in lookRows) row.Visible = value;
        });

        page.Add(overlayEnabled);
        foreach (TextMenu.Item row in lookRows)
        {
            // Change does not fire at construction, so the initial visibility is set here.
            row.Visible = _settings.OverlayEnabled;
            page.Add(row);
        }

        Action refresh = AddMetricGroups(page, MetricOutput.Overlay);
        AddBulkButtons(page, MetricOutput.Overlay, DialogIds.ShowAllId, DialogIds.HideAllId, refresh);

        page.Enter();
    }
}
