using System;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

// Rebuild key: the session, its version, visible room count, SpeedrunTool room timer type, and the
// raw index of the segment's first room. Another session's chart is rebuilt and takes nothing from
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
        return Get(curSession, curVersion, curRoomCount, curTimerType, SessionManager.StartRoomIndex);
    }

    // The key as read by Get(); a test passes its own.
    internal T Get(PracticeSession curSession, uint curVersion, int curRoomCount, int curTimerType, int curStartRoomIndex)
    {
        if (!ReferenceEquals(_session, curSession)) _value = null;

        T previous = null;
        if (_value != null && (
            _version   != curVersion   ||
            _timerType != curTimerType ||
            _roomCount != curRoomCount ||
            _startRoomIndex != curStartRoomIndex))
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
    }
}
