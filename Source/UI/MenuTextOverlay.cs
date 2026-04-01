using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static TextMenuExt.SubMenu CreateTextOverlaySubMenu(TextMenu menu)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.TextOverlayId), false);

        StatTextPosition[]    enumPositions    = Enum.GetValues<StatTextPosition>();
        StatTextOrientation[] enumOrientations = Enum.GetValues<StatTextOrientation>();

        TextMenu.Slider textSize = new(
            Dialog.Clean(DialogIds.TextSizeId),
            i => i.ToString(), 0, 100, _settings.TextSize);

        FormattedIntSlider textAlpha = new(
            Dialog.Clean(DialogIds.TextAlphaId),
            0, 100,
            _settings.TextAlpha,
            v => (v / 100f).ToString("0.00"));

        TextMenu.Slider textPosition = new(
            Dialog.Clean(DialogIds.TextPositionId),
            i => enumPositions[i].ToString(), 0, enumPositions.Length - 1,
            Array.IndexOf(enumPositions, _settings.TextPosition));

        TextMenu.Slider textOrientation = new(
            Dialog.Clean(DialogIds.TextOrientationId),
            i => enumOrientations[i].ToString(), 0, enumOrientations.Length - 1,
            Array.IndexOf(enumOrientations, _settings.TextOrientation));

        textSize.Change(v => { _settings.TextSize = v; TextOverlay.SetTextSize(v); });
        textAlpha.Change(v => { _settings.TextAlpha = v; TextOverlay.SetTextAlpha(_settings.TextAlpha); });
        textPosition.Change(v => { _settings.TextPosition = enumPositions[v]; TextOverlay.SetTextPosition(enumPositions[v]); });
        textOrientation.Change(v => { _settings.TextOrientation = enumOrientations[v]; TextOverlay.SetTextOrientation(enumOrientations[v]); });

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
}
