#define IBC_ASSERT_SER

using System;
using System.Text;
using Debug = UnityEngine.Debug;

namespace Ibc.Survival
{
    public struct BitStream
    {
        /// <summary>
        /// Internal byte buffer.
        /// </summary>
        public byte[] Buffer;
        
        /// <summary>
        /// Current bit position.
        /// </summary>
        public int BitPosition;
        
        /// <summary>
        /// Capacity of the byte buffer when reading/writing.
        /// </summary>
        public int Capacity;
        
        /// <summary>
        /// Whether the stream failed to write/read required number of bits.
        /// </summary>
        public bool Failed;
        
        private ByteConverter _converter;
        
        public BitStream(byte[] data, int size)
        {
            Buffer = data;
            Capacity = size;
            _converter = default;

            BitPosition = 0;
            Failed = false;
        }
        
        /// <summary>
        /// Write a single bit.
        /// </summary>
        public void WriteBit(bool value)
        {
            int index = BitPosition >> 3;
            int requiredCapacity = index + 1;
            if (requiredCapacity > Capacity)
            {
                Failed = true;
                return;
            }

            if (value) Buffer[index] = (byte)(Buffer[index] | (1u << (BitPosition & 7)));
            else Buffer[index] = (byte)(Buffer[index] & ~(1u << (BitPosition & 7)));
            BitPosition++;
        }

        /// <summary>
        /// Read a single bit.
        /// </summary>
        public bool ReadBit()
        {
            int index = BitPosition >> 3;
            int requiredCapacity = index + 1;
            if (requiredCapacity > Capacity)
            {
                Failed = true;
                return false;
            }
            
            var value = (Buffer[index] & (1u << (BitPosition & 7))) != 0;
            BitPosition++;
            return value;
        }
        
        public void WriteFloat(float value)
        {
            WriteULong(_converter.FloatToUInt32(value), 32);
        }

        public float ReadFloat()
        {
            return _converter.UInt32ToFloat((uint)ReadULong(32));
        }

        public static int QuantizeFloatBits(float minValue, float maxValue, float precision = 10E5f)
        {
            return RequiredBits(0, (uint)((maxValue - minValue) * precision + 0.5f));
        }
        
        public static uint QuantizeFloat(float value, float minValue, float maxValue, float precision, int bits)
        {
            if (value < minValue)
                value = minValue;
            else if (value > maxValue)
                value = maxValue;

            uint mask = (uint)((1L << bits) - 1);
            uint temp = (uint)((value - minValue) * precision + 0.5f) & mask;
            return temp;
        }

        public static float DeQuantizeFloat(uint data, float minValue, float maxValue, float precision)
        {
            float adjusted = data / precision + minValue;
            if (adjusted < minValue)
                adjusted = minValue;
            else if (adjusted > maxValue)
                adjusted = maxValue;
            return adjusted;
        }
        
        public void WriteFloat(float value, float minValue, float maxValue, float precision = 1000)
        {
            int bits = QuantizeFloatBits(minValue, maxValue, precision);
            uint qf = QuantizeFloat(value, minValue, maxValue, precision, bits);
            WriteULong(qf, bits);
        }

        public float ReadFloat(float minValue, float maxValue, float precision = 1000)
        {
            int bits = QuantizeFloatBits(minValue, maxValue, precision);
            return DeQuantizeFloat((uint)ReadULong(bits), minValue, maxValue, precision);
        }
        
        public void WriteShort(short value)
        {
            ushort zigzag = (ushort)((value << 1) ^ (value >> 15));
            WriteULong(zigzag, 16);
        }

        
        public short ReadShort()
        {
            ushort value = (ushort)ReadULong(16);
            short zagzig = (short)((short)(value >> 1) ^ (-(short)(value & 1)));
            return zagzig;
        }

        public void WriteInt(int value)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            WriteULong(zigzag, 32);
        }

        
        public int ReadInt()
        {
            uint value = (uint)ReadULong(32);
            int zagzig = ((int)(value >> 1) ^ (-(int)(value & 1)));
            return zagzig;
        }
        
        public void WriteLong(long value)
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
            WriteULong(zigzag, 64);
        }

        
        public long ReadLong()
        {
            ulong value = ReadULong(64);
            long zagzig = ((long)(value >> 1) ^ (-(long)(value & 1)));
            return zagzig;
        }

        public void WriteLong(long value, long minValue, long maxValue)
        {
#if IBC_ASSERT_SER
            long range = checked(maxValue - minValue);
            Debug.Assert(range > 0);
            Debug.Assert(value <= maxValue && value >= minValue);
#endif
            //both positive or both negative
            if ((minValue >= 0 && maxValue > 0) || (minValue < 0 && maxValue <= 0))
            {
                int sign = 1;
                if (minValue < 0)
                    sign = -1;
                
                ulong temp = (ulong)(value * sign);
                WriteULong(temp, (ulong)(minValue * sign), (ulong)(maxValue * sign));
            }
            //different sign
            else
            {
                ulong temp = (ulong)(value - minValue);
                WriteULong(temp, 0, (ulong)(maxValue - minValue));
            }
        }
        
        public long ReadLong(long minValue, long maxValue)
        {
#if IBC_ASSERT_SER
            long range = checked(maxValue - minValue);
            Debug.Assert(range > 0);
#endif

            //both positive or both negative
            if ((minValue >= 0 && maxValue > 0) || (minValue < 0 && maxValue <= 0))
            {
                int sign = 1;
                if (minValue < 0)
                    sign = -1;
                
                ulong temp = ReadULong((ulong)(minValue * sign), (ulong)(maxValue * sign));
                long result = (long)temp * sign;
#if IBC_ASSERT_SER
                Debug.Assert(result <= maxValue && result >= minValue);
#endif
                return result;
            }
            //different sign
            else
            {
                ulong temp = ReadULong(0, (ulong)(maxValue - minValue));
                long result = ((long)temp) + minValue;
#if IBC_ASSERT_SER
                Debug.Assert(result <= maxValue && result >= minValue);
#endif
                return result;

            }
        }

        public void WriteULong(ulong value, ulong minValue, ulong maxValue)
        {
#if IBC_ASSERT_SER
            Debug.Assert(value <= maxValue && value >= minValue);
#endif
            WriteULong(value, RequiredBits(minValue, maxValue));
        }

        public ulong ReadULong(ulong minValue, ulong maxValue)
        {
            ulong result = ReadULong(RequiredBits(minValue, maxValue));
#if IBC_ASSERT_SER
            Debug.Assert(result <= maxValue && result >= minValue);
#endif
            return result;
        }
        
        public void WriteULong(ulong value, int bits)
        {
            if (bits == 0)
            {
                WriteBit(false);
                return;
            }
            

            if (bits < 0 || bits > 64)
            {
#if IBC_ASSERT_SER
                Debug.LogError($"Bits out of range: {bits}/{64}");
#endif
                Failed = true;
                return;
            }

            ulong highbit = 1UL << (bits - 1);
            ulong max = (highbit) | (highbit - 1);
            if (value > max)
            {
#if IBC_ASSERT_SER
                Debug.LogError($"Value out of range: {value}/{max}");
#endif
                Failed = true;
                return;
            }

            int requiredCapacity = ((BitPosition + bits - 1) >> 3) + 1;
            if (requiredCapacity > Capacity)
            {
#if IBC_ASSERT_SER
                //Debug.LogError($"Overflow: {requiredCapacity}/{Capacity}");
#endif
                Failed = true;
                return;
            }
            
            const int maxbits = 8;
            const int modulus = maxbits - 1;
            int offset = BitPosition & modulus;
            int index = BitPosition >> 3;
            int totalpush = offset + bits;

            ulong mask = ulong.MaxValue >> (64 - bits);
            ulong offsetmask = mask << offset;
            ulong offsetcomp = value << offset;

            Buffer[index] = (byte)((Buffer[index] & ~offsetmask) | (offsetcomp & offsetmask));

            offset = maxbits - offset;
            totalpush -= maxbits;

            while (totalpush > maxbits)
            {
                ++index;
                offsetcomp = value >> offset;
                Buffer[index] = (byte)offsetcomp;
                offset += maxbits;
                totalpush -= maxbits;
            }

            if (totalpush > 0)
            {
                ++index;
                offsetmask = mask >> offset;
                offsetcomp = value >> offset;
                Buffer[index] = (byte)((Buffer[index] & ~offsetmask) | (offsetcomp & offsetmask));
            }

            BitPosition += bits;
        }


        public ulong ReadULongWithOverflow(int bits)
        {
            if (bits == 0)
                return ReadBit() ? 1u : 0u;
            
            if (bits < 0 || bits > 64)
            {
#if IBC_ASSERT_SER
                Debug.LogError($"Bits out of range: {bits}/{64}");
#endif
                Failed = true;
                return 0;
            }

            const int maxbits = 8;
            const int modulus = maxbits - 1;
            int offset = BitPosition & modulus;
            int index = BitPosition >> 3;

            if (index >= Capacity)
                return 0;

            ulong mask = ulong.MaxValue >> (64 - bits);
            ulong value = (ulong)Buffer[index] >> offset;
            offset = maxbits - offset;
            while (offset < bits && index < Capacity - 1)
            {
                index++;
                value |= (ulong)Buffer[index] << offset;
                offset += maxbits;
            }

            BitPosition += bits;
            ulong result = value & mask;
            ulong highbit = 1UL << (bits - 1);
            ulong max = (highbit) | (highbit - 1);

            if (result > max)
            {
#if IBC_ASSERT_SER
                Debug.LogError($"Value out of range: {value}/{max}");
#endif
                Failed = true;
                return 0;
            }
            return result;
        }
        
        
        public ulong ReadULong(int bits)
        {
            int requiredCapacity = ((BitPosition + bits - 1) >> 3) + 1;
            if (requiredCapacity > Capacity)
            {
#if IBC_ASSERT_SER
                //Debug.LogError($"Overflow {requiredCapacity}/{Capacity}");
#endif
                Failed = true;
                return 0;
            }

            return ReadULongWithOverflow(bits);
        }
        
        
        public void CompressWriteInt32(int value)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            CompressWriteUInt32(zigzag);
        }

        public void CompressWriteUInt32(uint value)
        {
            int bucket = CompressionModel.CalculateBucket(value);
            uint offset = CompressionModel.bucketOffsets[bucket];
            int bits = CompressionModel.bucketSizes[bucket];
            ushort encodeEntry = CompressionModel.encodeTable[bucket];
            WriteULong((uint)(encodeEntry >> 8), encodeEntry & 0xFF);
            WriteULong(value - offset, bits);
        }
        
        
        public int ReadCompressInt32()
        {
            uint value = ReadCompressUInt32();
            int zagzig = ((int)(value >> 1) ^ (-(int)(value & 1)));
            return zagzig;
        }

        public uint ReadCompressUInt32()
        {
            int bitPosition = BitPosition;
            uint peekBits = (uint)ReadULongWithOverflow(CompressionModel.k_MaxHuffmanSymbolLength);
            ushort huffmanEntry = CompressionModel.decodeTable[peekBits];
            int symbol = huffmanEntry >> 8;
            int length = huffmanEntry & 0xFF;
            BitPosition = bitPosition + length;
            uint offset = CompressionModel.bucketOffsets[symbol];
            int bits = CompressionModel.bucketSizes[symbol];
            return (uint)(ReadULong(bits) + offset);
        }


        public void WriteBytes(byte[] data, int offset, int length)
        {
            for (int i = offset; i < length; i++)
            {
                WriteULong(data[i], 8);
            }
        }

        public void ReadBytes(byte[] bytes, int length)
        {
            for (int i = 0; i < length; i++)
            {
                bytes[i] = (byte)ReadULong(8);
            }
        }

        
        /// <summary>
        /// Write ascii string with max length of ushort.MaxValue.
        /// </summary>
        /// <exception cref="Exception">Throws when length greater than 255</exception>
        public void WriteString(string data)
        {
#if IBC_ASSERT_SER
            Debug.Assert(data.Length <= ushort.MaxValue);
#endif
            WriteULong((byte)data.Length, 16);
            for (int i = 0; i < data.Length; i++)
                WriteULong(_converter.ToAscii(data[i]), 8);
        }

        /// <summary>
        /// Read ascii string.
        /// </summary>
        public string ReadString()
        {
            
            int length = (int)ReadULong(16);
#if IBC_ASSERT_SER
            Debug.Assert(length <= 255);
#endif
            StringBuilder builder = new StringBuilder(length);
            char c;
            for (int i = 0; i < length; i++)
            {
                c = _converter.FromAscii((byte)ReadULong(8));
                builder.Append(c);
            }

            return builder.ToString();
        }

        public static int RequiredBits(ulong maxValue)
        {
            return RequiredBits(0, maxValue);
        }
        
        public static int RequiredBits(ulong minValue, ulong maxValue)
        {
#if IBC_ASSERT_SER
            ulong range = checked(maxValue - minValue);
            Debug.Assert(range > 0);
#endif
            return (int)Math.Floor(Math.Log(maxValue - minValue, 2)) + 1;
        }

        public void Dispose()
        {
#if IBC_ASSERT_SER
            if(Failed)
                Debug.LogError("Bitstream: Failed");
#endif
        }

        public void SetCapacity(int capacity)
        {
            Capacity = capacity;
        }
    }
}
