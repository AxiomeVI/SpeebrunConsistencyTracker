using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class Attempt
{
    public DateTime Timestamp { get; }
    public AttemptOutcome Outcome { get; }

    public TimeTicks SegmentTime { get; }

    public IReadOnlyDictionary<int, TimeTicks> CompletedRooms { get; }

    public DnfInfo? DnfInfo { get; }

    public bool IsCompleted => Outcome == AttemptOutcome.Completed;

    private Attempt(
        DateTime timestamp,
        AttemptOutcome outcome,
        Dictionary<int, TimeTicks> completedRooms,
        TimeTicks segmentTime,
        DnfInfo? dnf)
    {
        Timestamp = timestamp;
        Outcome = outcome;
        CompletedRooms = completedRooms;
        SegmentTime = segmentTime;
        DnfInfo = dnf;
    }

    public static Attempt Completed(
        DateTime timestamp,
        Dictionary<int, TimeTicks> roomTicks,
        TimeTicks segmentTime)
    {
        return new Attempt(
            timestamp,
            AttemptOutcome.Completed,
            roomTicks,
            segmentTime,
            null
        );
    }

    public static Attempt Dnf(
        DateTime timestamp,
        Dictionary<int, TimeTicks> completedRooms,
        TimeTicks segmentTime,
        DnfInfo dnf)
    {
        ArgumentNullException.ThrowIfNull(dnf);

        return new Attempt(
            timestamp,
            AttemptOutcome.Dnf,
            completedRooms,
            segmentTime,
            dnf
        );
    }

    public int TotalRoomCount 
        => CompletedRooms.Count + (DnfInfo != null ? 1 : 0);
}
