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

    // Shared across slots: every session in play is in the same level. Set from OnLoadLevel.
    public static string LevelName { get; set; } = "unknown";

    public static int RoomCount { get; private set; } = 0;

    public static int StartRoomIndex =>
        SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType == RoomTimerType.CurrentRoom ? 0 : 1;

    private static RoomTimerType _lastRoomTimerType = SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

    private sealed class ManagerSegmentShape : ISegmentShape
    {
        public int StartRoomIndex => SessionManager.StartRoomIndex;
        public int RoomCount => SessionManager.RoomCount;
    }

    private static readonly ISegmentShape _segmentShape = new ManagerSegmentShape();

    // SRT OnSaveState. Overwrites any prior data for the slot.
    public static void SaveSlot(string slotName)
    {
        var session = new PracticeSession(
            _segmentShape,
            initialColumnCapacity: Math.Max(16, SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms + 4));
        _slots[slotName] = session;
        CurrentSession = session;
        UpdateRoomCount();
    }

    // SRT OnLoadState. An unsaved slot gets a fresh session.
    public static void LoadSlot(string slotName)
    {
        if (!_slots.TryGetValue(slotName, out PracticeSession session))
        {
            session = new PracticeSession(
                _segmentShape,
                initialColumnCapacity: Math.Max(16, SpeedrunTool.SpeedrunToolSettings.Instance.NumberOfRooms + 4));
            _slots[slotName] = session;
        }
        CurrentSession = session;
        CurrentSession.StartNewAttempt();
    }

    // SRT OnClearState.
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

    // Level exit or full clear.
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
