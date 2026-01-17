using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {

    [Tracked]
    public class TextOverlay : Entity {

        private SpeebrunConsistencyTrackerModule Mod => SpeebrunConsistencyTrackerModule.Instance;

        private StatTextComponent StatText { get; set; }

        public TextOverlay() {
            Depth = -101;
            Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;

            StatText = new StatTextComponent(true, true, StatTextPosition.TopLeft);
            InitStatTextOptions();

            ApplyModSettings();
        }

        private void ApplyModSettings() {
            var settings = SpeebrunConsistencyTrackerModule.Settings.IngameOverlay;

            Visible = settings.TextEnabled;

            SetTextVisible(settings.TextEnabled);
            SetTextPosition(settings.TextPosition);
            SetTextOffsetX(settings.TextOffsetX);
            SetTextOffsetY(settings.TextOffsetY);
            SetTextSize(settings.TextSize);
        }
        
        private void SetVisibility(bool visible) {
            Visible = visible;
        }
        private void CheckVisibility() {
            var settings = SpeebrunConsistencyTrackerModule.Settings.IngameOverlay;
            if (settings.TextEnabled 
                && (!settings.OnlyShowInPauseMenu 
                    || (Engine.Scene is Level level && (level.PauseMainMenuOpen || level.Entities.FindFirst<TextMenu>() != null)))) {
                SetVisibility(true);
            } else {
                SetVisibility(false);
            }
        }

        public override void Update() {
            base.Update();
            var settings = SpeebrunConsistencyTrackerModule.Settings.IngameOverlay;
            CheckVisibility();
            if (Engine.Scene is Level level && (level.Paused || level.PauseMainMenuOpen || level.Entities.FindFirst<TextMenu>() != null)) {
                return;
            }

            if (SpeebrunConsistencyTrackerModule.Settings.ButtonToggleTextOverlayEnabled.Pressed) {
                bool currentVisible = settings.TextEnabled;
                settings.TextEnabled = !currentVisible;
                Mod.SaveSettings();
            }

            if (!Visible) {
                return;
            }

            if (SpeebrunConsistencyTrackerModule.Settings.ButtonToggleTextOverlayText.Pressed) {
                settings.TextEnabled = !settings.TextEnabled;
                Mod.SaveSettings();
                SetTextVisible(settings.TextEnabled);
            }
        }

        private void InitStatTextOptions() {
            StatText.Font = Dialog.Language.Font;
            StatText.FontFaceSize = Dialog.Language.FontFaceSize;
        }

        public void SetTextVisible(bool visible) {
            StatText.OptionVisible = visible;
            UpdateTextVisibility();
        }
        public void SetText(string text) {
            StatText.Text = text.Replace("\\n", "\n");
        }
        public void SetTextPosition(StatTextPosition pos) {
            StatText.SetPosition(pos);
        }
        public void SetTextOffsetX(int offset) {
            StatText.OffsetX = offset;
            StatText.SetPosition();
        }
        public void SetTextOffsetY(int offset)
        {
            StatText.OffsetY = offset;
            StatText.SetPosition();
        }
        //size in percent as int
        public void SetTextSize(int size) {
            StatText.Scale = (float)size / 100;
        }
        
        //size in percent as int
        public void SetTextAlpha(int alpha) {
            StatText.Alpha = (float)alpha / 100;
        }

        private void UpdateTextVisibility() {
            StatText.Visible = StatText.OptionVisible;
        }

        public override void Render() {
            base.Render();

            if (StatText.Visible) {
                StatText.Render();
            }
        }
    }
}
