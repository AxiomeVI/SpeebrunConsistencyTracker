using Monocle;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System.Collections.Generic;

// Adaptated from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/TextOverlay.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {

    [Tracked]
    public class TextOverlay : Entity {

        private readonly SpeebrunConsistencyTrackerModuleSettings _settings = SpeebrunConsistencyTrackerModule.Settings;

        private TextComponent StatText { get; set; }
        private bool _textVisible;

        public TextOverlay() {
            Depth = -101;
            Tag = Tags.HUD | Tags.Global;

            StatText = new TextComponent(StatTextPosition.TopLeft, StatTextOrientation.Horizontal, 1f);
            InitStatTextOptions();
            ApplyModSettings();
        }

        public void ApplyModSettings() {
            Visible = false; // UpdateTextOverlay drives visibility each frame; start hidden
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
            StatText?.Orientation = orientation;
        }

        public void SetTextAlpha(float alpha) {
            StatText?.SetAlpha((float)alpha/100);
        }

        public void SetTextVisible(bool visible) {
            _textVisible = visible;
        }
        public void SetText(List<string> text) {
            StatText?.Text = text;
        }
        public void SetTextPosition(StatTextPosition pos) {
            StatText?.SetPosition(pos);
        }
        public void SetTextOffsetX(int offset) {
            if (StatText == null) return;
            StatText.OffsetX = offset;
            StatText.SetPosition();
        }
        public void SetTextOffsetY(int offset)
        {
            if (StatText == null) return;
            StatText.OffsetY = offset;
            StatText.SetPosition();
        }
        //size in percent as int
        public void SetTextSize(int size) {
            StatText?.Scale = (float)size / 100;
        }

        public override void Render() {
            base.Render();
            if (_settings.Enabled && _textVisible)
            {
                StatText.Render();
            }
        }

        public override void Removed(Scene scene)
        {
            base.Removed(scene);
            StatText = null;
        }

    }
}
