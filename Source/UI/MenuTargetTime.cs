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

        // Sliders
        TextMenu.Slider minutes = new(
            Dialog.Clean(DialogIds.Minutes),
            i => i.ToString(),
            0, 30,
            _settings.Minutes);

        FormattedIntSlider seconds = new(
            Dialog.Clean(DialogIds.Seconds),
            0, 59,
            _settings.Seconds,
            v => v.ToString("D2"));

        TextMenu.Slider ms1 = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsFirstDigit);
        TextMenu.Slider ms2 = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsSecondDigit);
        TextMenu.Slider ms3 = new(Dialog.Clean(DialogIds.Milliseconds), i => i.ToString(), 0, 9, _settings.MillisecondsThirdDigit);

        minutes.Change(v => { _settings.Minutes = v;                   MetricEngine.InvalidateSettingsHash(); });
        seconds.Change(v => { _settings.Seconds = v;                   MetricEngine.InvalidateSettingsHash(); });
        ms1.Change(v =>     { _settings.MillisecondsFirstDigit = v;    MetricEngine.InvalidateSettingsHash(); });
        ms2.Change(v =>     { _settings.MillisecondsSecondDigit = v;   MetricEngine.InvalidateSettingsHash(); });
        ms3.Change(v =>     { _settings.MillisecondsThirdDigit = v;    MetricEngine.InvalidateSettingsHash(); });

        // Buttons — declare first so SyncSlidersFromSettings can close over it
        TextMenu.Button inputTimeButton = new(Dialog.Clean(DialogIds.InputTargetTimeId) + ": " + GetTargetTime());

        void SyncSlidersFromSettings()
        {
            minutes.Index = _settings.Minutes;
            seconds.Index = _settings.Seconds;
            ms1.Index     = _settings.MillisecondsFirstDigit;
            ms2.Index     = _settings.MillisecondsSecondDigit;
            ms3.Index     = _settings.MillisecondsThirdDigit;
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
                        _settings.Minutes                 = result.Minutes;
                        _settings.Seconds                 = result.Seconds;
                        _settings.MillisecondsFirstDigit  = result.Milliseconds / 100;
                        _settings.MillisecondsSecondDigit = result.Milliseconds / 10 % 10;
                        _settings.MillisecondsThirdDigit  = result.Milliseconds % 10;
                        SyncSlidersFromSettings();
                        SpeebrunConsistencyTrackerModule.PopupMessage(
                            $"{Dialog.Clean(DialogIds.PopupTargetTimeSetid)} {result:mm\\:ss\\.fff}");
                        _instance.SaveSettings();
                    }
                    else
                    {
                        SpeebrunConsistencyTrackerModule.PopupMessage(
                            Dialog.Clean(DialogIds.PopupInvalidTargetTimeid));
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
        sub.Add(ms1);
        sub.Add(ms2);
        sub.Add(ms3);

        minutes.Visible = inGame;
        seconds.Visible = inGame;
        ms1.Visible     = inGame;
        ms2.Visible     = inGame;
        ms3.Visible     = inGame;
        inputTimeButton.Visible = !inGame;

        importButton.AddDescription(sub, menu, Dialog.Clean(DialogIds.TargetTimeFormatId));
        ms1.AddDescription(sub, menu, Dialog.Clean(DialogIds.MillisecondsFirst));
        ms2.AddDescription(sub, menu, Dialog.Clean(DialogIds.MillisecondsSecond));
        ms3.AddDescription(sub, menu, Dialog.Clean(DialogIds.MillisecondsThird));

        sub.Visible = _settings.Enabled;
        return sub;
    }
}
