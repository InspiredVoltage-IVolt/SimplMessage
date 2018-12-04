using System.Net.Sockets;
using SimplSockets;

namespace SimplMessage
{
    // <summary>
    // A received message.
    // </summary>
    public class ReceivedMessage
    {
        public Socket Socket { get; private set; }
        internal int ThreadId { get; private set; }
        public string Identifier { get; private set; }

        /// <summary>
        /// The message bytes.
        /// </summary>
        public byte[] Bytes { get; private set; }



        public ReceivedMessage(int threadId, Socket socket, string identifier, byte[] bytes)
        {
            ThreadId = threadId;
            Socket = socket;
            Identifier = identifier;
            Bytes = bytes;
        }

        public TOut GetContent<TOut>() { return SimplMessageClient.GetMessage<TOut>(Bytes); }
    }

}
