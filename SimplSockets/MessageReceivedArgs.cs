using System;

namespace SimplSockets
{
    /// <summary>
    /// PooledMessage received args.
    /// </summary>
    public class MessageReceivedArgs : EventArgs
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal MessageReceivedArgs() { }

        /// <summary>
        /// The received message.
        /// </summary>
        public PooledMessage ReceivedMessage { get; internal set; }
    }
}

