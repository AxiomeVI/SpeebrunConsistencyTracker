using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    // The keypad screen, over the Mod Options menu, from the title screen and the pause menu alike.
    private static void OpenTargetTimeEntry(TextMenu menu)
    {
        Audio.Play(SFX.ui_main_savefile_rename_start);
        // The button is a top-level row, so unfocusing the menu is enough to keep it from reading
        // the Confirm typed on the keypad as another press.
        menu.Focused = false;
        // Captured, not read again at end of frame: Engine.Scene can change in between.
        Monocle.Scene scene = menu.Scene;
        scene.Add(new UI.TargetTimeEntry(GetTargetTime(), typed =>
        {
            if (TimeParser.TryParseTime(typed, out TimeSpan result))
            {
                _settings.SetTargetTime(result);
                MetricEngine.InvalidateSettingsHash();
                SpeebrunConsistencyTrackerModule.PopupMessage(
                    $"{Dialog.Clean(DialogIds.PopupTargetTimeSetId)} {result:mm\\:ss\\.fff}");
                _instance.SaveSettings();
            }
            else
            {
                SpeebrunConsistencyTrackerModule.PopupMessage(
                    Dialog.Clean(DialogIds.PopupInvalidTypedTargetTimeId));
            }
        }, () => menu.Focused = true));
        scene.OnEndOfFrame += () => scene.Entities.UpdateLists();
    }
}
