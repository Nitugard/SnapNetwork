using System;
using UnityEngine;

namespace Ibc.Survival
{

    
    [Serializable]
    public abstract class ConnectionState : IDisposable
    {
        [HideInInspector]
        protected Connection Connection;

        protected bool LogInfoEnabled, LogErrorEnabled;
        protected string HostName => Connection.IsServer ? "Server" : "Client";
        
        public ConnectionState(Connection connection)
        {
            Connection = connection;
        }

        public abstract void OnConnect();
        public abstract void OnDisconnect(bool isTimeout);
        public abstract void NetworkTick();
        public abstract void Dispose();

        public abstract void OnEnter();
        public abstract void OnExit();

        protected void Log(string msg)
        {
            if(LogInfoEnabled)
                Debug.Log($"[{HostName}] [{Connection}]: {msg}");
        }

        protected void LogError(string msg)
        {
            if (LogErrorEnabled)
                Debug.LogError($"[{HostName}] [{Connection}]: {msg}");
        }
    }
}