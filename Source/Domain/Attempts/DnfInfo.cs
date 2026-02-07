using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed record DnfInfo(
    int Room,
    TimeTicks TimeIntoRoomTicks
);
