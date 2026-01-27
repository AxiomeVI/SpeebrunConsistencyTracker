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
    private static SpeebrunConsistencyTrackerModuleSettings Settings => SpeebrunConsistencyTrackerModule.Settings;

    private static PracticeSession? _currentSession;
    private static AttemptBuilder? _currentAttemptBuilder;

    private static int _currentRoomIndex;

    public static string PreviousRoom { get; set; } = string.Empty;

    public static void Reset()
    {
        _currentSession = null;
        _currentAttemptBuilder = null;
        _currentRoomIndex = 0;
        PreviousRoom = string.Empty;
    }


    public static void OnSaveState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled) return;
        _currentSession = new PracticeSession();
        _currentAttemptBuilder = new AttemptBuilder(0);
        _currentRoomIndex = 0;
        PreviousRoom = string.Empty;
    }

    public static void OnClearState()
    {
        if (!Settings.Enabled) return;
        Reset();
    }

    public static void OnBeforeLoadState(Level level)
    {
        if (!Settings.Enabled) return;
        if (_currentAttemptBuilder != null)
        {
            // If the previous attempt is incomplete and some room was timed, mark as DNF
            if (!RoomTimerIntegration.RoomTimerIsCompleted() && RoomTimerIntegration.GetRoomTime() > 0)
            {
                var dnfRoomIndex = new RoomIndex(_currentRoomIndex);
                TimeTicks ticks = new TimeTicks(RoomTimerIntegration.GetRoomTime());

                _currentAttemptBuilder.SetDnf(dnfRoomIndex, ticks);

                EndCurrentAttempt();
            }
        }
    }

    public static void OnLoadState(Dictionary<Type, Dictionary<string, object>> dictionary, Level level)
    {
        if (!Settings.Enabled) return;
        if (_currentSession == null)
        {
            _currentSession = new PracticeSession();
        }

        var nextIndex = _currentSession.TotalAttempts;
        _currentAttemptBuilder = new AttemptBuilder(nextIndex);
        _currentRoomIndex = 0;
    }

    public static void CompleteRoom(long ticks)
    {
        if (_currentAttemptBuilder == null)
            throw new InvalidOperationException("No active attempt.");

        var roomIndex = new RoomIndex(_currentRoomIndex);
        _currentAttemptBuilder.CompleteRoom(roomIndex, new TimeTicks(ticks) - _currentAttemptBuilder.GetRoomTimeAtIndex(_currentRoomIndex-1));
        _currentRoomIndex++;
    }


    public static void EndCurrentAttempt()
    {
        if (_currentAttemptBuilder == null)
            return;

        var attempt = _currentAttemptBuilder.Build();
        _currentSession.AddAttempt(attempt);

        // Define room count dynamically if first completed attempt
        if (_currentSession.RoomCount == 0 && attempt.Outcome == AttemptOutcome.Completed)
            _currentSession.RoomCount = attempt.CompletedRooms.Count;

        _currentAttemptBuilder = null;
    }

    public static PracticeSession? CurrentSession => _currentSession;
    public static bool HasActiveAttempt => _currentAttemptBuilder != null;
}
