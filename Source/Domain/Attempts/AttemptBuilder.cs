using System;
using System.Collections.Generic;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class AttemptBuilder
{
    private readonly DateTime _startTime;
    private readonly List<TimeTicks> _roomTicks = [];
    private TimeTicks _segmentTime = TimeTicks.Zero;
    private int? _dnfRoom;
    private TimeTicks? _dnfTicks;

    public AttemptBuilder()
    {
        _startTime = DateTime.UtcNow;
    }

    public TimeTicks SegmentTime
        => _segmentTime;

    public void CompleteRoom(TimeTicks ticks)
    {
        _roomTicks.Add(ticks);
        _segmentTime += ticks;
    }

    public void SetDnf(int room, TimeTicks ticks)
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
            return Attempt.Dnf(_startTime, _roomTicks, _segmentTime, dnfInfo);
        }
        else
        {
            return Attempt.Completed(_startTime, _roomTicks, _segmentTime);
        }
    }

    public int Count 
        => _roomTicks.Count;

    public TimeTicks GetRoomTimeAt(int index)
    {
        return _roomTicks[index];
    }
}
