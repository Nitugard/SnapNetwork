// Created by Dragutin Sredojevic
// All Rights Reserved
// Contact: contact@nitugard.com
// Website: www.nitugard.com

using System;

namespace Ibc.Survival
{
    /// <summary>
    /// Wrapper around bit stream that allows easy exchange of data with stream through <see cref="IStream"/> interface.
    /// </summary>
    public struct StreamReader : IStream, IDisposable
    {
        public bool IsWriting => false;
        public int Length => ((BitStream.BitPosition - 1) >> 3) + 1;
        public bool Fail
        {
            get => BitStream.Failed;
            set => BitStream.Failed = value;
        }

        public int BitPosition
        {
            get => BitStream.BitPosition;
            set => BitStream.BitPosition = value;
        }

        public BitStream BitStream;

        public StreamReader(Packet packet) : this(packet.Data, packet.Length)
        {
            
        }
        
        public StreamReader(byte[] data, int length)
        {
            BitStream = new BitStream(data, length);
        }
        
        public void ExchangeDebug(ulong test = IStream.DebugLongInt)
        {
            ulong value = BitStream.ReadULong(64);
            if (value != test) throw new Exception("ExchangeDebug Fail");
        }

        public void Exchange(ref bool value)
        {
            value = BitStream.ReadBit();
        }

        public void Exchange(ref byte value)
        {
            value = (byte)BitStream.ReadULong(8);
        }

        public void Exchange(ref byte value, int bits)
        {
            value = (byte)BitStream.ReadULong(bits);
        }

        public void Exchange(ref byte value, byte minValue, byte maxValue)
        {
            value = (byte)BitStream.ReadULong(minValue, maxValue);
        }

        public void Exchange(ref ushort value)
        {
            value = (ushort)BitStream.ReadULong(16);
        }

        public void Exchange(ref ushort value, int bits)
        {
            value = (ushort)BitStream.ReadULong(bits);
        }

        public void Exchange(ref ushort value, ushort minValue, ushort maxValue)
        {
            value = (ushort)BitStream.ReadULong(minValue, maxValue);
        }

        public void Exchange(ref short value)
        {
            value = BitStream.ReadShort();
        }

        public void Exchange(ref short value, short minValue, short maxValue)
        {
            value = (short)BitStream.ReadLong(minValue, maxValue);
        }

        public void Exchange(ref int value)
        {
            value = BitStream.ReadInt();
        }

        public void Exchange(ref uint value, int bits)
        {
            value = (uint)BitStream.ReadULong(bits);
        }

        public void Exchange(ref uint value, uint minValue, uint maxValue)
        {
            value = (uint)BitStream.ReadULong(minValue, maxValue);
        }

        public void Exchange(ref ulong value)
        {
            value = BitStream.ReadULong(64);
        }

        public void Exchange(ref ulong value, int bits)
        {
            value = BitStream.ReadULong(bits);
        }

        public void Exchange(ref int value, int minValue, int maxValue)
        {
            value = (int)BitStream.ReadLong(minValue, maxValue);
        }

        public void Exchange(ref uint value)
        {
            value = (uint)BitStream.ReadULong(32);
        }

        public void Exchange(ref ulong value, ulong minValue, ulong maxValue)
        {
            value = BitStream.ReadULong(minValue, maxValue);
        }

        public void Exchange(ref long value)
        {
            value = BitStream.ReadLong();
        }

        public void Exchange(ref long value, long minValue, long maxValue)
        {
            value = BitStream.ReadLong(minValue, maxValue);
        }

        public void Exchange(ref float value)
        {
            value = BitStream.ReadFloat();
        }

        public void Exchange(ref float value, float minValue, float maxValue, int precision)
        {
            value = BitStream.ReadFloat(minValue, maxValue, precision);
        }

        public void Exchange(ref string value)
        {
            value = BitStream.ReadString();
        }

        public void ExchangeDelta(ref byte value, byte baseLine)
        {
            value = (byte)(BitStream.ReadCompressInt32() + baseLine);
        }

        public void ExchangeDelta(ref ushort value, ushort baseLine)
        {
            value = (ushort)(BitStream.ReadCompressInt32() + baseLine);
        }

        public void ExchangeDelta(ref short value, short baseLine)
        {
            value = (short)(BitStream.ReadCompressInt32() + baseLine);
        }

        public void ExchangeDelta(ref int value, int baseLine)
        {
            value = (int)(BitStream.ReadCompressInt32() + baseLine);
        }

        public void ExchangeDelta(ref uint value, uint baseLine)
        {
            value = (uint)(BitStream.ReadCompressInt32() + baseLine);
        }

        public void ExchangeDelta(ref long value, long baseLine)
        {
            value = (long)(BitStream.ReadCompressInt32() + baseLine);
        }
        
        public void ExchangeDelta(ref float value, float baseLine, float minValue, float maxValue, int precision)
        {
            bool changed = Math.Abs(value - baseLine) > 1.0f / precision;
            Exchange(ref changed);
            
            if (changed)
                Exchange(ref value, minValue, maxValue, precision);
        }

        public void Dispose()
        {
            BitStream.Dispose();
        }
    }
}
