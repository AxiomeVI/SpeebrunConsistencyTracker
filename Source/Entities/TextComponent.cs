using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;

// Adapted from https://github.com/viddie/ConsistencyTrackerMod/blob/main/Entities/StatTextComponent.cs
namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities {
    public class TextComponent(bool active, bool visible, StatTextPosition position) : Component(active, visible) {

        public StatTextPosition Position { get; set; } = position;
        public string Text { get; set; } = "";
        public bool OptionVisible { get; set; }
        public float Scale { get; set; } = 1f;
        public float Alpha {
            get => _Alpha;
            set {
                _Alpha = value;
                UpdateColor();
            }
        }
        private float _Alpha { get; set; } = 1f;
        public PixelFont Font { get; set; }
        public float FontFaceSize { get; set; }
        public Color TextColor { get; set; } = Color.White;
        public float StrokeSize { get; set; } = 2f;
        public Color StrokeColor { get; set; } = Color.Black;

        public int OffsetX { get; set; } = 5;
        public int OffsetY { get; set; } = 5;

        public Vector2 Justify { get; set; } = new Vector2();

        public float PosX { get; set; } = 0;
        public float PosY { get; set; } = 0;
        public float LineSpacing { get; set; } = 1.1f;

        private static readonly int WIDTH = 1920;
        private static readonly int HEIGHT = 1080;

        public void SetPosition() {
            SetPosition(Position);
        }
        public void SetPosition(StatTextPosition pos) {
            Position = pos;

            switch (pos) {
                case StatTextPosition.TopLeft:
                    PosX = 0 + OffsetX;
                    PosY = 0 + OffsetY;
                    Justify = new Vector2(0, 0);
                    break;

                case StatTextPosition.TopCenter:
                    PosX = (WIDTH / 2) + OffsetX;
                    PosY = 0 + OffsetY;
                    Justify = new Vector2(0.5f, 0);
                    break;

                case StatTextPosition.TopRight:
                    PosX = WIDTH - OffsetX;
                    PosY = 0 + OffsetY;
                    Justify = new Vector2(1, 0);
                    break;
                    
                    
                case StatTextPosition.MiddleLeft:
                    PosX = 0 + OffsetX;
                    PosY = (HEIGHT / 2) + OffsetY;
                    Justify = new Vector2(0, 0.5f);
                    break;

                case StatTextPosition.MiddleCenter:
                    PosX = (WIDTH / 2) + OffsetX;
                    PosY = (HEIGHT / 2) + OffsetY;
                    Justify = new Vector2(0.5f, 0.5f);
                    break;

                case StatTextPosition.MiddleRight:
                    PosX = WIDTH - OffsetX;
                    PosY = (HEIGHT / 2) + OffsetY;
                    Justify = new Vector2(1, 0.5f);
                    break;

                    
                case StatTextPosition.BottomLeft:
                    PosX = 0 + OffsetX;
                    PosY = HEIGHT - OffsetY;
                    Justify = new Vector2(0, 1);
                    break;

                case StatTextPosition.BottomCenter:
                    PosX = (WIDTH / 2) + OffsetX;
                    PosY = HEIGHT - OffsetY;
                    Justify = new Vector2(0.5f, 1f);
                    break;

                case StatTextPosition.BottomRight:
                    PosX = WIDTH - OffsetX;
                    PosY = HEIGHT - OffsetY;
                    Justify = new Vector2(1, 1);
                    break;
            }
        }

        private void UpdateColor() {
            TextColor = new Color(1f, 1f, 1f, Alpha);
            StrokeColor = new Color(0f, 0f, 0f, Alpha);
        }

        public override void Render() {
            base.Render();
            
            // Split text by newlines
            string[] lines = Text.Split('\n');
            
            if (lines.Length == 1)
            {
                // Single line - render as before
                Font.DrawOutline(
                    FontFaceSize,
                    Text,
                    new Vector2(PosX, PosY),
                    Justify,
                    Vector2.One * Scale,
                    TextColor,
                    StrokeSize,
                    StrokeColor
                );
            }
            else
            {
                // Multi-line - render each line with proper spacing
                Vector2 sampleSize = ActiveFont.Measure(lines[0]) * Scale;
                float lineHeight = sampleSize.Y * LineSpacing;
                
                // Calculate total height for vertical centering
                float totalHeight = lineHeight * lines.Length;
                
                // Adjust starting Y position based on justify
                float startY = PosY;
                if (Justify.Y == 0.5f) // Middle justify
                {
                    startY -= totalHeight / 2;
                }
                else if (Justify.Y == 1f) // Bottom justify
                {
                    startY -= totalHeight;
                }
                
                // Render each line
                for (int i = 0; i < lines.Length; i++)
                {
                    float currentY = startY + i * lineHeight;
                    
                    Font.DrawOutline(
                        FontFaceSize,
                        lines[i],
                        new Vector2(PosX, currentY),
                        new Vector2(Justify.X, 0), // Only horizontal justify, vertical handled by offset
                        Vector2.One * Scale,
                        TextColor,
                        StrokeSize,
                        StrokeColor
                    );
                }
            }
        }
    }
}