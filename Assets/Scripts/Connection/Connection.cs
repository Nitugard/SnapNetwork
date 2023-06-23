using System;
using System.Collections.Generic;
using ENet;
using UnityEngine;

namespace Ibc.Survival
{
    [Serializable]
    public struct Packet
    {
        public byte[] Data;
        public int Length;
    }
    
    
    
    [Serializable]
    public class IntegerSampler
    {
        public Action OnSampleTaken;
        public int SampleFrequency;
        public long TrackedValue;
        public long CachedValue;
        public long PreviousCachedValue;
        private float _time = 0;

        public long Accumulated => TrackedValue - CachedValue;
        public long Difference => CachedValue - PreviousCachedValue;

        public IntegerSampler(int sampleFrequency, long trackedValue = 0)
        {
            SampleFrequency = sampleFrequency;
            TrackedValue = trackedValue;
        }
        
        public void Update(float time, long trackedValue)
        {
            TrackedValue = trackedValue;
            
            if (time > _time + 1.0f / SampleFrequency)
            {
                PreviousCachedValue = CachedValue;
                CachedValue = TrackedValue;
                _time = time;
                OnSampleTaken?.Invoke();
            }
        }
    }


    
    [Serializable]
    public abstract class Connection : IDisposable
    {
        public bool IsServer => this is ServerConnection;
        public bool IsClient => this is ClientConnection;

        public readonly Peer Peer;
        
        public Queue<Packet> RecvPackets;
        public Queue<Packet> SendReliablePackets;
        public Queue<Packet> SendUnreliablePackets;

        public IntegerSampler BytesSent, BytesRecv, PacketsSent, PacketsRecv;
        
        [SerializeReference]
        private ConnectionState _currentState;

        private ConnectionState[] _states;
        private ConnectionManager _connManager;
        
        public Action StateChangeEvent;

        public Connection(ConnectionManager connectionManager, Peer peer)
        {
            RecvPackets = new Queue<Packet>();
            SendReliablePackets = new Queue<Packet>();
            SendUnreliablePackets = new Queue<Packet>();
            _connManager = connectionManager;

            BytesSent = new IntegerSampler(1, 0);
            BytesRecv = new IntegerSampler(1, 0);
            PacketsSent = new IntegerSampler(1, 0);
            PacketsRecv = new IntegerSampler(1, 0);
            
            Peer = peer;
        }

        public void SetStates(ConnectionState[] states)
        {
            _states = states;
        }
        
        public void ChangeState<T>() where T : ConnectionState
        {
            if(_states == null)
                throw new Exception($"Change State Failed: States are not initialized");

            for (var i = 0; i < _states.Length; i++)
            {
                if (_states[i].GetType() == typeof(T))
                {
                    _currentState?.OnExit();
                    _currentState = _states[i];
                    _currentState?.OnEnter();
                    
                    try
                    {
                        StateChangeEvent?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    return;
                }
            }

            throw new Exception($"Change State Failed: Could not find state {typeof(T)}");
        }


        public void NetworkTick()
        {
            //process received packets
            BytesSent.Update(Time.time, (long) Peer.BytesSent);
            BytesRecv.Update(Time.time, (long) Peer.BytesReceived);
            PacketsSent.Update(Time.time, (long) Peer.PacketsSent);

            _currentState?.NetworkTick();
        }


        public virtual void OnConnect()
        {
            _currentState?.OnConnect();
        }

        public virtual void OnDisconnect(bool isTimeout)
        {
            _currentState?.OnDisconnect(isTimeout);

        }


        public virtual void Dispose()
        {
            _currentState?.OnExit();
            _currentState?.Dispose();
        }

        public void SendReliable<T>(T message) where T : IMessage
        {
            Send(message, SendReliablePackets, 1024);
        }
        
        public void SendUnreliable<T>(T message) where T : IMessage
        {
            Send(message, SendUnreliablePackets, 4096);
        }

        private void Send<T>(T message, Queue<Packet> sendQueue, int size) where T : IMessage
        {
            byte[] data = new byte[size];
            StreamWriter stream = new StreamWriter(data, data.Length);
            message.Exchange(ref stream);
            Debug.Assert(!stream.Fail);
            sendQueue.Enqueue(new Packet()
            {
                Data = data,
                Length = stream.Length
            });
        }

        public ConnectionState GetState()
        {
            return _currentState;
        }

        public ConnectionManager GetConnectionManager()
        {
            return _connManager;
        }
    }

}