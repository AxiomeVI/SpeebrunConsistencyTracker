namespace Celeste.Mod.SpeebrunConsistencyTracker.Charts;

// What each control captures the mouse with: one number per control that can be on screen with
// another, so a press started on one is never read by the next. All of them are listed here; a
// new control takes the next number.
internal static class CaptureIds
{
    public const int ModeBar        = 41;   // Absolute | Relative, and the scatter's spread toggle
    public const int Nav            = 42;   // < and >
    public const int Delete         = 43;
    public const int RoomZoom       = 44;
    public const int SegmentZoom    = 45;
    public const int Tools          = 46;   // Save PNG
    public const int TargetDrag     = 47;
    public const int Hint           = 48;
    public const int WallScroll     = 49;
    public const int WallBack       = 50;
    // Shared by the trajectory's two zooms on purpose: one drag pans both axes.
    public const int TrajectoryZoom = 51;
    public const int ClearPins      = 52;
    public const int ResetZoom      = 53;
    public const int HistogramFoot  = 54;
    public const int ColumnScroll   = 55;
    public const int TrajectoryFoot = 56;
    public const int TrajectoryWidget = 57;
    public const int WidgetButton     = 58;
    public const int WidgetEdit       = 59;
    public const int ThresholdFoot    = 60;
}
