using System;
using Ibc.Survival;

namespace Ibc.Game
{
    
    public enum ControllerCommandType : byte
    {
        JoinGame,
        ChatMessage,
        ContainerItemDrag,
        ContainerItemDrop
    }
    
    [Serializable]
    public struct ControllerCommand<T> : IMessage where T: IMessage
    {
        public ControllerCommandType CommandType;
        public T Cmd;
        
        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            byte temp = (byte) CommandType;
            stream.Exchange(ref temp);
            CommandType = (ControllerCommandType) temp;
            Cmd.Exchange(ref stream);
        }
    }
}