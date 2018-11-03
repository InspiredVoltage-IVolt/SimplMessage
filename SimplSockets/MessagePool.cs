using System;
using System.Buffers;
using System.Net.Sockets;

namespace SimplSockets
{
    public class Message : IMessage
    {
        internal Socket Socket;
        internal int ThreadId;

        public Message(int length)
        {
            Content = new byte[length];
        }

        public Message(byte[] message)
        {
            Content = message;
        }

        public byte[] Content { get; set; }
        public int Length => Content?.Length??0;
        public void Sent() { }
        public void Dispose() { Content = null; }
    }

    public class PooledMessage : IMessage
    {
        internal Socket Socket;
        internal int ThreadId;

        private static readonly ArrayPool<byte> ArrayPool = ArrayPool<byte>.Shared;
        private bool _rented          = false;
        private bool _sent            = false;
        private bool _returnAfterSend = false;
        public byte[] Content { get; set; }

        public int Length { get; set; }

        private PooledMessage() { }

        public static PooledMessage Rent(int length)
        {
            var message = new PooledMessage
            {
                Content = ArrayPool.Rent(length),
                Length = length,
                _rented = true
            };
            return message;
        }

        public static void Return(byte[] content)
        {
            ArrayPool.Return(content);
        }

        public void Sent()
        {
            _sent = true;
            if (_returnAfterSend && _sent) Return();
        }

        public void ReturnAfterSend()
        {
            _returnAfterSend = true;
            if (_returnAfterSend && _sent) Return();
        }

        public bool IsRented()
        {
            return _rented;
        }

        public void Return()
        {
            if (!_rented) return;
            ArrayPool.Return(Content);
            Content          = null;
            Length           = 0;
            _rented          = false;
            _returnAfterSend = false;
            _sent            = false;
        }

        public void Dispose()
        {
            Return();
        }
    }
}
