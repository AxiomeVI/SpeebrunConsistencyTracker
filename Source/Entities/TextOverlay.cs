using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System.Collections.Generic;

// Adaptated from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/TextOverlay.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {

    public static class TextOverlay {

        private static TextComponent StatText;
        private static bool _textVisible = false;

        public static void Init() {
            StatText ??= new TextComponent(StatTextPosition.TopLeft, StatTextOrientation.Horizontal, 1f) {
                Font = Dialog.Language.Font,
                FontFaceSize = Dialog.Language.FontFaceSize
            };
            ApplyModSettings();
        }

        public static void ApplyModSettings() {
            var s = SpeebrunConsistencyTrackerModule.Settings;
            SetTextPosition(s.TextPosition);
            SetTextOffsetX(s.TextOffsetX);
            SetTextOffsetY(s.TextOffsetY);
            SetTextSize(s.TextSize);
            SetTextAlpha(s.TextAlpha);
            SetTextOrientation(s.TextOrientation);
        }

        public static void Clear() {
            StatText.Text = [];
            _textVisible = false;
        }

        public static void SetTextOrientation(StatTextOrientation orientation) {
            StatText.Orientation = orientation;
        }

        public static void SetTextAlpha(float alpha) {
            StatText.SetAlpha((float)alpha/100);
        }

        public static bool IsVisible => SpeebrunConsistencyTrackerModule.Settings.OverlayEnabled && _textVisible;

        public static void SetTextVisible(bool visible) {
            _textVisible = visible;
        }

        public static void SetText(List<string> text) {
            StatText.Text = text;
        }

        public static void SetTextPosition(StatTextPosition pos) {
            StatText.SetPosition(pos);
        }

        public static void SetTextOffsetX(int offset) {
            StatText.OffsetX = offset;
            StatText.SetPosition();
        }

        public static void SetTextOffsetY(int offset) {
            StatText.OffsetY = offset;
            StatText.SetPosition();
        }

        // size in percent as int
        public static void SetTextSize(int size) {
            StatText.Scale = (float)size / 100;
        }

        public static void Render() {
            if (SpeebrunConsistencyTrackerModule.Settings.OverlayEnabled && _textVisible)
            {
                StatText.Render();
            }
        }
    }
}
