#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities;

public static class GraphInteractivity
{
    private static float _mouseHudX;
    private static float _mouseHudY;

    private static readonly List<HoverInfo> _pinnedItems = new();
    public static IReadOnlyList<HoverInfo> PinnedItems => _pinnedItems;

    private static bool _prevMouseLeft;

    // Cached button rect (set each frame when pins exist, used for hit-test).
    // Off-screen sentinel prevents (0,0) from accidentally matching on the first frame.
    private static Microsoft.Xna.Framework.Rectangle _clearButtonRect = new(-9999, -9999, 0, 0);

    public static HoverInfo? CurrentHover { get; private set; }

    public static void Update()
    {
        var mouse = Mouse.GetState();
        var vp    = Engine.Viewport;
        _mouseHudX = (mouse.X - vp.X) * (ChartConstants.Screen.ScreenWidth  / (float)vp.Width);
        _mouseHudY = (mouse.Y - vp.Y) * (ChartConstants.Screen.ScreenHeight / (float)vp.Height);

        var mousePos = new Vector2(_mouseHudX, _mouseHudY);
        var rawHover = GraphManager.CurrentOverlay?.HitTest(mousePos);
        CurrentHover = rawHover == null ? null : rawHover with { MouseHudPos = mousePos };

        bool leftDown = mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        bool clicked  = leftDown && !_prevMouseLeft;
        _prevMouseLeft = leftDown;

        if (clicked)
        {
            var overlay = GraphManager.CurrentOverlay;
            // Check clear-button first (only active when button is visible)
            if ((_pinnedItems.Count > 0 || (overlay?.HasPins ?? false)) && _clearButtonRect.Contains((int)_mouseHudX, (int)_mouseHudY))
            {
                _pinnedItems.Clear();
                overlay?.ClearPins();
            }
            else if (CurrentHover != null && (overlay?.HandleClick(CurrentHover) ?? false))
            {
                // Overlay handled the click itself (e.g. RunTrajectory manages its own pin state)
            }
            else if (CurrentHover != null)
            {
                int existing = CurrentHover.Key != null
                    ? _pinnedItems.FindIndex(p => p.Key == CurrentHover.Key)
                    : _pinnedItems.FindIndex(p => p.Label == CurrentHover.Label);
                if (existing >= 0)
                {
                    _pinnedItems.RemoveAt(existing);
                }
                else if (CurrentHover.PinGroup != null)
                {
                    // Replace any existing pin in the same group (single-pin-per-group rule)
                    int groupIdx = _pinnedItems.FindIndex(p => p.PinGroup == CurrentHover.PinGroup);
                    if (groupIdx >= 0)
                        _pinnedItems[groupIdx] = CurrentHover;
                    else
                        _pinnedItems.Add(CurrentHover);
                }
                else
                {
                    _pinnedItems.Add(CurrentHover);
                }
            }
            // click in void (CurrentHover == null, not on button) — no action
        }
    }

    public static void Clear()
    {
        CurrentHover     = null;
        _prevMouseLeft   = false;
        _clearButtonRect = new(-9999, -9999, 0, 0);
        _pinnedItems.Clear();
        GraphManager.CurrentOverlay?.ClearPins();
    }

    public static void Render()
    {
        var overlay = GraphManager.CurrentOverlay;

        // 1. Pinned items — highlights
        foreach (var pinned in _pinnedItems)
            overlay?.DrawHighlight(pinned);

        // 2. Hover — highlight
        if (CurrentHover != null)
        {
            // Overlays that manage their own pins drive DrawHighlight via internal state (no-arg).
            // Generic overlays use DrawHighlight(HoverInfo) so pinned items can be re-rendered.
            if (overlay?.ManagesPins == true)
                overlay.DrawHighlight();
            else
                overlay?.DrawHighlight(CurrentHover);
        }

        // 3. Tooltips (drawn after all highlights so they appear on top)
        foreach (var pinned in _pinnedItems)
        {
            if (pinned.Label.Length > 0)
                DrawTooltip(pinned);
        }
        if (CurrentHover != null && CurrentHover.Label.Length > 0)
            DrawTooltip(CurrentHover);

        // 4. "✕ Clear pins" button
        if ((_pinnedItems.Count > 0 || (overlay?.HasPins ?? false)) && overlay != null)
            DrawClearPinsButton(overlay);

        // 5. Cursor (always on top)
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

    private static void DrawClearPinsButton(BaseChartOverlay overlay)
    {
        const string text  = "Clear pins";
        const float  scale = ChartConstants.FontScale.AxisLabelSmall;
        const float  pad   = ChartConstants.Interactivity.TooltipBgPadding;

        var bounds   = overlay.ChartBounds;
        Vector2 size = ActiveFont.Measure(text) * scale;

        float bgW = size.X + pad * 2f;
        float bgH = size.Y + pad * 2f;
        float bgX = bounds.X + bounds.Width  - bgW - 6f;
        float bgY = bounds.Y + 6f;

        // Cache rect for hit-testing in Update()
        _clearButtonRect = new Microsoft.Xna.Framework.Rectangle(
            (int)bgX, (int)bgY, (int)bgW, (int)bgH);

        bool hovered = _clearButtonRect.Contains((int)_mouseHudX, (int)_mouseHudY);

        // Border then background to make the button clearly distinct from the chart background
        Draw.Rect(bgX - 1f, bgY - 1f, bgW + 2f, bgH + 2f, Color.OrangeRed * 0.9f);
        Draw.Rect(bgX, bgY, bgW, bgH, hovered ? Color.OrangeRed * 0.35f : Color.Black * 0.92f);
        ActiveFont.DrawOutline(
            text,
            new Vector2(bgX + pad, bgY + pad),
            Vector2.Zero,
            Vector2.One * scale,
            hovered ? Color.White : Color.OrangeRed,
            ChartConstants.Stroke.OutlineSize,
            Color.Black);
    }

    private static void DrawCursor(float x, float y)
    {
        // Fixed HUD-space crosshair. All rects use top-left + size convention.
        // Thickness 3 → offset -1 from center. Gap 7px each side. Arms 8px long.
        const float gap  = 7f;
        const float arm  = 8f;
        const float half = 1f; // (thickness-1)/2
        Color color = Color.Yellow;

        Draw.Rect(x - gap - arm, y - half, arm, 3f, color); // left
        Draw.Rect(x + gap + 1f,  y - half, arm, 3f, color); // right
        Draw.Rect(x - half, y - gap - arm, 3f, arm, color); // up
        Draw.Rect(x - half, y + gap + 1f,  3f, arm, color); // down
        Draw.Rect(x - half, y - half,      3f, 3f, color);  // center
    }
}
