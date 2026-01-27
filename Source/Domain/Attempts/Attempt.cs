using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class Attempt
{
    public int Index { get; }
    public DateTime Timestamp { get; }
    public AttemptOutcome Outcome { get; }

    public IReadOnlyDictionary<RoomIndex, TimeTicks> CompletedRooms { get; }

    public DnfInfo? DnfInfo { get; }

    public bool IsCompleted => Outcome == AttemptOutcome.Completed;

    private Attempt(
        int index,
        DateTime timestamp,
        AttemptOutcome outcome,
        Dictionary<RoomIndex, TimeTicks> completedRooms,
        DnfInfo? dnf)
    {
        Index = index;
        Timestamp = timestamp;
        Outcome = outcome;
        CompletedRooms = completedRooms;
        DnfInfo = dnf;
    }

    public static Attempt Completed(
        int index,
        DateTime timestamp,
        Dictionary<RoomIndex, TimeTicks> roomTicks)
    {
        return new Attempt(
            index,
            timestamp,
            AttemptOutcome.Completed,
            roomTicks,
            null
        );
    }

    public static Attempt Dnf(
        int index,
        DateTime timestamp,
        Dictionary<RoomIndex, TimeTicks> completedRooms,
        DnfInfo dnf)
    {
        if (dnf is null)
            throw new ArgumentNullException(nameof(dnf));

        return new Attempt(
            index,
            timestamp,
            AttemptOutcome.Dnf,
            completedRooms,
            dnf
        );
    }

    public TimeTicks SegmentTime
        => new TimeTicks(CompletedRooms.Values.Sum(t => t.Ticks));

    public int TotalRoomCount 
        => CompletedRooms.Count + (DnfInfo != null ? 1 : 0);
}
