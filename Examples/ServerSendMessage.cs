using System;
using SimplMessage;
using System.Net;

namespace SimplSocketsClient
{
    public class ServerSendMessage
    {
        public void Start()
        {
            CreateClient();
            CreateServer();                                    
        }

        // Define a class to be send
        public class ClassA
        {
            public int    VarInt;
            public double VarDouble;
        }
  
        void CreateServer()
        {
            // Create the server
            var server = new SimplMessageServer();

            // Start listening for client connections on loopback end point
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));

            // Wait until a new client has connected
            var connectedClient = server.WaitForNewClient();

            // Create an object to send
            var objectToSend = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            // Send it with an implicit descriptor (which is the class name)
            Console.WriteLine($"server sending received message: {objectToSend.VarDouble}, {objectToSend.VarInt} with implicit descriptor {typeof(ClassA).Name}");
            server.Send(objectToSend, connectedClient);
        }

        void CreateClient()
        {
            // Create the client
            var client = new SimplMessageClient();

            // Make the client connect automatically
            client.AutoConnect();

            // Create a callback for received data of type classA. This time we use a lambda function instead 
            client.AddCallBack<ClassA>((receivedMessage) =>
            {
                // get data from received message cast to ClassA
                var receivedObject = receivedMessage.GetContent<ClassA>();

                // Notify that the server received data
                Console.WriteLine($"Client received message: {receivedObject.VarDouble}, {receivedObject.VarInt}");
            });
        }

    }
}