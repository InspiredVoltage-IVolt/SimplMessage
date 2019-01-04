using System;
using SimplMessage;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SimplSocketsClient
{
    public class ConnectionClientServer
    {
        private SimplMessageServer _server;
        private SimplMessageClient _client;

        public void Start()
        {
            CreateServer();
            CreateClient();
        }

        // Define a class to be send
        public class ClassA
        {
            public int    VarInt;
            public double VarDouble;
        }

        // Define a class to receive
        public class ClassB
        {
            public string VarString;
        }

        void CreateServer()
        {
            // Create the server           
            _server = new SimplMessageServer(
                () => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                keepAlive            : true,
                messageBufferSize    : 65536,
                communicationTimeout : 10000,
                maxMessageSize       : 10485760,
                useNagleAlgorithm    : false);

            // Create a callback for received data of class A
            _server.AddCallBack<ClassA>(ServerReceivedClassACallback);

            // Indicate when a client has connected
            _server.ClientConnected += (s, e) => {
                Console.WriteLine($"Server: connected from {e.ConnectedClient.IPEndPoint}");
            };

            // Indicate when a client has disconnected
            _server.ClientDisconnected += (s, e) =>
            {
                Console.WriteLine($"Server: a client disconnected");
            };

            // Start listening for client connections 
            _server.Listen(
                ipEndPoint: new IPEndPoint(IPAddress.Loopback, 5000),
                discoverable: true,
                name: "ServerName",
                description: "Description of server");
        }

        void CreateClient()
        {
            // Create the client
            _client = new SimplMessageClient(
                () => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                keepAlive            : true,
                messageBufferSize    : 65536,
                communicationTimeout : 10000,
                maxMessageSize       : 10485760,
                useNagleAlgorithm    : false);


            // Add a callback that will be triggered when a message from the server is received
            _client.AddCallBack      <ClassB>(ClientReceivedClassBCallback);

            // Indicate when connected
            _client.Connected += (s, e) => { 
                Console.WriteLine($"Client: connected to {e.IPEndPoint.Address}!");
            };

            // Indicate when disconnected
            _client.Disconnected += (s, e) =>
            {
                 //Disconnected not triggered, due to botched wasconnected logic
                Console.WriteLine("Client: disconnected");
            };

            // Indicate when trying to connect
            _client.ConnectionAttempt += (s, e) =>
            {
                Console.WriteLine($"Client: trying to connect to {e.IPEndPoint.Address}!");
            };

            // Make the client connect automatically
            _client.AutoConnect("ServerName");

            // Wait until the connection is actually made
            _client.WaitForConnection();

            // Create an object to send
            var objectToSend  = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            // Send it
            Console.WriteLine($"Client: sending message: {objectToSend.VarDouble}, {objectToSend.VarInt}");
            _client.Send(objectToSend);

            // Wait some time for the server to respond.
            // This is needed, because if the server responds while the client is not connected, the respons is lost
            // there is no resend functionality
            Thread.Sleep(100);

            // Change mode to fixed IP auto connection
            _client.AutoConnect(new IPEndPoint(IPAddress.Loopback, 5000));

            // Now disconnect the previous connection
            _client.Disconnect();

            // Wait until the new connection is actually made based on the fixed IP
            _client.WaitForConnection();

            // Send the object again
            Console.WriteLine($"Client: sending message: {objectToSend.VarDouble}, {objectToSend.VarInt}");
            _client.Send(objectToSend);

            // Wait some time for the server to respond
            Thread.Sleep(100);

            // Change to manual connection, this will disable autoconnecting                        
            _client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));

            // The previous function should block until the connection is made, so we will not wait, but just check
            if (_client.IsConnected())
            {
                 Console.WriteLine("Client: Connected");
            }
            else
            {
                Console.WriteLine("Client: not connected, should not happen!");
            }

            // Send the object again
            Console.WriteLine($"Client: sending message: {objectToSend.VarDouble}, {objectToSend.VarInt}");
            _client.Send(objectToSend);

            // Wait some time for the server to respond
            Thread.Sleep(100);

            // Now disconnect the previous connection
            _client.Disconnect();

            // Since we have disabled the auto connect, there should be no reconnection,
            // so we'll add a timeout to prevent hanging
            bool connected = _client.WaitForConnection(TimeSpan.FromSeconds(10));
            if (connected)
            {
                Console.WriteLine("Client: Connected, even though we disabled autoconnect!");
            }
            else
            {
                Console.WriteLine("Client: did not reconnect, because we disabled autoconnect");
            }
        }

        private void ServerReceivedClassACallback(ReceivedMessage receivedMessage)
        {
            // get data from received message
            var receivedObject = receivedMessage.GetContent<ClassA>();

            // Indicate that the server received data
            Console.WriteLine($"Server: received message {receivedObject.VarDouble}, {receivedObject.VarInt}");

            // Create a response object with string content, based on the received message
            var sendMessage = new ClassB() {VarString = $"{receivedObject.VarDouble}, {receivedObject.VarInt}"};

            // Reply to received message 
            Console.WriteLine($"Server: replying to client {sendMessage.VarString}");
            _server.Reply(sendMessage, receivedMessage);
        }

        private void ClientReceivedClassBCallback(ReceivedMessage receivedMessage)
        {
            // Unpack and display the data in the received class
            var message = receivedMessage.GetContent<ClassB>();
            Console.WriteLine($"Client: received message: {message.VarString}");
        }
    }
}