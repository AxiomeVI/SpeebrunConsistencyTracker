using System.Collections.Generic;
using Celeste.Mod.MenuTools;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.UI;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

// The Mod Options section: a few actions, then one full-screen page per topic.
//
// ⚠️ A page is created in its button's Pressed handler every time, with the Mod Options menu as
// parent and no submenu parent, and never from Everest's TextMenuExt.SubMenu: CelesteMenuTools'
// pages cannot take the input back from one.
public static partial class ModMenuOptions
{
    private static SpeebrunConsistencyTrackerModuleSettings _settings => SpeebrunConsistencyTrackerModule.Settings;
    private static SpeebrunConsistencyTrackerModule _instance => SpeebrunConsistencyTrackerModule.Instance;

    private static string GetTargetTime() =>
        $"{_settings.Minutes}:{_settings.Seconds:D2}.{_settings.Milliseconds:D3}";

    public static void CreateMenu(TextMenu menu, bool inGame)
    {
        List<TextMenu.Item> rows = [];

        rows.Add(new ValueButton(Dialog.Clean(DialogIds.TargetTimeId), GetTargetTime)
            .Pressed(() => OpenTargetTimeEntry(menu)));

        if (inGame)
        {
            rows.Add(new TextMenu.Button(Dialog.Clean(DialogIds.ExportNowId)).Pressed(() =>
            {
                if (_settings.ExportMode == ExportChoice.Clipboard)
                    SpeebrunConsistencyTrackerModule.ExportDataToClipboard();
                else
                    SpeebrunConsistencyTrackerModule.ExportDataToFiles();
            }));
        }

        rows.Add(new ValueButton(Dialog.Clean(DialogIds.StatsOverlayPageId),
                () => Dialog.Clean(_settings.OverlayEnabled ? "options_on" : "options_off"))
            .Pressed(() => OpenOverlayPage(menu)));
        rows.Add(new TextMenu.Button(Dialog.Clean(DialogIds.ChartsPageId)).Pressed(() => OpenChartsPage(menu)));
        rows.Add(new TextMenu.Button(Dialog.Clean(DialogIds.ExportPageId)).Pressed(() => OpenExportPage(menu)));

        // At the top level on purpose: the remap screen only unfocuses the menu, so from inside a
        // submenu the submenu would keep reading input behind it.
        rows.Add(CelesteHotkeys.HotkeyMenu.OpenButton(
            menu, Hotkeys.Set, Hotkeys.Text, SpeebrunConsistencyTrackerModule.Instance.SaveSettings));

        // Turning this off calls Clear(), which erases every save-state slot's data.
        TextMenu.OnOff enabledToggle = new(Dialog.Clean(DialogIds.EnabledId), _settings.Enabled);
        enabledToggle.Change(value =>
        {
            _settings.Enabled = value;
            foreach (TextMenu.Item row in rows) row.Visible = value;
            if (!value)
                SpeebrunConsistencyTrackerModule.Clear();
        });

        menu.Add(enabledToggle);
        foreach (TextMenu.Item row in rows)
        {
            // Change does not fire at construction, so the initial visibility is set here.
            row.Visible = _settings.Enabled;
            menu.Add(row);
        }
    }

    // A full-screen page under its own header. The caller adds the rows, then Enter()s it.
    private static TextMenuPage NewPage(TextMenu menu, string headerId)
    {
        TextMenuPage page = new(menu);
        page.Add(new TextMenu.Header(Dialog.Clean(headerId)));
        return page;
    }
}
