using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeedrunTool.RoomTimer;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
public class SessionManager
{
    private readonly PracticeSession _currentSession = new();
    private Attempt _currentAttempt = new();

    public int RoomCount { get; private set; } = 0;

    public static int StartRoomIndex =>
        SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType == RoomTimerType.CurrentRoom ? 0 : 1;

    private RoomTimerType _lastRoomTimerType = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

    public SessionManager()
    {
        _currentSession.AddAttempt(_currentAttempt);
        UpdateRoomCount();
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
        {
            int roomIndex = _currentAttempt.Count; // captured before CompleteRoom mutates Count
            _currentAttempt.CompleteRoom(roomTime);
            if (roomIndex < RoomCount)
                _currentSession.BumpVersion();
        }
    }

    public PracticeSession CurrentSession => _currentSession;
    public bool HasActiveAttempt => _currentAttempt != null;

    public void UpdateRoomCount()
    {
        var currentType = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;
        if (currentType != _lastRoomTimerType)
        {
            _lastRoomTimerType = currentType;
            _currentSession.RecomputeMaxRoomCount();
        }

        int attemptRooms = _currentAttempt?.TotalRoomCount ?? 0;
        if (attemptRooms > _currentSession.MaxRoomCount)
            _currentSession.MaxRoomCount = attemptRooms;
        RoomCount = Math.Min(_currentSession.MaxRoomCount, SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms);
    }
}
