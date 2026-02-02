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
    }
}
