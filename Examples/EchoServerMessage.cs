using System;
using SimplMessage;
using System.Net;

namespace SimplSocketsClient
{
    public class EchoServerMessage
    {
        private SimplMessageServer _server;
        private SimplMessageClient _client;

        public void Start()
        {
            CreateServer();
            CreateClient();
            Console.ReadLine();
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
            _server = new SimplMessageServer();

            // Create a callback for received data of class A
            _server.AddCallBack<ClassA>(ServerReceivedClassACallback);

            // Start listening for client connections 
            _server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
        }

        void CreateClient()
        {
            // Create the client
            _client = new SimplMessageClient();

            // Add a callback that will be triggered when a message from the server is received
            _client.AddCallBack      <ClassB>(ClientReceivedClassBCallback);

            // Indicate when connected
            _client.Connected += (s, e) => { 
                Console.WriteLine($"The client has connected to {e.IPEndPoint.Address}!");
            };

            // Indicate when disconnected
            _client.Disconnected += (s, e) =>
            {
                Console.WriteLine("The client has disconnected!");
            };

            // Make the client connect automatically
            _client.AutoConnect();

            // Wait until the connection is actually made
            _client.WaitForConnection();

            // Create an object to send
            var objectToSend  = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            // Send it
            _client.Send(objectToSend);            
        }

        private void ServerReceivedClassACallback(ReceivedMessage receivedMessage)
        {
            // get data from received message
            var receivedObject = receivedMessage.GetContent<ClassA>();

            // Indicate that the server received data
            Console.WriteLine($"Server received message: {receivedObject.VarDouble}, {receivedObject.VarInt}");

            // Create a response object, based on the received message
            var sendMessage = new ClassB() {VarString = $"{receivedObject.VarDouble}, {receivedObject.VarInt}"};
           
            // Reply to received message 
            _server.Reply(sendMessage, receivedMessage);
        }

        private void ClientReceivedClassBCallback(ReceivedMessage receivedMessage)
        {
            // Unpack and display the data in the received class
            var message = receivedMessage.GetContent<ClassB>();
            Console.WriteLine($"Client received message: {message.VarString}");
        }
    }
}