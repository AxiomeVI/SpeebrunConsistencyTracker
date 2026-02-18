using System;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
public class SessionManager
{
    private readonly PracticeSession _currentSession = new();
    private AttemptBuilder _currentAttemptBuilder = new();

    public void OnBeforeLoadState()
    {
        // If the previous attempt is incomplete and some room was timed, mark as DNF
        if (HasActiveAttempt && !RoomTimerIntegration.RoomTimerIsCompleted() && RoomTimerIntegration.GetRoomTime() > 0)
        {
            var dnfRoomIndex = _currentAttemptBuilder.Count;
            TimeTicks ticks = new(RoomTimerIntegration.GetRoomTime());
            _currentAttemptBuilder.SetDnf(dnfRoomIndex, ticks);
            EndCurrentAttempt();
        }
    }

    public void OnLoadState()
    {
        _currentAttemptBuilder = new AttemptBuilder();
    }

    public long CurrentSplitTime()
    {
        return _currentAttemptBuilder.SegmentTime.Ticks;
    }

    public void CompleteRoom(long ticks)
    {
        if (!HasActiveAttempt)
            return;
        TimeTicks roomTime = new TimeTicks(ticks) - _currentAttemptBuilder.SegmentTime;
        if (roomTime > 0)
            _currentAttemptBuilder.CompleteRoom(roomTime);
    }


    public void EndCurrentAttempt()
    {
        if (!HasActiveAttempt)
            return;

        var attempt = _currentAttemptBuilder.Build();
        _currentSession.AddAttempt(attempt);

        // Define room count dynamically if first completed attempt
        if (_currentSession.RoomCount == 0 && attempt.Outcome == AttemptOutcome.Completed)
            _currentSession.RoomCount = attempt.CompletedRooms.Count;

        _currentAttemptBuilder = null;
    }

    public PracticeSession CurrentSession => _currentSession;
    public AttemptBuilder CurrentAttempt => _currentAttemptBuilder;
    public bool HasActiveAttempt => _currentAttemptBuilder != null;
    public int DynamicRoomCount()
    {
        if (_currentSession.RoomCount == 0) {
            return Math.Max(CurrentSession.Attempts.Select(a => a.CompletedRooms.Count).DefaultIfEmpty(0).Max(), _currentAttemptBuilder.Count);
        }
        else
        {
            return _currentSession.RoomCount;
        }
    }
}
