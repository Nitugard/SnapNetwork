using System;
using System.Collections.Generic;
using Ibc.Game;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Ibc.Survival
{
    [Serializable]
    public class ServerGameConnectionState : GameConnectionState
    {
        public Dictionary<EntityId, EntityBehaviour> Entities;
        [SerializeReference]
        public List<Frame> Frames;
        public int AckedSnapshotTick;

        private Frame _emptyFrame;
        private DeltaFrame _deltaFrame;
        
        public ServerGameConnectionState(Connection connection, Controller controller, EntityManager entityManager) 
            : base(connection, controller, entityManager)
        {
            EntityManager.OnEntityAdded += OnEntityAdded;
            EntityManager.OnEntityRemoved += OnEntityRemoved;
            
            LogInfoEnabled = true;
            LogErrorEnabled = true;

            Frames = new List<Frame>();
            Entities = new Dictionary<EntityId, EntityBehaviour>();
            
            _emptyFrame = EntityManager.NewFrame(0);
            _deltaFrame = EntityManager.NewDeltaFrame();
            
            foreach (EntityBehaviour entityBehaviour in EntityManager.GetAllEntities())
                OnEntityAdded(entityBehaviour);
        }

        private void OnEntityAdded(EntityBehaviour obj)
        {
            if (Controller.IsEntityAssociated(obj.Entity.Id))
            {
                Controller.OnEntityLinkSet(obj);
            }

            if (Entities.TryAdd(obj.Id, obj))
            {
                Log($"Entity added {obj}");
            }
            else
            {
                LogError($"Could not add entity {obj}");
            }
        }
        
        private void OnEntityRemoved(EntityBehaviour obj)
        {
            //check if entity has association with this controller
            if (Controller.IsEntityAssociated(obj.Entity.Id))
            {
                Controller.OnEntityLinkUnset(obj);
            }
            
            if (Entities.Remove(obj.Id))
            {
                Log($"Entity removed {obj}");
            }
            else
            {
                LogError($"Could not remove entity {obj}");
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
            var entities = Controller.GetAssociatedEntities();
            for (var i = entities.Count - 1; i >= 0; i--)
            {
                var associatedEntity = entities[i];
                RemoveEntityControllerAssociation(associatedEntity);
            }
            
            EntityManager.OnEntityAdded -= OnEntityAdded;
            EntityManager.OnEntityRemoved -= OnEntityRemoved;
        }

        public override void OnEnter()
        {
            ResetRemoteTick();
            Frames.Clear();
        }

        public override void OnExit()
        {
            
        }

        protected override void Tick()
        {
            RemoveOldFrames();
            RecvCommandManager.ProcessCommands();

            var entities = EntityManager.GetAllEntities();
            foreach (var entity in entities)
                entity.Sv_NetworkTick(LocalTick);

            Controller.Sv_NetworkTick_Internal(RemoteTick - 2);
        }

        private void RemoveOldFrames()
        {
            while (Frames.Count > HostSettings.FrameCacheCount)
            {
                Frames.RemoveAt(0);
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

        protected Frame CaptureFrame()
        {
            Frame frame = EntityManager.NewFrame(LocalTick);
            foreach ((EntityId id, EntityBehaviour entity) in Entities)
            {
                var entityState = entity.GetState();
                entityState.Tick = LocalTick;
                frame.AddEntity(entityState);
            }

            return frame;
        }

        protected override void SendPackets()
        {
            Frame srcFrame = GetSrcFrame();
            Frame destFrame = CaptureFrame();
            
            _deltaFrame.Clear();
            _deltaFrame.CalculateDelta(srcFrame, destFrame, Connection);
            
            Frames.Add(destFrame);
            
            byte[] data = new byte[4096];
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
            
            // Controller header
            StreamWriter streamControllerHeader = stream;
            Controller.ResultHeader controllerHeader = Controller.GetResultHeader();
            controllerHeader.Exchange(ref stream);

            // Snapshot tick header
            DeltaFrame.TickHeader snapshotTickHeader = new DeltaFrame.TickHeader(srcFrame.Tick);
            snapshotTickHeader.Exchange(ref stream, tickHeader.RemoteTick);

            // Snapshot delta header
            StreamWriter streamSnapshotHeader = stream;
            DeltaFrame.Header snapshotHeader = _deltaFrame.HeaderData; 
            snapshotHeader.Exchange(ref stream);

            //DATA
            // Command data
            SendCommandManager.WriteCommands(ref stream, ref commandHeader);
            commandHeader.Exchange(ref commandHeaderStreamWriter);
            
            // Controller
            Controller.WriteResultData(ref stream, ref controllerHeader);
            controllerHeader.Exchange(ref streamControllerHeader);
            
            // Snapshot data
            SnapshotMessage.WriteDelta(ref stream, Connection, _deltaFrame);
            //overwrite snapshot header data
            snapshotHeader.Exchange(ref streamSnapshotHeader);

            Debug.Assert(!stream.Fail);
            
            Connection.SendUnreliablePackets.Enqueue(new Packet()
            {
                Data = data,
                Length = stream.Length
            });
        }

        private Frame GetSrcFrame()
        {
            int tickFrequency = HostSettings.TickFrequency;
            float tickMs = (1f / tickFrequency) * 1000;
            int ticksWindow = (int) (HostSettings.DeltaFrameWindowMs / tickMs);

            if (!TryGetFrame(AckedSnapshotTick, out Frame srcFrame, out _) || (LocalTick - srcFrame.Tick > ticksWindow))
            {
                srcFrame = _emptyFrame;
            }

            return srcFrame;
        }

        
        protected override void ReadPackets()
        {
            //read the last received packet
            if (Connection.RecvPackets.TryDequeue(out var packet))
            {
                //HEADERS
                StreamReader stream = new StreamReader(packet);

                // Tick header
                TickHeader tickHeader = default;
                tickHeader.Exchange(ref stream);

                // Command ack header
                CommandId ackCommandId = default;
                ackCommandId.Exchange(ref stream);

                // Command header
                CommandManager.Header commandHeader = default;
                commandHeader.Exchange(ref stream);
                
                // Snapshot ack header
                int snapshotId = 0;
                stream.ExchangeDelta(ref snapshotId, tickHeader.RemoteTick);
                TryUpdateAckAndRemoveAckFrames(snapshotId);
                
                // Controller header
                Controller.InputHeader inputHeader = default;
                inputHeader.Exchange(ref stream);
                
                //DATA
                // Command data
                SendCommandManager.TryAckCommands(ackCommandId);
                RecvCommandManager.ReadCommands(ref stream, ref commandHeader);
                
                // Controller data
                Controller.ReadInputData(ref stream, ref inputHeader);

                
                LastRecvGameConnectionPacket = new GameConnectionPacket()
                {
                    IsValid = true,
                    TickHeader = tickHeader,
                    TickSynchronized = false
                };
            }
        }
        
        protected override void SpawnPlayerCommandDelegate(Connection connection, ref StreamReader streamreader, CommandId commandid,
            CommandState commandstate)
        {
            
            Debug.Log("Spawn Player Command Received");
            switch (commandstate)
            {

                case CommandState.Invalid:
                    break;
                case CommandState.Received:
                {
                    
                    //check if player already has entity association
                    if (Controller.GetAssociatedEntities().Count > 0)
                    {
                        LogError($"Spawn Player Failed: Controller has entity association");
                        return;
                    }

                    var player = EntityManager.GetPrefab("Player");

                    if (player == null)
                    {
                        LogError($"Spawn Player Failed: Could not find player entity prefab");
                        return;
                    }

                    var playerObj = EntityManager.Instantiate(player);

                    var playerEntity = playerObj.GetComponent<EntityBehaviour>();
                    playerEntity.ForceInitialize();
                    AssociateEntityWithController(playerEntity.Id);
                    Log($"Spawn Player Command executed");
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
                    Controller.Sv_OnControllerCommandReceived(commandType, commandid, ref streamreader);
                }
                    break;
                case CommandState.Acked:
                {
                    Controller.Sv_OnControllerCommandAcked(commandType, commandid, ref streamreader);
                }
                    break;
                case CommandState.Dropped:
                {
                    Controller.Sv_OnControllerCommandDropped(commandType, commandid, ref streamreader);
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
                        entityBehaviour.Sv_CommandReceivedInternal(connection, header.CommandType, commandid, streamreader, header.LocalTick);
                    }
                        break;
                    case CommandState.Acked:
                    {
                        entityBehaviour.Sv_CommandAckedInternal(connection, header.CommandType, commandid, streamreader, header.LocalTick);
                    }
                        break;
                    case CommandState.Dropped:
                    {
                        entityBehaviour.Sv_CommandDroppedInternal(connection, header.CommandType, commandid, streamreader, header.LocalTick);
                    }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(commandstate), commandstate, null);
                }
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
                    break;
                case CommandState.Acked:
                    break;
                case CommandState.Dropped:
                {
                    LogError("AssociateEntityWithController command dropped");
                    //todo: resend maybe?
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(CommandState), commandstate, null);
            }
        }

        public bool TryUpdateAckAndRemoveAckFrames(int ackSnapshotTick)
        {
            //ack time must be different from zero since client would send zero by default
            if (ackSnapshotTick == 0)
                return false;

            if (!TryGetFrame(ackSnapshotTick, out _, out int index))
            {
                return false;
            }

            //take max due to server authority
            AckedSnapshotTick = Math.Max(AckedSnapshotTick, ackSnapshotTick); 

            //remove old frames that client ack-ed
            while (index > 0)
            {
                Frames.RemoveAt(0);
                --index;
            }

            return true;
        }

        public bool AssociateEntityWithController(EntityId id)
        {
            if (!EntityManager.TryGetEntity(id, out var entityBehaviour))
            {
                LogError($"AssociateEntityWithController failed: Entity {id} does not exists");
                return false;
            }
            
            if (entityBehaviour.HasController)
            {
                LogError($"AssociateEntityWithController failed: Entity {id} has associated controller");
                return false;
            }

            //associate entity with controller on the server side
            Controller.AssociateEntity(entityBehaviour.Entity.Id);
            entityBehaviour.AssociateController(Controller);

            Controller.OnEntityLinkSet(entityBehaviour);

            Log($"Entity associated with controller: {id}");
            var entityMessage = new EntityMessage(entityBehaviour.Entity.Id, (byte) EntityControllerAssociateType.Link);
            return SendCommand((byte) CommandType.AssociateEntityWithControllerCommand, entityMessage);
        }
        
        public bool RemoveEntityControllerAssociation(EntityId id)
        {
            //remove association and entity
            if (EntityManager.TryGetEntity(id, out var entityBehaviour))
            {
                if (entityBehaviour.HasController)
                {
                    Controller.DeAssociateEntity(entityBehaviour.Entity.Id);
                    entityBehaviour.AssociateController(null);

                    Controller.OnEntityLinkUnset(entityBehaviour);

                    Log($"Entity de-associated with controller: {id}");
                    var entityMessage = new EntityMessage(entityBehaviour.Entity.Id,
                        (byte) EntityControllerAssociateType.Unlink);
                    return SendCommand((byte) CommandType.AssociateEntityWithControllerCommand, entityMessage);
                }
                else
                {
                    LogError($"RemoveEntityControllerAssociation failed: Entity {id} has no associated controller");
                    return false;
                }
            }
            else
            {
                LogError($"RemoveEntityControllerAssociation failed: Entity {id} does not exist");
            }

            return false;
        }
        
        
    }
}