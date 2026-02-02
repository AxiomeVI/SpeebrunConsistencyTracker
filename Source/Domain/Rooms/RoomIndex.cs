using System;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Rooms
{
    public readonly struct RoomIndex : IEquatable<RoomIndex>
    {
        public int Value { get; }

        public RoomIndex(int value)
        {
            Value = value;
        }

        public static implicit operator int(RoomIndex index) => index.Value;
        public static explicit operator RoomIndex(int value) => new(value);

        public bool Equals(RoomIndex other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RoomIndex other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(RoomIndex left, RoomIndex right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RoomIndex left, RoomIndex right)
        {
            return !(left == right);
        }
    }

}