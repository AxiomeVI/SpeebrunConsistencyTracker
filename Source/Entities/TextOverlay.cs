using Monocle;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System.Collections.Generic;

// Adaptated from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/TextOverlay.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {

    [Tracked]
    public class TextOverlay : Entity {

        private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

        private TextComponent StatText { get; set; }

        public TextOverlay() {
            Depth = -101;
            Tag = Tags.HUD | Tags.Global;

            StatText = new TextComponent(true, true, StatTextPosition.TopLeft, StatTextOrientation.Horizontal, 1f);
            InitStatTextOptions();
            ApplyModSettings();
        }

        public void ApplyModSettings() {
            Visible = _settings.OverlayEnabled;
            SetTextVisible(_settings.OverlayEnabled);
            SetTextPosition(_settings.TextPosition);
            SetTextOffsetX(_settings.TextOffsetX);
            SetTextOffsetY(_settings.TextOffsetY);
            SetTextSize(_settings.TextSize);
            SetTextAlpha(_settings.TextAlpha);
            SetTextOrientation(_settings.TextOrientation);
        }

        private void InitStatTextOptions() {
            StatText.Font = Dialog.Language.Font;
            StatText.FontFaceSize = Dialog.Language.FontFaceSize;
        }

        public void SetTextOrientation(StatTextOrientation orientation)
        {
            if (StatText != null) StatText.Orientation = orientation;
        }

        public void SetTextAlpha(float alpha) {
            if (StatText != null) StatText.Alpha = alpha;
        }

        public void SetTextVisible(bool visible) {
            if (StatText != null) StatText.OptionVisible = visible;
            UpdateTextVisibility();
        }
        public void SetText(List<string> text) {
            if (StatText != null) StatText.Text = text;
        }
        public void SetTextPosition(StatTextPosition pos) {
            StatText?.SetPosition(pos);
        }
        public void SetTextOffsetX(int offset) {
            if (StatText != null) StatText.OffsetX = offset;
            StatText?.SetPosition();
        }
        public void SetTextOffsetY(int offset)
        {
            if (StatText != null) StatText.OffsetY = offset;
            StatText?.SetPosition();
        }
        //size in percent as int
        public void SetTextSize(int size) {
            if (StatText != null) StatText.Scale = (float)size / 100;
        }
        
        //size in percent as int
        public void SetTextAlpha(int alpha) {
            if (StatText != null) StatText.Alpha = (float)alpha / 100;
        }

        private void UpdateTextVisibility() {
            if (StatText != null) StatText.Visible = StatText.OptionVisible;
        }

        public override void Render() {
            base.Render();
            if (_settings.Enabled && StatText.Visible)
            {
                StatText.Render();
            }
        }

        public override void Removed(Scene scene)
        {
            base.Removed(scene);
            StatText = null;
        }

        public override void SceneEnd(Scene scene)
        {
            base.SceneEnd(scene);
            StatText = null;
        }
    }
}
