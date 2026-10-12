using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

// Rebuild key: session version, visible room count, SpeedrunTool room timer type.
internal sealed class ChartCache<T> where T : class
{
    private readonly Func<int, T> _build;
    private readonly Action<T, T> _carry;

    private T _value;
    private uint _version;
    private int _roomCount;
    private int _timerType;

    // build receives the room count the key was read with, so both see the same value.
    // carry hands a rebuilt value what the one it replaces held (previous, then fresh).
    public ChartCache(Func<int, T> build, Action<T, T> carry = null) => (_build, _carry) = (build, carry);

    public T Get()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;

        T previous = null;
        if (_value != null && (
            _version   != curVersion   ||
            _timerType != curTimerType ||
            _roomCount != curRoomCount))
        {
            previous = _value;
            _value   = null;
        }

        if (_value == null)
        {
            _value     = _build(curRoomCount);
            _version   = curVersion;
            _roomCount = curRoomCount;
            _timerType = curTimerType;
            if (previous != null) _carry?.Invoke(previous, _value);
        }

        return _value;
    }

    public void Clear()
    {
        _value     = null;
        _version   = 0;
        _roomCount = 0;
        _timerType = 0;
    }
}
