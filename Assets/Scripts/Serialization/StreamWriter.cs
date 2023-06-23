// Created by Dragutin Sredojevic
// All Rights Reserved
// Contact: contact@nitugard.com
// Website: www.nitugard.com

using System;
using UnityEngine;

namespace Ibc.Survival
{
    public struct StreamWriter : IStream, IDisposable
    {
        public bool IsWriting => true;
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
        
        public StreamWriter(Packet packet) : this(packet.Data, packet.Length)
        {
            
        }
        
        public StreamWriter(byte[] data, int length)
        {
            BitStream = new BitStream(data, length);
        }
        
        public void ExchangeDebug(ulong test = IStream.DebugLongInt)
        {
            BitStream.WriteULong(test, 64);
        }

        public void Exchange(ref bool value)
        {
            BitStream.WriteBit(value);
        }

        public void Exchange(ref byte value)
        {
            BitStream.WriteULong(value, 8);
        }

        public void Exchange(ref byte value, int bits)
        {
            BitStream.WriteULong(value, bits);
        }

        public void Exchange(ref byte value, byte minValue, byte maxValue)
        {
            BitStream.WriteULong(value, minValue, maxValue);
        }

        public void Exchange(ref ushort value)
        {
            BitStream.WriteULong(value, 16);
        }

        public void Exchange(ref ushort value, int bits)
        {
            BitStream.WriteULong(value, bits);
        }

        public void Exchange(ref ushort value, ushort minValue, ushort maxValue)
        {
            BitStream.WriteULong(value, minValue, maxValue);
        }

        public void Exchange(ref short value)
        {
            BitStream.WriteShort(value);
        }

        public void Exchange(ref short value, short minValue, short maxValue)
        {
            BitStream.WriteLong(value, minValue, maxValue);
        }

        public void Exchange(ref int value)
        {
            BitStream.WriteInt(value);
        }

        public void Exchange(ref uint value, int bits)
        {
            BitStream.WriteULong(value, bits);
        }

        public void Exchange(ref uint value, uint minValue, uint maxValue)
        {
            BitStream.WriteULong(value, minValue, maxValue);
        }

        public void Exchange(ref ulong value)
        {
            BitStream.WriteULong(value, 64);
        }

        public void Exchange(ref ulong value, int bits)
        {
            BitStream.WriteULong(value, bits);
        }

        public void Exchange(ref int value, int minValue, int maxValue)
        {
            BitStream.WriteLong(value, minValue, maxValue);
        }

        public void Exchange(ref uint value)
        {
            BitStream.WriteULong(value, 32);
        }

        public void Exchange(ref ulong value, ulong minValue, ulong maxValue)
        {
            BitStream.WriteULong(value, minValue, maxValue);
        }

        public void Exchange(ref long value)
        {
            BitStream.WriteLong(value);
        }

        public void Exchange(ref long value, long minValue, long maxValue)
        {
            BitStream.WriteLong(value, minValue, maxValue);
        }

        public void Exchange(ref float value)
        {
            BitStream.WriteFloat(value);
        }

        public void Exchange(ref float value, float minValue, float maxValue, int precision)
        {
            BitStream.WriteFloat(value, minValue, maxValue, precision);
        }

        public void Exchange(ref string value)
        {
            BitStream.WriteString(value);
        }

        public void ExchangeDelta(ref byte value, byte baseLine)
        {
            int delta = (int)(value - baseLine);
            BitStream.CompressWriteInt32(delta);
        }

        public void ExchangeDelta(ref ushort value, ushort baseLine)
        {
            int delta = (int)(value - baseLine);
            BitStream.CompressWriteInt32(delta);
        }

        public void ExchangeDelta(ref short value, short baseLine)
        {
            int delta = (int)(value - baseLine);
            BitStream.CompressWriteInt32(delta);
        }

        public void ExchangeDelta(ref int value, int baseLine)
        {
            int delta = (int)(value - baseLine);
            BitStream.CompressWriteInt32(delta);
        }

        public void ExchangeDelta(ref uint value, uint baseLine)
        {
            int delta = (int)((long)value - baseLine);
            BitStream.CompressWriteInt32(delta);
        }

        public void ExchangeDelta(ref long value, long baseLine)
        {
            int delta = (int)(value - baseLine);
            BitStream.CompressWriteInt32(delta);
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
