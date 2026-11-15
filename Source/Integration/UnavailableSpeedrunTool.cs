namespace Celeste.Mod.SpeebrunConsistencyTracker.Integration;

// What the mod reads when SpeedrunTool cannot be bound. It must name no SpeedrunTool type, so it
// is safe to build whatever SpeedrunTool's shape. With the timer Off and no room time, nothing is
// recorded and no session is ever created, since no save-state callback was registered either.
internal sealed class UnavailableSpeedrunTool(string reason) : ISpeedrunTool
{
    public bool Available => false;
    public string UnavailableReason { get; } = reason;

    public RoomTimerMode TimerType => RoomTimerMode.Off;
    public int NumberOfRooms => 0;
    public long CurrentRoomTime => 0L;
    public bool TimerCompleted => false;
    public int SplitCount => 0;
    public bool EndPointExists => false;
    public bool ReachedRoomIdEndPoint => false;
    public string RoomName => null;
    public bool LevelCompleted => false;
    public string SlotName => null;
    public bool PopupShowing => false;

    public void ExportRoomTimes() { }

    // SpeedrunTool's popup is the one that may be missing. The level-load notice uses Celeste's
    // own MiniTextbox instead, see the module's OnLoadLevel.
    public void Popup(string message) =>
        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTracker), $"Popup not shown, SpeedrunTool is unavailable: {message}");
}
