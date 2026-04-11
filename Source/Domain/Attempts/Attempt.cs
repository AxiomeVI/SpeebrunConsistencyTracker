using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class Attempt() : IEquatable<Attempt>
{
    public readonly DateTime CreatedAt = DateTime.UtcNow;

    // Raw running sum of ALL completed rooms including any StartRoomIndex prefix rooms.
    // Use SegmentTime() for the user-visible segment time.
    public TimeTicks TotalSegmentTime = TimeTicks.Zero;
    private readonly List<TimeTicks> _completedRooms = [];

    private static int RoomCount => SessionManagement.SessionManager.RoomCount;
    private static int StartRoomIndex => SessionManagement.SessionManager.StartRoomIndex;
    private int VisibleRoomCount => Math.Max(0, _completedRooms.Count - StartRoomIndex);

    public void CompleteRoom(TimeTicks ticks)
    {
        _completedRooms.Add(ticks);
        TotalSegmentTime += ticks;
    }

    public TimeTicks GetRoomTime(int visibleIndex) => _completedRooms[StartRoomIndex + visibleIndex];

    public bool IsCompleted() => VisibleRoomCount >= RoomCount;

    // +1 so that MaxRoomCount always "sees" the room the player is currently in,
    // even before they complete it. Used only for MaxRoomCount tracking.
    public int TotalRoomCount => VisibleRoomCount + 1;

    public int Count => VisibleRoomCount;

    public TimeTicks SegmentTime()
    {
        int start = StartRoomIndex;
        int end = Math.Min(start + RoomCount, _completedRooms.Count);
        if (end <= start) return TimeTicks.Zero;

        TimeTicks totalTicks = TimeTicks.Zero;
        foreach (var room in _completedRooms[start..end])
        {
            totalTicks += room;
        }

        return totalTicks;
    }

    public bool Equals(Attempt other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CreatedAt == other.CreatedAt;
    }

    public override bool Equals(object obj)
        => Equals(obj as Attempt);

    public override int GetHashCode() => CreatedAt.GetHashCode();
}
