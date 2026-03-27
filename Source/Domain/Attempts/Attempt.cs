using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class Attempt() : IEquatable<Attempt>
{
    public TimeTicks TotalSegmentTime = TimeTicks.Zero;
    public List<TimeTicks> CompletedRooms = [];

    private static int RoomCount => SpeebrunConsistencyTrackerModule.Instance.sessionManager.RoomCount;

    public void CompleteRoom(TimeTicks ticks)
    {
        CompletedRooms.Add(ticks);
        TotalSegmentTime += ticks;
    }

    public bool IsCompleted() => CompletedRooms.Count >= RoomCount;

    public int TotalRoomCount => CompletedRooms.Count + 1;

    public int Count => CompletedRooms.Count;

    public TimeTicks SegmentTime()
    {
        int countToSum = Math.Min(RoomCount, CompletedRooms.Count);
        if (countToSum <= 0) return TimeTicks.Zero;

        TimeTicks totalTicks = TimeTicks.Zero;
        foreach (var room in CompletedRooms[..countToSum])
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
