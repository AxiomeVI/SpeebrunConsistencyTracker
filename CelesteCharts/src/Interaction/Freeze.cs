#nullable enable

using System;

namespace Celeste.Mod.CelesteCharts;

/// <summary>
///     Whether a chart surface freezes the level while it is shown: the mod author's choice, passed where
///     the surface is opened. A mod that wants its players to choose wires its own setting into it.
/// </summary>
internal enum FreezeMode {
    /// <summary>The level keeps running: mouse only, which Celeste ignores, so Madeline never moves.</summary>
    Never,

    /// <summary>The level is frozen for as long as the surface is shown.</summary>
    WhileShown,

    /// <summary>The player switches it, with a button on the surface.</summary>
    Switchable,
}

/// <summary>What a mod's freezer does to the level this frame.</summary>
internal enum FreezeAction {
    None,
    Freeze,
    Unfreeze,
}

/// <summary>
///     Freezing the level for charts, shared by every mod that uses CelesteCharts. A surface that wants
///     the level frozen asks every frame it is shown; each mod's freezer asks <see cref="Decide"/> what to
///     do, and only one freeze or unfreeze happens. When nobody has asked for two frames, the level is
///     unfrozen — but only if CelesteCharts froze it, never a freeze the game or another mod made.
/// </summary>
// ⚠️ SHIPPED CONTRACT between mods at different commits, as CursorClaim: the AppContext key and the
// meaning of each slot never change; slots are only appended, and readers accept a longer array.
//   [0] the frame a freeze was last asked for
//   [1] 1 while CelesteCharts holds the level frozen, else 0 — what another mod reads (Holding) to
//       know that the level is frozen for charts and not by the game
//   [2..7] reserved
// Asked every frame rather than counted up and down: a count leaks when a mod forgets to count down (a
// crash, an unload), and the level would stay frozen.
internal sealed class FreezeClaim {
    public const string Key = "CelesteCharts.Freeze";
    private const int Slots = 8;

    private readonly long[] _slots;
    private static FreezeClaim? _shared;

    public FreezeClaim(long[] slots) {
        if (slots.Length < Slots) throw new ArgumentException($"a freeze claim needs {Slots} slots", nameof(slots));
        _slots = slots;
    }

    /// <summary>The claim every mod in this game shares, made by the first one to ask.</summary>
    public static FreezeClaim Shared {
        get {
            if (AppContext.GetData(Key) is not long[] slots || slots.Length < Slots) {
                slots = new long[Slots];
                slots[0] = long.MinValue;
                AppContext.SetData(Key, slots);
            }
            return _shared is { } claim && ReferenceEquals(claim._slots, slots) ? claim : _shared = new FreezeClaim(slots);
        }
    }

    /// <summary>Whether CelesteCharts holds the level frozen: for a mod that must tell a chart's freeze from the game's.</summary>
    public bool Holding => _slots[1] == 1;

    /// <summary>A surface wants the level frozen this frame.</summary>
    public void Request(long frame) => _slots[0] = frame;

    /// <summary>
    ///     What to do to the level this frame, given whether it is frozen now. A request from this frame or
    ///     the one before keeps it frozen (a surface drawn after the freezer asks a frame late).
    /// </summary>
    public FreezeAction Decide(long frame, bool frozenNow) {
        bool wanted = _slots[0] != long.MinValue && _slots[0] >= frame - 1;
        if (wanted) {
            if (frozenNow) return FreezeAction.None;   // already frozen, by us or by the game: nothing to take
            _slots[1] = 1;
            return FreezeAction.Freeze;
        }
        if (_slots[1] != 1) return FreezeAction.None;
        _slots[1] = 0;
        // Someone else already unfroze it: nothing to undo.
        return frozenNow ? FreezeAction.Unfreeze : FreezeAction.None;
    }
}
