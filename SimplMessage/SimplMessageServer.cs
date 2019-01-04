using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using SimplSockets;
using System.Threading;
using System.Threading.Tasks;


namespace SimplMessage
{
    public class SimplMessageServer : IDisposable
    {
        private readonly SimplSocketServer _simplSocketServer;
        private MsgPackCliSerializer _serializer;
        private Dictionary<string, Action<ReceivedMessage>> _callbacks;
        private Action<ReceivedMessage> _genericCallback;

        public List<ConnectedClient> ConnectedClients { get { return _simplSocketServer.ConnectedClients; } }

        /// <summary>
        /// An event that is fired when a client successfully connects to the server. Hook into this to do something when a connection succeeds.
        /// </summary>
        public event EventHandler<ClientConnectedArgs> ClientConnected;
        
        /// <summary>
        /// An event that is fired when a client has disconnected from the server. Hook into this to do something when a connection is lost.
        /// </summary>
        public event EventHandler                      ClientDisconnected;

        /// <summary>
        /// Get instance of SimplMessageServer
        /// </summary>
        /// <returns>instantiated SimplMessageServer</returns>
        public static SimplMessageServer Instance { get { return Nested.instance; } }
        private class Nested
        {
            static Nested() { } // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
            internal static readonly SimplMessageServer instance = new SimplMessageServer();
        }

        /// <summary>
        /// The constructor. It is initialized with a default socket.
        /// </summary>
        /// <param name="keepAlive">Whether or not to send keep-alive packets and detect if alive</param>
        /// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
        /// <param name="communicationTimeout">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize">The maximum message size.</param>
        /// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
        public SimplMessageServer(bool keepAlive = true, int messageBufferSize = 65536,
            int communicationTimeout = 10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false)
        {
            _simplSocketServer = new SimplSocketServer(messageBufferSize, communicationTimeout, maxMessageSize,
                useNagleAlgorithm);
            Initialize();
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="socketFunc">The function that creates a new socket. Use this to specify your socket constructor and initialize settings.</param>
        /// <param name="keepAlive">Whether or not to send keep-alive packets and detect if alive</param>
        /// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
        /// <param name="communicationTimeout">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize">The maximum message size.</param>
        /// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
        public SimplMessageServer(Func<Socket> socketFunc, bool keepAlive = true, int messageBufferSize = 65536,
            int communicationTimeout = 10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false)
        {
            _simplSocketServer = new SimplSocketServer(socketFunc, messageBufferSize, communicationTimeout,
                maxMessageSize, useNagleAlgorithm);
            Initialize();
        }

        private void Initialize()
        {
            _callbacks = new Dictionary<string, Action<ReceivedMessage>>();
            _serializer = new MsgPackCliSerializer();
            _simplSocketServer.MessageReceived    += MessageReceived;
            _simplSocketServer.ClientConnected    += (s, e) => { ClientConnected   ?.Invoke(s, e); };
            _simplSocketServer.ClientDisconnected += (s, e) => { ClientDisconnected?.Invoke(s, e); };
        }
        
        


        private ReceivedMessage MakeReceivedMessage(PooledMessage pooledMessage)
        {
            ReceivedMessage receivedMessage = null;
            try
            {
                ProtocolHelper.ExtractIdentifier(pooledMessage.Content, out string identifier,out byte[] messageContent);
                receivedMessage = new ReceivedMessage(pooledMessage.ThreadId,pooledMessage.Socket,identifier,messageContent);
            }
            catch (Exception) { } finally { pooledMessage.Dispose();}

            return receivedMessage;
        }

        private void MessageReceived(object sender, MessageReceivedArgs e)
        {
            // Split content up into identifier and actual content
            var receivedMessage = MakeReceivedMessage(e.ReceivedMessage);
            if (receivedMessage==null) return;
            try
            {

                if (_callbacks.ContainsKey(receivedMessage.Identifier))
                {
                    var callback = _callbacks[receivedMessage.Identifier];
                    callback?.Invoke(receivedMessage);
                    return;
                }

                // If no callbacks with matching identifiers are found, send as a generic callback 
                _genericCallback?.Invoke(receivedMessage);
            }
            catch (Exception)
            {
            }
        }

        public void AddCallBack(Action<ReceivedMessage> callback)
        {
            _genericCallback = callback;
        }

        public void AddCallBack<T>(Action<ReceivedMessage> callback) { AddCallBack(typeof(T).Name, callback); }
        public void AddCallBack(string identifier, Action<ReceivedMessage> callback)
        {
            if (!_callbacks.ContainsKey(identifier))
            {
                _callbacks.Add(identifier, callback);
            }
        }

        public void RemoveCallBack<T>() { RemoveCallBack(typeof(T).Name); }
        public void RemoveCallBack(string identifier)
        {
            if (_callbacks.ContainsKey(identifier))
            {
                _callbacks.Remove(identifier);
            }
        }

        public void Send<TIn>(TIn message, ConnectedClient connectedClient)
        {
            Send<TIn>(typeof(TIn).Name, message, connectedClient);
        }

        public void Send<TIn>(string identifier, TIn message, ConnectedClient connectedClient)
        {
            var rawMessage = _serializer.Serialize(identifier, message);
            if (rawMessage == null) return;
            _simplSocketServer.Send(rawMessage, connectedClient);
        }


        public void Send<TIn>(Socket connectedSocket, TIn message)
        {
            Send<TIn>(connectedSocket, typeof(TIn).Name, message);
        }

        public void Send<TIn>(Socket connectedSocket, string identifier, TIn message)
        {
            var rawMessage = _serializer.Serialize(identifier, message);
            if (rawMessage == null) return;
            _simplSocketServer.Send(rawMessage, connectedSocket);
        }

        public void Broadcast<TIn>(string identifier, TIn message)
        {
            var rawMessage = _serializer.Serialize<TIn>(identifier, message);
            if (rawMessage == null) return;
            _simplSocketServer.Broadcast(rawMessage);
        }

        public void Broadcast<TIn>(TIn message)
        {
            Broadcast<TIn>(typeof(TIn).Name, message);
        }

        public void Reply<TIn>(string identifier, TIn message, ReceivedMessage receivedMessage)
        {
            var rawMessage = _serializer.Serialize<TIn>(identifier, message);
            if (rawMessage == null) return;
            _simplSocketServer.Reply(rawMessage, receivedMessage.Socket, receivedMessage.ThreadId);
        }


        public void Reply<TIn>(TIn message, ReceivedMessage receivedMessage)
        {
            Reply<TIn>(typeof(TIn).Name, message, receivedMessage);
        }

        public static TOut GetMessage<TOut>(byte[] message)
        {
            var outMessage = MsgPackCliSerializer.DeserializeMessage<TOut>(message);
            if (outMessage == null) return default(TOut);
            return outMessage;
        }

        public void Close()
        {
            _simplSocketServer.Close();
        }
        public void Dispose()
        {
            _simplSocketServer.Dispose();
        }

        public void Listen(IPEndPoint ipEndPoint, bool discoverable = true, string name = "SimplMessageServer", string description = null)
        {
            _simplSocketServer.Listen(ipEndPoint, discoverable, name, description);
        }

        public void Listen(IPAddress IPAddress, int port, bool discoverable = true, string name = "SimplMessageServer", string description = null)
        {
            _simplSocketServer.Listen(new IPEndPoint(IPAddress, port), discoverable, name, description);
        }

        public async Task<ConnectedClient> WaitForNewClientAsync()
        {
            return await _simplSocketServer.WaitForNewClientAsync();
        }

#if (!WINDOWS_UWP)
        public ConnectedClient WaitForNewClient()
        {
            return _simplSocketServer.WaitForNewClient();
        }
#endif

    }
}
