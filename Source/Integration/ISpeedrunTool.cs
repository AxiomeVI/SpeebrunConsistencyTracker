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

    /// <summary>
    ///     How many split times the CurrentRoom timer holds for this run. A split adds one; a
    ///     rewrite of an existing split does not.
    /// </summary>
    int SplitCount { get; }

    /// <summary>An end point is set: an end point entity in the level, or a room-id end point.</summary>
    bool EndPointExists { get; }

    /// <summary>The player is in the room-id end point's room.</summary>
    bool ReachedRoomIdEndPoint { get; }

    /// <summary>The room the room timer sees: the current level's room name, or null outside a level.</summary>
    string RoomName { get; }

    /// <summary>The current level has completed, which ends the room timer's run.</summary>
    bool LevelCompleted { get; }

    /// <summary>The save slot SpeedrunTool is on, or null when it does not say.</summary>
    string SlotName { get; }

    /// <summary>SpeedrunTool's own room-times export, to its configured destination.</summary>
    void ExportRoomTimes();

    void Popup(string message);
}
