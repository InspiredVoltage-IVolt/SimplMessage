using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using SimplSockets;
using System.Threading.Tasks;

namespace SimplMessage
{

    public class SimplMessageClient : IDisposable
    {
        private readonly SimplSocketClient                  _simplSocketClient;
        private MsgPackCliSerializer                        _serializer;
        private Dictionary<string, Action<ReceivedMessage>> _callbacks;
        private Dictionary<string, Action<ReceivedMessage>> _updateCallbacks;
        private Queue<ReceivedMessage>                      _updateCallbackQueue;
        private Action<ReceivedMessage>                     _genericCallback = null;

        /// <summary>
        /// An event that is fired when a client successfully connects to the server. Hook into this to do something when a connection succeeds.
        /// </summary>
        public event EventHandler<ServerEndpointArgs> Connected;

        /// <summary>
        /// An event that is fired when a client has disconnected from the server. Hook into this to do something when a connection is lost.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// An event that is fired when the client successfully connects to the server.
        /// </summary>
        public event EventHandler<ServerEndpointArgs> ConnectionAttempt;

        /// <summary>
        /// Get instance of SimplMessageClient
        /// </summary>
        /// <returns>instantiated SimplMessageClient</returns>
        public static SimplMessageClient Instance { get { return Nested.instance; } }
        private class Nested
        {
            static Nested() { } // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
            internal static readonly SimplMessageClient instance = new SimplMessageClient();
        }

        /// <summary>
        /// The constructor. It is initialized with a default socket.
        /// </summary>
        /// <param name="keepAlive">Whether or not to send keep-alive packets and detect if alive</param>
        /// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
        /// <param name="communicationTimeout">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize">The maximum message size.</param>
        /// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
        public SimplMessageClient(bool keepAlive = true, int messageBufferSize = 65536, int communicationTimeout = 10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false)
        {
            _simplSocketClient = new SimplSocketClient(keepAlive,messageBufferSize,communicationTimeout,maxMessageSize,useNagleAlgorithm);
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
        public SimplMessageClient(Func<Socket> socketFunc, bool keepAlive = true, int messageBufferSize = 65536,
            int communicationTimeout = 10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false)
        {
            _simplSocketClient = new SimplSocketClient(socketFunc, keepAlive, messageBufferSize, communicationTimeout, maxMessageSize, useNagleAlgorithm);
            Initialize();
        }

        private void Initialize()
        {
            _callbacks                             = new Dictionary<string, Action<ReceivedMessage>>();
            _updateCallbacks                       = new Dictionary<string, Action<ReceivedMessage>>();
            _updateCallbackQueue                   = new Queue<ReceivedMessage>();
            _serializer                            = new MsgPackCliSerializer();
            _simplSocketClient.MessageReceived    += MessageReceived;
            _simplSocketClient.Connected          += (s, e) => { Connected?.Invoke(s, e); };
            _simplSocketClient.Disconnected       += (s, e) => { Disconnected?.Invoke(s, e); };
            _simplSocketClient.ConnectionAttempt  += (s, e) => { ConnectionAttempt?.Invoke(s, e); };
        }

        private ReceivedMessage MakeReceivedMessage(PooledMessage pooledMessage)
        {
            ReceivedMessage receivedMessage = null;
            try
            {
                ProtocolHelper.ExtractIdentifier(pooledMessage.Content, out string identifier, out byte[] messageContent);
                receivedMessage = new ReceivedMessage(pooledMessage.ThreadId, pooledMessage.Socket, identifier, messageContent);
            }
            catch (Exception) { }
            finally { pooledMessage.Dispose(); }

            return receivedMessage;
        }

        private void MessageReceived(object sender, MessageReceivedArgs e)
        {
            try
            {
                //ProtocolHelper.ExtractIdentifier(e.ReceivedMessage.Content, out string identifier, out byte[] messageContent);
                // Split content up into identifier and actual content
                var receivedMessage = MakeReceivedMessage(e.ReceivedMessage);

                // Message came from pool, so dispose to return it
                e.ReceivedMessage.Dispose();

                if (receivedMessage==null) return;
                if (_callbacks.ContainsKey(receivedMessage.Identifier))
                {
                    var callback = _callbacks[receivedMessage.Identifier];
                    callback?.Invoke(receivedMessage);
                    return;
                }

                // Now check the callbacks that are being processed when the update function is called
                // This is for use in Unity (and similar environments), where it is needed to process the callbacks at a specific moment
                if (_updateCallbacks.ContainsKey(receivedMessage.Identifier))
                {
                    // Since we already split the message up to get the identifier, store it like that for later use
                    _updateCallbackQueue.Enqueue(receivedMessage);
                    return;
                }

                // If no callbacks with matching identifiers are found, send as a generic callback 
                _genericCallback?.Invoke(receivedMessage);

            }
            catch (Exception) {}
        }

        public void Dispose()
        {
            _simplSocketClient.Dispose();
        }

        /// <summary>
        /// Calling this function will trigger all the callbacks attached through AddUpdateCallBack
        /// </summary>
        public void UpdateCallbacks()
        {
            // Only process the items that are in the queue when we start processing
            var queueCount = _updateCallbackQueue.Count;
            for (var j = 0; j < queueCount; j++)
            {
                var receivedMessage = _updateCallbackQueue.Dequeue();
                if (_updateCallbacks.ContainsKey(receivedMessage.Identifier))
                {
                    var callback = _updateCallbacks[receivedMessage.Identifier];
                    callback?.Invoke(receivedMessage);
                }
            }
        }

        /// <summary>
        /// Calling this function will trigger all the callbacks attached through AddUpdateCallBack 
        /// This will run only for a limited timespan
        /// </summary>
        /// <param name="maxDuration">Timespan for which UpdateCallbacks will run</param>
        public void UpdateCallbacks(TimeSpan maxDuration)
        {
            // Only process the items that are in the queue when we start processing
            var queueCount = _updateCallbackQueue.Count;
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (var j = 0; j < queueCount; j++)
            {
                var receivedMessage = _updateCallbackQueue.Dequeue();
                if (_updateCallbacks.ContainsKey(receivedMessage.Identifier))
                {
                    var callback = _updateCallbacks[receivedMessage.Identifier];
                    callback?.Invoke(receivedMessage);
                }
                // after timespan stop processing
                if (stopWatch.Elapsed>=maxDuration) return;
            }
        }

        public void AddCallBack(Action<ReceivedMessage> callback)
        {
            _genericCallback = callback;
        }

        public void AddCallBack<T>(                   Action<ReceivedMessage> callback) { AddCallBack(typeof(T).Name, callback); }
        public void AddCallBack   (string identifier, Action<ReceivedMessage> callback)
        {
            if (!_callbacks.ContainsKey(identifier))
            {
                _callbacks.Add(identifier, callback);
            }
        }

        public void RemoveCallBack<T>() { RemoveCallBack(typeof(T).Name); }
        public void RemoveCallBack   (string identifier)
        {
            if (_callbacks.ContainsKey(identifier))
            {
                _callbacks.Remove(identifier);
            }
        }

        public void AddUpdateCallBack<T>(                   Action<ReceivedMessage> callback) { AddUpdateCallBack(typeof(T).Name, callback); }
        public void AddUpdateCallBack   (string identifier, Action<ReceivedMessage> callback)
        {
            if (!_updateCallbacks.ContainsKey(identifier))
            {
                _updateCallbacks.Add(identifier, callback);
            }
        }

        public void RemoveUpdateCallBack<T>() { RemoveUpdateCallBack(typeof(T).Name); }
        public void RemoveUpdateCallBack   (string identifier)
        {
            if (_updateCallbacks.ContainsKey(identifier))
            {
                _updateCallbacks.Remove(identifier);
            }
        }

        public bool Connect(EndPoint endPoint)
        {
            return _simplSocketClient.Connect(endPoint);
        }

        public bool Connect(IPAddress IPAddress, int port)
        {
            return _simplSocketClient.Connect(new IPEndPoint(IPAddress,port));
        }

        public void AutoConnect(string serverName = "SimplMessageServer")
        {
            _simplSocketClient.AutoConnect(serverName);
        }

        public void Disconnect()
        {
            _simplSocketClient.Disconnect();
        }

        public void Close()
        {
            _simplSocketClient.Close();
        }

        public void ManualConnect()
        {
            _simplSocketClient.ManualConnect();
        }

        public void AutoConnect(IPEndPoint endPoint)
        {
            _simplSocketClient.AutoConnect(endPoint);
        }

#if (!WINDOWS_UWP)
        public void WaitForConnection()
        {
             _simplSocketClient.WaitForConnection();
        }

        public bool WaitForConnection(TimeSpan timeOut)
        {
            return _simplSocketClient.WaitForConnection(timeOut);
        }
#endif


        public async Task WaitForConnectionAsync()
        {
            await _simplSocketClient.WaitForConnectionAsync();
        }

        public async Task<bool>WaitForConnectionAsync(TimeSpan timeOut)
        {
            return await _simplSocketClient.WaitForConnectionAsync(timeOut);
        }


        public void Send<TIn>(                  TIn message) { Send<TIn>(typeof(TIn).Name, message); }
        public void Send<TIn>(string identifier,TIn message)
        {
            var rawMessage = _serializer.Serialize(identifier,message);
            if (rawMessage==null) return;
            _simplSocketClient.Send(rawMessage);
        }            

        public TOut SendReceive<TIn, TOut>(                   TIn message, int replyTimeout = 0) { return SendReceive<TIn, TOut>(typeof(TIn).Name, message, replyTimeout); }
        public TOut SendReceive<TIn, TOut>(string identifier, TIn message, int replyTimeout = 0)
        {
            var rawMessage  = _serializer.Serialize(identifier,message);
            if (rawMessage  == null) return default(TOut);
            var rawResponse = _simplSocketClient.SendReceive(rawMessage);
            if (rawResponse == null) return default(TOut);
            var response    = _serializer.Deserialize<TOut>(rawResponse.Content);
            rawResponse.Dispose();
            if (response == null) return default(TOut);
            return response;
        }

        public async Task<TOut> SendReceiveAsync<TIn, TOut>(                   TIn message, int replyTimeout = 0) { return await SendReceiveAsync<TIn, TOut>(typeof(TIn).Name, message, replyTimeout); }
        public async Task<TOut> SendReceiveAsync<TIn, TOut>(string identifier, TIn message, int replyTimeout = 0)
        {
            var rawMessage = _serializer.Serialize(identifier, message);
            if (rawMessage == null) return default(TOut);
            var rawResponse = await _simplSocketClient.SendReceiveAsync(rawMessage);
            if (rawResponse == null) return default(TOut);
            var response = _serializer.Deserialize<TOut>(rawResponse.Content);
            rawResponse.Dispose();
            if (response == null) return default(TOut);
            return response;
        }

        public static T GetMessage<T>(byte[] message)
        {
            var outMessage = MsgPackCliSerializer.DeserializeMessage<T>(message);
            if (outMessage == null) return default(T);
            return outMessage;
        }

        public bool IsConnected()
        {
            return _simplSocketClient.IsConnected();
        }


    }
}
