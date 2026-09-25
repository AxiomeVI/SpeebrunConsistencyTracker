using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

// Room labels are drawn once per visible room per frame and never change, so they are cached
// rather than interpolated on the Render path. The array grows to the largest room index seen and
// is never cleared: a segment has tens of rooms and the strings are immutable.
public static class RoomLabels
{
    private static string[] _cache = [];

    // roomIndex is 0-based; the label is 1-based, so index 0 reads "R1".
    public static string For(int roomIndex)
    {
        if (roomIndex < 0) return $"R{roomIndex + 1}";
        if (roomIndex >= _cache.Length) Grow(roomIndex + 1);
        return _cache[roomIndex];
    }

    private static void Grow(int count)
    {
        string[] grown = new string[count];
        Array.Copy(_cache, grown, _cache.Length);
        for (int i = _cache.Length; i < count; i++) grown[i] = $"R{i + 1}";
        _cache = grown;
    }
}
