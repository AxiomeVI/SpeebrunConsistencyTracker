using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
public static class SessionManager
{
    private static PracticeSession? _currentSession;
    private static AttemptBuilder? _currentAttemptBuilder;

    private static int _currentRoomIndex;

    public static string PreviousRoom { get; set; } = "";

    public static void Reset()
    {
        _currentSession = null;
        _currentAttemptBuilder = null;
        _currentRoomIndex = 0;
        PreviousRoom = "";
    }


    public static void OnSaveState()
    {
        _currentSession = new PracticeSession();
        _currentAttemptBuilder = new AttemptBuilder();
        _currentRoomIndex = 0;
        PreviousRoom = "";
    }

    public static void OnClearState()
    {
        Reset();
    }

    public static void OnBeforeLoadState()
    {
        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), "OnBeforeLoadState");
        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"IsActive: {IsActive.ToString()}");
        if (IsActive)
        {
            Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"RoomTimerIntegration.RoomTimerIsCompleted(): ${RoomTimerIntegration.RoomTimerIsCompleted()}");
            Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"RoomTimerIntegration.GetRoomTime(): ${RoomTimerIntegration.GetRoomTime()}");
            // If the previous attempt is incomplete and some room was timed, mark as DNF
            if (!RoomTimerIntegration.RoomTimerIsCompleted() && RoomTimerIntegration.GetRoomTime() > 0)
            {
                Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), "End current attempt early");
                var dnfRoomIndex = new RoomIndex(_currentRoomIndex);
                TimeTicks ticks = new TimeTicks(RoomTimerIntegration.GetRoomTime());
                Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"DNF room: ${dnfRoomIndex}");
                _currentAttemptBuilder.SetDnf(dnfRoomIndex, ticks);
                EndCurrentAttempt();
            }
        }
    }

    public static void OnLoadState()
    {
        if (_currentSession == null)
        {
            _currentSession = new PracticeSession();
        }

        _currentAttemptBuilder = new AttemptBuilder();
        _currentRoomIndex = 0;
    }

    public static void CompleteRoom(long ticks)
    {
        if (!IsActive)
            return;
        var roomIndex = new RoomIndex(_currentRoomIndex);
        TimeTicks roomTime = new TimeTicks(ticks) - _currentAttemptBuilder.SegmentTime;
        _currentAttemptBuilder.CompleteRoom(roomIndex, roomTime);
        _currentRoomIndex++;
        Logger.Log(LogLevel.Info, nameof(SpeebrunConsistencyTrackerModule), $"Room added: {roomTime}");
    }


    public static void EndCurrentAttempt()
    {
        if (!IsActive)
            return;

        var attempt = _currentAttemptBuilder.Build();
        _currentSession.AddAttempt(attempt);

        // Define room count dynamically if first completed attempt
        if (_currentSession.RoomCount == 0 && attempt.Outcome == AttemptOutcome.Completed)
            _currentSession.RoomCount = attempt.CompletedRooms.Count;

        _currentAttemptBuilder = null;
    }

    public static PracticeSession? CurrentSession => _currentSession;
    public static bool IsActive => _currentSession != null && _currentAttemptBuilder != null;
}
