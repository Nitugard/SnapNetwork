using UnityEngine;

namespace Ibc.Survival
{
    public static class StreamExtensions
    {
        #region FLOAT_VECTOR

        public static void Exchange<TStream>(this ref TStream stream, ref Vector2 value) where TStream : struct, IStream
        {
            stream.Exchange(ref value.x);
            stream.Exchange(ref value.y);
        }
        
        public static void ExchangeDelta<TStream>(this ref TStream stream, ref Vector2 value, Vector2 baseLine, float minValue, float maxValue, int precision)
            where TStream : struct, IStream
        {
            stream.ExchangeDelta(ref value.x, baseLine.x, minValue, maxValue, precision);
            stream.ExchangeDelta(ref value.y, baseLine.y, minValue, maxValue, precision);
        }

        public static void ExchangeDelta<TStream>(this ref TStream stream, ref Vector2 value, Vector2 baseLine, Vector2 minValue, Vector2 maxValue, int precision)
            where TStream : struct, IStream
        {
            stream.ExchangeDelta(ref value.x, baseLine.x, minValue.x, maxValue.x, precision);
            stream.ExchangeDelta(ref value.y, baseLine.y, minValue.y, maxValue.y, precision);
        }

        public static void Exchange<TStream>(this ref TStream stream, ref Vector3 value) where TStream : struct, IStream
        {
            stream.Exchange(ref value.x);
            stream.Exchange(ref value.y);
            stream.Exchange(ref value.z);
        }

        public static void ExchangeDelta<TStream>(this ref TStream stream, ref Vector3 value, Vector3 baseLine, float minValue, float maxValue, int precision)
            where TStream : struct, IStream
        {
            stream.ExchangeDelta(ref value.x, baseLine.x, minValue, maxValue, precision);
            stream.ExchangeDelta(ref value.y, baseLine.y, minValue, maxValue, precision);
            stream.ExchangeDelta(ref value.z, baseLine.z, minValue, maxValue, precision);
        }
        
        public static void ExchangeDelta<TStream>(this ref TStream stream, ref Vector3 value, Vector3 baseLine, Vector3 minValue, Vector3 maxValue, int precision)
            where TStream : struct, IStream
        {
            stream.ExchangeDelta(ref value.x, baseLine.x, minValue.x, maxValue.x, precision);
            stream.ExchangeDelta(ref value.y, baseLine.y, minValue.y, maxValue.y, precision);
            stream.ExchangeDelta(ref value.z, baseLine.z, minValue.z, maxValue.z, precision);
        }


        #endregion

    }
}
