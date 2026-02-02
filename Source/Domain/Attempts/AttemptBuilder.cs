using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class AttemptBuilder
{
    private readonly DateTime _startTime;
    private readonly Dictionary<RoomIndex, TimeTicks> _roomTicks = [];
    private TimeTicks _segmentTime = TimeTicks.Zero;
    private RoomIndex? _dnfRoom;
    private TimeTicks? _dnfTicks;

    public AttemptBuilder()
    {
        _startTime = DateTime.UtcNow;
    }

    public TimeTicks SegmentTime
        => _segmentTime;

    public void CompleteRoom(RoomIndex room, TimeTicks ticks)
    {
        if (_roomTicks.ContainsKey(room))
            throw new InvalidOperationException($"Room {room.Value} already completed");

        if (_dnfRoom != null)
            throw new InvalidOperationException("Cannot complete room after DNF");

        _roomTicks[room] = ticks;
        _segmentTime += ticks;
    }

    public void SetDnf(RoomIndex room, TimeTicks ticks)
    {
        if (_dnfRoom != null)
            throw new InvalidOperationException("DNF already set");

        _dnfRoom = room;
        _dnfTicks = _segmentTime - ticks;
    }

    public Attempt Build()
    {
        if (_dnfRoom != null)
        {
            var dnfInfo = new DnfInfo(_dnfRoom.Value, _dnfTicks.Value);
            return Attempt.Dnf(_startTime, new Dictionary<RoomIndex, TimeTicks>(_roomTicks), _segmentTime, dnfInfo);
        }
        else
        {
            return Attempt.Completed(_startTime, new Dictionary<RoomIndex, TimeTicks>(_roomTicks), _segmentTime);
        }
    }
}
