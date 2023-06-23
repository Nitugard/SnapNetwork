using UnityEngine;

namespace Ibc.Survival
{
    [CreateAssetMenu(fileName = "HostSettings", menuName = "IbcSurvival/Host Settings")]
    public class HostSettings : ScriptableObject
    {
        [Header("Host Settings")] public string Address = "127.0.0.1";
        public ushort Port = 9992;
        public int MaxConnections = 1024;

        [Header("Timeout Settings")] public uint TimeoutLimit = 10;
        public uint TimeoutMinimum = 1000;
        public uint TimeoutMaximum = 30000;

        [Header("Peer Settings")] public uint PingInterval = 500;

        [Header("Network Settings")] [Tooltip("Frequency of the network simulation loop")]
        public int TickFrequency = 60;

        [Tooltip(
            "How often to send packets inside network simulation loop, 1 = every tick, 2 = every second tick, ...")]
        public int SendRate = 1;

        [Header("Snapshot Settings")]
        public CommandConfiguration CommandSettings =
            new CommandConfiguration()
            {
                MaximumCommandSize = 1024,
                MaximumCommandsInQueue = 64,
                MaximumCommandsInPacket = 64
            };

        public ControllerConfiguration ControllerConfiguration = new ControllerConfiguration()
        {
            //TODO:
        };

        public int DeltaFrameWindowMs = 1000;
        public int FrameCacheCount = 20;

    }
}