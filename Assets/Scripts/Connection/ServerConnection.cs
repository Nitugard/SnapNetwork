
using ENet;

namespace Ibc.Survival
{

    public class ServerConnection : Connection
    {
        public ServerConnection(ConnectionManager connectionManager, Peer peer) : base(connectionManager, peer)
        {
        }
    }
}