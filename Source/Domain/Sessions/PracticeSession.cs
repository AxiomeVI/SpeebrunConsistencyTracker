using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

public sealed class PracticeSession : IEquatable<PracticeSession>
{
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public string levelName;
    public int RoomCount { get; set; }
    private readonly List<Attempt> _attempts = [];
    public IReadOnlyList<Attempt> Attempts => _attempts;

    public PracticeSession ()
    {
        if (Engine.Scene is Level level)
        {
            string[] parts = level.Session.Area.GetSID().Split('-', 2);
            levelName = parts.Length > 1 ? parts[1] : "unknown";
        }
    }

    public void AddAttempt(Attempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
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

    public bool Equals(PracticeSession other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return StartedAt == other.StartedAt
            && RoomCount == other.RoomCount
            && _attempts.Count == other._attempts.Count;
    }

    public override bool Equals(object obj)
        => Equals(obj as PracticeSession);

    public override int GetHashCode()
        => HashCode.Combine(StartedAt, RoomCount, _attempts.Count);
}
