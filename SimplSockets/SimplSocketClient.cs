using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// Todo:
// Add disable/enable pooled response

namespace SimplSockets
{
    /// <summary>
    /// Wraps sockets and provides intuitive, extremely efficient, scalable methods for client-server communication.
    /// </summary>
    public class SimplSocketClient : IDisposable
    {
        private enum ConnectionModes
        {
            Manual,
            fixedEndPoint,
            Discovery
        }

        // Mode in which connections are set up
        private ConnectionModes      _connectionMode                                  = ConnectionModes.Manual;
        // The function that creates a new socket                                     
        private readonly Func<Socket> _socketFunc                                     = null;
        // The currently used socket                                                  
        private Socket                _socket                                         = null;
        // Lock for socket access                                                     
        private readonly object       _socketLock                                     = new object();
        // The message buffer size to use for send/receive                            
        private readonly int          _messageBufferSize                              = 0;
        // The communication timeout, in milliseconds                                 
        private readonly int          _communicationTimeout                           = 0;
        // The maximum message size                                                   
        private readonly int          _maxMessageSize                                 = 0;
        // Whether or not to use the Nagle algorithm                                  
        private readonly bool         _useNagleAlgorithm                              = false;
        // The linger option                                                          
        private readonly LingerOption _lingerOption                                   = new LingerOption(true, 0);
        // The send buffer queue                                                      
        private readonly BlockingQueue<SocketAsyncEventArgs> _sendBufferQueue         = null;
        // The receive buffer queue                                                   
        private readonly BlockingQueue<SocketAsyncEventArgs> _receiveBufferQueue      = null;
        // The send buffer manual reset event                                        
        private readonly ManualResetEventSlim                _sendBufferReset         = new ManualResetEventSlim(false);
        private bool _lastStateConnected                                              = false;
                                                                                     
        // Whether or not a connection currently exists                              
        private volatile bool _isConnected                                            = false;
        private readonly object _isConnectedLock                                      = new object();
                                                                                     
        private Probe _probe                                                          = null;
        private readonly object _beaconsLock                                          = new Object();
        private IEnumerable<BeaconLocation> _beacons                                  = null;
                                                                                     
        // The client multiplexer                                                    
        private readonly Dictionary<int, MultiplexerData> _clientMultiplexer          = null;
        private readonly ReaderWriterLockSlim _clientMultiplexerLock                  = new ReaderWriterLockSlim();

        // Various pools
        private readonly Pool<MultiplexerData>      _multiplexerDataPool               = null;
        private readonly Pool<SocketAsyncEventArgs> _socketAsyncEventArgsSendPool      = null;
        private readonly Pool<SocketAsyncEventArgs> _socketAsyncEventArgsReceivePool   = null;
        private readonly Pool<SocketAsyncEventArgs> _socketAsyncEventArgsKeepAlivePool = null;
        private readonly Pool<MessageReceivedArgs>  _messageReceivedArgsPool           = null;
        private readonly Pool<SocketErrorArgs>      _socketErrorArgsPool               = null;

        // The date time of the last response
        private DateTime _lastResponse = DateTime.UtcNow;
        


        // A completely blind guess at the number of expected threads on the multiplexer. 100 sounds good, right? Right.
        private const int PredictedThreadCount = 100;

        /// <summary>
        /// An event that is fired when the client successfully connects to the server.
        /// </summary>
        public event EventHandler<ServerEndpointArgs> Connected;

        /// <summary>
        /// An event that is fired when the client successfully connects to the server.
        /// </summary>
        public event EventHandler<ServerEndpointArgs> ConnectionAttempt;

        /// <summary>
        /// An event that is fired when the client is disconnected from the server. 
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Whether or not to send keep-alive packets and detect if alive.
        /// </summary>
        public bool KeepAlive { get; set; }

        /// <summary>
        /// Get instance of SimpleSocketClient
        /// </summary>
        /// <returns>instantiated SimplSocketClient</returns>
        public static SimplSocketClient Instance { get { return Nested.instance; } }
        private class Nested
        {
            static Nested() { } // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
            internal static readonly SimplSocketClient instance = new SimplSocketClient();
        }

        /// <summary>
        /// Convenience function to creates a new TCP socket with default settings. This socket can be modified and can be used to initialize the constructor with.
        /// </summary>
        /// <returns>instantiated Socket</returns>
        public static Socket GetSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {NoDelay = true};
        }

        /// <summary>
        /// The constructor. It is initialized with a default socket.
        /// </summary>
        /// <param name="keepAlive">Whether or not to send keep-alive packets and detect if alive</param>
        /// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
        /// <param name="communicationTimeout">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize">The maximum message size.</param>
        /// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
        public SimplSocketClient(bool keepAlive = true, int messageBufferSize = 65536,int communicationTimeout = 10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false) : this
            (                
                GetSocket,
                keepAlive,
                messageBufferSize,
                communicationTimeout, 
                maxMessageSize, 
                useNagleAlgorithm)
        {}

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="socketFunc">The function that creates a new socket. Use this to specify your socket constructor and initialize settings.</param>
        /// <param name="keepAlive">Whether or not to send keep-alive packets and detect if alive</param>
        /// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
        /// <param name="communicationTimeout">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize">The maximum message size.</param>
        /// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
        public SimplSocketClient(Func<Socket> socketFunc, bool keepAlive = true, int messageBufferSize = 65536, int communicationTimeout = 10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false)
        {
            // Sanitize
            if (socketFunc == null)
            {
                throw new ArgumentNullException(nameof(socketFunc));
            }

            KeepAlive = keepAlive;

            if (messageBufferSize < 512)
            {
                throw new ArgumentException("must be >= 512", nameof(messageBufferSize));
            }
            if (communicationTimeout < 5000)
            {
                throw new ArgumentException("must be >= 5000", nameof(communicationTimeout));
            }
            if (maxMessageSize < 1024)
            {
                throw new ArgumentException("must be >= 1024", nameof(maxMessageSize));
            }

            _socketFunc           = socketFunc;
            _messageBufferSize    = messageBufferSize;
            _communicationTimeout = communicationTimeout;
            _maxMessageSize       = maxMessageSize;
            _useNagleAlgorithm    = useNagleAlgorithm;

            _sendBufferQueue      = new BlockingQueue<SocketAsyncEventArgs>();
            _receiveBufferQueue   = new BlockingQueue<SocketAsyncEventArgs>();

            // Initialize the client multiplexer
            _clientMultiplexer    = new Dictionary<int, MultiplexerData>(PredictedThreadCount);

            // Create the pools
            _multiplexerDataPool = new Pool<MultiplexerData>(PredictedThreadCount, () => new MultiplexerData { ManualResetEventSlim = new AsyncManualResetEvent() }, multiplexerData => 
            {
                multiplexerData.Message = null;
                multiplexerData.ManualResetEventSlim.Reset();
            });
            _socketAsyncEventArgsSendPool = new Pool<SocketAsyncEventArgs>(PredictedThreadCount, () =>
            {
                var poolItem = new SocketAsyncEventArgs();
                poolItem.Completed += OperationCallback;
                return poolItem;
            });
            _socketAsyncEventArgsReceivePool = new Pool<SocketAsyncEventArgs>(PredictedThreadCount, () =>
            {
                var poolItem = new SocketAsyncEventArgs();
                poolItem.SetBuffer(new byte[messageBufferSize], 0, messageBufferSize);
                poolItem.Completed += OperationCallback;
                return poolItem;
            });
            _socketAsyncEventArgsKeepAlivePool = new Pool<SocketAsyncEventArgs>(PredictedThreadCount, () =>
            {
                var poolItem = new SocketAsyncEventArgs();
                poolItem.SetBuffer(ProtocolHelper.ControlBytesPlaceholder, 0, ProtocolHelper.ControlBytesPlaceholder.Length);
                poolItem.Completed += OperationCallback;
                return poolItem;
            });
            _messageReceivedArgsPool = new Pool<MessageReceivedArgs>(PredictedThreadCount, () => new MessageReceivedArgs(), messageReceivedArgs => { messageReceivedArgs.ReceivedMessage = null; });
            _socketErrorArgsPool     = new Pool<SocketErrorArgs>    (PredictedThreadCount, () => new SocketErrorArgs()    , socketErrorArgs     => { socketErrorArgs.Exception           = null; });
        }

        public void ManualConnect()
        {
            _connectionMode = ConnectionModes.Manual;
        }

        /// <summary>
        /// Automatically reconnect if connection gets broken
        /// </summary>
        /// <param name="endPoint">The endpoint to connect to.</param>
        /// <returns></returns>
        public void AutoConnect(IPEndPoint endPoint)
        {
            if (_connectionMode == ConnectionModes.fixedEndPoint) return;
            _connectionMode = ConnectionModes.fixedEndPoint;
            Task.Run(() => KeepConnectedAsync(endPoint));
        }

        /// <summary>
        /// Discover sever and automatically reconnect if connection gets broken
        /// </summary>
        /// <param name="serverName">The endpoint to connect to.</param>
        /// <returns></returns>
        public void AutoConnect(string serverName= "SimplSocketServer")
        {
            if (_connectionMode == ConnectionModes.Discovery) return;
            _connectionMode = ConnectionModes.Discovery;
            _probe = new Probe(serverName);
            _probe.BeaconsUpdated += beacons =>  {
                lock (_beaconsLock)
                {
                    _beacons = beacons.ToList();

                }
            };

            _probe.Start();

            Task.Run(() => KeepConnectedAsync(serverName));
        }

        private async Task KeepConnectedAsync(IPEndPoint endPoint)
        {
            while (_connectionMode == ConnectionModes.fixedEndPoint)
            {
                if (!_isConnected)
                {
                    ConnectionAttempt?.Invoke(this, new ServerEndpointArgs(endPoint));
                    SafeConnect(endPoint);
                    await Task.Delay(1000);
                }
            }
        }

        public async Task WaitForConnectionAsync()
        {
            while (!_isConnected)
            {
                await Task.Delay(500);
            }
            return;
        }

        public async Task<bool> WaitForConnectionAsync(TimeSpan timeOut)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (!_isConnected)
            {
                if (stopwatch.Elapsed > timeOut) return false;
                await Task.Delay(500);
            }
            return true;
        }

#if (!WINDOWS_UWP)
        public void WaitForConnection()
        {
            while (!_isConnected)
            {
                Thread.Sleep(500);
            }
            return;
        }

        public bool WaitForConnection(TimeSpan timeOut)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (!_isConnected)
            {
                if (stopwatch.Elapsed > timeOut) return false;
                Thread.Sleep(500);
            }
            return true;
        }
#endif

        private async Task KeepConnectedAsync(string serverName)
        {
            var ipaddresses = NetworkHelper.GetCurrentIpv4Addresses();
            while (_connectionMode == ConnectionModes.Discovery)
            {
                // Check if not connected
                if (!_isConnected)
                {
                    lock (_beaconsLock)
                    {
                        // 
                        if (_beacons != null)
                        {
                            foreach (var beacon in _beacons)
                            {
                                ConnectionAttempt?.Invoke(this, new ServerEndpointArgs(beacon.Address));
                                if (SafeConnect(beacon.Address)) break;
                                // Check if IP adress received is a local adress
                                if (ipaddresses.Contains(beacon.Address.Address))
                                {
                                    var loopBackEndPoint = new IPEndPoint(IPAddress.Loopback, beacon.Address.Port);
                                    ConnectionAttempt?.Invoke(this, new ServerEndpointArgs(loopBackEndPoint));
                                    if (SafeConnect(loopBackEndPoint)) break;
                                }
                            }
                        }
                    }
                }
                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// Connects to an endpoint. Once this is called, you must call Close before calling Connect again. Will never issue an Exception
        /// </summary>
        /// <param name="endPoint">The endpoint.</param>
        /// <returns>true if connection is successful, false otherwise.</returns>
        public bool SafeConnect(EndPoint endPoint)
        {
            try { return Connect(endPoint); } catch (Exception) { return false; }
        }

        /// <summary>
        /// Connects to an endpoint. Once this is called, you must call Close before calling Connect again.
        /// </summary>
        /// <param name="endPoint">The endpoint.</param>
        /// <returns>true if connection is successful, false otherwise.</returns>
        public bool Connect(EndPoint endPoint)
        {
            // Sanitize
            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            _connectionMode = ConnectionModes.Manual;

            lock (_isConnectedLock)
            {
                if (_isConnected)
                {
                    Disconnect();
                }

                if (_isConnected)
                {
                    throw new InvalidOperationException("socket is already in use");
                }

                // Create socket
                _socket = _socketFunc();
                // Turn on or off Nagle algorithm
                _socket.NoDelay = !_useNagleAlgorithm;
                // Set the linger state
                _socket.LingerState = _lingerOption;

                // Post a connect to the socket synchronously
                try
                {
                    _socket.Connect(endPoint);
                }
                catch (SocketException ex)
                {
                    HandleCommunicationError(_socket, ex);
                    return false;
                }

                // Post a receive to the socket as the client will be continuously receiving messages to be pushed to the queue
                if (!TryUnsafeSocketOperation(_socket, SocketAsyncOperation.Receive, _socketAsyncEventArgsReceivePool.Pop()))
                {
                    return false;
                }
                
                // Spin up the keep-alive
                Task.Run(() => SendKeepAlive(_socket));

                // Process all queued sends on a separate thread
                Task.Run(() => ProcessSendQueue(_socket));

                // Process all incoming messages on a separate thread
                Task.Run(() => ProcessReceivedMessage(_socket));

                _isConnected        = true;
                _lastStateConnected = true;
                Connected?.Invoke(this,new ServerEndpointArgs(_socket.RemoteEndPoint as IPEndPoint));
            }

            return true;
        }

        /// <summary>
        /// Returns if client is connected
        /// </summary>
        /// <returns>true if connect to server</returns>
        public bool IsConnected()
        {
            lock (_isConnectedLock)
            {
                return _isConnected;
            }
        }

        /// <summary>
        /// Sends a message to the server without waiting for a response (one-way communication).
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void Send(byte[] message)
        {
            Send(new Message(message));
        }
        
        /// <summary>
        /// Sends a message to the server without waiting for a response (one-way communication).
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void Send(IMessage message)
        {
            // Sanitize
            if (message?.Content == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!_isConnected) return;

            // Get the current thread ID
            var threadId = GetThreadId();

            var messageWithControlBytes    = ProtocolHelper.AppendControlBytesToMessage(message, threadId);
            var socketAsyncEventArgs       = _socketAsyncEventArgsSendPool.Pop();
            socketAsyncEventArgs.SetBuffer(messageWithControlBytes.Content, 0, messageWithControlBytes.Length);
            socketAsyncEventArgs.UserToken = messageWithControlBytes;

            _sendBufferQueue.Enqueue(socketAsyncEventArgs);
        }



        private static int GetThreadId()
        {
#if (WINDOWS_UWP)
            int threadId = Task.CurrentId ?? 0;
#else
            int threadId = Thread.CurrentThread.ManagedThreadId;
#endif
            return threadId;
        }

        /// <summary>
        /// Sends a message to the server and then waits for the response to that message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="replyTimeout">Time to wait for an answer.</param>
        /// <returns>The response message.</returns>
        public PooledMessage SendReceive(byte[] message, int replyTimeout = 0)
        {      
            return SendReceive(new Message(message));
        }


        /// <summary>
        /// Sends a message to the server and then waits for the response to that message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="replyTimeout">Time to wait for an answer.</param>
        /// <returns>The response message.</returns>
        public PooledMessage SendReceive(IMessage message, int replyTimeout = 0)
        {
            // Sanitize
            if (message?.Content == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (replyTimeout == 0) replyTimeout = _communicationTimeout;

            if (!_isConnected) return null;

            // Get the current thread ID
            int threadId                   = GetThreadId();

            // Enroll in the multiplexer
            var multiplexerData            = EnrollMultiplexer(threadId);
            var messageWithControlBytes    = ProtocolHelper.AppendControlBytesToMessage(message, threadId);
            var socketAsyncEventArgs       = _socketAsyncEventArgsSendPool.Pop();
            socketAsyncEventArgs.SetBuffer(messageWithControlBytes.Content, 0, messageWithControlBytes.Length);
            socketAsyncEventArgs.UserToken = messageWithControlBytes;

            // Prioritize sends that have receives to the front of the queue
            _sendBufferQueue.EnqueueFront(socketAsyncEventArgs);

            // Wait for our message to go ahead from the receive callback, or until the timeout is reached
            if (!multiplexerData.ManualResetEventSlim.Wait(replyTimeout))
            {
                // Timeout
                HandleCommunicationError(_socket, new TimeoutException("The connection timed out before the response message was received"));

                // Un-enroll from the multiplexer
                UnenrollMultiplexer(threadId);

                // tell message that it can return to the pool
                messageWithControlBytes?.Return();

                // No signal
                return null;
            }

            // Now get the command string
            var result = multiplexerData.Message;

            // Un-enroll from the multiplexer
            UnenrollMultiplexer(threadId);

            // tell message that it can return to the pool
            messageWithControlBytes?.Return();

            return result;
        }

        /// <summary>
        /// Sends a message to the server and then awaits for the response to that message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="replyTimeout">Time to wait for an answer.</param>
        /// <returns>The response message.</returns>
        public async Task<PooledMessage> SendReceiveAsync(byte[] message, int replyTimeout = 0)
        {
            return await SendReceiveAsync(new Message(message), replyTimeout);
        }

        /// <summary>
        /// Sends a message to the server and then awaits for the response to that message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="replyTimeout">Time to wait for an answer.</param>
        /// <returns>The response message.</returns>
        public async Task<PooledMessage> SendReceiveAsync(IMessage message, int replyTimeout = 0)
        {
            // Sanitize
            if (message?.Content == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (replyTimeout == 0) replyTimeout = _communicationTimeout;

            if (!_isConnected) return null;

            // Get the current thread ID
            int threadId = GetThreadId();

            // Enroll in the multiplexer
            var multiplexerData            = EnrollMultiplexer(threadId);
            var messageWithControlBytes    = ProtocolHelper.AppendControlBytesToMessage(message, threadId);
            var socketAsyncEventArgs       = _socketAsyncEventArgsSendPool.Pop();
            socketAsyncEventArgs.SetBuffer(messageWithControlBytes.Content, 0, messageWithControlBytes.Length);
            socketAsyncEventArgs.UserToken = messageWithControlBytes;

            // Prioritize sends that have receives to the front of the queue
            _sendBufferQueue.EnqueueFront(socketAsyncEventArgs);

            // Wait for our message to go ahead from the receive callback, or until the timeout is reached
            //if (!await multiplexerData.ManualResetEventSlim.WaitOneAsync(replyTimeout, new CancellationToken()))
            if (!await multiplexerData.ManualResetEventSlim.WaitAsyncR(replyTimeout))
                {
                // Timeout
                HandleCommunicationError(_socket, new TimeoutException("The connection timed out before the response message was received"));

                // Un-enroll from the multiplexer
                UnenrollMultiplexer(threadId);

                // tell message that it can return to the pool
                messageWithControlBytes?.Return();

                // No signal
                return null;
            }

            // Now get the command string
            var result = multiplexerData.Message;

            // Un-enroll from the multiplexer
            UnenrollMultiplexer(threadId);

            // tell message that it can return to the pool
            messageWithControlBytes?.Return();

            return result;
        }

        // Todo: Add zero copy version, using segment

        /// <summary>
        /// An event that is fired whenever a message is received. Hook into this to process messages and potentially call Reply to send a response.
        /// </summary>
        public event EventHandler<MessageReceivedArgs> MessageReceived;

        /// <summary>
        /// An event that is fired whenever a socket communication error occurs. Hook into this to do something when communication errors happen.
        /// </summary>
        public event EventHandler<SocketErrorArgs> Error;

        /// <summary>
        /// Disposes the instance and frees unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Close/dispose the socket
            Close();
        }

        private async void SendKeepAlive(Socket socket)
        {
            _lastResponse = DateTime.UtcNow;

            while (_isConnected && KeepAlive)
            {
                await Task.Delay(1000);

                // Do the keep-alive
                var socketAsyncEventArgs = _socketAsyncEventArgsKeepAlivePool.Pop();

                _sendBufferQueue.Enqueue(socketAsyncEventArgs);

                // Confirm that we've heard from the server recently
                if ((DateTime.UtcNow - _lastResponse).TotalMilliseconds > _communicationTimeout)
                {
                    HandleCommunicationError(socket, new Exception("Keep alive timed out"));
                    return;
                }
            }
        }

        private void OperationCallback(object sender, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            switch (socketAsyncEventArgs.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ReceiveCallback((Socket)sender, socketAsyncEventArgs);
                    break;
                case SocketAsyncOperation.Send:
                    SendCallback((Socket)sender, socketAsyncEventArgs);
                    break;
                default:
                    throw new InvalidOperationException("Unknown case called, should program something for this");
            }
        }

        private void SendCallback(Socket socket, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            // Check for error
            if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Return();
                HandleCommunicationError(socket, new Exception("Send failed, error = " + socketAsyncEventArgs.SocketError));
            }

            _sendBufferReset.Set();

            if (socketAsyncEventArgs.Buffer.Length == ProtocolHelper.ControlBytesPlaceholder.Length)
            {
                // Notify that data has been send. This could possibly return the memory to buffer
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Return();
                _socketAsyncEventArgsKeepAlivePool.Push(socketAsyncEventArgs);
                return;
            }
           
            // Notify that data has been send. This could possibly return the memory to buffer
            (socketAsyncEventArgs.UserToken as PooledMessage)?.Sent();
            // Clear the buffer reference
            socketAsyncEventArgs.SetBuffer(null, 0, 0);
            // Push the socketAsyncEventArgs back in the pool
            _socketAsyncEventArgsSendPool.Push(socketAsyncEventArgs);
        }

        private void ReceiveCallback(Socket socket, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            // Check for error
            if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                HandleCommunicationError(socket, new Exception("Receive failed, error = " + socketAsyncEventArgs.SocketError));
            }

            // Get the message state
            int bytesRead = socketAsyncEventArgs.BytesTransferred;

            // Read the data
            if (bytesRead > 0)
            {
                // Add to receive queue
                _receiveBufferQueue.Enqueue(socketAsyncEventArgs);
            }
            else
            {
                // 0 bytes means disconnect
                HandleCommunicationError(socket, new Exception("Received 0 bytes (graceful disconnect)"));

                _socketAsyncEventArgsReceivePool.Push(socketAsyncEventArgs);
                return;
            }

            socketAsyncEventArgs = _socketAsyncEventArgsReceivePool.Pop();

            // Post a receive to the socket as the client will be continuously receiving messages to be pushed to the queue
            TryUnsafeSocketOperation(socket, SocketAsyncOperation.Receive, socketAsyncEventArgs);
        }

        private void ProcessSendQueue(Socket handler)
        {
            while (_isConnected)
            {
                // Get the next buffer from the queue
                var socketAsyncEventArgs = _sendBufferQueue.Dequeue();
                if (socketAsyncEventArgs == null)
                {
                    continue;
                }

                _sendBufferReset.Reset();

                // Do the send
                TryUnsafeSocketOperation(_socket, SocketAsyncOperation.Send, socketAsyncEventArgs);

                if (!_sendBufferReset.Wait(_communicationTimeout))
                {
                    HandleCommunicationError(_socket, new TimeoutException("The connection timed out before the send acknowledgement was received"));
                    return;
                }
            }
        }

        private void ProcessReceivedMessage(Socket handler)
        {
            int bytesToRead = -1;
            int threadId    = -1;

            int controlBytesOffset = 0;
            byte[] protocolBuffer  = new byte[ProtocolHelper.ControlBytesPlaceholder.Length];
            PooledMessage resultBuffer = null;

            // Loop until socket is done
            while (_isConnected)
            {
                // Get the next buffer from the queue
                var socketAsyncEventArgs = _receiveBufferQueue.Dequeue();
                if (socketAsyncEventArgs == null)
                {
                    continue;
                }

                var buffer = socketAsyncEventArgs.Buffer;
                int bytesRead = socketAsyncEventArgs.BytesTransferred;

                int currentOffset = 0;

                while (currentOffset < bytesRead)
                {
                    // Check if we need to get our control byte values
                    if (bytesToRead == -1)
                    {
                        var controlBytesNeeded = ProtocolHelper.ControlBytesPlaceholder.Length - controlBytesOffset;
                        var controlBytesAvailable = bytesRead - currentOffset;

                        var controlBytesToCopy = Math.Min(controlBytesNeeded, controlBytesAvailable);

                        // Copy bytes to control buffer
                        Buffer.BlockCopy(buffer, currentOffset, protocolBuffer, controlBytesOffset, controlBytesToCopy);

                        controlBytesOffset += controlBytesToCopy;
                        currentOffset += controlBytesToCopy;

                        // Check if done
                        if (controlBytesOffset == ProtocolHelper.ControlBytesPlaceholder.Length)
                        {
                            // Parse out control bytes
                            ProtocolHelper.ExtractControlBytes(protocolBuffer, out bytesToRead, out threadId);

                            // Reset control bytes offset
                            controlBytesOffset = 0;

                            // Ensure message is not larger than maximum message size
                            if (bytesToRead > _maxMessageSize)
                            {
                                HandleCommunicationError(handler, new InvalidOperationException(string.Format("message of length {0} exceeds maximum message length of {1}", bytesToRead, _maxMessageSize)));
                                return;
                            }
                        }

                        // Continue the loop
                        continue;
                    }

                    // Have control bytes, get message bytes

                    // SPECIAL CASE: if empty message, skip a bunch of stuff
                    if (bytesToRead != 0)
                    {
                        // Initialize buffer if needed
                        if (resultBuffer == null)
                        {
                            //resultBuffer = new byte[bytesToRead]
                            resultBuffer = PooledMessage.Rent(bytesToRead);
                        }

                        var bytesAvailable = bytesRead - currentOffset;
                        var bytesToCopy    = Math.Min(bytesToRead, bytesAvailable);

                        // Copy bytes to buffer
                        Buffer.BlockCopy(buffer, currentOffset, resultBuffer.Content, resultBuffer.Length - bytesToRead, bytesToCopy);

                        currentOffset += bytesToCopy;
                        bytesToRead   -= bytesToCopy;
                    }

                    // Check if we're done
                    if (bytesToRead == 0)
                    {
                        if (resultBuffer != null)
                        {
                            // Done, add to complete received messages
                            CompleteMessage(handler, threadId, resultBuffer);

                            // Reset message state
                            resultBuffer = null;
                        }
                        
                        bytesToRead   = -1;
                        threadId      = -1;

                        _lastResponse = DateTime.UtcNow;
                    }
                }

                _socketAsyncEventArgsReceivePool.Push(socketAsyncEventArgs);
            }
        }

        private void CompleteMessage(Socket handler, int threadId, PooledMessage receivedMessage)
        {
            // Try and signal multiplexer
            var multiplexerData = GetMultiplexerData(threadId);
            if (multiplexerData != null)
            {
                multiplexerData.Message = receivedMessage;
                SignalMultiplexer(threadId);
                return;
            }

            // No multiplexer
            receivedMessage.Socket   = handler;
            receivedMessage.ThreadId = threadId;
  

            // Fire the event if needed 
            var messageReceived = MessageReceived;
            if (messageReceived != null)
            {
                // Create the message received args 
                var messageReceivedArgs = _messageReceivedArgsPool.Pop();
                messageReceivedArgs.ReceivedMessage = receivedMessage;
                // Fire the event 
                messageReceived(this, messageReceivedArgs);
                // Back in the pool
                _messageReceivedArgsPool.Push(messageReceivedArgs);
            }
            else
            {
                // Return the message to the pool ourselves,
                // since there is nobody listening to the event to return it
                receivedMessage.Return();
            }
        }
        /// <summary>
        /// Handles an error in socket communication.
        /// </summary>
        /// <param name="socket">The socket that raised the exception.</param>
        /// <param name="ex">The exception that the socket raised.</param>
        private void HandleCommunicationError(Socket socket, Exception ex)
        {
            if (CloseSocket(ref socket)) return;

            // No longer connected

            _isConnected = false;

            // Clear receive queue for this client
            _receiveBufferQueue.Clear();

            // Raise the error event 
            var error = Error;
            if (error != null)
            {
                var socketErrorArgs = _socketErrorArgsPool.Pop();
                socketErrorArgs.Exception = ex;
                error(this, socketErrorArgs);
                _socketErrorArgsPool.Push(socketErrorArgs);
            }

            NotifyDisconnect();

        }

        private void NotifyDisconnect()
        {
            if (_lastStateConnected)
            {
                Disconnected?.Invoke(this, null);
                _lastStateConnected = false;
            }
        }

        /// <summary>
        /// Disconnects the connection. Once this is called, you can call Connect again to start a new client connection.
        /// Note that this will not stop autoconnecting
        /// </summary>
        public void Disconnect()
        {
            Close();
        }

        /// <summary>
        /// Closes the connection. Once this is called, you can call Connect again to start a new client connection.
        /// /// Note that this will not stop autoconnecting
        /// </summary>
        public void Close()
        {
            // Close the socket
            CloseSocket(ref _socket);

        }

        private bool CloseSocket(ref Socket socket)
        {
            lock (_socketLock)
            {
                if (socket == null) return true;
                    // Close the socket
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)          { } // Socket was not able to be shutdown, likely because it was never opened
                catch (ObjectDisposedException)  { return true; }  // Socket was already closed/disposed, so return out to prevent raising the Error event multiple times. This is most likely to happen when an error occurs during heavily multithreaded use


                // Close / dispose the socket
#if (!WINDOWS_UWP)
                try
                {
                    socket?.Disconnect(false);
                    socket?.Close();
                } catch(Exception){};
#endif
                try
                {
                    socket?.Dispose();
                    socket = null;
                } catch (Exception) {};
                               
            }

            _isConnected = false;
            NotifyDisconnect();
            return false;
        }

        private MultiplexerData EnrollMultiplexer(int threadId)
        {
            var multiplexerData = _multiplexerDataPool.Pop();

            _clientMultiplexerLock.EnterWriteLock();
            try
            {
                _clientMultiplexer.Add(threadId, multiplexerData);
            }
            finally
            {
                _clientMultiplexerLock.ExitWriteLock();
            }

            return multiplexerData;
        }

        private void UnenrollMultiplexer(int threadId)
        {
            var multiplexerData = GetMultiplexerData(threadId);
            if (multiplexerData == null)
            {
                return;
            }

            _clientMultiplexerLock.EnterWriteLock();
            try
            {
                _clientMultiplexer.Remove(threadId);
            }
            finally
            {
                _clientMultiplexerLock.ExitWriteLock();
            }

            _multiplexerDataPool.Push(multiplexerData);
        }

        private MultiplexerData GetMultiplexerData(int threadId)
        {
            MultiplexerData multiplexerData = null;
            
            _clientMultiplexerLock.EnterReadLock();
            try
            {
                _clientMultiplexer.TryGetValue(threadId, out multiplexerData);
            }
            finally
            {
                _clientMultiplexerLock.ExitReadLock();
            }

            return multiplexerData;
        }

        private void SignalMultiplexer(int threadId)
        {
            MultiplexerData multiplexerData = null;

            _clientMultiplexerLock.EnterReadLock();
            try
            {
                if (!_clientMultiplexer.TryGetValue(threadId, out multiplexerData))
                {
                    // Nothing to do
                    return;
                }
            }
            finally
            {
                _clientMultiplexerLock.ExitReadLock();
            }

            multiplexerData.ManualResetEventSlim.Set();
        }

        private bool TryUnsafeSocketOperation(Socket socket, SocketAsyncOperation operation, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            try
            {
                bool result = false;
                switch (operation)
                {
                    case SocketAsyncOperation.Accept:
                        result = socket.AcceptAsync(socketAsyncEventArgs);
                        break;
                    case SocketAsyncOperation.Send:
                        result = socket.SendAsync(socketAsyncEventArgs);
                        break;
                    case SocketAsyncOperation.Receive:
                        result = socket.ReceiveAsync(socketAsyncEventArgs);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown case called, should program something for this");
                }

                if (!result)
                {
                    OperationCallback(socket, socketAsyncEventArgs);
                }
            }
            catch (SocketException ex)
            {
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Return();
                HandleCommunicationError(socket, ex);
                return false;
            }
            catch (ObjectDisposedException)
            {
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Return();
                // If disposed, handle communication error was already done and we're just catching up on other threads. suppress it.
                return false;
            }
            catch (Exception)
            {
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Return();
                return false;
            }
            return true;
        }
    }
}
