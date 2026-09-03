namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

// The visible segment of an attempt row: rooms [StartRoomIndex, StartRoomIndex + RoomCount).
// Both values change while a session is running (SRT NumberOfRooms / RoomTimerType, and the
// attempts recorded so far), so implementations must be read live, never snapshotted.
public interface ISegmentShape
{
    int StartRoomIndex { get; }
    int RoomCount { get; }
}
