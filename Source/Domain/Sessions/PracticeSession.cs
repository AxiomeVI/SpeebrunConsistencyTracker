using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

public sealed class PracticeSession : IEquatable<PracticeSession>
{
    public string levelName;
    public int MaxRoomCount { get; set; } = 0;
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
    public int TotalDnfs(int segmentLength) => _attempts.Count(a => !a.IsCompleted(segmentLength));
    public int TotalCompleted(int segmentLength) => _attempts.Count(a => a.IsCompleted(segmentLength));

    public IReadOnlyDictionary<int, int> TotalAttemptsPerRoom =>
        _attempts
            .SelectMany(a =>
                Enumerable.Range(0, a.TotalRoomCount)
                        .Select(r => r))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

    public IReadOnlyDictionary<int, int> DnfPerRoom =>
        _attempts
            .GroupBy(a => a.Count) // The dnf room index is Attempt.CompletedRooms.Count
            .ToDictionary(g => g.Key, g => g.Count());

    public IReadOnlyDictionary<int, int> CompletedRunsPerRoom =>
        _attempts
            .Where(a => a.Count > 0)
            .SelectMany(a =>
                Enumerable.Range(0, a.Count)
                        .Select(r => r))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

    public IEnumerable<TimeTicks> GetSegmentTimes(int segmentLength) => _attempts.Where(a => a.Count >= segmentLength).Select(a => a.SegmentTime(segmentLength));

    public IEnumerable<TimeTicks> GetRoomTimes(int roomIndex) =>
        _attempts
            .Where(a => roomIndex < a.Count)
            .Select(a => a.CompletedRooms[roomIndex]);

    public bool Equals(PracticeSession other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return _attempts.Count == other._attempts.Count && (_attempts.Count == 0 || _attempts[^1].Equals(other._attempts[^1]));
    }

    public override bool Equals(object obj)
        => Equals(obj as PracticeSession);

    public override int GetHashCode() => HashCode.Combine(MaxRoomCount, _attempts.Count);
}
