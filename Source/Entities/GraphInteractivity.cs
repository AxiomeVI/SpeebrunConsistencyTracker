using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities;

// What one frame of mouse interaction asked for, left to the caller to apply. GraphInteractivity
// returns an intent instead of driving the graph manager itself, so this folder stays free of a
// dependency on SessionManagement.
// NavigationSteps is signed: positive moves forward through the graph slots, negative back.
public readonly record struct GraphInteraction(int NavigationSteps, bool DeleteRequested)
{
    public static readonly GraphInteraction None   = new(0, false);
    public static readonly GraphInteraction Delete = new(0, true);

    public static GraphInteraction Navigate(int steps) => new(steps, false);
}

public static class GraphInteractivity
{
    private static float _mouseHudX;
    private static float _mouseHudY;

    private static readonly List<HoverInfo> _pinnedItems = new();
    public static IReadOnlyList<HoverInfo> PinnedItems => _pinnedItems;

    private static bool _prevMouseLeft;

    // Hit-test rects, refreshed each frame. The off-screen sentinel keeps (0,0) from matching
    // before the first refresh.
    private static Microsoft.Xna.Framework.Rectangle _deleteButtonRect = new(-9999, -9999, 0, 0);
    private static Microsoft.Xna.Framework.Rectangle _prevArrowRect     = new(-9999, -9999, 0, 0);
    private static Microsoft.Xna.Framework.Rectangle _nextArrowRect     = new(-9999, -9999, 0, 0);
    private static Microsoft.Xna.Framework.Rectangle _prevSkipArrowRect = new(-9999, -9999, 0, 0);
    private static Microsoft.Xna.Framework.Rectangle _nextSkipArrowRect = new(-9999, -9999, 0, 0);

    public static HoverInfo? CurrentHover { get; private set; }

    public static GraphInteraction Update(BaseChartOverlay overlay)
    {
        var mouse = Mouse.GetState();
        var vp    = Engine.Viewport;
        _mouseHudX = (mouse.X - vp.X) * (ChartConstants.Screen.ScreenWidth  / (float)vp.Width);
        _mouseHudY = (mouse.Y - vp.Y) * (ChartConstants.Screen.ScreenHeight / (float)vp.Height);

        var mousePos  = new Vector2(_mouseHudX, _mouseHudY);
        var rawHover  = overlay?.HitTest(mousePos);
        CurrentHover  = rawHover == null ? null : rawHover with { MouseHudPos = mousePos };
        int? hoveredCol = overlay?.ColumnHitTest(mousePos); // updates _hoveredColumnIndex for strip tinting

        bool leftDown = mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        bool clicked  = leftDown && !_prevMouseLeft;
        _prevMouseLeft = leftDown;

        if (!clicked)
            return GraphInteraction.None;

        // The caller deletes the pinned runs and then calls ClearPins, keeping the order this
        // branch used to run inline.
        if (_pinnedItems.Count > 0 && (overlay?.SupportsDeleteRuns ?? false) && _deleteButtonRect.Contains((int)_mouseHudX, (int)_mouseHudY))
            return GraphInteraction.Delete;

        if (CurrentHover != null && (overlay?.HandleClick(CurrentHover) ?? false))
            return GraphInteraction.None;

        if (CurrentHover != null)
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
                // One pin per group: the new one replaces it.
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
            return GraphInteraction.None;
        }

        if (_prevSkipArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY))
            return GraphInteraction.Navigate(-3);

        if (_prevArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY))
            return GraphInteraction.Navigate(-1);

        if (_nextArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY))
            return GraphInteraction.Navigate(1);

        if (_nextSkipArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY))
            return GraphInteraction.Navigate(3);

        if (hoveredCol.HasValue)
        {
            overlay!.ToggleColumn(hoveredCol.Value);
            ClearPins(overlay);
        }

        return GraphInteraction.None;
    }

    public static void Clear(BaseChartOverlay overlay)
    {
        CurrentHover      = null;
        _deleteButtonRect = new(-9999, -9999, 0, 0);
        _pinnedItems.Clear();
        overlay?.ClearPins();
        overlay?.ClearHiddenColumns();
    }

    public static void ClearPins(BaseChartOverlay overlay)
    {
        _pinnedItems.Clear();
        overlay?.ClearPins();
    }

    // _prevMouseLeft is physical input state, rewritten every Update. Clear() deliberately leaves
    // it alone: zeroing it on a graph change made a held button read as a fresh click next frame.

    public static void Render(BaseChartOverlay overlay)
    {
        foreach (var pinned in _pinnedItems)
            overlay?.DrawHighlight(pinned);

        if (CurrentHover != null)
        {
            // Self-pinning overlays hold their own hover state; generic ones need the HoverInfo
            // so pinned items can be re-rendered.
            if (overlay?.ManagesPins == true)
                overlay.DrawHighlight();
            else
                overlay?.DrawHighlight(CurrentHover);
        }

        // Buttons before tooltips, so tooltips land on top.
        if (_pinnedItems.Count > 0 && (overlay?.SupportsDeleteRuns ?? false) && overlay != null)
            DrawDeleteRunsButton(overlay);

        foreach (var pinned in _pinnedItems)
        {
            if (pinned.Label.Length > 0)
                DrawTooltip(pinned);
        }
        if (CurrentHover != null && CurrentHover.Label.Length > 0)
            DrawTooltip(CurrentHover);

        if (overlay != null)
            DrawNavigationArrows(overlay);

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
            const float colGap = ChartConstants.Interactivity.TooltipColumnGap;
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
            Draw.Rect(bgX, labelY - bgPad, totalW + bgPad * 2f, totalH + bgPad * 2f, ChartConstants.Colors.PanelBackgroundColor);
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
            ChartConstants.Colors.PanelBackgroundColor);

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

    private static void DrawDeleteRunsButton(BaseChartOverlay overlay)
    {
        const string text  = "Delete";
        const float  scale = ChartConstants.FontScale.AxisLabelSmall;
        const float  pad   = ChartConstants.Interactivity.TooltipBgPadding;

        var bounds   = overlay.ChartBounds;
        Vector2 size = ActiveFont.Measure(text) * scale;

        float bgW = size.X + pad * 2f;
        float bgH = size.Y + pad * 2f;
        float bgX = bounds.X + bounds.Width - bgW - 6f;
        float bgY = bounds.Y + 6f;

        _deleteButtonRect = new Microsoft.Xna.Framework.Rectangle(
            (int)bgX, (int)bgY, (int)bgW, (int)bgH);

        bool hovered = _deleteButtonRect.Contains((int)_mouseHudX, (int)_mouseHudY);

        Draw.Rect(bgX - 1f, bgY - 1f, bgW + 2f, bgH + 2f, Color.Crimson * 0.9f);
        Draw.Rect(bgX, bgY, bgW, bgH, hovered ? Color.Crimson * 0.5f : ChartConstants.Colors.PanelBackgroundColor);
        ActiveFont.DrawOutline(
            text,
            new Vector2(bgX + pad, bgY + pad),
            Vector2.Zero,
            Vector2.One * scale,
            hovered ? Color.White : Color.Crimson,
            ChartConstants.Stroke.OutlineSize,
            Color.Black);
    }

    private static void DrawNavigationArrows(BaseChartOverlay overlay)
    {
        const float gap    = 10f;  // gap between the two buttons
        const float belowY = 8f;   // vertical gap below chart bottom edge

        var bounds = overlay.ChartBounds;

        const float btnW = 50f;
        const float btnH = 30f;

        float totalW = btnW * 4 + gap * 3;
        float startX = bounds.X + (bounds.Width - totalW) / 2f;
        float bgY    = bounds.Y + bounds.Height + belowY;

        float prevSkipX = startX;
        float prevX     = startX + btnW + gap;
        float nextX     = startX + (btnW + gap) * 2;
        float nextSkipX = startX + (btnW + gap) * 3;

        _prevSkipArrowRect = new Microsoft.Xna.Framework.Rectangle((int)prevSkipX, (int)bgY, (int)btnW, (int)btnH);
        _prevArrowRect     = new Microsoft.Xna.Framework.Rectangle((int)prevX,     (int)bgY, (int)btnW, (int)btnH);
        _nextArrowRect     = new Microsoft.Xna.Framework.Rectangle((int)nextX,     (int)bgY, (int)btnW, (int)btnH);
        _nextSkipArrowRect = new Microsoft.Xna.Framework.Rectangle((int)nextSkipX, (int)bgY, (int)btnW, (int)btnH);

        bool prevSkipHovered = _prevSkipArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY);
        bool prevHovered     = _prevArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY);
        bool nextHovered     = _nextArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY);
        bool nextSkipHovered = _nextSkipArrowRect.Contains((int)_mouseHudX, (int)_mouseHudY);

        DrawNavButton(prevSkipX, bgY, btnW, btnH, "<<", prevSkipHovered);
        DrawNavButton(prevX,     bgY, btnW, btnH, "<",  prevHovered);
        DrawNavButton(nextX,     bgY, btnW, btnH, ">",  nextHovered);
        DrawNavButton(nextSkipX, bgY, btnW, btnH, ">>", nextSkipHovered);
    }

    private static void DrawNavButton(float x, float y, float w, float h, string label, bool hovered)
    {
        const float scale = ChartConstants.FontScale.AxisLabelSmall;
        Draw.Rect(x - 1f, y - 1f, w + 2f, h + 2f, Color.White * 0.6f);
        Draw.Rect(x, y, w, h, hovered ? Color.White * 0.25f : ChartConstants.Colors.PanelBackgroundColor);
        ActiveFont.DrawOutline(
            label,
            new Vector2(x + w / 2f, y + h / 2f),
            new Vector2(0.5f, 0.5f),
            Vector2.One * scale,
            hovered ? Color.White : Color.LightGray,
            ChartConstants.Stroke.OutlineSize,
            Color.Black);
    }

    private static void DrawCursor(float x, float y)
    {
        // HUD-space crosshair. Rects are top-left + size, so thickness 3 offsets -1 from center.
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

public static class PinKey
{
    private const string SegPrefix  = "seg:";
    private const string RoomPrefix = "room:";

    public static string ForSegment(int attemptIndex) => $"{SegPrefix}{attemptIndex}";
    public static string ForRoom(int attemptIndex, int visibleRoomIndex) => $"{RoomPrefix}{attemptIndex}:{visibleRoomIndex}";

    public static bool TryParseSegment(string? key, out int attemptIndex)
    {
        attemptIndex = 0;
        return key != null && key.StartsWith(SegPrefix) && int.TryParse(key.Substring(SegPrefix.Length), out attemptIndex);
    }

    public static bool TryParseRoom(string? key, out int attemptIndex, out int visibleRoomIndex)
    {
        attemptIndex = 0;
        visibleRoomIndex = 0;
        if (key == null || !key.StartsWith(RoomPrefix)) return false;
        var parts = key.Split(':');
        return parts.Length == 3
            && int.TryParse(parts[1], out attemptIndex)
            && int.TryParse(parts[2], out visibleRoomIndex);
    }
}
