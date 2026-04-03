using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

public sealed class PracticeSession
{
    public string levelName;
    public int MaxRoomCount { get; set; } = 0;
    public uint Version { get; private set; } = 0;
    private readonly List<Attempt> _attempts = [];
    public IReadOnlyList<Attempt> Attempts => _attempts;

    public Attempt CurrentAttempt { get; private set; }

    public PracticeSession()
    {
        if (Engine.Scene is Level level)
        {
            string[] parts = level.Session.Area.GetSID().Split('-', 2);
            levelName = parts.Length > 1 ? parts[1] : "unknown";
        }
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

    public IReadOnlyDictionary<int, int> TotalAttemptsPerRoom =>
        _attempts
            .SelectMany(a => Enumerable.Range(0, a.TotalRoomCount))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

    public IReadOnlyDictionary<int, int> DnfPerRoom =>
        _attempts
            .Where(a => !a.IsCompleted())
            .GroupBy(a => a.Count)
            .ToDictionary(g => g.Key, g => g.Count());

    public IReadOnlyDictionary<int, int> CompletedRunsPerRoom =>
        _attempts
            .Where(a => a.Count > 0)
            .SelectMany(a => Enumerable.Range(0, a.Count))
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

    public IEnumerable<TimeTicks> GetSegmentTimes() =>
        _attempts.Where(a => a.IsCompleted()).Select(a => a.SegmentTime());

    public IEnumerable<TimeTicks> GetRoomTimes(int roomIndex) =>
        _attempts
            .Where(a => roomIndex < a.Count)
            .Select(a => a.CompletedRooms[SessionManagement.SessionManager.StartRoomIndex + roomIndex]);
}
