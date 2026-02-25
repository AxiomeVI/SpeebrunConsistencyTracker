using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
public class SessionManager
{
    private readonly PracticeSession _currentSession = new();
    private Attempt _currentAttempt = new();

    public SessionManager()
    {
        _currentSession.AddAttempt(_currentAttempt);
    }

    public void OnLoadState()
    {
        _currentSession.MaxRoomCount = Math.Max(_currentSession.MaxRoomCount, _currentAttempt?.TotalRoomCount ?? 0);
        _currentAttempt = new Attempt();
        _currentSession.AddAttempt(_currentAttempt);
    }

    public void CompleteRoom(long ticks)
    {
        TimeTicks roomTime = new TimeTicks(ticks) - _currentAttempt.TotalSegmentTime;
        if (roomTime > 0)
            _currentAttempt.CompleteRoom(roomTime);
    }

    public PracticeSession CurrentSession => _currentSession;
    public bool HasActiveAttempt => _currentAttempt != null;

    public int DynamicRoomCount()
    {
        _currentSession.MaxRoomCount = Math.Max(_currentSession.MaxRoomCount, _currentAttempt?.TotalRoomCount ?? 0);
        return Math.Min(_currentSession.MaxRoomCount, SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms);
    }
}
