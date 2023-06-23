namespace Ibc.Survival
{
    public static class StaticAllocator
    {
        public static ArrayPoolPool<byte> ByteArrayPool = new ArrayPoolPool<byte>();
    }
}