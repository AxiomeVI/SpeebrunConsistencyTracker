using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeebrunConsistencyTracker.Entities;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public static class SessionManager
{
    private static readonly Dictionary<string, PracticeSession> _slots = new();
    public static PracticeSession CurrentSession { get; private set; }

    // Shared across slots: every session in play is in the same level. Set from OnLoadLevel.
    // Entering another level drops the stash, which only holds sessions of the level it was made in;
    // entering the same one again (from the overworld) keeps it.
    public static string LevelName
    {
        get => _levelName;
        set
        {
            if (value != _levelName) DropStash();
            _levelName = value;
        }
    }
    private static string _levelName = "unknown";

    // Counted again on read when the session, the timer type, NumberOfRooms or the end point moved since the last count: the
    // trajectory widget reads it with no chart open, where nothing else calls UpdateRoomCount
    // before the next room.
    public static int RoomCount
    {
        get
        {
            if (CountIsStale) UpdateRoomCount();
            return _roomCount;
        }
        private set => _roomCount = value;
    }
    private static int _roomCount;

    public static int StartRoomIndex =>
        Srt.Tool.TimerType == RoomTimerMode.CurrentRoom ? 0 : 1;

    // Null until the first UpdateRoomCount, which then recounts. Not read at type initialisation:
    // a failure there would poison this type for the whole process.
    private static RoomTimerMode? _lastRoomTimerType;
    // The session the count was last taken for. The timer type is one for every slot: a slot that
    // was not current when it changed still holds a count taken under the old type.
    private static PracticeSession _countedFor;
    // NumberOfRooms changes from a hotkey with nothing calling UpdateRoomCount; it only moves the Math.Min.
    private static int _lastNumberOfRooms;
    // An end point is set and removed from SpeedrunTool's hotkeys and its map editor, the same way.
    private static bool _lastEndPointExists;

    private static bool CountIsStale => CurrentSession != null
        && (!ReferenceEquals(CurrentSession, _countedFor) || Srt.Tool.TimerType != _lastRoomTimerType
            || Srt.Tool.NumberOfRooms != _lastNumberOfRooms || Srt.Tool.EndPointExists != _lastEndPointExists);

    private sealed class ManagerSegmentShape : ISegmentShape
    {
        public int StartRoomIndex => SessionManager.StartRoomIndex;
        public int RoomCount => SessionManager.RoomCount;
    }

    private static readonly ISegmentShape _segmentShape = new ManagerSegmentShape();

    // What the last destructive action discarded, one deep: a second one replaces it. An action that
    // discards no run leaves it alone, so a repeated clear does not erase what the first one kept.
    // Not persisted: a game restart loses it.
    private static readonly Dictionary<string, PracticeSession> _stash = new();
    private static string _stashedCurrentSlot;
    // The engine frame of the clear that filled the stash, when a slot clear did: see ClearSlot.
    private static ulong? _stashFrame;
    // A slot clear stashed a session, and no save has followed yet: the save that does is the one
    // that replaces it. SpeedrunTool clears a slot before it saves over it, so this is how a
    // replacing save really arrives; see ClearSlot.
    private static bool _slotClearedWithRuns;

    public static bool HasStash => _stash.Count > 0;

    // Any slot holds a recorded run: what makes a wipe worth confirming.
    public static bool AnyRuns => _slots.Values.Any(session => session.HasRuns);

    public static void DropStash()
    {
        _stash.Clear();
        _stashedCurrentSlot = null;
        _stashFrame = null;
        _slotClearedWithRuns = false;
    }

    // A session that recorded a run in the level now played. A session of another level is not
    // worth keeping: a restore there would put statistics of one level into another.
    private static bool IsStashable(PracticeSession session) =>
        session.HasRuns && session.LevelName == LevelName;

    // gather: the session joins what the stash already holds, as part of the same wipe. Otherwise it
    // replaces the stash. False when the session is not stashable, which leaves the stash alone.
    private static bool Stash(string slotName, PracticeSession session, bool gather, ulong? frame = null)
    {
        if (!IsStashable(session)) return false;
        if (!gather) DropStash();
        _stash[slotName] = session;
        _stashFrame = frame;
        if (ReferenceEquals(CurrentSession, session)) _stashedCurrentSlot = slotName;
        return true;
    }

    // Puts the stash back, over any slot of the same name, and makes the session that was current
    // then current again. The sessions it replaces are not stashed: restoring twice is not an undo.
    // False when there was nothing to restore.
    public static bool RestoreStash()
    {
        if (_stash.Count == 0) return false;
        PracticeSession current = null;
        foreach (KeyValuePair<string, PracticeSession> entry in _stash)
        {
            // A session this puts back over the current one must take its place as current, or
            // the current one is left outside every slot.
            if (_slots.TryGetValue(entry.Key, out PracticeSession replaced) && ReferenceEquals(replaced, CurrentSession))
                current = entry.Value;
            _slots[entry.Key] = entry.Value;
        }
        if (_stashedCurrentSlot != null && _slots.TryGetValue(_stashedCurrentSlot, out PracticeSession wasCurrent))
            current = wasCurrent;
        if (current != null) CurrentSession = current;
        DropStash();
        UpdateRoomCount();
        return true;
    }

    // SRT OnSaveState. Overwrites any prior data for the slot, which goes to the stash when it
    // held a run. Returns whether this save replaced a session that did: here, or in the clear
    // SpeedrunTool made just before it.
    public static bool SaveSlot(string slotName)
    {
        bool stashed = _slotClearedWithRuns;
        _slotClearedWithRuns = false;
        if (_slots.TryGetValue(slotName, out PracticeSession replaced))
            stashed |= Stash(slotName, replaced, gather: false);
        var session = NewSession();
        _slots[slotName] = session;
        CurrentSession = session;
        _lastSplitRoom = Srt.Tool.RoomName;
        UpdateRoomCount();
        return stashed;
    }

    // SRT OnBeforeLoadState. The timer still holds the abandoned attempt's time here: SpeedrunTool
    // zeroes it in its own load action, which runs before OnLoadState.
    public static void BeforeLoad(long ticks) =>
        CurrentSession?.NoteTimeBeforeReload(new TimeTicks(ticks));

    // SRT OnLoadState. An unsaved slot gets a fresh session, whose constructor already opened the
    // attempt this load starts: a second one would leave attempt 0 empty and number runs from #2.
    public static void LoadSlot(string slotName)
    {
        _slotClearedWithRuns = false;
        // The level already holds the restored state here.
        _lastSplitRoom = Srt.Tool.RoomName;
        if (_slots.TryGetValue(slotName, out PracticeSession session))
        {
            CurrentSession = session;
            CurrentSession.StartNewAttempt();
            return;
        }
        session = NewSession();
        _slots[slotName] = session;
        CurrentSession = session;
    }

    private static PracticeSession NewSession() => new(
        _segmentShape,
        initialColumnCapacity: Math.Max(16, Srt.Tool.NumberOfRooms + 4))
    {
        LevelName = LevelName,
    };

    // SRT OnClearState. SpeedrunTool also clears a slot just before it saves over it, so this is
    // where a session replaced by a save arrives; it is stashed here, and the SaveSlot that follows
    // finds no slot to replace.
    //
    // SpeedrunTool's scene-switch clear calls this once per saved slot, all in the one frame of the
    // scene's Begin (SaveSlotsManager.ClearStateWhenSwitchScene). Calls given the same frame are one
    // wipe, and the stash gathers them; any other call replaces the stash. No frame, no gathering.
    public static void ClearSlot(string slotName, ulong? frame = null)
    {
        if (_slots.TryGetValue(slotName, out PracticeSession clearedSession))
        {
            bool sameWipe = frame != null && _stashFrame == frame && HasStash;
            if (Stash(slotName, clearedSession, gather: sameWipe, frame))
                _slotClearedWithRuns = true;
            _slots.Remove(slotName);
            if (ReferenceEquals(_countedFor, clearedSession)) _countedFor = null;
            if (ReferenceEquals(CurrentSession, clearedSession))
            {
                CurrentSession = null;
                RoomCount = 0;
            }
        }
    }

    // A full clear: the module's Clear(), on unload, the clear hotkey and the menu. The mod hooks no
    // level exit; SpeedrunTool's clear-state callback reaches ClearSlot instead. Every slot goes to
    // the stash when any of them held a run.
    public static void ClearAll()
    {
        if (_slots.Values.Any(IsStashable))
        {
            DropStash();
            foreach (KeyValuePair<string, PracticeSession> entry in _slots)
                Stash(entry.Key, entry.Value, gather: true);
        }
        _slots.Clear();
        CurrentSession = null;
        _countedFor = null;
        _lastSplitRoom = null;
        RoomCount = 0;
    }

    public static void CompleteRoom(long ticks)
    {
        if (CurrentSession == null) return;
        TimeTicks roomTime = new TimeTicks(ticks) - CurrentSession.RunningSegmentTime;
        if (roomTime > 0)
        {
            CurrentSession.CompleteRoom(roomTime);
            // Here, not only where the count is read: MaxRoomCount grows from the active attempt
            // alone, and a reload puts that attempt back at room 0 before any reader has run.
            UpdateRoomCount();
        }
    }

    // The room the active attempt's last split entered. SaveSlot and LoadSlot set it to the save
    // state's room.
    private static string _lastSplitRoom;

    // SpeedrunTool's UpdateTimerState, before SpeedrunTool's own: endPoint is its argument, and
    // the room timer still holds the time of the room being left.
    //
    // A call is a split when it enters a room other than the last split's, or when it is the
    // first to end the attempt's timer. SpeedrunTool repeats the call on every frame on an end
    // point, in the room-id end point's room and after a level end, and Time keeps moving on the
    // first two (#15). Do not read Srt.Tool.TimerCompleted here: SpeedrunTool sets it one
    // transition early when roomNumber reaches NumberOfRooms.
    public static void TimerStateUpdating(bool endPoint)
    {
        PracticeSession session = CurrentSession;
        if (session == null || session.CurrentAttemptIndex < 0) return;
        ISpeedrunTool srt = Srt.Tool;
        string room = srt.RoomName;
        bool entersRoom = room != _lastSplitRoom;
        // Under NextRoom, before the first room change, SpeedrunTool's timer has not started.
        bool finishes = !session.ActiveAttemptFinished
            && session.CurrentRoomIndex >= StartRoomIndex
            && (endPoint || srt.ReachedRoomIdEndPoint || srt.LevelCompleted);
        if (!entersRoom && !finishes) return;

        _lastSplitRoom = room;
        int recordedBefore = session.CurrentRoomIndex;
        long segmentTime = srt.CurrentRoomTime;
        if (segmentTime > 0)
            CompleteRoom(segmentTime);
        if (finishes && session.CurrentRoomIndex > recordedBefore)
        {
            session.FinishActiveAttempt();
            // CompleteRoom's count took the room after the finish as the one being entered.
            session.RecomputeMaxRoomCount();
            UpdateRoomCount();
        }
    }

    // Drops whatever the given pin keys name. Segment pins first: room pins on the same attempt
    // then become no-ops.
    public static void DeletePinned(IReadOnlyList<string> pinKeys)
    {
        PracticeSession session = CurrentSession;
        if (session == null) return;

        var deletedAttempts = new HashSet<int>();
        foreach (string key in pinKeys)
            if (PinKey.TryParseSegment(key, out int attemptIdx))
            {
                session.DeleteAttempt(attemptIdx);
                deletedAttempts.Add(attemptIdx);
            }
        foreach (string key in pinKeys)
            if (PinKey.TryParseRoom(key, out int attemptIdx, out int roomIdx)
                && !deletedAttempts.Contains(attemptIdx))
                session.DeleteCell(attemptIdx, roomIdx);
    }

    public static void UpdateRoomCount()
    {
        if (CurrentSession == null)
        {
            RoomCount = 0;
            return;
        }

        if (CountIsStale)
        {
            _lastRoomTimerType = Srt.Tool.TimerType;
            _lastNumberOfRooms = Srt.Tool.NumberOfRooms;
            _lastEndPointExists = Srt.Tool.EndPointExists;
            _countedFor = CurrentSession;
            CurrentSession.RecomputeMaxRoomCount();
        }

        CurrentSession.BumpMaxForActiveAttempt();
        // With an end point SpeedrunTool does not read NumberOfRooms: the segment ends on the end
        // point's split, and the most recent finished attempt says where that is (#15).
        if (_lastEndPointExists)
            RoomCount = CurrentSession.FinishedRoomCount ?? CurrentSession.MaxRoomCount;
        else
            RoomCount = Math.Min(CurrentSession.MaxRoomCount, Srt.Tool.NumberOfRooms);
    }
}
