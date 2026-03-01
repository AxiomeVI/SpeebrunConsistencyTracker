using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    /// <summary>
    /// Base class for all bar chart / histogram overlays. Handles common layout, rendering pipeline,
    /// title drawing, axis drawing, and legend entry drawing.
    /// </summary>
    public abstract class BaseChartOverlay : Entity
    {
        protected readonly string title;
        protected readonly Vector2 position;
        protected readonly float width = 1800f;
        protected readonly float height = 900f;
        protected readonly float margin = 80f;
        protected readonly Color backgroundColor = Color.Black * 0.8f;
        protected readonly Color axisColor = Color.White;
        protected readonly float MAX_BAR_WIDTH = 120f;

        protected BaseChartOverlay(string title, Vector2? pos = null)
        {
            this.title = title;
            Depth = -100;
            position = pos ?? new Vector2((1920 - width) / 2, (1080 - height) / 2);
            Tag = Tags.HUD | Tags.Global;
        }

        protected abstract void DrawBars(float x, float y, float w, float h);
        protected abstract void DrawLabels(float x, float y, float w, float h);

        protected virtual void DrawAxes(float x, float y, float w, float h)
        {
            Draw.Line(new Vector2(x, y + h), new Vector2(x + w, y + h), axisColor, 2f);
            Draw.Line(new Vector2(x, y), new Vector2(x, y + h), axisColor, 2f);
        }

        protected void DrawTitle()
        {
            Vector2 titleSize = ActiveFont.Measure(title) * 0.7f;
            ActiveFont.DrawOutline(
                title,
                new Vector2(position.X + width / 2 - titleSize.X / 2, position.Y + 10),
                new Vector2(0f, 0f),
                Vector2.One * 0.7f,
                Color.White, 2f, Color.Black);
        }

        protected static void DrawLegendEntry(float x, float y, string text, Color color, float scale, bool right = false)
        {
            Vector2 textSize = ActiveFont.Measure(text) * scale;
            float boxSize = 12f;
            float spacing = 5f;
            float totalWidth = textSize.X + boxSize + spacing;
            float startX = right ? x - totalWidth : x;
            float boxY = y + (textSize.Y / 2f) - (boxSize / 2f);

            Draw.Rect(startX, boxY, boxSize, boxSize, color);
            ActiveFont.DrawOutline(
                text,
                new Vector2(startX + boxSize + spacing, y),
                new Vector2(0f, 0f),
                Vector2.One * scale,
                Color.White, 2f, Color.Black);
        }

        public override void Render()
        {
            base.Render();
            Draw.Rect(position, width, height, backgroundColor);
            float gx = position.X + margin;
            float gy = position.Y + margin;
            float gw = width - margin * 2;
            float gh = height - margin * 2;
            DrawAxes(gx, gy, gw, gh);
            DrawBars(gx, gy, gw, gh);
            DrawLabels(gx, gy, gw, gh);
        }
    }
}