using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

public sealed class PracticeSession
{
    private readonly AttemptMatrix _matrix;
    public int MaxRoomCount { get; set; } = 0;
    public uint Version { get; private set; } = 0;

    // Active attempt recording state
    public int CurrentAttemptIndex { get; private set; } = -1;
    public int CurrentRoomIndex { get; private set; } = 0;
    public TimeTicks RunningSegmentTime { get; private set; } = TimeTicks.Zero;

    private static int StartRoomIndex => SessionManagement.SessionManager.StartRoomIndex;
    private static int RoomCount => SessionManagement.SessionManager.RoomCount;

    public PracticeSession(int initialColumnCapacity = 16, int initialRowCapacity = 64)
    {
        _matrix = new AttemptMatrix(initialColumnCapacity, initialRowCapacity);
        StartNewAttempt();
    }

    // --- Recording ---

    public void StartNewAttempt()
    {
        // Finalize prior attempt: mark current room as DNF if recording started
        if (CurrentAttemptIndex >= 0 && CurrentRoomIndex > 0)
        {
            _matrix.EnsureColumns(CurrentRoomIndex + 1);
            _matrix.SetCell(CurrentAttemptIndex, CurrentRoomIndex, RoomCell.DNF);
        }

        _matrix.AddRow();
        CurrentAttemptIndex = _matrix.RowCount - 1;
        CurrentRoomIndex = 0;
        RunningSegmentTime = TimeTicks.Zero;
        Version++;
    }

    public void CompleteRoom(TimeTicks time)
    {
        int visibleIndex = CurrentRoomIndex - StartRoomIndex;
        _matrix.EnsureColumns(CurrentRoomIndex + 1);
        _matrix.SetCell(CurrentAttemptIndex, CurrentRoomIndex, RoomCell.Completed(time));
        CurrentRoomIndex++;
        RunningSegmentTime += time;
        // Skip Version bump once we're past the visible segment — downstream caches don't depend on those cells.
        if (visibleIndex < RoomCount) Version++;
    }

    // --- Cell access ---

    public RoomCell GetCell(int attemptIndex, int visibleRoomIndex)
        => _matrix[attemptIndex, StartRoomIndex + visibleRoomIndex];

    public void DeleteCell(int attemptIndex, int visibleRoomIndex)
    {
        int raw = StartRoomIndex + visibleRoomIndex;
        var current = _matrix[attemptIndex, raw].State;
        if (current == RoomCellState.NotReached || current == RoomCellState.Deleted) return;
        _matrix.SetCell(attemptIndex, raw, RoomCell.Deleted);
        Version++;
    }

    public void DeleteAttempt(int attemptIndex)
    {
        var row = _matrix.GetRow(attemptIndex);
        bool changed = false;
        for (int c = 0; c < row.Length; c++)
        {
            if (row[c].State != RoomCellState.NotReached)
            {
                _matrix.SetCell(attemptIndex, c, RoomCell.Deleted);
                changed = true;
            }
        }
        if (changed) Version++;
    }

    // --- Column queries (per-room) ---

    public IEnumerable<TimeTicks> GetRoomTimes(int visibleRoomIndex)
    {
        int raw = StartRoomIndex + visibleRoomIndex;
        if (raw >= _matrix.ColumnCount) yield break;
        foreach (var cell in _matrix.GetColumn(raw))
            if (cell.HasTime) yield return cell.Time;
    }

    public IEnumerable<int> GetRoomAttemptIndices(int visibleRoomIndex)
    {
        int raw = StartRoomIndex + visibleRoomIndex;
        if (raw >= _matrix.ColumnCount) yield break;
        int i = 0;
        foreach (var cell in _matrix.GetColumn(raw))
        {
            if (cell.HasTime) yield return i;
            i++;
        }
    }

    // --- Row queries (per-attempt) ---

    public int ContiguousCount(int attemptIndex)
    {
        int start = StartRoomIndex;
        int count = 0;
        var row = _matrix.GetRow(attemptIndex);
        for (int c = start; c < row.Length; c++)
        {
            if (row[c].State != RoomCellState.Completed) break;
            count++;
        }
        return count;
    }

    public int ReachedRoomCount(int attemptIndex)
    {
        var row = _matrix.GetRow(attemptIndex);
        int highest = -1;
        for (int c = row.Length - 1; c >= StartRoomIndex; c--)
        {
            if (row[c].State != RoomCellState.NotReached)
            {
                highest = c;
                break;
            }
        }
        return Math.Max(0, highest - StartRoomIndex + 1);
    }

    public bool IsCompleted(int attemptIndex)
        => ContiguousCount(attemptIndex) >= RoomCount;

    public TimeTicks SegmentTime(int attemptIndex)
    {
        int start = StartRoomIndex;
        int end = start + RoomCount;
        var row = _matrix.GetRow(attemptIndex);
        TimeTicks total = TimeTicks.Zero;
        for (int c = start; c < end && c < row.Length; c++)
        {
            if (!row[c].HasTime) return TimeTicks.Zero;
            total += row[c].Time;
        }
        return total;
    }

    // --- Aggregates ---

    public int AttemptCount => _matrix.RowCount;

    // Version-cached aggregates
    private uint _cachedVersion = uint.MaxValue;
    private int _cachedTotalAttempts;
    private int _cachedTotalCompleted;
    private Dictionary<int, int> _totalAttemptsPerRoom;
    private Dictionary<int, int> _dnfPerRoom;
    private Dictionary<int, int> _completedRunsPerRoom;

    private void RefreshPerRoomCaches()
    {
        if (_cachedVersion == Version) return;
        _cachedVersion = Version;

        int roomCount = RoomCount;
        int start = StartRoomIndex;
        int[] totals = new int[roomCount];
        int[] dnfs = new int[roomCount];
        int[] completeds = new int[roomCount];
        int totalAttempts = 0;
        int totalCompleted = 0;

        for (int a = 0; a < _matrix.RowCount; a++)
        {
            var row = _matrix.GetRow(a);
            bool rowCounted = false;
            bool rowCompleted = true;
            for (int r = 0; r < roomCount; r++)
            {
                int c = start + r;
                var state = c < row.Length ? row[c].State : RoomCellState.NotReached;
                if (state == RoomCellState.Completed)
                {
                    totals[r]++;
                    completeds[r]++;
                    if (!rowCounted) { totalAttempts++; rowCounted = true; }
                }
                else
                {
                    rowCompleted = false;
                    if (state == RoomCellState.DNF)
                    {
                        totals[r]++;
                        dnfs[r]++;
                        if (!rowCounted) { totalAttempts++; rowCounted = true; }
                    }
                }
            }
            if (rowCompleted && rowCounted) totalCompleted++;
        }

        _totalAttemptsPerRoom = new Dictionary<int, int>(roomCount);
        _dnfPerRoom = new Dictionary<int, int>(roomCount);
        _completedRunsPerRoom = new Dictionary<int, int>(roomCount);
        for (int r = 0; r < roomCount; r++)
        {
            _totalAttemptsPerRoom[r] = totals[r];
            _dnfPerRoom[r] = dnfs[r];
            _completedRunsPerRoom[r] = completeds[r];
        }
        _cachedTotalAttempts = totalAttempts;
        _cachedTotalCompleted = totalCompleted;
    }

    public int TotalAttempts { get { RefreshPerRoomCaches(); return _cachedTotalAttempts; } }
    public IReadOnlyDictionary<int, int> TotalAttemptsPerRoom { get { RefreshPerRoomCaches(); return _totalAttemptsPerRoom; } }
    public IReadOnlyDictionary<int, int> DnfPerRoom { get { RefreshPerRoomCaches(); return _dnfPerRoom; } }
    public IReadOnlyDictionary<int, int> CompletedRunsPerRoom { get { RefreshPerRoomCaches(); return _completedRunsPerRoom; } }

    public int TotalCompleted { get { RefreshPerRoomCaches(); return _cachedTotalCompleted; } }

    public int TotalDnfs => TotalAttempts - TotalCompleted;

    public IEnumerable<TimeTicks> GetSegmentTimes()
    {
        for (int a = 0; a < _matrix.RowCount; a++)
            if (IsCompleted(a))
                yield return SegmentTime(a);
    }

    public IEnumerable<int> GetCompletedAttemptIndices()
    {
        for (int a = 0; a < _matrix.RowCount; a++)
            if (IsCompleted(a))
                yield return a;
    }

    public void RecomputeMaxRoomCount()
    {
        int max = 0;
        for (int a = 0; a < _matrix.RowCount; a++)
        {
            int reached = ReachedRoomCount(a);
            if (reached > max) max = reached;
        }
        MaxRoomCount = max;
        BumpMaxForActiveAttempt();
    }

    public void BumpMaxForActiveAttempt()
    {
        int activeReach = Math.Max(0, CurrentRoomIndex - StartRoomIndex + 1);
        if (activeReach > MaxRoomCount) MaxRoomCount = activeReach;
    }
}
