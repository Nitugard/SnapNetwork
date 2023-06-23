using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ibc.Survival
{

    public struct HandshakeMessage : IMessage
    {
        public string Data;

        public void Exchange<TStream>(ref TStream stream) where TStream : IStream
        {
            stream.Exchange(ref Data);
        }
    }

    [Serializable]
    public class HandshakeConnectionState : ConnectionState
    {
        public HandshakeConnectionState(Connection connection) : base(connection)
        {
        }

        public override void OnConnect()
        {
            if (Connection.IsClient)
            {
                HandshakeMessage handshakeMessage = new HandshakeMessage();
                handshakeMessage.Data = $"Request:ServerInfo";
                Connection.SendReliable(handshakeMessage);
                Debug.Log("Client sent request");
            }
        }

        public override void OnDisconnect(bool isTimeout)
        {
            
        }

        public void Sv_ProcessHandshakeData(Dictionary<string, string> tokens)
        {
            if (tokens.TryGetValue("Request", out var requestType))
            {
                
                if (requestType == "ServerInfo")
                {
                    HandshakeMessage handshakeMessage;
                    handshakeMessage.Data = $"Response:ServerInfo\n";
                    handshakeMessage.Data += $"Name:Server Name\n";
                    Connection.SendReliable(handshakeMessage);
                }
                else if (requestType == "JoinGame")
                {
                    Connection.ChangeState<ServerGameConnectionState>();
                }
            }
        }

        public void Cl_ProcessHandshakeData(Dictionary<string, string> tokens)
        {
            if (tokens.TryGetValue("Response", out var responseType))
            {
                if (responseType == "ServerInfo")
                {
                    foreach (var (key, val) in tokens)
                        Debug.Log($"{key}:{val}");
                    
                    HandshakeMessage handshakeMessage;
                    handshakeMessage.Data = $"Request:JoinGame\n";
                    handshakeMessage.Data += $"Name:Anonymous\n";
                    
                    Connection.SendReliable(handshakeMessage);
                    Connection.ChangeState<ClientGameConnectionState>();
                }
            }
        }

        public override void NetworkTick()
        {
            while(Connection.RecvPackets.TryDequeue(out var packet))
            {
                if (packet.Unpack(out HandshakeMessage handshakeMessage))
                {
                    var dict = new Dictionary<string, string>();
                    var lines = handshakeMessage.Data.Split("\n");
                    foreach (var line in lines)
                    {
                        var tokens = line.Split(":");
                        if (tokens.Length == 2)
                            dict.Add(tokens[0], tokens[1]);
                    }

                    if (Connection.IsServer)
                    {
                        Sv_ProcessHandshakeData(dict);
                    }
                    else
                    {
                        Cl_ProcessHandshakeData(dict);
                    }
                }
                else
                {
                    Debug.Log($"Could not unpack {nameof(HandshakeMessage)} message");
                }
            }
        }

        public override void Dispose()
        {
            
        }

        public override void OnEnter()
        {
        }

        public override void OnExit()
        {
        }
    }

}