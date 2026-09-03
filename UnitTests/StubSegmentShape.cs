using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

namespace SpeebrunConsistencyTracker.UnitTests;

// Both values are settable so a test can move them mid-session, as the real implementation does
// when SpeedrunTool reports a new room count or the player flips the timer type.
public sealed class StubSegmentShape(int startRoomIndex = 0, int roomCount = 3) : ISegmentShape
{
    public int StartRoomIndex { get; set; } = startRoomIndex;
    public int RoomCount { get; set; } = roomCount;
}
