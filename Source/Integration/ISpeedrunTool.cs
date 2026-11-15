namespace Celeste.Mod.SpeebrunConsistencyTracker.Integration;

// SpeedrunTool's RoomTimerType, by value. The values are what ChartCache, MetricsExporter and
// TrajectoryWidget key on, and SpeedrunToolMembers checks them against SpeedrunTool's own enum.
public enum RoomTimerMode
{
    Off = 0,
    NextRoom = 1,
    CurrentRoom = 2,
}

/// <summary>
///     Everything the mod reads from SpeedrunTool, and the two things it asks it to do. The one
///     instance in play is <see cref="Srt.Tool"/>.
/// </summary>
// Nothing outside Source/Integration/ (and the twin's Source/Testing/) may name a SpeedrunTool
// type: a member SpeedrunTool renames then throws when the calling method is JIT-compiled, which
// is inside Level.Update for most readers.
public interface ISpeedrunTool
{
    /// <summary>False when a member the mod binds is missing. Every reader still answers.</summary>
    bool Available { get; }

    /// <summary>Why <see cref="Available"/> is false, for the log. Null when available.</summary>
    string UnavailableReason { get; }

    RoomTimerMode TimerType { get; }
    int NumberOfRooms { get; }

    /// <summary>The CurrentRoom timer's time in ticks, whatever the selected timer type.</summary>
    long CurrentRoomTime { get; }

    /// <summary>The selected room timer has completed.</summary>
    bool TimerCompleted { get; }

    /// <summary>The save slot SpeedrunTool is on, or null when it does not say.</summary>
    string SlotName { get; }

    /// <summary>SpeedrunTool's own room-times export, to its configured destination.</summary>
    void ExportRoomTimes();

    void Popup(string message);
}
