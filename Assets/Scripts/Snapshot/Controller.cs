#define IBC_DEBUG_INSPECTOR
using System;
using System.Collections.Generic;
using Ibc.Game;
using UnityEngine;

namespace Ibc.Survival
{

    public enum EntityControllerAssociateType : byte
    {
        Invalid,
        Link,
        Unlink
    }

    
    [Serializable]
    public struct ControllerConfiguration
    {
        public byte ClientInputBufferSize;
        public byte ServerInputBufferSize;
    }
    
    /// <summary>
    /// Input structure provided to the controller that is sent across the network in order
    /// to sync controller state.
    /// </summary>
    /// <typeparam name="T">Type of the input</typeparam>
    public interface IInput<in T> where T : unmanaged, IInput<T>
    {
        void ExchangeDelta<TStream>(ref TStream stream, T baseLine) where TStream : struct, IStream;

        /// <summary>
        /// Indicates whether the input has changed.
        /// </summary>
        /// <param name="baseLine">Base line to compare with</param>
        /// <returns>True if changed otherwise false</returns>
        bool HasChanged(T baseLine);
    }
    
    /// <summary>
    /// Result that controller outputs during simulation call. Interface provides method to check
    /// whether the resulting state is out of sync with the server resulting state for the given input.
    /// </summary>
    /// <typeparam name="T">Type of the result</typeparam>
    public interface IResult<in T> where T : unmanaged, IResult<T>
    {
        /// <summary>
        /// Whether the controller is out of sync.
        /// </summary>
        /// <param name="baseLine">Baseline to compare with</param>
        /// <returns>Zero if in sync otherwise any other number</returns>
        int IsOutOfSync(T baseLine);
        
        void Exchange<TStream>(ref TStream stream) where TStream : struct, IStream;
    }

    /// <summary>
    /// Base controller class that provides methods to write/read inputs and result from the stream.
    /// Controller can be associated with entity using <see cref="AssociateEntity"/> method. 
    /// </summary>
    [Serializable]
    public abstract class Controller
    {
        
        /// <summary>
        /// Input header when writing inputs to the stream as a client. Indicates number of sent inputs.
        /// </summary>
        public struct InputHeader
        {
            /// <summary>
            /// Number of sent inputs in the packet.
            /// </summary>
            public byte Count;

            public void Exchange<TStream>(ref TStream stream) where TStream : struct, IStream
            {
                stream.Exchange(ref Count);
            }
        }

        /// <summary>
        /// Result header that server uses to ack client inputs.
        /// </summary>
        public struct ResultHeader
        {
            /// <summary>
            /// Input tick which is ack-ed.
            /// </summary>
            public int Tick;
            
            /// <summary>
            /// Whether the resulting state is included. If server packet does not have enough
            /// space then resulting controller state(used for sync) will not be sent.
            /// </summary>
            public bool ResultIncluded;
            
            public void Exchange<TStream>(ref TStream stream) where TStream : struct, IStream
            {
                stream.Exchange(ref Tick);
                stream.Exchange(ref ResultIncluded);
            }
        }

        /// <summary>
        /// List of associated entities with the controller.
        /// </summary>
        protected List<EntityId> AssociatedEntities = new List<EntityId>();

        /// <summary>
        /// Reference to the connection that created this controller.
        /// </summary>
        protected Connection Connection;

        /// <summary>
        /// Reference to the entity manager.
        /// </summary>
        protected EntityManager EntityManager;
        
        /// <summary>
        /// Cached controller configuration.
        /// </summary>
        protected ControllerConfiguration ControllerConfiguration;
        
        /// <summary>
        /// Initialize the controller for the first time.
        /// </summary>
        public Controller(Connection connection, EntityManager entityManager, ControllerConfiguration configuration)
        {
            EntityManager = entityManager;
            Connection = connection;
            ControllerConfiguration = configuration;
        }
        
        /// <summary>
        /// Associate entity with the controller making it a controllable entity.
        /// </summary>
        /// <param name="entity">Target entity</param>
        /// <returns>True if entity is associated otherwise false</returns>
        internal bool AssociateEntity(EntityId entity)
        {
            //entity must not be associated in order for method to work
            if (IsEntityAssociated(entity))
            {
                Debug.LogWarning($"{nameof(AssociateEntity)} failed. Entity already associated: {entity}");
                return false;
            }

            AssociatedEntities.Add(entity);
            OnEntityAssociated(entity);
            Debug.Log($"{nameof(AssociateEntity)} {entity.Value}");
            return true;
        }
        
        /// <summary>
        /// De associate entity with the controller making it a no longer controllable entity.
        /// </summary>
        /// <param name="entity">Target entity</param>
        /// <returns>True if entity is de-associated otherwise false</returns>
        internal bool DeAssociateEntity(EntityId entity)
        {
            //entity must be associated in order for method to work
            if (!IsEntityAssociated(entity))
            {
                Debug.LogWarning($"{nameof(DeAssociateEntity)} failed, {entity}");
                return false;
            }

            AssociatedEntities.Remove(entity);
            OnEntityDeAssociated(entity);
            Debug.Log($"{nameof(DeAssociateEntity)} {entity}");
            return true;
        }

        /// <summary>
        /// Returns true if entity is associated with the controller otherwise false.
        /// </summary>
        /// <param name="entity">Target entity</param>
        public bool IsEntityAssociated(EntityId entity)
        {
            return AssociatedEntities.Contains(entity);
        }

        /// <summary>
        /// Returns true if entity is associated with the controller otherwise false.
        /// </summary>
        /// <param name="entity">Target entity</param>
        public int GetAssociatedEntityIndex(EntityId entity)
        {
            for (int i = 0; i < AssociatedEntities.Count; i++)
            {
                if (AssociatedEntities[i].Equals(entity))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Invoked when associated entity gets linked with entity behaviour.
        /// </summary>
        /// <param name="entityBehaviour">Target entity behaviour</param>
        public abstract void OnEntityLinkSet(EntityBehaviour entityBehaviour);
        
        /// <summary>
        /// Invoked when associated entity gets unlinked with entity behaviour.
        /// </summary>
        /// <param name="entityBehaviour">Target entity behaviour</param>
        public abstract void OnEntityLinkUnset(EntityBehaviour entityBehaviour);
        
        /// <summary>
        /// Invoked when entity gets associated with this controller.
        /// </summary>
        /// <param name="entity">Target entity</param>
        protected abstract void OnEntityAssociated(EntityId entity);

        /// <summary>
        /// Invoked when entity gets de-associated with this controller.
        /// </summary>
        /// <param name="entity">Target entity</param>
        protected abstract void OnEntityDeAssociated(EntityId entity);
        
        /// <summary>
        /// Returns list of associated entities.
        /// </summary>
        public virtual List<EntityId> GetAssociatedEntities() => AssociatedEntities;

        /// <summary>
        /// Called on server fixed tick.
        /// </summary>
        internal abstract void Sv_NetworkTick_Internal(int tick);
        
        /// <summary>
        /// Called on client network tick.
        /// </summary>
        internal abstract void Cl_NetworkTick_Internal(int tick);
        
        internal abstract void WriteInputData(ref StreamWriter stream, ref InputHeader inputHeader);
        internal abstract void ReadInputData(ref StreamReader stream, ref InputHeader inputHeader);
        
        internal abstract void WriteResultData(ref StreamWriter stream, ref ResultHeader resultHeader);
        internal abstract void ReadResultData(ref StreamReader stream, ref ResultHeader resultHeader);

        internal abstract ResultHeader GetResultHeader();
        internal abstract InputHeader GetInputHeader();

        public virtual void Cl_OnControllerCommandReceived(ControllerCommandType cmdType, CommandId id, ref StreamReader streamReader){}

        public virtual void Cl_OnControllerCommandDropped(ControllerCommandType cmdType, CommandId id, ref StreamReader streamReader){}

        public virtual void Cl_OnControllerCommandAcked(ControllerCommandType cmdType, CommandId id, ref StreamReader streamReader){}
        
        public virtual void Sv_OnControllerCommandReceived(ControllerCommandType cmdType, CommandId id, ref StreamReader streamreader) {}
        public virtual void Sv_OnControllerCommandAcked(ControllerCommandType cmdType, CommandId id, ref StreamReader streamreader) {}
        public virtual void Sv_OnControllerCommandDropped(ControllerCommandType cmdType, CommandId id, ref StreamReader streamreader) {}

        public bool SendControllerCommand<T>(ControllerCommandType cmdType, T cmd, out CommandId cmdId) where T : struct, IMessage
        {
            if (Connection.GetState() is GameConnectionState gameConnectionState)
            {
                var controllerCmd = new ControllerCommand<T>();
                controllerCmd.CommandType = cmdType;
                controllerCmd.Cmd = cmd;
                return gameConnectionState.SendCommand(CommandType.ControllerCommand, controllerCmd, out cmdId);
            }
            else
            {
                Debug.LogError($"SendControllerCommand failed: Connection is not in game state {Connection.GetState()}");
            }

            cmdId = default;
            return false;
        }

        public Connection GetConnection()
        {
            return Connection;
        }
    }

    //todo: improve comment
    /// <summary>
    /// Client needs to run half RTT ahead of the server, so that the inputs <see cref="TInput"/> it sends arrive "just in time"
    /// for the server to process them.
    /// This class provides methods to sync resulting state of the controller <see cref="TResult"/> that changed due to
    /// previously applied inputs <see cref="TInput"/>.
    /// Sync is done in fixed intervals when client sends all unacked inputs delta compressed and server sends input ack together
    /// with correct resulting state.
    /// In order to speed up or slow down the client server updates client delta time factor <see cref="_deltaTimeAdjusted"/>
    /// such that new added inputs will take more or less time to execute thus filling the buffer more or less quickly. Server
    /// sends number of inputs in the buffer as a part of the result message, client uses this number to adjust delta time.
    /// </summary>
    /// <typeparam name="TInput">Controller input type</typeparam>
    /// <typeparam name="TResult">Controller resulting state type</typeparam>
    [Serializable]
    public abstract class Controller<TInput, TResult> : Controller 
        where TInput : unmanaged, IInput<TInput>
        where TResult : unmanaged, IResult<TResult>

    {
        /// <summary>
        /// Class that caches input together with tick number when the input is applied.
        /// </summary>
        [Serializable]
        private struct InternalInput
        {
            public int Tick;
            public TInput Input;
            public TResult Result;
        }
        
        /// <summary>
        /// Cached inputs on both server and client.
        /// </summary>
#if IBC_DEBUG_INSPECTOR
        [SerializeField] 
#endif
        private List<InternalInput> _inputs;

        /// <summary>
        /// Last acked input tick.
        /// </summary>
#if IBC_DEBUG_INSPECTOR
        [SerializeField] 
#endif
        private int _ackedInputTick;
        
        
        /// <summary>
        /// Maximum input tick received on the server.
        /// </summary>
#if IBC_DEBUG_INSPECTOR
        [SerializeField] 
#endif
        private int _maximumRecvInputTick;

        
        /// <summary>
        /// Last processed client input cached.
        /// </summary>
#if IBC_DEBUG_INSPECTOR
        [SerializeField] 
#endif
        private InternalInput _lastProcessedInput;

        public Controller(Connection connection, EntityManager entityManager, ControllerConfiguration configuration)
        : base(connection, entityManager, configuration)
        {
            _lastProcessedInput = default;
            _ackedInputTick = -1;
            _maximumRecvInputTick = -1;
            _inputs = new List<InternalInput>(configuration.ClientInputBufferSize);
        }

        
        /// <summary>
        /// Called on client side of the connection for each network tick.
        /// This method returns input that is applied to the controller and sent to the server.
        /// </summary>
        protected abstract TInput Cl_NetworkTick(int tick);
        
        /// <summary>
        /// Called on server side of the connection for each network tick.
        /// </summary>
        protected abstract void Sv_NetworkTick(int tick);
        
        /// <summary>
        /// Method that both server and client use to simulate(run) the controller.
        /// </summary>
        /// <param name="input">Input applied to the controller</param>
        /// <param name="tick">Client tick</param>
        /// <returns>Resulting controller state after applying input</returns>
        protected abstract TResult Simulate(TInput input, int tick);
        
        /// <summary>
        /// Reset controller state to the target state.
        /// </summary>
        /// <param name="result">Target state</param>
        protected abstract void ResetState(TResult result);


        internal override void Cl_NetworkTick_Internal(int tick)
        {
            if (_inputs.Count > ControllerConfiguration.ClientInputBufferSize)
                _inputs.RemoveAt(0);
            
            var input = Cl_NetworkTick(tick);

            //simulate
            InternalInput internalInput = default;
            internalInput.Input = input;
            internalInput.Tick = tick;
            internalInput.Result = Simulate(input, tick);
            
            //cache
            _inputs.Add(internalInput);
        }

        internal override void Sv_NetworkTick_Internal(int tick)
        {
            while(_inputs.Count > 0 && _inputs[0].Tick < tick)
                _inputs.RemoveAt(0);

            var inputIndex = FindInputIndex(tick);
            var currentInput = _lastProcessedInput;
            if (inputIndex != -1)
                currentInput = _inputs[inputIndex];
                
             currentInput.Tick = tick;
             currentInput.Result = Simulate(currentInput.Input, tick);
            _lastProcessedInput = currentInput;
            
            Sv_NetworkTick(tick);
        }

        /// <summary>
        /// Removes inputs and sync result state if necessary based on provided result state for given input .
        /// </summary>
        private bool ConfirmInputsAndSyncState(TResult result, int tick)
        {
            var index = FindInputIndex(tick);

            if (index == -1)
            {
                Debug.Log($"Could not find input with tick {tick}");
                return false;
            }
            
            //sync the controller
            int error = result.IsOutOfSync(_inputs[index].Result);
            if (error != 0)
            {
                Debug.LogError($"Controller state out of sync for tick: {tick}, error: {error}");
                ResetState(result);
                for (int i = index + 1; i < _inputs.Count; ++i)
                {
                    var internalInput = _inputs[i];
                    internalInput.Result = Simulate(internalInput.Input, internalInput.Tick);
                    _inputs[i] = internalInput;
                }
            }
            //else Debug.Log($"Validated controller state for the tick: {tick}");

            _inputs.RemoveRange(0, index + 1);
            return true;
        }

        /// <summary>
        /// Returns input index with given input identifier or -1 if not found.
        /// </summary>
        private int FindInputIndex(int tick)
        {
            int index = -1;
            for (int i = 0; i < _inputs.Count; ++i)
            {
                if (_inputs[i].Tick.Equals(tick))
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        
        internal override void WriteInputData(ref StreamWriter stream, ref InputHeader inputHeader)
        {
            inputHeader.Count = 0;
            
            //stream failed elsewhere
            if (stream.Fail)
                return;

            //no input provided
            if (_inputs.Count == 0)
                return;

            //find the index of the last acked input
            int ackedInputIndex = FindInputIndex(_ackedInputTick);
            if (ackedInputIndex == -1) ackedInputIndex = 0;
            else ackedInputIndex++;
            
            //write inputs packed
            InternalInput input, previousInput = default;
            byte sent = 0;
            for (int inputIndex = ackedInputIndex; inputIndex < _inputs.Count; inputIndex++)
            {
                int currentBitPosition = stream.BitPosition;

                //exchange tick for the first input
                if (inputIndex == ackedInputIndex)
                {
                    int firstInputTick = _inputs[inputIndex].Tick;
                    stream.Exchange(ref firstInputTick);
                }

                //write inputs by packing them
                input = _inputs[inputIndex];
                bool hasChanged = input.Input.HasChanged(previousInput.Input);
                stream.BitStream.WriteBit(hasChanged);
                if(hasChanged)
                    input.Input.ExchangeDelta(ref stream, previousInput.Input);
                
                previousInput = input;
                
                //check if input could be written
                if (stream.Fail)
                {
                    //reset bit position and stop writing
                    stream.BitPosition = currentBitPosition;
                    stream.Fail = false;
                    break;
                }

                sent++;

                if (sent >= 255)
                    break;
            }

            //update header
            inputHeader.Count = sent;

            //int numberOfUnAckedInputs = _inputs.Count - ackedInputIndex;
            //Debug.Log($"{sent}/{numberOfUnAckedInputs} - {_inputs.Count}");
        }


        internal override void ReadInputData(ref StreamReader stream, ref InputHeader inputHeader)
        {
            if (inputHeader.Count == 0)
                return;

            int firstInputTick = 0;
            InternalInput temporaryInput = default, previousInput = default;
            int read = 0;

            for (ushort j = 0; j < inputHeader.Count; j++)
            {
                //read tick of the first input
                if (j == 0)
                    stream.Exchange(ref firstInputTick);

                bool hasChanged = stream.BitStream.ReadBit();
                temporaryInput.Input = previousInput.Input;
                if (hasChanged)
                    temporaryInput.Input.ExchangeDelta(ref stream, previousInput.Input);
                previousInput = temporaryInput;

                if (stream.Fail)
                {
                    //todo: client sent invalid number of inputs(invalid packet)
                    //stop reading
                    break;
                }

                int currentInputTick = firstInputTick + j;

                //only store inputs that are older, we allow client to make offset
                if (_maximumRecvInputTick < currentInputTick)
                {
                    var recvInput = new InternalInput()
                    {
                        Input = temporaryInput.Input,
                        Tick = currentInputTick,
                        Result = default,
                    };

                    _inputs.Add(recvInput);
                    _maximumRecvInputTick = currentInputTick;
                }

                read++;
            }

            Debug.Assert(read <= 255);
            Debug.Assert(read == inputHeader.Count);
        }


        internal override void WriteResultData(ref StreamWriter stream, ref ResultHeader resultHeader)
        {
            resultHeader.ResultIncluded = false;
            
            if (stream.Fail)
                return;

            //try to write resulting data
            StreamWriter streamResetPoint = stream;
            stream.ExchangeDelta(ref _lastProcessedInput.Tick, resultHeader.Tick);
            _lastProcessedInput.Result.Exchange(ref stream);
            if (stream.Fail)
            {
                stream = streamResetPoint;
                return;
            }

            resultHeader.ResultIncluded = true;
        }

        internal override void ReadResultData(ref StreamReader stream, ref ResultHeader resultHeader)
        {
            _ackedInputTick = resultHeader.Tick;
            
            if (resultHeader.ResultIncluded)
            {
                stream.ExchangeDelta(ref _lastProcessedInput.Tick, resultHeader.Tick);
                TResult result = default;
                result.Exchange(ref stream);
                ConfirmInputsAndSyncState(result, _lastProcessedInput.Tick);
            }
        }

        internal override ResultHeader GetResultHeader()
        {
            return new ResultHeader()
            {
                Tick = _maximumRecvInputTick,
                ResultIncluded = false
            };
        }

        internal override InputHeader GetInputHeader()
        {
            return new InputHeader()
            {
                Count = (byte)_inputs.Count
            };
        }
    }
}