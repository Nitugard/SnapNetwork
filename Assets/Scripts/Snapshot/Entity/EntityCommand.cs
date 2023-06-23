using System;
using Ibc.Game;

namespace Ibc.Survival
{
    public enum EntityCommandType : byte
    {
        Invalid,
        ContainerRequest,
        ContainerUpdate,
        FireCommand
    }

    public struct EntityCommandHeader : IMessage
    {
        public EntityCommandType CommandType;
        public EntityId EntityId;
        public int LocalTick;

        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            byte temp = (byte) CommandType;
            stream.Exchange(ref temp);
            stream.Exchange(ref LocalTick);
            stream.ExchangeDelta(ref EntityId.Value, 0);
            CommandType = (EntityCommandType) temp;
        }
    }
    
    [Serializable]
    public struct EntityCommand<T> : IMessage where T: IMessage
    {
        public EntityCommandHeader Header;
        public T Cmd;
        
        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            Header.Exchange(ref stream);
            Cmd.Exchange(ref stream);
        }
    }
}