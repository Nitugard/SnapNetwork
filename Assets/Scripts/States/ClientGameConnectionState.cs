using System;
using System.Collections.Generic;
using Ibc.Game;
using UnityEngine;

namespace Ibc.Survival
{
    [Serializable]
    public class ClientGameConnectionState : GameConnectionState
    {
        public int LastReceiveSnapshotTick;

        [SerializeReference]
        public List<Frame> Frames;

        private Frame _emptyFrame;
        private int _lerpFrameTick;

        public ClientGameConnectionState(Connection connection, Controller controller, EntityManager entityManager) 
            : base(connection, controller, entityManager)
        {
            _emptyFrame = entityManager.NewFrame(0);
            Frames = new List<Frame>();
            LastReceiveSnapshotTick = -1;
            
            EntityManager.OnEntityAdded += OnEntityAdded;
            EntityManager.OnEntityRemoved += OnEntityRemoved;

        }
        
        /// <summary>
        /// Callback method invoked when entity is removed(destroyed) from the entity manager(world).
        /// This method removes entity behaviour from the controller but the association is not removed. 
        /// </summary>
        /// <param name="entityBehaviour">Target entity</param>
        private void OnEntityRemoved(EntityBehaviour entityBehaviour)
        {
            Debug.Log($"Entity removed: {entityBehaviour.Entity.Id}");

            if (Controller.IsEntityAssociated(entityBehaviour.Entity.Id))
            {
                Controller.OnEntityLinkUnset(entityBehaviour);
                entityBehaviour.AssociateController(null);
            }
        }

        /// <summary>
        /// Callback method invoked when entity is added(created) in the entity manager(world).
        /// Method checks if controller has association with entity and if it does association is added.
        /// </summary>
        /// <param name="entityBehaviour">Target entity</param>
        private void OnEntityAdded(EntityBehaviour entityBehaviour)
        {
            Debug.Log($"Entity added: {entityBehaviour.Entity.Id}");

            if (Controller.IsEntityAssociated(entityBehaviour.Entity.Id))
            {
                Controller.OnEntityLinkSet(entityBehaviour);
                entityBehaviour.AssociateController(Controller);
            }
        }
        
        public override void OnConnect()
        {
        }

        public override void OnDisconnect(bool isTimeout)
        {
        }

        public override void Dispose()
        {
        }

        public override void OnEnter()
        {
            ResetRemoteTick();
            Frames.Clear();

            SendCommandManager.SendCommand(CommandType.SpawnPlayerCommand, new EmptyMessage(), out _);
        }
        

        public override void OnExit()
        {
        }

        protected override void Tick()
        {
            RemoveOldFrames();
            CreateAndPredictEntityStates();
            CreateAndLerpEntityStates();

            var entities = EntityManager.GetAllEntities();
            foreach (var entity in entities)
                entity.Cl_NetworkTick(RemoteTickMax);
            
            RecvCommandManager.ProcessCommands();
            Controller.Cl_NetworkTick_Internal(LocalTick);
        }
        
        private void RemoveOldFrames()
        {
            while (Frames.Count > HostSettings.FrameCacheCount)
            {
                Frames.RemoveAt(0);
            }
        }

        protected override void SendPackets()
        {
            var data = new byte[4096];
            StreamWriter stream = new StreamWriter(data, data.Length);

            //HEADERS
            // Tick header
            TickHeader tickHeader = default;
            tickHeader.RemoteTick = LocalTick;
            tickHeader.Exchange(ref stream);
            
            // Command ack header
            CommandId ackCommandId = RecvCommandManager.GetLastRecvCommandId();
            ackCommandId.Exchange(ref stream);

            // Command header
            StreamWriter commandHeaderStreamWriter = stream;
            CommandManager.Header commandHeader = default;
            commandHeader.Exchange(ref stream);
            
            // Snapshot ack header
            stream.ExchangeDelta(ref LastReceiveSnapshotTick, LocalTick);

            // Controller header
            StreamWriter controllerHeaderStreamWriter = stream;
            Controller.InputHeader inputHeader = Controller.GetInputHeader();
            inputHeader.Exchange(ref stream);

            //DATA
            // Command data
            SendCommandManager.WriteCommands(ref stream, ref commandHeader);
            commandHeader.Exchange(ref commandHeaderStreamWriter);
            
            // Controller data
            Controller.WriteInputData(ref stream, ref inputHeader);
            inputHeader.Exchange(ref controllerHeaderStreamWriter);

            Connection.SendUnreliablePackets.Enqueue(new Packet()
            {
                Data = data,
                Length = stream.Length
            });
        }

        protected override void ReadPackets()
        {
            //read the last received packet
            if (Connection.RecvPackets.TryPeek(out var packet))
            {
                StreamReader stream = new StreamReader(packet);

                //HEADERS
                // Tick header
                TickHeader tickHeader = default;
                tickHeader.Exchange(ref stream);

                // Command ack header
                CommandId ackCommandId = default;
                ackCommandId.Exchange(ref stream);

                // Command header
                CommandManager.Header commandHeader = default;
                commandHeader.Exchange(ref stream);
                
                // Controller header
                Controller.ResultHeader controllerHeader = Controller.GetResultHeader();
                controllerHeader.Exchange(ref stream);

                // Snapshot tick header
                DeltaFrame.TickHeader snapshotTickHeader = default;
                snapshotTickHeader.Exchange(ref stream, tickHeader.RemoteTick);
                
                // Snapshot delta header
                DeltaFrame.Header snapshotHeader = default;
                snapshotHeader.Exchange(ref stream);
                
                //DATA
                // Read command data
                SendCommandManager.TryAckCommands(ackCommandId);
                RecvCommandManager.ReadCommands(ref stream, ref commandHeader);
                
                // Controller
                Controller.ReadResultData(ref stream, ref controllerHeader);

                // Read snapshot data
                if (TryReadSnapshot(tickHeader, snapshotHeader, snapshotTickHeader, ref stream, out Frame frame))
                {
                    Frames.Add(frame);
                    LastReceiveSnapshotTick = frame.Tick;
                }

                LastRecvGameConnectionPacket = new GameConnectionPacket()
                {
                    IsValid = true,
                    TickHeader = tickHeader,
                    TickSynchronized = false
                };
            }
        }

        protected override void AssociateEntityWithControllerCommandDelegate(Connection connection, ref StreamReader streamreader,
            CommandId commandid, CommandState commandstate)
        {

            EntityMessage entityMessage = default;
            entityMessage.Exchange(ref streamreader);
            
            switch (commandstate)
            {

                case CommandState.Invalid:
                    break;
                case CommandState.Received:
                {
                    switch ((EntityControllerAssociateType)entityMessage.Data)
                    {
                        case EntityControllerAssociateType.Unlink:
                        {
                            //remove association
                            if (Controller.IsEntityAssociated(entityMessage.EntityId))
                            {
                                if (EntityManager.TryGetEntity(entityMessage.EntityId, out var entityBehaviour))
                                {
                                    entityBehaviour.AssociateController(null);
                                    Controller.DeAssociateEntity(entityMessage.EntityId);
                                    
                                    Controller.OnEntityLinkUnset(entityBehaviour);
                                }
                            }

                            break;
                        }
                        case EntityControllerAssociateType.Link:
                        {
                            //create association
                            if (Controller.AssociateEntity(entityMessage.EntityId))
                            {
                                if (EntityManager.TryGetEntity(entityMessage.EntityId, out var entityBehaviour))
                                {
                                    entityBehaviour.AssociateController(Controller);
                                    Controller.OnEntityLinkSet(entityBehaviour);
                                }
                            }

                            break;
                        }
                    }
                }
                    break;
                case CommandState.Acked:
                    break;
                case CommandState.Dropped:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(commandstate), commandstate, null);
            }
        }

        protected override void SpawnPlayerCommandDelegate(Connection connection, ref StreamReader streamreader, CommandId commandid,
            CommandState commandstate)
        {
            
            switch (commandstate)
            {

                case CommandState.Invalid:
                    break;
                case CommandState.Received:
                    break;
                case CommandState.Acked:
                {
                    Debug.Log("Server acked spawn player command");
                }
                    break;
                case CommandState.Dropped:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(commandstate), commandstate, null);
            }
        }

        protected override void ControllerCommandDelegate(Connection connection, ref StreamReader streamreader, CommandId commandid,
            CommandState commandstate)
        {
            var commandType = (ControllerCommandType)streamreader.BitStream.ReadULong(8);
            switch (commandstate)
            {
                case CommandState.Invalid:
                    break;
                case CommandState.Received:
                {
                    Controller.Cl_OnControllerCommandReceived(commandType, commandid, ref streamreader);
                }
                    break;
                case CommandState.Acked:
                {
                    Controller.Cl_OnControllerCommandAcked(commandType, commandid, ref streamreader);
                }
                    break;
                case CommandState.Dropped:
                {
                    Controller.Cl_OnControllerCommandDropped(commandType, commandid, ref streamreader);
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(commandstate), commandstate, null);
            }
        }
        
        protected override void EntityCommandDelegate(Connection connection, ref StreamReader streamreader, CommandId commandid,
            CommandState commandstate)
        {
            EntityCommandHeader header = default;
            header.Exchange(ref streamreader);

            if (EntityManager.TryGetEntity(header.EntityId, out var entityBehaviour))
            {
                switch (commandstate)
                {
                    case CommandState.Invalid:
                        break;
                    case CommandState.Received:
                    {
                        entityBehaviour.Cl_CommandReceivedInternal(header.CommandType, commandid, streamreader, header.LocalTick);
                    }
                        break;
                    case CommandState.Acked:
                    {
                        entityBehaviour.Cl_CommandAckedInternal(header.CommandType, commandid, streamreader, header.LocalTick);
                    }
                        break;
                    case CommandState.Dropped:
                    {
                        entityBehaviour.Cl_CommandDroppedInternal(header.CommandType, commandid, streamreader, header.LocalTick);
                    }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(commandstate), commandstate, null);
                }
            }
        }

        public bool TryGetFrame(int tick, out Frame frame, out int index)
        {
            frame = null;
            index = Frames.FindIndex(t=>t.Tick == tick);
            if (index == -1) return false;
            frame = Frames[index];
            return true;
        }
        
        public bool TryGetFrameOrEmpty(int tick, out Frame frame)
        {
            if (tick == 0)
            {
                _emptyFrame.Clear();
                frame = _emptyFrame;
                return true;
            }
            
            return TryGetFrame(tick, out frame, out _);
        }
        
        private bool TryReadSnapshot(TickHeader tickHeader, DeltaFrame.Header header, DeltaFrame.TickHeader snapshotTickHeader, ref StreamReader stream, out Frame frame)
        {
            if (!TryGetFrameOrEmpty(snapshotTickHeader.SourceTick, out Frame srcFrame))
            {
                Debug.Log($"Could not find src frame for tick: {snapshotTickHeader.SourceTick}");
                frame = null;
                return false;
            }
            
            frame = EntityManager.NewFrame(0);
            frame.CopyFrom(srcFrame);
            frame.Tick = tickHeader.RemoteTick;
            SnapshotMessage.ReadSnapshot(ref stream, Connection, header, srcFrame, frame);
            return true;
        }
        
        
        private void CreateAndPredictEntityStates()
        {
            if (Frames.Count == 0)
                return;
            
            Frame frame = Frames[^1];

            for (int i = 0; i < frame.Count; ++i)
            {
                var entity = frame[i];
                if (!EntityManager.TryGetEntity(entity.Id, out var entityBehaviour))
                {
                    if(EntityManager.GetPrefab(entity.PrefabId).SyncType == EntitySyncType.Predicted)
                        entityBehaviour = EntityManager.SpawnEntityInternal(entity, frame.States[i], Vector3.zero, Quaternion.identity);
                }

                if (entityBehaviour != null && entityBehaviour.SyncType == EntitySyncType.Predicted)
                    entityBehaviour.SetState(frame.States[i]);
            }

            var entitiesList = EntityManager.GetAllEntities();
            for (var i = entitiesList.Count - 1; i >= 0; i--)
            {
                if (entitiesList[i].SyncType == EntitySyncType.Predicted && !frame.TryGetEntity(entitiesList[i].Id, out var entityStateBase))
                {
                    EntityManager.DestroyEntityInternal(entitiesList[i].Id);
                }
            }
        }
        
        public bool TryGetFirstMatchingFrame(int requiredTick, out int i)
        {
            for (i = Frames.Count - 1; i >= 0; --i)
            {
                if (Frames[i].Tick < requiredTick)
                    return true;
            }

            i = -1;
            return false;
        }
        
        private void CreateAndLerpEntityStates()
        {
            if (TryGetFirstMatchingFrame(RemoteTickMax, out var index))
            {
                if (index != Frames.Count - 1)
                {
                    var srcFrame = Frames[index];
                    var destFrame = Frames[index + 1];

                    if (_lerpFrameTick != srcFrame.Tick)
                    {
                        SetEntities(srcFrame);
                        _lerpFrameTick = srcFrame.Tick;
                    }

                    var t = (RemoteTickMax - srcFrame.Tick) / (float)(destFrame.Tick - srcFrame.Tick);
                    LerpEntityState(srcFrame, destFrame, RemoteTick, t);
                }
                else
                {
                    LogError("Snapshot lerp stall");
                }
            }
            else
            {
                LogError("Snapshot lerp no matching frame found");
            }
        }



        private void SetEntities(Frame frame)
        {
            for (int i = 0; i < frame.Count; ++i)
            {
                var entity = frame[i];
                if (!EntityManager.TryGetEntity(entity.Id, out var entityBehaviour))
                {
                    if (EntityManager.GetPrefab(entity.PrefabId).SyncType == EntitySyncType.Interpolated)
                    {
                        entityBehaviour = EntityManager.SpawnEntityInternal(entity, frame.States[i], Vector3.zero,
                            Quaternion.identity);
                    }
                }

                if (entityBehaviour != null && entityBehaviour.SyncType == EntitySyncType.Interpolated)
                    entityBehaviour.SetState(frame.States[i]);
            }

            var entitiesList = EntityManager.GetAllEntities();
            for (var i = entitiesList.Count - 1; i >= 0; i--)
            {
                if (entitiesList[i].SyncType == EntitySyncType.Interpolated && !frame.TryGetEntity(entitiesList[i].Id, out var entityStateBase))
                {
                    EntityManager.DestroyEntityInternal(entitiesList[i].Id);
                }
            }
        }

        private void LerpEntityState(Frame srcFrame, Frame destFrame, int tick, float t)
        {
            for (int i = 0; i < srcFrame.Count; ++i)
            {
                var fromFrameEntityState = srcFrame.States[i];

                if(EntityManager.TryGetEntity(fromFrameEntityState.Id, out var entityBehaviour))
                {
                    if (entityBehaviour.SyncType == EntitySyncType.Interpolated)
                    {
                        if (destFrame.TryGetEntity(fromFrameEntityState.Entity.Id, out var destState))
                        {
                            entityBehaviour.GetState().InterpolateInternal(fromFrameEntityState, destState, tick, t);
                        }
                    }
                }
            }
        }
    }
}