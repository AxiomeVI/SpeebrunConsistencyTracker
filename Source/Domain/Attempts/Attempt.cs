using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class Attempt() : IEquatable<Attempt>
{
    // Raw running sum of ALL completed rooms including any StartRoomIndex prefix rooms.
    // Use SegmentTime() for the user-visible segment time.
    public TimeTicks TotalSegmentTime = TimeTicks.Zero;
    public List<TimeTicks> CompletedRooms = [];

    private static int RoomCount => SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;
    private static int StartRoomIndex => SessionManagement.SessionManager.StartRoomIndex;
    private int VisibleRoomCount => Math.Max(0, CompletedRooms.Count - StartRoomIndex);

    public void CompleteRoom(TimeTicks ticks)
    {
        CompletedRooms.Add(ticks);
        TotalSegmentTime += ticks;
    }

    public bool IsCompleted() => VisibleRoomCount >= RoomCount;

    public int TotalRoomCount => VisibleRoomCount + 1;

    public int Count => VisibleRoomCount;

    public TimeTicks SegmentTime()
    {
        int start = StartRoomIndex;
        int end = Math.Min(start + RoomCount, CompletedRooms.Count);
        if (end <= start) return TimeTicks.Zero;

        TimeTicks totalTicks = TimeTicks.Zero;
        foreach (var room in CompletedRooms[start..end])
        {
            totalTicks += room;
        }

        return totalTicks;
    }

    public bool Equals(Attempt other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return TotalSegmentTime == other.TotalSegmentTime && CompletedRooms.Count == other.CompletedRooms.Count;
    }

    public override bool Equals(object obj)
        => Equals(obj as Attempt);

    public override int GetHashCode() => HashCode.Combine(TotalSegmentTime.Ticks, CompletedRooms.Count);
}
