using System;
using System.Collections.Generic;

namespace Ibc.Survival
{
    

    public enum CommandType : byte
    {
        AssociateEntityWithControllerCommand,   
        SpawnPlayerCommand,
        ControllerCommand,
        EntityCommand,
    }
    
    [Serializable]
    public class CommandRegister
    {
        private readonly Dictionary<CommandType, CommandDelegate> _commandTypeToProcessCommandDelegateMap = new Dictionary<CommandType, CommandDelegate>();

        public bool RegisterCallback(CommandType cmdType, CommandDelegate commandDelegate)
        {
            return _commandTypeToProcessCommandDelegateMap.TryAdd(cmdType, commandDelegate);
        }
        
        public bool ContainsCallback(CommandType cmdType)
        {
            return _commandTypeToProcessCommandDelegateMap.ContainsKey(cmdType);
        }
        
        public bool TryGetProcessCommandDelegate(CommandType cmdType, out CommandDelegate commandDelegate)
        {
            return _commandTypeToProcessCommandDelegateMap.TryGetValue(cmdType, out commandDelegate);
        }
        
        public bool UnregisterCallback(CommandType cmdType)
        {
            return _commandTypeToProcessCommandDelegateMap.Remove(cmdType);
        }

        public void ClearCallbacks()
        {
            _commandTypeToProcessCommandDelegateMap.Clear();
        }
    }
}