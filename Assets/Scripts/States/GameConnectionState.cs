
using System;
using UnityEngine;

namespace Ibc.Survival
{
    
    /// <summary>
    /// Common header that both server and client messages share.
    /// Contains data about the ticks used to sync simulations. 
    /// </summary>
    [Serializable]
    public struct TickHeader : IMessage
    {
        
        /// <summary>
        /// This is the local client tick when message was sent.
        /// </summary>
        public int RemoteTick;


        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            stream.Exchange(ref RemoteTick);
        }
    }

    [Serializable]
    public struct GameConnectionPacket
    {
        public bool IsValid;
        public TickHeader TickHeader;
        public bool TickSynchronized;
    }

    [Serializable]
    public abstract class GameConnectionState : ConnectionState
    {
        public int LocalTick
        {
            get => _ticks;
        }
        /// <summary>
        /// Estimated remote tick of the remote end of this connection.
        /// </summary>
        public int RemoteTick
        {
            get => _remoteTick;
            protected set => _remoteTick = value;
        }
        
        /// <summary>
        /// Maximum tick that connection stepped up to.
        /// </summary>
        public int RemoteTickMax
        {
            get => _remoteTickMax;
            protected set => _remoteTickMax = value;
        }
        
        
        protected HostSettings HostSettings;
        
        /// <summary>
        /// Current tick count.
        /// </summary>
        private int _ticks;
        private int _remoteTick;
        private int _remoteTickMax;

        /// <summary>
        /// Accumulator used to run simulation at network tick rate.
        /// </summary>
        private float _accumulator;
        
        public CommandManager RecvCommandManager;
        public CommandManager SendCommandManager;
        public CommandRegister CommandRegister;
        
        /// <summary>
        /// Controller associated with the connection.
        /// </summary>
        [SerializeReference]
        public Controller Controller;

        protected EntityManager EntityManager;
        
        protected GameConnectionPacket LastRecvGameConnectionPacket;

        public GameConnectionState(Connection connection, Controller controller, EntityManager entityManager) : base(connection)
        {
            HostSettings = Resources.Load<HostSettings>("HostSettings");
            if (HostSettings == null) throw new Exception($"Could not load {nameof(HostSettings)}");

            CommandRegister = new CommandRegister();
            RecvCommandManager = new CommandManager(connection, CommandRegister, HostSettings.CommandSettings);
            SendCommandManager = new CommandManager(connection, CommandRegister, HostSettings.CommandSettings);

            EntityManager = entityManager;
            Controller = controller;
            
            CommandRegister.RegisterCallback(CommandType.AssociateEntityWithControllerCommand, AssociateEntityWithControllerCommandDelegate);
            CommandRegister.RegisterCallback(CommandType.SpawnPlayerCommand, SpawnPlayerCommandDelegate);
            CommandRegister.RegisterCallback(CommandType.ControllerCommand, ControllerCommandDelegate);
            CommandRegister.RegisterCallback(CommandType.EntityCommand, EntityCommandDelegate);

        }


        public bool SendCommand<T>(CommandType cmdType, T command, out CommandId commandId) where T : struct, IMessage
        {
            return SendCommandManager.SendCommand(cmdType, command, out commandId);
        }

        
        public bool SendCommand<T>(CommandType cmdType, T command) where T : struct, IMessage
        {
            return SendCommandManager.SendCommand(cmdType, command, out _);
        }
        
        public override void NetworkTick()
        {
            
            _accumulator += Time.deltaTime;
            var tickTimeSeconds = 1f / HostSettings.TickFrequency;
            
            ReadPackets();
            TryInitializeRemoteTick();

            while (_accumulator > tickTimeSeconds)
            {
                //increment ticks
                _ticks++;

                //process packets each tick
                SyncRemoteTick();
                Tick();
                
                //send packets every so often
                if ((_ticks % HostSettings.SendRate) == 0)
                {
                    SendPackets();
                }

                _accumulator -= tickTimeSeconds;
            }
        }

        protected abstract void Tick();
        
        protected abstract void SendPackets();

        protected abstract void ReadPackets();

        protected abstract void AssociateEntityWithControllerCommandDelegate(Connection connection, ref StreamReader streamreader, CommandId commandid,
            CommandState commandstate);

        protected abstract void SpawnPlayerCommandDelegate(Connection connection, ref StreamReader streamreader,
            CommandId commandid,
            CommandState commandstate);
        
        protected abstract void ControllerCommandDelegate(Connection connection, ref StreamReader streamreader,
            CommandId commandid, CommandState commandstate);
        
        protected abstract void EntityCommandDelegate(Connection connection, ref StreamReader streamreader,
            CommandId commandid, CommandState commandstate);

        private void TryInitializeRemoteTick()
        {
            //remote tick estimate is not yet initialized
            if (_remoteTick == -1 && LastRecvGameConnectionPacket.IsValid)
            {
                //remote tick estimate should stay two packets behind the remote on average
                //which provides a nice de jitter buffer
                RemoteTick = LastRecvGameConnectionPacket.TickHeader.RemoteTick - HostSettings.SendRate * 2;
            }
        }

        protected void ResetRemoteTick()
        {
            RemoteTick = -1;
            RemoteTickMax = -1;
        }
        
        private void SyncRemoteTick()
        {
            //adjust the tick so we are as close to the last packet we received as possible
            //we are trying to stay as close to remote_send_rate * 2 ticks behind the last received packet
            //there is a sweet spot where our diff compared to the last received packet is: > remote_send_rate and < (remote_send_rate * 3) 
            //where we dont do any adjustments to our remoteTick value
            
            //Move at the same rate forward as the remote end
            RemoteTick++;
            RemoteTickMax = Math.Max(RemoteTickMax, RemoteTick);

            if (LastRecvGameConnectionPacket.TickSynchronized || !LastRecvGameConnectionPacket.IsValid)
                return;
            
            //difference between our expected remote tick and the remoteTick of the last packet that arrived
            var tickHeader = LastRecvGameConnectionPacket.TickHeader;
            int diff = tickHeader.RemoteTick - RemoteTick;
            int remoteSendRate = HostSettings.SendRate;
                    
            if (diff >= remoteSendRate * 3) {

                // step back our local simulation forward, at most two packets worth of ticks 
                RemoteTick += Math.Min(diff - remoteSendRate * 2, remoteSendRate * 4);

                // if we have drifted closer to getting ahead of the
                // remote simulation ticks, we should stall one tick
            } else if (diff >= 0 && diff < remoteSendRate) {

                // stall a single tick
                RemoteTick -= 1;

                // if we are ahead of the remote simulation, 
                // but not more then two packets worth of ticks
            } else if (diff < 0 && Math.Abs(diff) <= remoteSendRate * 2) {

                // step back one packets worth of ticks
                RemoteTick -= remoteSendRate;

                // if we are way out of sync (more then two packets ahead)
                // just re-initialize the connections remoteTick 
            } else if (diff < 0 && Math.Abs(diff) > remoteSendRate * 2) {

                // perform same initialization as we did on first packet
                RemoteTick = tickHeader.RemoteTick - (remoteSendRate * 2);

            }
                    
            RemoteTickMax = Math.Max(RemoteTickMax, RemoteTick);
            LastRecvGameConnectionPacket.TickSynchronized = true;
        }
        
    }
}