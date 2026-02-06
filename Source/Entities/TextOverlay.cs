using Monocle;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

// Adaptated from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/TextOverlay.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {

    [Tracked]
    public class TextOverlay : Entity {

        private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

        private TextComponent StatText { get; set; }

        public TextOverlay() {
            Depth = -101;
            Tag = Tags.HUD | Tags.Global;

            StatText = new TextComponent(true, true, StatTextPosition.TopLeft);
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
