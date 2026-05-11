using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Time;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public enum RoomCellState : byte
{
    // NotReached must be 0 so default(RoomCell) == NotReached (matrix zero-init gives NotReached cells for free).
    NotReached,
    Completed,
    DNF,
    Deleted
}

public readonly struct RoomCell(RoomCellState state, TimeTicks time)
{
    public readonly RoomCellState State = state;
    public readonly TimeTicks Time = time; // meaningful only when State == Completed

    public static RoomCell Completed(TimeTicks time) => new(RoomCellState.Completed, time);
    public static readonly RoomCell DNF = new(RoomCellState.DNF, TimeTicks.Zero);
    public static readonly RoomCell Deleted = new(RoomCellState.Deleted, TimeTicks.Zero);
    public static readonly RoomCell NotReached = new(RoomCellState.NotReached, TimeTicks.Zero);

    public bool HasTime => State == RoomCellState.Completed;
}
