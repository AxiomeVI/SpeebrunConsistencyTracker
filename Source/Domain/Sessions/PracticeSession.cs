using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

public sealed class PracticeSession
{
    public int MaxRoomCount { get; set; } = 0;
    public uint Version { get; private set; } = 0;
    private readonly List<Attempt> _attempts = [];
    public IReadOnlyList<Attempt> Attempts => _attempts;

    public Attempt CurrentAttempt { get; private set; }

    public PracticeSession()
    {
        StartNewAttempt();
    }

    public void StartNewAttempt()
    {
        CurrentAttempt = new Attempt();
        AddAttempt(CurrentAttempt);
    }

    private void AddAttempt(Attempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        _attempts.Add(attempt);
        Version++;
    }

    public void BumpVersion() => Version++;

    public void RecomputeMaxRoomCount()
    {
        MaxRoomCount = _attempts.Count > 0 ? _attempts.Max(a => a.TotalRoomCount) : 0;
    }

    public int TotalAttempts => _attempts.Count;
    public int TotalDnfs() => _attempts.Count(a => !a.IsCompleted());
    public int TotalCompleted() => _attempts.Count(a => a.IsCompleted());

    // Cached per-room dictionaries — invalidated when Version changes.
    private uint _cachedVersion = uint.MaxValue;
    private Dictionary<int, int> _totalAttemptsPerRoom;
    private Dictionary<int, int> _dnfPerRoom;
    private Dictionary<int, int> _completedRunsPerRoom;

    private void RefreshPerRoomCaches()
    {
        if (_cachedVersion == Version) return;
        _cachedVersion = Version;
        _totalAttemptsPerRoom = _attempts
            .SelectMany(a => Enumerable.Range(0, a.TotalRoomCount))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());
        _dnfPerRoom = _attempts
            .Where(a => !a.IsCompleted())
            .GroupBy(a => a.Count)
            .ToDictionary(g => g.Key, g => g.Count());
        _completedRunsPerRoom = _attempts
            .Where(a => a.Count > 0)
            .SelectMany(a => Enumerable.Range(0, a.Count))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public IReadOnlyDictionary<int, int> TotalAttemptsPerRoom { get { RefreshPerRoomCaches(); return _totalAttemptsPerRoom; } }
    public IReadOnlyDictionary<int, int> DnfPerRoom           { get { RefreshPerRoomCaches(); return _dnfPerRoom; } }
    public IReadOnlyDictionary<int, int> CompletedRunsPerRoom { get { RefreshPerRoomCaches(); return _completedRunsPerRoom; } }

    public IEnumerable<TimeTicks> GetSegmentTimes() =>
        _attempts.Where(a => a.IsCompleted()).Select(a => a.SegmentTime());

    public IEnumerable<TimeTicks> GetRoomTimes(int roomIndex) =>
        _attempts
            .Where(a => roomIndex < a.Count)
            .Select(a => a.GetRoomTime(roomIndex));

    /// <summary>
    /// Returns the original attempt indices (into _attempts) for attempts that reached roomIndex.
    /// Parallel to GetRoomTimes(roomIndex).
    /// </summary>
    public IEnumerable<int> GetRoomAttemptIndices(int roomIndex) =>
        Enumerable.Range(0, _attempts.Count)
            .Where(i => roomIndex < _attempts[i].Count);

    /// <summary>
    /// Removes attempts at the given indices. Indices are processed in descending order so
    /// earlier removals don't shift later ones. Bumps Version afterward.
    /// </summary>
    public void RemoveAttempts(IReadOnlyList<int> indices)
    {
        foreach (int i in indices.OrderByDescending(x => x))
            _attempts.RemoveAt(i);
        Version++;
    }
}
