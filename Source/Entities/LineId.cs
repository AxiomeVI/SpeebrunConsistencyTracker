using System.Globalization;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Entities
{
    public enum LineKind { None, Attempt, Sob, Baseline }

    // A drawn trajectory line is an attempt, the SoB line or the average baseline. Value is the
    // wire form the identity travels in: attempts 0..n-1, SoB at n, the baseline at n+1 — it
    // leaves as HoverInfo.Key and comes back through HandleClick. Same idiom as PinKey in
    // GraphInteractivity, one encode/decode pair rather than an int decoded at every call site.
    public readonly record struct LineId(LineKind Kind, int Value)
    {
        public static readonly LineId None = new(LineKind.None, -1);

        public bool IsNone     => Kind == LineKind.None;
        public bool IsAttempt  => Kind == LineKind.Attempt;
        public bool IsSob      => Kind == LineKind.Sob;
        public bool IsBaseline => Kind == LineKind.Baseline;

        public static LineId Attempt(int attemptIndex)  => new(LineKind.Attempt, attemptIndex);
        public static LineId Sob(int attemptCount)      => new(LineKind.Sob,      attemptCount);
        public static LineId Baseline(int attemptCount) => new(LineKind.Baseline, attemptCount + 1);

        public string ToKey() => Value.ToString(CultureInfo.InvariantCulture);

        // Decodes the wire form. LineId.None for anything outside the three ranges, so a stray
        // key falls through to GraphInteractivity's generic pin path instead of pinning nothing.
        public static LineId FromKey(string? key, int attemptCount)
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw) || raw < 0)
                return None;
            if (raw <  attemptCount)     return Attempt(raw);
            if (raw == attemptCount)     return Sob(attemptCount);
            if (raw == attemptCount + 1) return Baseline(attemptCount);
            return None;
        }
    }
}
