using System;
using System.Collections.Generic;
using Celeste.Mod.MenuTools;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static void OpenExportPage(TextMenu menu)
    {
        TextMenuPage page = NewPage(menu, DialogIds.ExportPageId);

        // Listed rather than enumerated: ExportChoice still carries the retired Sheet member.
        ExportChoice[] enumExportChoices = [ExportChoice.Clipboard, ExportChoice.File];

        TextMenu.Slider exportMode = new(
            Dialog.Clean(DialogIds.ExportModeId),
            i => Utility.EnumLabels.For(enumExportChoices[i]),
            0, enumExportChoices.Length - 1,
            Array.IndexOf(enumExportChoices, _settings.ExportMode));
        exportMode.Change(v => _settings.ExportMode = enumExportChoices[v]);

        TextMenu.OnOff exportWithSRT = new(Dialog.Clean(DialogIds.SrtExportId), _settings.ExportWithSRT);
        exportWithSRT.Change(b => _settings.ExportWithSRT = b);

        page.Add(exportMode);
        exportMode.AddDescription(page, Dialog.Clean(DialogIds.ExportPathId));
        page.Add(exportWithSRT);
        // SpeedrunTool hands its section over through the clipboard, whatever its own export mode
        // is set to, so with that set to File the section arrives empty and nothing said why.
        exportWithSRT.AddDescription(page, Dialog.Clean(DialogIds.SrtExportDescId));

        Action refreshMetrics = AddMetricGroups(page, MetricOutput.Export);

        page.Add(new TextMenu.SubHeader(Dialog.Clean(DialogIds.ExtraSectionsId)));
        List<(MetricToggles.Section Section, TextMenu.OnOff Row)> sections = [];
        foreach (MetricToggles.Section section in MetricToggles.ExportSections)
        {
            TextMenu.OnOff row = new(Dialog.Clean(section.LabelKey), section.Get(_settings));
            row.Change(on => section.Set(_settings, on));
            page.Add(row);
            sections.Add((section, row));
        }

        AddBulkButtons(page, MetricOutput.Export, DialogIds.IncludeAllId, DialogIds.IncludeNoneId, () =>
        {
            refreshMetrics();
            foreach ((MetricToggles.Section section, TextMenu.OnOff row) in sections)
                row.Index = section.Get(_settings) ? 1 : 0;
        });

        page.Enter();
    }
}
