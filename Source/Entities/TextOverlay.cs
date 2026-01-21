using Monocle;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {

    [Tracked]
    public class TextOverlay : Entity {

        private SpeebrunConsistencyTrackerModule Mod => SpeebrunConsistencyTrackerModule.Instance;

        private TextComponent StatText { get; set; }

        public TextOverlay() {
            Depth = -101;
            Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;

            StatText = new TextComponent(true, true, StatTextPosition.TopLeft);
            InitStatTextOptions();
            ApplyModSettings();
        }

        private void ApplyModSettings() {
            var settings = SpeebrunConsistencyTrackerModule.Settings.IngameOverlay;
            Visible = settings.OverlayEnabled;
            SetTextVisible(settings.OverlayEnabled);
            SetTextPosition(settings.TextPosition);
            SetTextOffsetX(settings.TextOffsetX);
            SetTextOffsetY(settings.TextOffsetY);
            SetTextSize(settings.TextSize);
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
            if (!SpeebrunConsistencyTrackerModule.Settings.Enabled) return;
            if (StatText.Visible) {
                StatText.Render();
            }
        }
    }
}
