using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;
using Celeste.Mod.UI;

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

        minutes.Change(v => { _settings.Minutes = v;                   MetricEngine.InvalidateSettingsHash(); });
        seconds.Change(v => { _settings.Seconds = v;                   MetricEngine.InvalidateSettingsHash(); });
        milliseconds.Change(v => { _settings.Milliseconds = v;         MetricEngine.InvalidateSettingsHash(); });

        // Declared first so SyncSlidersFromSettings can close over it.
        TextMenu.Button inputTimeButton = new(Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime());

        void SyncSlidersFromSettings()
        {
            minutes.Index = _settings.Minutes;
            seconds.Index = _settings.Seconds;
            milliseconds.Index = _settings.Milliseconds;
            inputTimeButton.Label = Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime();
            MetricEngine.InvalidateSettingsHash();
        }

        inputTimeButton.Pressed(() =>
        {
            Audio.Play(SFX.ui_main_savefile_rename_start);
            string pendingValue = GetTargetTime();
            menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                GetTargetTime(),
                v => pendingValue = v,
                confirmed =>
                {
                    if (!confirmed) return;
                    if (TimeParser.TryParseTime(pendingValue, out TimeSpan result))
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
                },
                9, 0);
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

        minutes.Visible = inGame;
        seconds.Visible = inGame;
        milliseconds.Visible = inGame;
        inputTimeButton.Visible = !inGame;

        importButton.AddDescription(sub, menu, Dialog.Clean(DialogIds.TargetTimeFormatId));


        sub.Visible = _settings.Enabled;
        return sub;
    }
}
