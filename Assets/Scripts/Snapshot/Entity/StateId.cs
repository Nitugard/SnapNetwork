using System;

namespace Ibc.Survival
{
    /// <summary>
    /// State identifier <see cref="EntityStateBase{T}"/>. Wrapper around integer type.
    /// </summary>
    [Serializable]
    public struct StateId : IEquatable<StateId>, IComparable<StateId>
    {
        public const byte MaxValue = Byte.MaxValue;
        public byte Value;

        public StateId(int val)
        {
            Value = (byte)val;
        }

        public static StateId Invalid => new StateId(MaxValue);

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool Equals(StateId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is StateId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public int CompareTo(StateId other)
        {
            return Value.CompareTo(other.Value);
        }
    }
}