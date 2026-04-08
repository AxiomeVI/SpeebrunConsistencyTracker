#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities;

public static class GraphInteractivity
{
    private static float _mouseHudX;
    private static float _mouseHudY;

    public static HoverInfo? CurrentHover { get; private set; }

    public static void Update()
    {
        var mouse = Mouse.GetState();
        var vp    = Engine.Viewport;
        _mouseHudX = (mouse.X - vp.X) * (ChartConstants.Screen.ScreenWidth  / (float)vp.Width);
        _mouseHudY = (mouse.Y - vp.Y) * (ChartConstants.Screen.ScreenHeight / (float)vp.Height);

        CurrentHover = GraphManager.CurrentOverlay?.HitTest(new Vector2(_mouseHudX, _mouseHudY));
    }

    public static void Clear()
    {
        CurrentHover = null;
    }

    public static void Render()
    {
        if (CurrentHover != null)
        {
            GraphManager.CurrentOverlay?.DrawHighlight();
            if (CurrentHover.Label.Length > 0)
                DrawTooltip(CurrentHover);
        }
        DrawCursor(_mouseHudX, _mouseHudY);
    }

    private static void DrawTooltip(HoverInfo hover)
    {
        const float scale  = ChartConstants.FontScale.AxisLabelMedium;
        const float bgPad  = ChartConstants.Interactivity.TooltipBgPadding;

        string[] lines      = hover.Label.Split('\n');
        float    lineHeight = ActiveFont.Measure("A").Y * scale;
        float    labelX     = hover.LabelPos.X;
        float    labelY     = hover.LabelPos.Y;

        bool twoColumn = System.Array.TrueForAll(lines, l => l.Contains('\t'));
        if (twoColumn)
        {
            const float colGap = 12f;
            string[] leftParts  = new string[lines.Length];
            string[] rightParts = new string[lines.Length];
            float maxLeftW = 0f, maxRightW = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                int tab = lines[i].IndexOf('\t');
                leftParts[i]  = lines[i][..tab];
                rightParts[i] = lines[i][(tab + 1)..];
                maxLeftW  = System.Math.Max(maxLeftW,  ActiveFont.Measure(leftParts[i]).X  * scale);
                maxRightW = System.Math.Max(maxRightW, ActiveFont.Measure(rightParts[i]).X * scale);
            }
            float totalW = maxLeftW + colGap + maxRightW;
            float totalH = lineHeight * lines.Length;
            float bgX    = labelX - totalW / 2f - bgPad;
            Draw.Rect(bgX, labelY - bgPad, totalW + bgPad * 2f, totalH + bgPad * 2f, Color.Black * 0.92f);
            float leftX  = labelX - totalW / 2f;
            float rightX = leftX + maxLeftW + colGap;
            for (int i = 0; i < lines.Length; i++)
            {
                float rowY = labelY + i * lineHeight;
                ActiveFont.DrawOutline(leftParts[i],  new Vector2(leftX,  rowY), Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
                float rw = ActiveFont.Measure(rightParts[i]).X * scale;
                ActiveFont.DrawOutline(rightParts[i], new Vector2(rightX + maxRightW - rw, rowY), Vector2.Zero, Vector2.One * scale, Color.White, ChartConstants.Stroke.OutlineSize, Color.Black);
            }
            return;
        }

        float maxWidth    = 0f;
        foreach (var line in lines)
            maxWidth = System.Math.Max(maxWidth, ActiveFont.Measure(line).X * scale);

        float totalHeight = lineHeight * lines.Length;
        Draw.Rect(
            labelX - maxWidth / 2f - bgPad,
            labelY - bgPad,
            maxWidth + bgPad * 2f,
            totalHeight + bgPad * 2f,
            Color.Black * 0.92f);

        for (int i = 0; i < lines.Length; i++)
        {
            Vector2 lineSize = ActiveFont.Measure(lines[i]) * scale;
            ActiveFont.DrawOutline(
                lines[i],
                new Vector2(labelX - lineSize.X / 2f, labelY + i * lineHeight),
                Vector2.Zero,
                Vector2.One * scale,
                Color.White,
                ChartConstants.Stroke.OutlineSize,
                Color.Black);
        }
    }

    private static void DrawCursor(float x, float y)
    {
        // Fixed HUD-space crosshair sized for 1920x1080
        // Gap of 4px around center, arms 10px long, 3px thick
        const float gap = 4f;
        const float arm = 6f;
        Color color = Color.Yellow;
        for (float d = -1f; d <= 1f; d++)
        {
            Draw.Line(x - gap - arm, y + d, x - gap, y + d, color);      // left
            Draw.Line(x + gap,       y + d, x + gap + arm, y + d, color); // right
            Draw.Line(x + d, y - gap - arm, x + d, y - gap, color);       // up
            Draw.Line(x + d, y + gap,       x + d, y + gap + arm, color); // down
        }
        // center dot
        Draw.Rect(x - 1f, y - 1f, 3f, 3f, color);
    }
}
