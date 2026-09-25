using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Menu;

public static partial class ModMenuOptions
{
    private static TextMenuExt.SubMenu CreateTargetTimeSubMenu(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu sub = new(Dialog.Clean(DialogIds.TargetTimeId), false);

        TextMenu.Slider minutes = new(
            Dialog.Clean(DialogIds.Minutes),
            i => i.ToString(),
            0, 59,
            _settings.Minutes);

        FormattedIntSlider seconds = new(
            Dialog.Clean(DialogIds.Seconds),
            0, 59,
            _settings.Seconds,
            v => v.ToString("D2"));

        // One 0..999 slider, not three digit sliders sharing a label. The three digits stay as
        // three settings because that is how they are persisted.
        FormattedIntSlider milliseconds = new(
            Dialog.Clean(DialogIds.Milliseconds),
            0, 999,
            _settings.Milliseconds,
            v => v.ToString("D3"));

        // Declared first so SyncSlidersFromSettings and the sliders can close over it.
        TextMenu.Button inputTimeButton = new(Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime());
        // The button shows the time too, and on the title screen it sits above the sliders.
        void RefreshButtonLabel() => inputTimeButton.Label = Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime();

        minutes.Change(v => { _settings.Minutes = v;           MetricEngine.InvalidateSettingsHash(); RefreshButtonLabel(); });
        seconds.Change(v => { _settings.Seconds = v;           MetricEngine.InvalidateSettingsHash(); RefreshButtonLabel(); });
        milliseconds.Change(v => { _settings.Milliseconds = v; MetricEngine.InvalidateSettingsHash(); RefreshButtonLabel(); });

        void SyncSlidersFromSettings()
        {
            minutes.Index = _settings.Minutes;
            seconds.Index = _settings.Seconds;
            milliseconds.Index = _settings.Milliseconds;
            RefreshButtonLabel();
            MetricEngine.InvalidateSettingsHash();
        }

        inputTimeButton.Pressed(() =>
        {
            Audio.Play(SFX.ui_main_savefile_rename_start);
            // ⚠️ The SUBMENU, not the menu: a SubMenu reads MenuConfirm by its own Focused flag, so
            // with only the menu unfocused every Confirm typed here also pressed this button again
            // and stacked another screen.
            sub.Focused = false;
            // Captured, not read again at end of frame: Engine.Scene can change in between.
            Monocle.Scene scene = menu.Scene;
            scene.Add(new UI.TargetTimeEntry(GetTargetTime(), typed =>
            {
                if (TimeParser.TryParseTime(typed, out TimeSpan result))
                {
                    _settings.SetTargetTime(result);
                    SyncSlidersFromSettings();
                    SpeebrunConsistencyTrackerModule.PopupMessage(
                        $"{Dialog.Clean(DialogIds.PopupTargetTimeSetId)} {result:mm\\:ss\\.fff}");
                    _instance.SaveSettings();
                }
                else
                {
                    SpeebrunConsistencyTrackerModule.PopupMessage(
                        Dialog.Clean(DialogIds.PopupInvalidTypedTargetTimeId));
                }
            }, () => sub.Focused = true));
            scene.OnEndOfFrame += () => scene.Entities.UpdateLists();
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
        sub.Add(milliseconds);

        // The sliders show everywhere: the typed entry is Everest's text screen, which reads the
        // keyboard layout, and a layout that needs Shift for digits types symbols instead.
        // The typed entry itself only exists on the title screen, where that screen can open.
        inputTimeButton.Visible = !inGame;

        importButton.AddDescription(sub, menu, Dialog.Clean(DialogIds.TargetTimeFormatId));


        sub.Visible = _settings.Enabled;
        return sub;
    }
}
