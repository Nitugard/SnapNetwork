using System;
using System.Collections.Generic;
using ENet;
using Ibc.Game;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Ibc.Survival
{
    
    public enum DisconnectReason : uint
    {
        Unknown,
        StopHost,
    }

    public class ConnectionManager : MonoBehaviour
    {
        private const int ChannelLimit = 3;
        public string HostName => IsServer ? "Server" : "Client";
        public bool IsServer { get; private set; }
        
        [SerializeField] private HostSettings _hostSettings;
        
        /// <summary>
        /// List of established connections for both server and client.
        /// </summary>
        [SerializeReference]
        private List<Connection> _connections;
        
        public IntegerSampler BytesSent, BytesRecv, PacketsSent, PacketsRecv;

        private Dictionary<Peer, Connection> _peerConnectionMap;
        private Host _host;

        [ContextMenu("Start Client")]
        public void StartClient()
        {
            StartHost(false);
        }

        [ContextMenu("Start Server")]
        public void StartServer()
        {
            StartHost(true);
        }

        public void StartHost(bool isServer)
        {
            Application.targetFrameRate = _hostSettings.TickFrequency;
            
            if (_host != null && _host.IsSet)
            {
                throw new Exception("Host is already running");
            }
            
            BytesSent = new IntegerSampler(1, 0);
            BytesRecv = new IntegerSampler(1, 0);
            PacketsSent = new IntegerSampler(1, 0);
            PacketsRecv = new IntegerSampler(1, 0);

            IsServer = isServer;
            
            gameObject.name = $"{HostName}HostManager";
            DontDestroyOnLoad(gameObject);
            
            if (!Library.Initialize())
            {
                throw new Exception("Failed to initialize ENet lib");
            }

            Address address = new Address();
            if(!IsServer)
                address.SetHost(_hostSettings.Address);
            address.Port = _hostSettings.Port;

            _connections = new List<Connection>(_hostSettings.MaxConnections);
            _peerConnectionMap = new Dictionary<Peer, Connection>(_hostSettings.MaxConnections);
            
            try
            {
                _host = new Host();
                if (IsServer)
                {
                    _host.Create(address, _hostSettings.MaxConnections, ChannelLimit);
                }
                else
                {
                    _host.Create();
                    var peer = _host.Connect(address, ChannelLimit, 0);
                    var connection = new ClientConnection(this, peer);
                    connection.SetStates(GetConnectionStatesForPeer(connection));
                    connection.ChangeState<HandshakeConnectionState>();

                    _connections.Add(connection);
                    _peerConnectionMap.Add(peer, connection);

                    peer.Timeout(_hostSettings.TimeoutLimit, _hostSettings.TimeoutMinimum, _hostSettings.TimeoutMaximum);
                }
                
                Debug.Log("Host started");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [ContextMenu("Stop Host")]
        public void StopHost()
        {
            if (_host != null && _host.IsSet)
            {
                foreach (var connection in _connections)
                {
                    connection.Peer.DisconnectNow((uint) DisconnectReason.StopHost);
                    connection.Dispose();
                }
                
                _connections.Clear();
                _peerConnectionMap.Clear();
                
                _host.Flush();
                _host.Dispose();
                _host = null;
                Debug.Log("Host stopped");
            }
            else
            {
                Debug.LogError("Stop host failed, host not started");
            }
        }

        public ConnectionState[] GetConnectionStatesForPeer(Connection connection)
        {
            var entityManager = GetComponent<EntityManager>();
            //var playerController = new PlayerController(connection, entityManager, _hostSettings.ControllerConfiguration);
            //todo: actual controller
            if (IsServer)
            {
                return new ConnectionState[]
                {
                    new HandshakeConnectionState(connection),
                    new ServerGameConnectionState(connection, null, entityManager),
                };
            }
            else
            {
                return new ConnectionState[]
                {
                    new HandshakeConnectionState(connection),
                    new ClientGameConnectionState(connection, null, entityManager),
                };
            }
        }

        private void ReceivePackets()
        {
            Event ev;
            //if (_host.CheckEvents(out ev) <= 0)
            {
                while (_host.Service(0, out ev) > 0)
                {
                    switch (ev.Type)
                    {
                        case EventType.Connect:
                        {
                            Debug.Log($"Peer connected: {PrintPeer(ev.Peer)} Data: {ev.Data}");

                            Connection connection;
                            if (IsServer)
                            {
                                connection = new ServerConnection(this, ev.Peer);
                                connection.SetStates(GetConnectionStatesForPeer(connection));
                                connection.ChangeState<HandshakeConnectionState>();
                                _peerConnectionMap.Add(ev.Peer, connection);
                                _connections.Add(connection);
                            }
                            else
                            {
                                connection = _connections[0];
                            }

                            connection.OnConnect();
                            ev.Peer.PingInterval(_hostSettings.PingInterval);
                        }
                            break;
                        case EventType.Disconnect:
                        {
                            Debug.Log($"Peer disconnected: {PrintPeer(ev.Peer)} Data: {ev.Data}");
                            if (_peerConnectionMap.Remove(ev.Peer, out var connection))
                            {
                                connection.OnDisconnect(false);
                                _connections.Remove(connection);
                            }
                        }
                            break;
                        case EventType.Receive:
                        {
                            if (_peerConnectionMap.TryGetValue(ev.Peer, out var connection))
                            {
                                byte[] data = new byte[ev.Packet.Length];
                                ev.Packet.CopyTo(data);
                                connection.RecvPackets.Enqueue(new Packet()
                                {
                                    Data = data,
                                    Length = ev.Packet.Length
                                });
                            }
                            else
                            {
                                Debug.LogError("Could not find connection for the peer");
                            }
                        }
                            break;
                        case EventType.Timeout:
                        {
                            Debug.Log($"Peer timeout: {PrintPeer(ev.Peer)} Data: {ev.Data}");
                            if (_peerConnectionMap.Remove(ev.Peer, out var connection))
                            {
                                connection.OnDisconnect(true);
                                _connections.Remove(connection);
                            }
                        }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
        
        private void SendPacket(Peer peer, Packet packet, byte channel, PacketFlags packetFlags)
        {
            try
            {
                var enetPacket = new ENet.Packet();
                enetPacket.Create(packet.Data, packet.Length, packetFlags);
                peer.Send(channel, ref enetPacket);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send packet: {packet.Length}");
                Debug.LogException(ex); 
            }
        }

        private void Update()
        {
            if (_host == null || !_host.IsSet)
                return;
            
            UpdateStats();
            ReceivePackets();
            NetworkTick();
            SendPackets();
            FlushPackets();
        }

        private void UpdateStats()
        {
            BytesSent.Update(Time.time, _host.BytesSent);
            BytesRecv.Update(Time.time, _host.BytesReceived);
            PacketsSent.Update(Time.time, _host.PacketsSent);
            PacketsRecv.Update(Time.time, _host.PacketsReceived);
        }

        private void FlushPackets()
        {
            foreach (var connectionBase in _connections)
            {
                connectionBase.RecvPackets.Clear();
                connectionBase.SendReliablePackets.Clear();
                connectionBase.SendUnreliablePackets.Clear();
            }
        }

        private void SendPackets()
        {
            foreach (var connectionBase in _connections)
            {
                SendPackets(connectionBase.Peer, connectionBase.SendReliablePackets, 0,
                    PacketFlags.Reliable | PacketFlags.Instant);
                SendPackets(connectionBase.Peer, connectionBase.SendUnreliablePackets, 1,
                    PacketFlags.UnreliableFragmented | PacketFlags.Instant);
            }
        }

        private void NetworkTick()
        {
            foreach (var connection in _connections)
            {
                connection.NetworkTick();
            }
        }

        private void SendPackets(Peer peer, Queue<Packet> queue, byte channel, PacketFlags flags)
        {
            while(queue.TryDequeue(out var packet))
            {
                SendPacket(peer, packet, channel, flags);
            }
        }


        private static string PrintPeer(Peer peer)
        {
            return $"Id: {peer.ID} IP:, {peer.IP}:{peer.Port} Rtt: {peer.RoundTripTime}";
        }

        public Connection GetConnection(int index)
        {
            return _connections[index];
        }

        public int GetConnectionCount()
        {
            return _connections.Count;
        }

        public HostSettings GetHostSettings()
        {
            return _hostSettings;
        }
    }

}