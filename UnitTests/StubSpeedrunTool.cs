using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;

namespace SpeebrunConsistencyTracker.UnitTests;

// SpeedrunTool's settings as a test sets them. Settable mid-test, as a player can change them.
public sealed class StubSpeedrunTool : ISpeedrunTool
{
    public bool Available { get; set; } = true;
    public string UnavailableReason { get; set; }
    public RoomTimerMode TimerType { get; set; } = RoomTimerMode.NextRoom;
    public int NumberOfRooms { get; set; } = 1;
    public long CurrentRoomTime { get; set; }
    public bool TimerCompleted { get; set; }
    public string SlotName { get; set; }

    public int RoomTimesExports { get; private set; }
    public List<string> Popups { get; } = [];

    public void ExportRoomTimes() => RoomTimesExports++;
    public void Popup(string message) => Popups.Add(message);
}
