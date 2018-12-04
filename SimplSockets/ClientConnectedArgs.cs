using System;
using System.Net;

namespace SimplSockets
{
    /// <summary>
    /// Connected Client args.
    /// </summary>
    public class ClientConnectedArgs : EventArgs
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal ClientConnectedArgs(ConnectedClient connectedClient) { ConnectedClient = connectedClient;}

        /// <summary>
        /// The received message.
        /// </summary>
        public ConnectedClient ConnectedClient { get; internal set; }
    }

    /// <summary>
    /// Connection attempt args.
    /// </summary>
    public class ServerEndpointArgs : EventArgs
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal ServerEndpointArgs(IPEndPoint ipEndPoint) { IPEndPoint = ipEndPoint; }

        /// <summary>
        /// The received message.
        /// </summary>
        public IPEndPoint IPEndPoint { get; internal set; }
    }

}
