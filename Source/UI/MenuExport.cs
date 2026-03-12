using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
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
                else if (_settings.ExportMode == ExportChoice.File)
                    SpeebrunConsistencyTrackerModule.ExportDataToFiles();
                else
                    SpeebrunConsistencyTrackerModule.ExportDataToSheet();  
            });
        exportStatsButton.Disabled = !inGame;

        sub.Add(exportStatsButton);
        sub.Add(exportMode);
        sub.Add(exportWithSRT);

        exportMode.AddDescription(sub, menu, Dialog.Clean(DialogIds.ExportPathId));
        exportMode.AddDescription(sub, menu, Dialog.Clean(DialogIds.SheetExportExplanationId));

        sub.Visible = _settings.Enabled;
        return sub;
    }
}
