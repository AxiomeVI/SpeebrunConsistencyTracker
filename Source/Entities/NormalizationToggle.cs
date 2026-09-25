using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    // The "Absolute | Relative" segmented button drawn at the bottom center of a chart.
    // Named Render, not Draw: Monocle.Draw is the static drawing class this file calls into.
    // Owned as a field by the overlays that offer a median-normalised Y axis, so the two
    // copies of the layout and its colours cannot drift apart.
    internal sealed class NormalizationToggle(bool normalized)
    {
        public bool Normalized { get; private set; } = normalized;
        public bool Hovered { get; private set; }

        // Off-screen until the first Render fills it in.
        private Microsoft.Xna.Framework.Rectangle _bounds = new(-9999, -9999, 0, 0);

        public void Render(Vector2 chartPosition, float chartWidth, float chartHeight)
        {
            const float scale = ChartConstants.FontScale.AxisLabelSmall;
            const float pad   = ChartConstants.Interactivity.TooltipBgPadding;
            const float divW  = 2f; // divider between segments

            Vector2 sizeAbs = ActiveFont.Measure(Dialog.Clean(DialogIds.ChartAbsolute)) * scale;
            Vector2 sizeRel = ActiveFont.Measure(Dialog.Clean(DialogIds.ChartRelative))  * scale;
            float colW   = Math.Max(sizeAbs.X, sizeRel.X) + pad * 2f;
            float btnH   = Math.Max(sizeAbs.Y, sizeRel.Y) + pad * 2f;
            float totalW = colW * 2 + divW;

            float bgX = MathF.Round(chartPosition.X + chartWidth / 2f - totalW / 2f);
            float bgY = MathF.Round(chartPosition.Y + chartHeight - btnH - 6f);

            _bounds = new Microsoft.Xna.Framework.Rectangle((int)bgX, (int)bgY, (int)totalW, (int)btnH);

            Draw.Rect(bgX - 1f, bgY - 1f, totalW + 2f, btnH + 2f, Color.White * 0.6f);

            bool absHovered = Hovered && !Normalized;
            Draw.Rect(bgX, bgY, colW, btnH,
                !Normalized ? Color.White * 0.35f
                : absHovered  ? Color.White * 0.15f
                              : ChartConstants.Colors.PanelBackgroundColor);
            ActiveFont.DrawOutline(Dialog.Clean(DialogIds.ChartAbsolute),
                new Vector2(bgX + colW / 2f - sizeAbs.X / 2f, bgY + pad),
                Vector2.Zero, Vector2.One * scale,
                !Normalized ? Color.White : Color.Gray * 0.8f,
                ChartConstants.Stroke.OutlineSize, Color.Black);

            Draw.Rect(bgX + colW, bgY, divW, btnH, Color.White * 0.6f);

            bool relHovered = Hovered && Normalized;
            Draw.Rect(bgX + colW + divW, bgY, colW, btnH,
                Normalized  ? Color.White * 0.35f
                : relHovered ? Color.White * 0.15f
                             : ChartConstants.Colors.PanelBackgroundColor);
            ActiveFont.DrawOutline(Dialog.Clean(DialogIds.ChartRelative),
                new Vector2(bgX + colW + divW + colW / 2f - sizeRel.X / 2f, bgY + pad),
                Vector2.Zero, Vector2.One * scale,
                Normalized ? Color.White : Color.Gray * 0.8f,
                ChartConstants.Stroke.OutlineSize, Color.Black);
        }

        // Side effect: stores the hover state the next Render reads. Returns it so the caller
        // can short-circuit its own hit test.
        public bool UpdateHover(Vector2 mouseHudPos) =>
            Hovered = _bounds.Contains((int)mouseHudPos.X, (int)mouseHudPos.Y);

        // Flips only while hovered; true means the click was consumed.
        public bool HandleClick()
        {
            if (!Hovered) return false;
            Normalized = !Normalized;
            return true;
        }
    }
}
