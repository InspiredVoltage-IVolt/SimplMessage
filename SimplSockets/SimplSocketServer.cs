using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SimplSockets
{
    // - todo test ConnectedClient functionality

    /// <summary>
    /// Wraps sockets and provides intuitive, extremely efficient, scalable methods for client-server communication.
    /// </summary>
    public class SimplSocketServer : ISimplSocketServer
    {
        // The function that creates a new socket
        private readonly Func<Socket> _socketFunc           = null;
        // The currently used socket
        private Socket                _socket               = null;
        // Lock for socket access
        private readonly object      _socketLock            = new object();
        // The message buffer size to use for send/receive
        private readonly int          _messageBufferSize    = 0;
        // The communication timeout, in milliseconds
        private readonly int          _communicationTimeout = 0;
        // The maximum message size
        private readonly int          _maxMessageSize       = 0;
        // Whether or not to use the Nagle algorithm
        private readonly bool         _useNagleAlgorithm    = false;
        // The linger option
        private readonly LingerOption _lingerOption         = new LingerOption(true, 0);

        // Whether or not the socket is currently listening
        private volatile bool         _isListening          = false;
        private Beacon _beacon;
        private readonly object       _isListeningLock      = new object();

        // The currently connected clients
        private readonly Dictionary<Socket,ConnectedClient> _currentlyConnectedClients = null;

        private readonly ReaderWriterLockSlim  _currentlyConnectedClientsLock = new ReaderWriterLockSlim();

        // The currently connected client receive queues
        private readonly Dictionary<Socket, BlockingQueue<SocketAsyncEventArgs>> _currentlyConnectedClientsReceiveQueues = null;
        private readonly ReaderWriterLockSlim _currentlyConnectedClientsReceiveQueuesLock = new ReaderWriterLockSlim();

        // Various pools
        private readonly Pool<SocketAsyncEventArgs> _socketAsyncEventArgsSendPool      = null;
        private readonly Pool<SocketAsyncEventArgs> _socketAsyncEventArgsReceivePool   = null;
        private readonly Pool<SocketAsyncEventArgs> _socketAsyncEventArgsKeepAlivePool = null;
        private readonly Pool<MessageReceivedArgs>  _messageReceivedArgsPool           = null;
        private readonly Pool<SocketErrorArgs>      _socketErrorArgsPool               = null;

        // A completely blind guess at the number of expected connections to this server. 100 sounds good, right? Right.
        private const int PredictedConnectionCount = 100;

        public List<ConnectedClient> ConnectedClients
        {
            get { return _currentlyConnectedClients.Values.ToList(); } 
        }

        /// <summary>
        /// Convenience function to creates a new TCP socket with default settings. This socket can be modified and can be used to initialize the constructor with.
        /// </summary>
        /// <returns>instantiated Socket</returns>
        public static Socket GetSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        }

        /// <summary>
        /// Get instance of SimplSocketServer
        /// </summary>
        /// <returns>instantiated SimplSocketServer</returns>
        public static SimplSocketServer Instance { get { return Nested.instance; } }
        private class Nested
        {
            static Nested() { } // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
            internal static readonly SimplSocketServer instance = new SimplSocketServer();
        }

        /// <summary>
        /// The constructor. It is initialized with a default socket.
        /// </summary>
        /// <param name="messageBufferSize"   >The message buffer size to use for send/receive.</param>
        /// <param name="communicationTimeout">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize"      >The maximum message size.</param>
        /// <param name="useNagleAlgorithm"   >Whether or not to use the Nagle algorithm.</param>
        public SimplSocketServer(int messageBufferSize = 65536, int communicationTimeout = 10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false) : this
        (
            GetSocket,
            messageBufferSize,
            communicationTimeout,
            maxMessageSize,
            useNagleAlgorithm)
        { }

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="socketFunc"          >The function that creates a new socket. Use this to specify your socket constructor and initialize settings.</param>
        /// <param name="messageBufferSize"   >The message buffer size to use for send/receive.</param>
        /// <param name="communicationTimeout">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize"      >The maximum message size.</param>
        /// <param name="useNagleAlgorithm"   >Whether or not to use the Nagle algorithm.</param>
        public SimplSocketServer(Func<Socket> socketFunc, int messageBufferSize = 65536, int communicationTimeout = 10*10000, int maxMessageSize = 10 * 1024 * 1024, bool useNagleAlgorithm = false)
        {
            // Sanitize
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

            _socketFunc           = socketFunc ?? throw new ArgumentNullException(nameof(socketFunc));
            _messageBufferSize    = messageBufferSize;
            _communicationTimeout = communicationTimeout;
            _maxMessageSize       = maxMessageSize;
            _useNagleAlgorithm    = useNagleAlgorithm;

            _currentlyConnectedClients              = new Dictionary<Socket, ConnectedClient>(PredictedConnectionCount);
            _currentlyConnectedClientsReceiveQueues = new Dictionary<Socket, BlockingQueue<SocketAsyncEventArgs>>(PredictedConnectionCount);

            // Create the pools
            _socketAsyncEventArgsSendPool = new Pool<SocketAsyncEventArgs>(PredictedConnectionCount, () =>
            {
                var poolItem = new SocketAsyncEventArgs();
                poolItem.Completed += OperationCallback;
                return poolItem;
            });
            _socketAsyncEventArgsReceivePool = new Pool<SocketAsyncEventArgs>(PredictedConnectionCount, () =>
            {
                var poolItem = new SocketAsyncEventArgs();
                poolItem.SetBuffer(new byte[messageBufferSize], 0, messageBufferSize);
                poolItem.Completed += OperationCallback;
                return poolItem;
            });
            _socketAsyncEventArgsKeepAlivePool = new Pool<SocketAsyncEventArgs>(PredictedConnectionCount, () =>
            {
                var poolItem = new SocketAsyncEventArgs();
                poolItem.SetBuffer(ProtocolHelper.ControlBytesPlaceholder, 0, ProtocolHelper.ControlBytesPlaceholder.Length);
                poolItem.Completed += OperationCallback;
                return poolItem;
            });
            //_receivedMessagePool = new Pool<ReceivedMessage>(PredictedConnectionCount, () => new ReceivedMessage(), receivedMessage =>
            //{
            //    receivedMessage.Message = null;
            //    receivedMessage.Socket  = null;
            //});
            _messageReceivedArgsPool = new Pool<MessageReceivedArgs>(PredictedConnectionCount, () => new MessageReceivedArgs(), messageReceivedArgs => { messageReceivedArgs.ReceivedMessage = null; });
            _socketErrorArgsPool     = new Pool<SocketErrorArgs>    (PredictedConnectionCount, () => new SocketErrorArgs()    , socketErrorArgs     => { socketErrorArgs.Exception           = null; });
        }

        /// <summary>
        /// Begin listening for incoming connections. Once this is called, you must call Close before calling Listen again.
        /// </summary>
        /// <param name="localEndpoint">The local endpoint to listen on.</param>
        public void Listen(IPAddress adress, int port, bool discoverable = true, string name = "SimplSocketServer", string description = null)
        {
            Listen(new IPEndPoint(adress, port), discoverable, name, description);
        }


        /// <summary>
            /// Begin listening for incoming connections. Once this is called, you must call Close before calling Listen again.
            /// </summary>
            /// <param name="localEndpoint">The local endpoint to listen on.</param>
            public void Listen(IPEndPoint localEndpoint, bool discoverable = true, string name= "SimplSocketServer", string description = null)
        {
            // Sanitize
            if (localEndpoint == null)
            {
                throw new ArgumentNullException(nameof(localEndpoint));
            }

            lock (_isListeningLock)
            {
                if (_isListening)
                {
                    throw new InvalidOperationException("socket is already in use");
                }

                _isListening = true;
            }

            // Create socket
            _socket = _socketFunc();

            _socket.Bind(localEndpoint);
            _socket.Listen(PredictedConnectionCount);

            // very important to not have buffer for accept, see remarks on 288 byte threshold: https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.acceptasync(v=vs.110).aspx
            var socketAsyncEventArgs = new SocketAsyncEventArgs();
            socketAsyncEventArgs.Completed += OperationCallback;

            // Post accept on the listening socket
            if (!TryUnsafeSocketOperation(_socket, SocketAsyncOperation.Accept, socketAsyncEventArgs))
            {
                lock (_isListeningLock)
                {
                    _isListening = false;
                    throw new Exception("Socket accept failed");
                }
            }

            // Spin up the keep-alive
            Task.Factory.StartNew(KeepAlive);

            // Start beacon if discoverable
            _beacon = new Beacon(name, (ushort)localEndpoint.Port);
            _beacon.BeaconData = description?? $"name {Dns.GetHostName()} ";
            _beacon.StartAsync();
            //_beacon.Start();
        }

        /// <summary>
        /// Broadcasts a message to all connected clients without waiting for a response (one-way communication).
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void Broadcast(byte[] message)
        {
            Broadcast(new Message(message));
        }
        
        /// <summary>
        /// Broadcasts a message to all connected clients without waiting for a response (one-way communication).
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void Broadcast(IMessage message)
        {
            // Sanitize
            if (message == null )
            {
                throw new ArgumentNullException(nameof(message));
            }

            // Get the current thread ID
            int threadId = GetThreadId();

            var messageWithControlBytes = ProtocolHelper.AppendControlBytesToMessage(message, threadId);

            List<Socket> bustedClients = null;

            // Do the send
            _currentlyConnectedClientsLock.EnterReadLock();
            try
            {
                foreach (var lookup in _currentlyConnectedClients)
                {
                    var client = lookup.Value;
                    var socketAsyncEventArgs = _socketAsyncEventArgsSendPool.Pop();
                    socketAsyncEventArgs.SetBuffer(messageWithControlBytes.Content, 0, messageWithControlBytes.Length);
                    socketAsyncEventArgs.UserToken = messageWithControlBytes;
                    // Post send on the listening socket
                    if (!TryUnsafeSocketOperation(client.Socket, SocketAsyncOperation.Send, socketAsyncEventArgs))
                    {
                        // Mark for disconnection
                        if (bustedClients == null)
                        {
                            bustedClients = new List<Socket>();
                        }

                        bustedClients.Add(client.Socket);
                    }
                }
            }
            finally
            {
                _currentlyConnectedClientsLock.ExitReadLock();
            }

            if (bustedClients != null)
            {
                foreach (var client in bustedClients)
                {
                    HandleCommunicationError(client, new Exception("Broadcast Send failed"));
                }
            }
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
        /// Sends a message back to the client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="receivedMessage">The received message which is being replied to.</param>
        public void Reply(byte[] message, PooledMessage receivedMessage)
        {
            Reply(new Message(message), receivedMessage);
        }
       
        /// <summary>
        /// Sends a message back to the client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="receivedMessage">The received message which is being replied to.</param>
        public void Reply(IMessage message, PooledMessage receivedMessage)
        {
            Send(message, receivedMessage.Socket, receivedMessage.ThreadId);
        }

        /// <summary>
        /// Sends a message to a client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="receivedMessage">The client which is being sent to.</param>
        public void Send(IMessage message, ConnectedClient connectedClient)
        {
            if (_currentlyConnectedClients.ContainsKey(connectedClient.Socket))
                Send(message, connectedClient.Socket, GetThreadId());
        }

        /// <summary>
        /// Sends a message to a client.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="ConnectedClient">The client which is being sent to.</param>
        public void Send(byte[] message, ConnectedClient connectedClient)
        {
            if (_currentlyConnectedClients.ContainsKey(connectedClient.Socket))
                Send(new Message(message), connectedClient.Socket, GetThreadId());
        }

        /// <summary>
        /// Sends a message to a client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="socket">The socket which is being send to.</param>
        public void Send(byte[] message, Socket socket) { Send(new Message(message), socket, GetThreadId()); }


        /// <summary>
        /// Sends a message to a client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="socket">The socket which is being replied to.</param>
        public void Send(IMessage message, Socket socket) { Send(message,socket, GetThreadId());}

        /// <summary>
        /// Sends a message to a client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="socket">The socket which is being replied to.</param>
        /// <param name="threadId">The thread which is being replied to.</param>
        public void Send(IMessage message, Socket socket, int threadId)
        {
            // Sanitize
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            if (socket == null)
            {
                throw new ArgumentException("contains corrupted data", "receivedMessageState");
            }

            var messageWithControlBytes = ProtocolHelper.AppendControlBytesToMessage(message, threadId);
            var socketAsyncEventArgs = _socketAsyncEventArgsSendPool.Pop();
            socketAsyncEventArgs.SetBuffer(messageWithControlBytes.Content, 0, messageWithControlBytes.Length);
            socketAsyncEventArgs.UserToken = messageWithControlBytes;

            // Do the send to the appropriate client
            TryUnsafeSocketOperation(socket, SocketAsyncOperation.Send, socketAsyncEventArgs);
        }

        /// <summary>
        /// Sends a message back to the client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="socket">The socket which is being replied to.</param>
        /// <param name="threadId">The thread which is being replied to.</param>
        public void Reply(byte[] message, Socket socket, int threadId)
        {
            Send(new Message(message), socket, threadId);
        }

        private bool CloseSocket(ref Socket socket, bool disposeSocket=true)
        {            
            lock (_socketLock)
            {
                if (socket == null) return true;
                // Close the socket
                try
                {
                    if (socket.Connected) socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                    // Socket was not able to be shutdown, likely because it was never opened
                }
                catch (ObjectDisposedException)
                {
                    // Socket was already closed/disposed, so return out to prevent raising the Error event multiple times
                    // This is most likely to happen when an error occurs during heavily multithreaded use
                    return true;
                }

#if (!WINDOWS_UWP)
                try
                {
                    if (socket.Connected) { socket?.Disconnect(false); }
                    socket?.Close();
                } catch (Exception) {}
                
#endif
                if (disposeSocket) DisposeSocket(ref socket);
            }

            return false;
        }

        private void DisposeSocket(ref Socket socket)
        {
            try
            {
                socket?.Dispose();
                socket = null;
            } catch (Exception) {}

        }

        /// <summary>
        /// Closes the connection. Once this is called, you can call Listen again.
        /// </summary>
        public void Close()
        {
            CloseSocket(ref _socket);

            // Dump all clients
            var clientList = new Dictionary<Socket,ConnectedClient>();
            _currentlyConnectedClientsLock.EnterWriteLock();
            try
            {
                clientList = new Dictionary<Socket, ConnectedClient>(_currentlyConnectedClients);
            }
            finally
            {
                _currentlyConnectedClientsLock.ExitWriteLock();
            }

            foreach (var client in clientList)
            {
                HandleCommunicationError(client.Value.Socket, new Exception("Host is shutting down"));
            }


            // No longer connected
            lock (_isListeningLock)
            {
                _isListening = false;
            }
        }

        /// <summary>
        /// Gets the currently connected client count.
        /// </summary>
        public int CurrentlyConnectedClientCount => _currentlyConnectedClients.Count;

        /// <summary>
        /// An event that is fired when a client successfully connects to the server. Hook into this to do something when a connection succeeds.
        /// </summary>
        public event EventHandler<ClientConnectedArgs> ClientConnected;

        /// <summary>
        /// An event that is fired when a client is not connected to the server anymore. Hook into this to do something when a connection succeeds.
        /// </summary>
        public event EventHandler ClientDisconnected;

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
            CloseSocket(ref _socket);
        }

        private async void KeepAlive()
        {
            List<Socket> bustedClients = null;

            while (true)
            {
                await Task.Delay(1000);

                if (_currentlyConnectedClients.Count == 0)
                {
                    continue;
                }

                bustedClients = null;

                // Do the keep-alive
                _currentlyConnectedClientsLock.EnterReadLock();
                try
                {
                    foreach (var lookup in _currentlyConnectedClients)
                    {
                        var client = lookup.Value;
                        var socketAsyncEventArgs = _socketAsyncEventArgsKeepAlivePool.Pop();

                        // Post send on the socket and confirm that we've heard from the client recently
                        if ((DateTime.UtcNow - client.LastResponse).TotalMilliseconds > _communicationTimeout
                            || !TryUnsafeSocketOperation(client.Socket, SocketAsyncOperation.Send, socketAsyncEventArgs))
                        {
                            // Mark for disconnection
                            if (bustedClients == null)
                            {
                                bustedClients = new List<Socket>();
                            }

                            bustedClients.Add(client.Socket);
                        }
                    }
                }
                finally
                {
                    _currentlyConnectedClientsLock.ExitReadLock();
                }

                if (bustedClients != null)
                {
                    foreach (var client in bustedClients)
                    {
                        HandleCommunicationError(client, new Exception("Keep alive failed for a connected client"));
                    }
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
                case SocketAsyncOperation.Accept:
                    AcceptCallback((Socket)sender, socketAsyncEventArgs);
                    break;
                default:
                    throw new InvalidOperationException("Unknown case called, should program something for this");
            }
        }

        private void AcceptCallback(Socket socket, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            // If our socket is disposed, stop
            if (socketAsyncEventArgs.SocketError == SocketError.OperationAborted)
            {
                return;
            }
            else if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                HandleCommunicationError(socket, new Exception("Accept failed, error = " + socketAsyncEventArgs.SocketError));
            }

            var handler = socketAsyncEventArgs.AcceptSocket;
            socketAsyncEventArgs.AcceptSocket = null;

            // Post accept on the listening socket
            if (!TryUnsafeSocketOperation(socket, SocketAsyncOperation.Accept, socketAsyncEventArgs))
            {
                throw new Exception("Socket accept failed");
            }

            try
            {
                // Turn on or off Nagle algorithm
                handler.NoDelay = !_useNagleAlgorithm;
                // Set the linger state
                handler.LingerState = _lingerOption;
            }
            catch (SocketException ex)
            {
                HandleCommunicationError(handler, ex);
                return;
            }
            catch (ObjectDisposedException)
            {
                // If disposed, handle communication error was already done and we're just catching up on other threads. suppress it.
                return;
            }

            // Enroll in currently connected client sockets
            var connectedClient = new ConnectedClient(handler);
            _currentlyConnectedClientsLock.EnterWriteLock();
            try
            {
                _currentlyConnectedClients.Add(connectedClient.Socket,connectedClient);
            }
            finally
            {
                _currentlyConnectedClientsLock.ExitWriteLock();
            }

            // Fire the event if needed
            ClientConnected?.Invoke(this, new ClientConnectedArgs(connectedClient));

            // Create receive buffer queue for this client
            _currentlyConnectedClientsReceiveQueuesLock.EnterWriteLock();
            try
            {
                _currentlyConnectedClientsReceiveQueues.Add(handler, new BlockingQueue<SocketAsyncEventArgs>());
            }
            finally
            {
                _currentlyConnectedClientsReceiveQueuesLock.ExitWriteLock();
            }

            if (!TryUnsafeSocketOperation(handler, SocketAsyncOperation.Receive, _socketAsyncEventArgsReceivePool.Pop()))
            {
                return;
            }

            ProcessReceivedMessage(connectedClient);
        }

        private void SendCallback(Socket socket, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            // Check for error
            if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                HandleCommunicationError(socket, new Exception("Send failed, error = " + socketAsyncEventArgs.SocketError));
            }

            if (socketAsyncEventArgs.Buffer.Length == ProtocolHelper.ControlBytesPlaceholder.Length)
            {
                _socketAsyncEventArgsKeepAlivePool.Push(socketAsyncEventArgs);
                return;
            }

            // Return the memory to buffer
            //PooledMessage.Return(socketAsyncEventArgs.Buffer);
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
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Dispose();
                HandleCommunicationError(socket, new Exception("Receive failed, error = " + socketAsyncEventArgs.SocketError));
            }

            // Get the message state
            int bytesRead = socketAsyncEventArgs.BytesTransferred;

            // Read the data
            if (bytesRead > 0)
            {
                // Add to receive queue
                BlockingQueue<SocketAsyncEventArgs> receiveBufferQueue = null;
                _currentlyConnectedClientsReceiveQueuesLock.EnterReadLock();
                try
                {
                    if (!_currentlyConnectedClientsReceiveQueues.TryGetValue(socket, out receiveBufferQueue))
                    {
                        // Peace out!
                        return;
                    }
                }
                finally
                {
                    _currentlyConnectedClientsReceiveQueuesLock.ExitReadLock();
                }

                receiveBufferQueue.Enqueue(socketAsyncEventArgs);
            }
            else
            {
                // 0 bytes means disconnect
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Dispose();
                HandleCommunicationError(socket, new Exception("Received 0 bytes (graceful disconnect)"));

                _socketAsyncEventArgsReceivePool.Push(socketAsyncEventArgs);
                return;
            }

            socketAsyncEventArgs = _socketAsyncEventArgsReceivePool.Pop();

            // Post a receive to the socket as the client will be continuously receiving messages to be pushed to the queue
            TryUnsafeSocketOperation(socket, SocketAsyncOperation.Receive, socketAsyncEventArgs);
        }

        private void ProcessReceivedMessage(ConnectedClient connectedClient)
        {
            int bytesToRead        = -1;
            int threadId           = -1;
            int availableTest      = 0;
            int controlBytesOffset = 0;
            byte[] protocolBuffer  = new byte[ProtocolHelper.ControlBytesPlaceholder.Length];
            PooledMessage resultBuffer    = null;

            var handler = connectedClient.Socket;

            BlockingQueue<SocketAsyncEventArgs> receiveBufferQueue = null;
            _currentlyConnectedClientsReceiveQueuesLock.EnterReadLock();
            try
            {
                if (!_currentlyConnectedClientsReceiveQueues.TryGetValue(handler, out receiveBufferQueue))
                {
                    // Peace out!
                    return;
                }
            }
            finally
            {
                _currentlyConnectedClientsReceiveQueuesLock.ExitReadLock();
            }

            // Loop until socket is done
            while (_isListening)
            {
                // If the socket is disposed, we're done
                try
                {
                    availableTest = handler.Available;
                }
                catch (ObjectDisposedException)
                {
                    // Peace out!
                    return;
                }

                // Get the next buffer from the queue
                var socketAsyncEventArgs = receiveBufferQueue.Dequeue();
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
                        if (resultBuffer == null) { resultBuffer = PooledMessage.Rent(bytesToRead); }

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

                        bytesToRead = -1;
                        threadId    = -1;

                        connectedClient.LastResponse = DateTime.UtcNow;
                    }
                }

                // Push the buffer back onto the pool
                _socketAsyncEventArgsReceivePool.Push(socketAsyncEventArgs);
            }
        }

        private void CompleteMessage(Socket handler, int threadId, PooledMessage receivedMessage)
        {
            receivedMessage.Socket   = handler;
          //  receivedMessage.ConnectedClient = GetConnectedClient(handler);
            receivedMessage.ThreadId = threadId;

            // Fire the event if needed 
            var messageReceived = MessageReceived;
            if (messageReceived != null)
            {
                // Create the message received args 
                var messageReceivedArgs             = _messageReceivedArgsPool.Pop();
                messageReceivedArgs.ReceivedMessage = receivedMessage;
                // Fire the event 
                messageReceived(this, messageReceivedArgs);
                // Back in the pool
                _messageReceivedArgsPool.Push(messageReceivedArgs);
            }
        }

        /// <summary>
        /// Handles an error in socket communication.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="ex">The exception that the socket raised.</param>
        private void HandleCommunicationError(Socket socket, Exception ex)
        {
            // Close but do not dispose
            CloseSocket(ref socket,false);

            // Remove receive buffer queue
            _currentlyConnectedClientsReceiveQueuesLock.EnterWriteLock();
            try
            {
                if (socket != null)
                {                                        
                    _currentlyConnectedClientsReceiveQueues.Remove(socket);                    
                }
            }
            finally
            {
                _currentlyConnectedClientsReceiveQueuesLock.ExitWriteLock();
            }

            // Try to un-enroll from currently connected client sockets
            _currentlyConnectedClientsLock.EnterWriteLock();
            try
            {
                if (_currentlyConnectedClients.ContainsKey(socket))
                {
                    _currentlyConnectedClients.Remove(socket);
                    ClientDisconnected?.Invoke(this, null);
                }
            }
            finally
            {
                _currentlyConnectedClientsLock.ExitWriteLock();
            }
            // After un-enrollment, dispose the socket
            DisposeSocket(ref socket);

            // Raise the error event 
            var error = Error;
            if (error != null)
            {
                var socketErrorArgs = _socketErrorArgsPool.Pop();
                socketErrorArgs.Exception = ex;
                error(this, socketErrorArgs);
                _socketErrorArgsPool.Push(socketErrorArgs);
            }
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
                if (operation != SocketAsyncOperation.Accept)
                {
                    (socketAsyncEventArgs.UserToken as PooledMessage)?.Dispose();
                    HandleCommunicationError(socket, ex);
                }
                return false;
            }
            catch (ObjectDisposedException)
            {
                // If disposed, handle communication error was already done and we're just catching up on other threads. suppress it.
                (socketAsyncEventArgs.UserToken as PooledMessage)?.Dispose();
                return false;
            }

            return true;
        }


        public async Task<ConnectedClient> WaitForConnectionAsync()
        {
            // Get list of connected clients before 
            var _connectedClients = ConnectedClients.ToList();

            while (true)
            {
                foreach (var connectedClient in ConnectedClients)
                {
                    if (!_connectedClients.Contains(connectedClient)) return connectedClient;
                }
                await Task.Delay(500);

            }
        }


        public async Task<ConnectedClient> WaitForNewClientAsync()
        {
            // Get list of connected clients before 
            var _connectedClients = ConnectedClients.ToList();

            while (true)
            {
                foreach (var connectedClient in ConnectedClients)
                {
                    if (!_connectedClients.Contains(connectedClient)) return connectedClient;
                }
                await Task.Delay(500);

            }
        }

#if (!WINDOWS_UWP)
        public ConnectedClient WaitForNewClient()
        {
            // Get list of connected clients before 
            var _connectedClients = ConnectedClients.ToList();

            while (true)
            {
                foreach (var connectedClient in ConnectedClients)
                {
                    if (!_connectedClients.Contains(connectedClient)) return connectedClient;
                }

                Thread.Sleep(500);

            }
        }
#endif

    }
}