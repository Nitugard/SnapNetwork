namespace Ibc.Survival
{
    public interface IMessage
    {
        
        public void Exchange<TStream>(ref TStream stream) where TStream : IStream;
    }
    
    public interface IMessageDelta<T> where T : struct
    {
        
        public void ExchangeDelta<TStream>(ref TStream stream, T baseLine) where TStream : IStream;
    }

    public struct EmptyMessage : IMessage
    {
        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
        }
    }
    
    public struct StringMessage : IMessage
    {
        public string Data;

        public StringMessage(string data)
        {
            Data = data;
        }

        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            stream.Exchange(ref Data);
        }
    }
    
    public struct ByteMessage : IMessage
    {
        public byte Data;
        
        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            stream.Exchange(ref Data);
        }
    }
    
    public struct IntegerMessage : IMessage
    {
        public int Data;
        
        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            stream.Exchange(ref Data);
        }
    }

    public struct EntityMessage : IMessage
    {
        public EntityId EntityId;
        public byte Data;

        public EntityMessage(EntityId entityId, byte data = 0)
        {
            EntityId = entityId;
            Data = data;
        }

        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            stream.Exchange(ref EntityId.Value);
            stream.ExchangeDelta(ref Data, 0);
        }
    }


    public static class MessageExtensions
    {
        public static bool Unpack<T>(this Packet packet, out T message) where T : struct, IMessage
        {
            StreamReader streamReader = new StreamReader(packet.Data, packet.Length);
            message = default(T);
            message.Exchange(ref streamReader);
            return !streamReader.Fail;
        }
        
        public static bool UnpackRef<T>(this Packet packet, ref T message) where T : struct, IMessage
        {
            StreamReader streamReader = new StreamReader(packet.Data, packet.Length);
            message.Exchange(ref streamReader);
            return !streamReader.Fail;
        }
        
        public static bool Unpack<T>(this Packet packet, T message) where T : class, IMessage
        {
            StreamReader streamReader = new StreamReader(packet.Data, packet.Length);
            message.Exchange(ref streamReader);
            return !streamReader.Fail;
        }
    }
}