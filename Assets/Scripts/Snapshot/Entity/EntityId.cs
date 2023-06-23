using System;

namespace Ibc.Survival
{
    /// <summary>
    /// Entity identifier <see cref="EntityStateBase"/>. Wrapper around integer type.
    /// </summary>
    [Serializable]
    public struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public const ushort MaxValue = UInt16.MaxValue;
        public ushort Value;

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool Equals(EntityId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public int CompareTo(EntityId other)
        {
            return Value.CompareTo(other.Value);
        }
    }
}