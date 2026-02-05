using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
public static class SessionManager
{
    private static PracticeSession? _currentSession;
    private static AttemptBuilder? _currentAttemptBuilder;

    private static int _currentRoomIndex;
    public static int EndOfChapterCutsceneSkipCounter = 0;
    public static bool EndOfChapterCutsceneSkipCheck = false;

    public static void Reset()
    {
        _currentSession = null;
        _currentAttemptBuilder = null;
        _currentRoomIndex = 0;
        EndOfChapterCutsceneSkipCounter = 0;
        EndOfChapterCutsceneSkipCheck = false;
        MetricsExporter.Reset();
    }


    public static void OnSaveState()
    {
        _currentSession = new PracticeSession();
        _currentAttemptBuilder = new AttemptBuilder();
        _currentRoomIndex = 0;
        EndOfChapterCutsceneSkipCounter = 0;
        EndOfChapterCutsceneSkipCheck = false;
        MetricsExporter.Reset();
    }

    public static void OnClearState()
    {
        Reset();
    }

    public static void OnBeforeLoadState()
    {
        // If the previous attempt is incomplete and some room was timed, mark as DNF
        if (HasActiveAttempt && !RoomTimerIntegration.RoomTimerIsCompleted() && RoomTimerIntegration.GetRoomTime() > 0)
        {
            var dnfRoomIndex = new RoomIndex(_currentRoomIndex);
            TimeTicks ticks = new(RoomTimerIntegration.GetRoomTime());
            _currentAttemptBuilder.SetDnf(dnfRoomIndex, ticks);
            EndCurrentAttempt();
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
        EndOfChapterCutsceneSkipCounter = 0;
        EndOfChapterCutsceneSkipCheck = false;
    }

    public static long CurrentSplitTime()
    {
        return _currentAttemptBuilder.SegmentTime.Ticks;
    }

    public static void CompleteRoom(long ticks)
    {
        if (!HasActiveAttempt)
            return;
        var roomIndex = new RoomIndex(_currentRoomIndex);
        TimeTicks roomTime = new TimeTicks(ticks) - _currentAttemptBuilder.SegmentTime;
        _currentAttemptBuilder.CompleteRoom(roomIndex, roomTime);
        if (!_currentSession.CheckpointAlreadySet() && _currentRoomIndex == 1 && Engine.Scene is Level level)
        {
            _currentSession?.SetCheckpoint(level.Session.LevelData.Name);
        }
        _currentRoomIndex++;
    }


    public static void EndCurrentAttempt()
    {
        if (!HasActiveAttempt)
            return;

        var attempt = _currentAttemptBuilder.Build();
        _currentSession.AddAttempt(attempt);

        // Define room count dynamically if first completed attempt
        if (_currentSession.RoomCount == 0 && attempt.Outcome == AttemptOutcome.Completed)
            _currentSession.RoomCount = attempt.CompletedRooms.Count;

        _currentAttemptBuilder = null;
        EndOfChapterCutsceneSkipCounter = 0;
        EndOfChapterCutsceneSkipCheck = false;
    }

    public static PracticeSession? CurrentSession => _currentSession;
    public static bool IsActive => _currentSession != null;
    public static bool HasActiveAttempt => _currentSession != null && _currentAttemptBuilder != null;
}
