using ENet;

namespace Ibc.Survival
{


    public class ClientConnection : Connection
    {
        public ClientConnection(ConnectionManager connectionManager, Peer peer) : base(connectionManager, peer)
        {
            
        }
    }

}