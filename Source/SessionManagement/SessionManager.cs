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
    public static string LevelName { get; set; } = "unknown";

    // Counted again on read when the session, the timer type or NumberOfRooms moved since the last count: the
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

    private static bool CountIsStale => CurrentSession != null
        && (!ReferenceEquals(CurrentSession, _countedFor) || Srt.Tool.TimerType != _lastRoomTimerType
            || Srt.Tool.NumberOfRooms != _lastNumberOfRooms);

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
        _slotClearedWithRuns = false;
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
        if (_slots.TryGetValue(slotName, out PracticeSession replaced) && replaced.HasRuns)
        {
            DropStash();
            _stash[slotName] = replaced;
            _stashedCurrentSlot = slotName;
            stashed = true;
        }
        var session = new PracticeSession(
            _segmentShape,
            initialColumnCapacity: Math.Max(16, Srt.Tool.NumberOfRooms + 4));
        _slots[slotName] = session;
        CurrentSession = session;
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
        if (_slots.TryGetValue(slotName, out PracticeSession session))
        {
            CurrentSession = session;
            CurrentSession.StartNewAttempt();
            return;
        }
        session = new PracticeSession(
            _segmentShape,
            initialColumnCapacity: Math.Max(16, Srt.Tool.NumberOfRooms + 4));
        _slots[slotName] = session;
        CurrentSession = session;
    }

    // SRT OnClearState. SpeedrunTool also clears a slot just before it saves over it, so this is
    // where a session replaced by a save arrives; it is stashed here, and the SaveSlot that follows
    // finds no slot to replace.
    public static void ClearSlot(string slotName)
    {
        if (_slots.TryGetValue(slotName, out PracticeSession clearedSession))
        {
            if (clearedSession.HasRuns)
            {
                DropStash();
                _stash[slotName] = clearedSession;
                if (ReferenceEquals(CurrentSession, clearedSession)) _stashedCurrentSlot = slotName;
                _slotClearedWithRuns = true;
            }
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
        if (AnyRuns)
        {
            DropStash();
            foreach (KeyValuePair<string, PracticeSession> entry in _slots)
            {
                _stash[entry.Key] = entry.Value;
                if (ReferenceEquals(entry.Value, CurrentSession)) _stashedCurrentSlot = entry.Key;
            }
        }
        _slots.Clear();
        CurrentSession = null;
        _countedFor = null;
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
            _countedFor = CurrentSession;
            CurrentSession.RecomputeMaxRoomCount();
        }

        CurrentSession.BumpMaxForActiveAttempt();
        RoomCount = Math.Min(CurrentSession.MaxRoomCount, Srt.Tool.NumberOfRooms);
    }
}
