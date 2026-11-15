using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Utility;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

// Rebuild key: the session, its version, visible room count, SpeedrunTool room timer type, and the
// raw index of the segment's first room, and the chart palette. A palette change rebuilds the chart
// and carries its view over, since the data did not change. Another session's chart is rebuilt and takes nothing from
// the one it replaces: its pins and hidden rooms named that session's runs.
internal sealed class ChartCache<T> where T : class
{
    private readonly Func<int, T> _build;
    private readonly Action<T, T, bool> _carry;

    private T _value;
    private PracticeSession _session;
    private uint _version;
    private int _roomCount;
    private int _timerType;
    private int _startRoomIndex;
    private ChartPalette _palette;

    // build receives the room count the key was read with, so both see the same value.
    // carry hands a rebuilt value what the one it replaces held (previous, then fresh), and whether
    // the visible rooms now start from another raw room: a room index kept from previous then
    // names the room beside the one it named.
    public ChartCache(Func<int, T> build, Action<T, T, bool> carry = null) => (_build, _carry) = (build, carry);

    public T Get()
    {
        PracticeSession curSession = SessionManager.CurrentSession;
        uint curVersion   = curSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)Integration.Srt.Tool.TimerType;
        return Get(curSession, curVersion, curRoomCount, curTimerType, SessionManager.StartRoomIndex, ChartPalette.Current);
    }

    // The key as read by Get(); a test passes its own. ChartPalette.Current is a new instance whenever
    // a colour setting changes, so the instance is the key, as in TrajectoryWidget.
    internal T Get(PracticeSession curSession, uint curVersion, int curRoomCount, int curTimerType, int curStartRoomIndex, ChartPalette curPalette = null)
    {
        if (!ReferenceEquals(_session, curSession)) _value = null;

        T previous = null;
        if (_value != null && (
            _version   != curVersion   ||
            _timerType != curTimerType ||
            _roomCount != curRoomCount ||
            _startRoomIndex != curStartRoomIndex ||
            !ReferenceEquals(_palette, curPalette)))
        {
            previous = _value;
            _value   = null;
        }

        if (_value == null)
        {
            bool renumbered = previous != null && _startRoomIndex != curStartRoomIndex;
            _value     = _build(curRoomCount);
            _session   = curSession;
            _version   = curVersion;
            _roomCount = curRoomCount;
            _timerType = curTimerType;
            _startRoomIndex = curStartRoomIndex;
            _palette   = curPalette;
            if (previous != null) _carry?.Invoke(previous, _value, renumbered);
        }

        return _value;
    }

    public void Clear()
    {
        _value     = null;
        _session   = null;
        _version   = 0;
        _roomCount = 0;
        _timerType = 0;
        _startRoomIndex = 0;
        _palette   = null;
    }
}
