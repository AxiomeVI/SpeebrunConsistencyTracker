using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Monocle;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
public class SessionManager
{
    private readonly PracticeSession _currentSession = new();
    private AttemptBuilder _currentAttemptBuilder = new();

    private int _currentRoomIndex;
    public int EndOfChapterCutsceneSkipCounter = 0;
    public bool EndOfChapterCutsceneSkipCheck = false;

    public void OnBeforeLoadState()
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

    public void OnLoadState()
    {
        _currentAttemptBuilder = new AttemptBuilder();
        _currentRoomIndex = 0;
        EndOfChapterCutsceneSkipCounter = 0;
        EndOfChapterCutsceneSkipCheck = false;
    }

    public long CurrentSplitTime()
    {
        return _currentAttemptBuilder.SegmentTime.Ticks;
    }

    public void CompleteRoom(long ticks)
    {
        if (!HasActiveAttempt)
            return;
        var roomIndex = new RoomIndex(_currentRoomIndex);
        TimeTicks roomTime = new TimeTicks(ticks) - _currentAttemptBuilder.SegmentTime;
        _currentAttemptBuilder.CompleteRoom(roomIndex, roomTime);
        _currentRoomIndex++;
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
        EndOfChapterCutsceneSkipCounter = 0;
        EndOfChapterCutsceneSkipCheck = false;
    }

    public PracticeSession CurrentSession => _currentSession;
    public bool HasActiveAttempt => _currentAttemptBuilder != null;
}
