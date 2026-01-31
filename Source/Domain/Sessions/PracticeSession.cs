using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

public sealed class PracticeSession
{
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public int RoomCount { get; set; }
    private readonly List<Attempt> _attempts = new();
    public IReadOnlyList<Attempt> Attempts => _attempts;


    public void AddAttempt(Attempt attempt)
    {
        if (attempt == null) 
            throw new ArgumentNullException(nameof(attempt));

        _attempts.Add(attempt);
    }

    public int TotalAttempts => _attempts.Count;
    public int TotalDnfs => _attempts.Count(a => a.Outcome == AttemptOutcome.Dnf);
    public int TotalCompleted => _attempts.Count(a => a.Outcome == AttemptOutcome.Completed);

    public IReadOnlyDictionary<RoomIndex, int> TotalAttemptsPerRoom =>
    _attempts
        .SelectMany(a =>
            Enumerable.Range(0, a.TotalRoomCount)
                      .Select(r => (RoomIndex)r))
        .GroupBy(r => r)
        .ToDictionary(g => g.Key, g => g.Count());

    public IReadOnlyDictionary<RoomIndex, int> DnfPerRoom =>
        _attempts
            .Where(a => !a.IsCompleted)
            .GroupBy(a => a.DnfInfo.Room)
            .ToDictionary(g => g.Key, g => g.Count());

    public IReadOnlyDictionary<RoomIndex, int> CompletedRunsPerRoom =>
        _attempts
            .Where(a => a.IsCompleted || a.CompletedRooms.Count > 0)
            .SelectMany(a =>
                Enumerable.Range(0, a.CompletedRooms.Count)
                        .Select(r => new RoomIndex(r)))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

    public IEnumerable<TimeTicks> GetSegmentTimes() =>
        _attempts
            .Where(a => a.Outcome == AttemptOutcome.Completed)
            .Select(a => a.SegmentTime);

    public IEnumerable<TimeTicks> GetRoomTimes(int roomIndex) =>
        _attempts
            .Where(a => a.Outcome == AttemptOutcome.Completed)
            .Where(a => a.CompletedRooms.ContainsKey(new RoomIndex(roomIndex)))
            .Select(a => a.CompletedRooms[new RoomIndex(roomIndex)]);
}
