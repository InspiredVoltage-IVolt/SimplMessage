using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimplSockets
{
    public class ConnectedClient
    {
        public ConnectedClient(Socket socket)
        {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            LastResponse = DateTime.UtcNow;
        }

        public IPEndPoint IPEndPoint { get { return (Socket?.RemoteEndPoint as IPEndPoint);} }

        public Socket Socket { get; private set; }

        public DateTime LastResponse { get; set; }
    }

}
