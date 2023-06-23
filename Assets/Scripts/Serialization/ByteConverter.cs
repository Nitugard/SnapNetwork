using System;
using System.Runtime.InteropServices;

namespace Ibc.Survival
{
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct ByteConverter
    {
        [FieldOffset(0)] public float float32;
        [FieldOffset(0)] public double float64;
        [FieldOffset(0)] public byte int8;
        [FieldOffset(0)] public short int16;
        [FieldOffset(0)] public ushort uint16;
        [FieldOffset(0)] public char character;
        [FieldOffset(0)] public int int32;
        [FieldOffset(0)] public uint uint32;
        [FieldOffset(0)] public long int64;
        [FieldOffset(0)] public ulong uint64;
        [FieldOffset(0)] public bool boolean;

        [FieldOffset(0)] public byte byte0;
        [FieldOffset(1)] public byte byte1;
        [FieldOffset(2)] public byte byte2;
        [FieldOffset(3)] public byte byte3;
        [FieldOffset(4)] public byte byte4;
        [FieldOffset(5)] public byte byte5;
        [FieldOffset(6)] public byte byte6;
        [FieldOffset(7)] public byte byte7;

        public byte this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return byte0;
                    case 1: return byte1;
                    case 2: return byte2;
                    case 3: return byte3;
                    case 4: return byte4;
                    case 5: return byte5;
                    case 6: return byte6;
                    case 7: return byte7;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        byte0 = value;
                        break;
                    case 1:
                        byte1 = value;
                        break;
                    case 2:
                        byte2 = value;
                        break;
                    case 3:
                        byte3 = value;
                        break;
                    case 4:
                        byte4 = value;
                        break;
                    case 5:
                        byte5 = value;
                        break;
                    case 6:
                        byte6 = value;
                        break;
                    case 7:
                        byte7 = value;
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        public byte ToAscii(char character)
        {
            uint64 = 0;
            this.character = character;
            return byte0;
        }

        
        public char FromAscii(byte b)
        {
            uint64 = 0;
            byte0 = b;
            return character;
        }

        public UInt32 FloatToUInt32(float value)
        {
            uint64 = 0;
            float32 = value;
            return uint32;
        }

        public float UInt32ToFloat(UInt32 value)
        {
            uint64 = 0;
            uint32 = value;
            return float32;
        }
    }
}