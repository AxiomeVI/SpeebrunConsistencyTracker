using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;

// Rebuild key: session version, visible room count, SpeedrunTool room timer type, plus extraKey.
internal sealed class ChartCache<T> where T : class
{
    private readonly Func<int, T> _build;
    private readonly bool _keyOnRoomCount;
    private readonly Func<int> _extraKey;

    private T _value;
    private uint _version;
    private int _roomCount;
    private int _timerType;
    private int _extra;

    // build receives the room count the key was read with, so both see the same value.
    // keyOnRoomCount is false for a per-room chart: its data is one matrix column, which the
    // visible room count does not change. extraKey is read live on every Get, for a chart built
    // from a setting too — without it the chart keeps a value the player has already changed.
    public ChartCache(Func<int, T> build, bool keyOnRoomCount = true, Func<int> extraKey = null)
    {
        _build          = build;
        _keyOnRoomCount = keyOnRoomCount;
        _extraKey       = extraKey;
    }

    public T Get()
    {
        uint curVersion   = SessionManager.CurrentSession.Version;
        int  curRoomCount = SessionManager.RoomCount;
        int  curTimerType = (int)SpeedrunTool.SpeedrunToolSettings.Instance.RoomTimerType;
        int  curExtra     = _extraKey?.Invoke() ?? 0;

        if (_value != null && (
            _version   != curVersion   ||
            _timerType != curTimerType ||
            _extra     != curExtra     ||
            (_keyOnRoomCount && _roomCount != curRoomCount)))
        {
            _value = null;
        }

        if (_value == null)
        {
            _value     = _build(curRoomCount);
            _version   = curVersion;
            _roomCount = curRoomCount;
            _timerType = curTimerType;
            _extra     = curExtra;
        }

        return _value;
    }

    public void Clear()
    {
        _value     = null;
        _version   = 0;
        _roomCount = 0;
        _timerType = 0;
        _extra     = 0;
    }
}
