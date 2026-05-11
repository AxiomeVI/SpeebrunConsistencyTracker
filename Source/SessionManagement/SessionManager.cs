using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;
using Celeste.Mod.SpeedrunTool.RoomTimer;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

public static class SessionManager
{
    private static readonly Dictionary<string, PracticeSession> _slots = new();
    public static PracticeSession CurrentSession { get; private set; }

    // Set from OnLoadLevel in the module; shared across all slots since all sessions are in the same level.
    public static string LevelName { get; set; } = "unknown";

    public static int RoomCount { get; private set; } = 0;

    public static int StartRoomIndex =>
        SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType == RoomTimerType.CurrentRoom ? 0 : 1;

    private static RoomTimerType _lastRoomTimerType = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

    // Called on SRT OnSaveState: fresh session for this slot, overwrites any prior data.
    public static void SaveSlot(string slotName)
    {
        var session = new PracticeSession(
            initialColumnCapacity: Math.Max(16, SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms + 4));
        _slots[slotName] = session;
        CurrentSession = session;
        UpdateRoomCount();
    }

    // Called on SRT OnLoadState: switch to the slot's session, start a new attempt.
    // If the slot was never saved in this session, create a fresh session for it.
    public static void LoadSlot(string slotName)
    {
        if (!_slots.TryGetValue(slotName, out PracticeSession session))
        {
            session = new PracticeSession(
                initialColumnCapacity: Math.Max(16, SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms + 4));
            _slots[slotName] = session;
        }
        CurrentSession = session;
        CurrentSession.StartNewAttempt();
    }

    // Called on SRT OnClearState: remove slot data, deactivate if it was the current slot.
    public static void ClearSlot(string slotName)
    {
        if (_slots.TryGetValue(slotName, out PracticeSession clearedSession))
        {
            _slots.Remove(slotName);
            if (ReferenceEquals(CurrentSession, clearedSession))
            {
                CurrentSession = null;
                RoomCount = 0;
            }
        }
    }

    // Called on level exit or full clear: wipe everything.
    public static void ClearAll()
    {
        _slots.Clear();
        CurrentSession = null;
        RoomCount = 0;
    }

    public static void CompleteRoom(long ticks)
    {
        if (CurrentSession == null) return;
        TimeTicks roomTime = new TimeTicks(ticks) - CurrentSession.RunningSegmentTime;
        if (roomTime > 0)
        {
            CurrentSession.CompleteRoom(roomTime);
        }
    }

    public static void UpdateRoomCount()
    {
        if (CurrentSession == null)
        {
            RoomCount = 0;
            return;
        }

        var currentType = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;
        if (currentType != _lastRoomTimerType)
        {
            _lastRoomTimerType = currentType;
            CurrentSession.RecomputeMaxRoomCount();
        }

        CurrentSession.BumpMaxForActiveAttempt();
        RoomCount = Math.Min(CurrentSession.MaxRoomCount, SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms);
    }
}
