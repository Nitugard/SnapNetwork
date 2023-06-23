// Created by Dragutin Sredojevic
// All Rights Reserved
// Contact: contact@nitugard.com
// Website: www.nitugard.com

namespace Ibc.Survival
{
    
    public interface IStream
    {
        public const ulong DebugLongInt = 0xf50ff055aa55abd5;
        
        bool IsWriting { get; }
        int Length { get; }
        bool Fail { get; set; }
        int BitPosition { get; set; }

        void ExchangeDebug(ulong test = DebugLongInt);
        
        void Exchange(ref bool value);
        void Exchange(ref byte value);
        void Exchange(ref byte value, int bits);
        void Exchange(ref byte value, byte minValue, byte maxValue);
        void Exchange(ref ushort value);
        void Exchange(ref ushort value, int bits);
        void Exchange(ref ushort value, ushort minValue, ushort maxValue);
        void Exchange(ref short value);
        void Exchange(ref short value, short minValue, short maxValue);
        void Exchange(ref int value);
        void Exchange(ref int value, int minValue, int maxValue);
        void Exchange(ref uint value);
        void Exchange(ref uint value, int bits);
        void Exchange(ref uint value, uint minValue, uint maxValue);
        void Exchange(ref ulong value);
        void Exchange(ref ulong value, int bits);
        void Exchange(ref ulong value, ulong minValue, ulong maxValue);
        void Exchange(ref long value);
        void Exchange(ref long value, long minValue, long maxValue);
        void Exchange(ref float value);
        void Exchange(ref float value, float minValue, float maxValue, int precision);
        void Exchange(ref string value);

        void ExchangeDelta(ref byte value, byte baseLine);
        void ExchangeDelta(ref ushort value, ushort baseLine);
        void ExchangeDelta(ref short value, short baseLine);
        void ExchangeDelta(ref int value, int baseLine);
        void ExchangeDelta(ref uint value, uint baseLine);
        void ExchangeDelta(ref long value, long baseLine);
        void ExchangeDelta(ref float value, float baseLine, float minValue, float maxValue, int precision);

    }
}