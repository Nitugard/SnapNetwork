using System;

namespace Ibc.Survival
{
    /// <summary>
    /// Prefab identifier. Wrapper around integer type.
    /// </summary>
    [Serializable]
    public struct PrefabId : IEquatable<PrefabId>, IComparable<PrefabId>
    {
        public const byte MaxValue = Byte.MaxValue;
        public byte Value;

        public PrefabId(int val)
        {
            Value = (byte)val;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool Equals(PrefabId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PrefabId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public int CompareTo(PrefabId other)
        {
            return Value.CompareTo(other.Value);
        }
    }
}