#define DEBUG_ENABLE_SERIALIZATION

using System;
using UnityEngine;

namespace Ibc.Survival
{
    
    [Serializable]
    public struct CommandConfiguration
    {
        public ushort MaximumCommandsInQueue;
        public byte MaximumCommandsInPacket;
        public int MaximumCommandSize;
    }
    
    /// <summary>
    /// State of the command.
    /// </summary>
    public enum CommandState
    {
        /// <summary>
        /// Default command state.
        /// </summary>
        Invalid,
        
        /// <summary>
        /// Command is received from remote end.
        /// </summary>
        Received,
        
        /// <summary>
        /// Sent command is acked by remote end.
        /// </summary>
        Acked,
        
        /// <summary>
        /// Command is dropped.
        /// </summary>
        Dropped,
    }

    public delegate void CommandDelegate(Connection connection, ref StreamReader streamReader, CommandId commandId, CommandState commandState);


    /// <summary>
    /// Unique command identifier for specific connection.
    /// <remarks>
    /// This is just a wrapper around integer type. This is done in order to have the ability to simply change underlying type.
    /// </remarks>
    /// </summary>
    [Serializable]
    public struct CommandId : IEquatable<CommandId>, IComparable<CommandId>
    {
        public static CommandId MaxValue => new CommandId(uint.MaxValue);

        public uint Value => _value;
        
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private uint _value;

        public CommandId(uint value)
        {
            _value = value;
        }

        public void Exchange<TStream>(ref TStream stream) where TStream : struct, IStream
        {
            stream.ExchangeDelta(ref _value, 1);
        }

        public bool Equals(CommandId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is CommandId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)_value;
        }
        
        public CommandId Increment()
        {
            return new CommandId(++_value);
        }

        public int CompareTo(CommandId other)
        {
            return _value.CompareTo(other._value);
        }
    }

    /// <summary>
    /// Manages and provides methods to send commands and receive them from remote end for specific connection <see cref="Connection"/>.
    /// <remarks>
    /// This manager uses circular buffer that keeps track of commands, sending new commands when circular buffer is full will
    /// overwrite the oldest command and delegate will be invoked to inform it that command is dropped.
    /// Commands are removed from circular buffer when ack is received from the remote end.
    /// </remarks>
    /// </summary>
    [Serializable]
    public class CommandManager 
    {
        /// <summary>
        /// Cached internal command data.
        /// </summary>
        [Serializable]
        private struct ConnectionCommand
        {
            public CommandType CommandType;
            public CommandId CommandId;
            public byte[] CommandData;
            public int CommandDataLength;
        }
        
        /// <summary>
        /// Header used when sync commands with the remote host. 
        /// </summary>
        public struct Header
        {
            /// <summary>
            /// Number of command sent.
            /// </summary>
            public byte Count;
            
            public void Exchange<TStream>(ref TStream stream) where TStream : struct, IStream
            {
                stream.Exchange(ref Count);
            }
        }
        
        /// <summary>
        /// Reference to the connection for which commands are managed.
        /// </summary>
        private Connection _connection;
        
        /// <summary>
        /// Last received command identifier.
        /// </summary>
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private CommandId _lastRecvCommandId;
        
        /// <summary>
        /// Last acked command identifier.
        /// </summary>
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private CommandId _lastAckCommandId;

        /// <summary>
        /// Unique command identifier counter.
        /// </summary>
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private CommandId _nextCommandId;

        /// <summary>
        /// Circular buffer that keeps track of buffered commands.
        /// </summary>
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private CircularBuffer<ConnectionCommand> _commandCircularBuffer;

        
        /// <summary>
        /// Maximum commands that will be serialized in a single packet.
        /// </summary>
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private int _maxCommandsPerPacket;
        
        /// <summary>
        /// Maximum size in bytes per command.
        /// </summary>
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private int _maxCommandSize;
        
        /// <summary>
        /// Current number of buffered commands.
        /// </summary>
        public int Count => _commandCircularBuffer.Count;


        private CommandRegister _commandRegister;

        
        /// <summary>
        /// Creates new instance of connection command manager for specific connection.
        /// </summary>
        public CommandManager(Connection connection, CommandRegister commandRegister, CommandConfiguration configuration)
        {
            _connection = connection;
            _maxCommandSize = configuration.MaximumCommandSize;
            _commandCircularBuffer = new CircularBuffer<ConnectionCommand>(configuration.MaximumCommandsInQueue);
            _maxCommandsPerPacket = configuration.MaximumCommandsInPacket;
            _nextCommandId = new CommandId(1);
            _commandRegister = commandRegister;
        }
        
        /// <summary>
        /// Send command to remote host. If circular buffer is full old overwritten command will issue
        /// command dropped callback.
        /// </summary>
        /// <returns>True if command is queued, false otherwise with error output</returns>
        public bool SendCommand<T>(CommandType commandType, T command, out CommandId cmdId) where T : struct, IMessage
        {
            cmdId = default;

            if (_nextCommandId.Equals(CommandId.MaxValue))
            {
                Debug.LogError($"Command id overflow");
                return false;
            }

            //create internal command storage
            ConnectionCommand connectionCommand = new ConnectionCommand();
            connectionCommand.CommandId = _nextCommandId;
            
            connectionCommand.CommandData = new byte[(_maxCommandSize)];
            connectionCommand.CommandType = commandType;
            
            StreamWriter streamWriter = new StreamWriter(connectionCommand.CommandData, _maxCommandSize);
            try
            {
                //cache command data
                command.Exchange(ref streamWriter);
                if (streamWriter.Fail) throw new Exception($"Stream overflow when writing command: {typeof(T).Name} {streamWriter.Length/streamWriter.BitStream.Capacity}");
                connectionCommand.CommandDataLength = streamWriter.Length;
                
                //update unique command id
                _nextCommandId = _nextCommandId.Increment();

                //check if overwriting old commands
                if (_commandCircularBuffer.IsFull)
                {
                    var connectionCommandOld = _commandCircularBuffer.Front();
                    if (_commandRegister.TryGetProcessCommandDelegate(commandType, out var processCommandDelegate))
                    {
                        try
                        {
                            StreamReader streamReader = new StreamReader(connectionCommandOld.CommandData,
                                connectionCommandOld.CommandDataLength);
                            processCommandDelegate.Invoke(_connection,
                                ref streamReader, connectionCommand.CommandId, CommandState.Dropped);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(
                                $"Command dropped but ProcessCommandInternal raised exception: {commandType}");
                            Debug.LogError(ex.Message);
                            Debug.LogError(ex.StackTrace);
                        }
                    }

                    //todo:
                    //deallocate
                    //_connection.ConnectionManager.ByteArrayPoolPool.Free(connectionCommandOld.CommandData);
                }
                
                //store command
                _commandCircularBuffer.PushBack(connectionCommand);
                cmdId = _nextCommandId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not write command {typeof(T).Name} to the stream, exception was raised");
                Debug.LogError(ex.Message);
                Debug.LogError(ex.StackTrace);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Process commands method will go through circular buffer and try to execute command receive callback
        /// while popping them no matter what.
        /// </summary>
        public void ProcessCommands()
        {
            while (!_commandCircularBuffer.IsEmpty)
            {
                var connectionCommand = _commandCircularBuffer.Front();
                _commandCircularBuffer.PopFront();

                Debug.Log($"Received cmd, type: {connectionCommand.CommandType} id: {connectionCommand.CommandId}");

                if (_commandRegister.TryGetProcessCommandDelegate(connectionCommand.CommandType, out var processCommandDelegate))
                {
                    try
                    {
                        StreamReader streamReader = new StreamReader(connectionCommand.CommandData, connectionCommand.CommandDataLength);
                        processCommandDelegate.Invoke(_connection, ref streamReader, connectionCommand.CommandId, CommandState.Received);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Command recv but ProcessCommandInternal raised exception");
                        Debug.LogException(ex);
                    }
                }

                //todo:
                //deallocate
                //_connection.ConnectionManager.ByteArrayPoolPool.Free(connectionCommand.CommandData);
            }
        }
        
        /// <summary>
        /// Remove all commands in the queue. Will not call any callback.
        /// </summary>
        public void ClearCommands()
        {
            while (!_commandCircularBuffer.IsEmpty)
            {
               var connectionCommand = _commandCircularBuffer.Front();
               _commandCircularBuffer.PopFront();
               
               //todo:
               //deallocate
               //_connection.ConnectionManager.ByteArrayPoolPool.Free(connectionCommand.CommandData);
            }
        }
        
        /// <summary>
        /// Returns connection command id for the last received command.
        /// </summary>
        /// <returns></returns>
        public CommandId GetLastRecvCommandId()
        {
            return _lastRecvCommandId;
        }

        /// <summary>
        /// Process and remove acked commands from the buffer.
        /// </summary>
        public void TryAckCommands(CommandId commandId)
        { 
            //clamp the command id to max sent command id
            commandId = new CommandId(Math.Clamp(commandId.Value, 0, _nextCommandId.Value - 1));
            
            //id has to be greater then last ack connection command id
            if (_lastAckCommandId.CompareTo(commandId) >= 0)
                return;
            
            //find the command range in the buffer to be removed
            int startIndex = 0, endIndex = -1;
            for (var i = 0; i < _commandCircularBuffer.Count; i++)
            {
                if (_commandCircularBuffer[i].CommandId.CompareTo(commandId) == 0)
                {
                    endIndex = i;
                    break;
                }
                
                if (startIndex == 0 && _commandCircularBuffer[i].CommandId.CompareTo(commandId) < 0)
                    startIndex = i;

            }

            if (endIndex == -1)
                return;

            //process and remove commands
            for (int i = startIndex; i < endIndex + 1; ++i)
            {
                var connectionCommand = _commandCircularBuffer.Front();
                if (_commandRegister.TryGetProcessCommandDelegate(connectionCommand.CommandType, out var processCommandDelegate))
                {
                    
                    try
                    {
                        StreamReader streamReader = new StreamReader(connectionCommand.CommandData,
                            connectionCommand.CommandDataLength);
                        processCommandDelegate.Invoke(_connection, ref streamReader, connectionCommand.CommandId, CommandState.Acked);
                        
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Command ack-ed but ProcessCommandInternal raised exception");
                        Debug.LogError(ex.Message);
                        Debug.LogError(ex.StackTrace);
                    }
                }
                else
                {
                    
                }

                //todo:
                //deallocate
                //_connection.ConnectionManager.ByteArrayPoolPool.Free(connectionCommand.CommandData);
                _commandCircularBuffer.PopFront();
            }

            _lastAckCommandId = commandId;
        }


        /// <summary>
        /// Writes commands to the stream until stream is full or until all data
        /// is sent. Stream won't overflow.
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        /// <param name="header">Header to update to reflect number of sent commands</param>
        public void WriteCommands(ref StreamWriter stream, ref Header header)
        {
            header.Count = 0;

            if (stream.Fail || _commandCircularBuffer.IsEmpty)
                return;
            
            ushort sent = 0;

            //get maximum commands that will be sent
            byte max = (byte)Math.Min(_commandCircularBuffer.Count, _maxCommandsPerPacket);
            for (int i = 0; i < max; i++)
            {
                //mark current valid stream position
                int bitPosition = stream.BitPosition;

                //send first command identifier
                var connectionCommand = _commandCircularBuffer[i];
                if (i == 0)
                {
                    connectionCommand.CommandId.Exchange(ref stream);
                }
                
                //send command type and buffer length
                var commandType = (byte)connectionCommand.CommandType;
                int bufferLength = connectionCommand.CommandDataLength;
                stream.ExchangeDelta(ref commandType, 0);
                stream.ExchangeDelta(ref bufferLength, 0);

                //write command bytes
                if(bufferLength != 0) stream.BitStream.WriteBytes(connectionCommand.CommandData, 0, connectionCommand.CommandDataLength);
                
                //if stream failed to write previous data then reset it to last valid position and stop writing
                if (stream.Fail)
                {
                    stream.BitPosition = bitPosition;
                    stream.Fail = false;
                    break;
                }
                
                sent++;
            }
            
            header.Count = (byte)sent;
        }
        
        
        /// <summary>
        /// Reads commands from the stream, stops reading if stream overflows.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <param name="header">Header that indicates number of commands that should be read</param>
        /// <returns>True if number of read commands match provided header and stream did not overflow</returns>
        public bool ReadCommands(ref StreamReader stream, ref Header header)
        {
            if (stream.Fail)
                return false;

            if (header.Count == 0)
                return true;
            
            Debug.Assert(header.Count <= _maxCommandsPerPacket);
            CommandId commandId = default;
            ushort i;
            byte commandType = 0;
            int bufferLength = 0;

            for (i = 0; i < header.Count; i++)
            {
                
                //read first command identifier
                if (i == 0)
                    commandId.Exchange(ref stream);

                //read command type and buffer length
                stream.ExchangeDelta(ref commandType, 0);
                stream.ExchangeDelta(ref bufferLength, 0);
                

                var recvCommand = new ConnectionCommand()
                {
                    CommandId = commandId,
                    CommandType = (CommandType)commandType,
                    CommandDataLength = bufferLength
                };
                
                if (bufferLength != 0)
                {
                    //read aligned buffer
                    byte[] commandData = new byte[(bufferLength)];
                    stream.BitStream.ReadBytes(commandData, bufferLength);
                    recvCommand.CommandData = commandData;
                }
                
                
                //cache received command
                if (_lastRecvCommandId.Value < recvCommand.CommandId.Value)
                {
                    _commandCircularBuffer.PushBack(recvCommand);
                    _lastRecvCommandId = recvCommand.CommandId;
                }
                else
                {
                    //todo:
                    //deallocate
                    //_connection.ConnectionManager.ByteArrayPoolPool.Free(recvCommand.CommandData);
                }

                commandId = commandId.Increment();
            }

            return i == header.Count;
        }
    }
}