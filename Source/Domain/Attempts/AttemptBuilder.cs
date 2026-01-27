using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class AttemptBuilder
{
    private readonly int _index;
    private readonly DateTime _startTime;
    private readonly Dictionary<RoomIndex, TimeTicks> _roomTicks = new();
    private RoomIndex? _dnfRoom;
    private TimeTicks? _dnfTicks;

    public AttemptBuilder(int index)
    {
        _index = index;
        _startTime = DateTime.UtcNow;
    }

    public TimeTicks GetRoomTimeAtIndex(int index)
    {
        RoomIndex roomIndex = new RoomIndex(index);
        return _roomTicks.ContainsKey(roomIndex) ? _roomTicks[roomIndex] : TimeTicks.Zero;
    }

    public void CompleteRoom(RoomIndex room, TimeTicks ticks)
    {
        if (_roomTicks.ContainsKey(room))
            throw new InvalidOperationException($"Room {room.Value} already completed");

        if (_dnfRoom != null)
            throw new InvalidOperationException("Cannot complete room after DNF");

        _roomTicks[room] = ticks;
    }

    public void SetDnf(RoomIndex room, TimeTicks ticks)
    {
        if (_dnfRoom != null)
            throw new InvalidOperationException("DNF already set");

        _dnfRoom = room;
        _dnfTicks = ticks;
    }

    public Attempt Build()
    {
        if (_dnfRoom != null)
        {
            var dnfInfo = new DnfInfo(_dnfRoom.Value, _dnfTicks.Value);
            return Attempt.Dnf(_index, _startTime, new Dictionary<RoomIndex, TimeTicks>(_roomTicks), dnfInfo);
        }
        else
        {
            if (_roomTicks.Count == 0)
                throw new InvalidOperationException("Cannot build a completed attempt with zero rooms");

            return Attempt.Completed(_index, _startTime, new Dictionary<RoomIndex, TimeTicks>(_roomTicks));
        }
    }
}
