using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using System.Collections.Generic;

// Adaptated from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/TextOverlay.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {

    public static class TextOverlay {

        private static TextComponent StatText;
        private static bool _textVisible = false;

        // The Text Overlay submenu is built on the title screen too, and its Change handlers land
        // here, but StatText only exists once a level has loaded -- it needs Dialog.Language's
        // font. Every setter returns early until then; Init() calls ApplyModSettings(), which
        // replays whatever was changed in the meantime out of the settings themselves.
        private static bool Ready => StatText != null;

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
            StatText?.Text?.Clear();
            _textVisible = false;
        }

        public static void SetTextOrientation(StatTextOrientation orientation) {
            if (!Ready) return;
            StatText.Orientation = orientation;
        }

        public static void SetTextAlpha(float alpha) {
            if (!Ready) return;
            StatText.SetAlpha((float)alpha/100);
        }

        public static bool IsVisible => SpeebrunConsistencyTrackerModule.Settings.OverlayEnabled && _textVisible;

        public static void SetTextVisible(bool visible) {
            _textVisible = visible;
        }

        public static void SetText(List<string> text) {
            if (!Ready) return;
            StatText.Text = text;
        }

        public static void SetTextPosition(StatTextPosition pos) {
            if (!Ready) return;
            StatText.SetPosition(pos);
        }

        public static void SetTextOffsetX(int offset) {
            if (!Ready) return;
            StatText.OffsetX = offset;
            StatText.SetPosition();
        }

        public static void SetTextOffsetY(int offset) {
            if (!Ready) return;
            StatText.OffsetY = offset;
            StatText.SetPosition();
        }

        // size in percent as int
        public static void SetTextSize(int size) {
            if (!Ready) return;
            StatText.Scale = (float)size / 100;
        }

        public static void Render() {
            if (Ready && SpeebrunConsistencyTrackerModule.Settings.OverlayEnabled && _textVisible)
            {
                StatText.Render();
            }
        }
    }
}
