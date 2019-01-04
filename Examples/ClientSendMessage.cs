using System;
using SimplMessage;
using System.Net;

namespace SimplSocketsClient
{
    public class ClientSendMessage
    {
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

   
        void CreateServer()
        {
            // Create the server
            var server = new SimplMessageServer();

            // Create a callback for received data of type classA
            // (You could also implement this as a lambda function)
            server.AddCallBack<ClassA>(ServerReceivedClassACallback);

            // Create a callback for received data of type classA with a custom identifier
            server.AddCallBack("ObjectOfTypeClassA",ServerReceivedClassACallback);
        
            // Start listening for client connections on loopback end point
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
        }

        void CreateClient()
        {
            // Create the client
            var client = new SimplMessageClient();

            // Make the client connect automatically
            client.AutoConnect();

            // Wait until the connection is actually made
            client.WaitForConnection();

            // Create an object to send
            var objectToSend  = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            // Send it with an implicit descriptor (which is the class name)
            Console.WriteLine($"Client sending received message: {objectToSend.VarDouble}, {objectToSend.VarInt} with implicit descriptor {typeof(ClassA).Name}");
            client.Send(objectToSend);

            // Send it with a custom descriptor
            Console.WriteLine($"Client sending received message: {objectToSend.VarDouble}, {objectToSend.VarInt} with descriptor ObjectOfTypeClassA");
            client.Send("ObjectOfTypeClassA",objectToSend);            
        }

        private void ServerReceivedClassACallback(ReceivedMessage receivedMessage)
        {
            // get data from received message
            var receivedObject = receivedMessage.GetContent<ClassA>();

            // Notify that the server received data
            Console.WriteLine($"Server received message: {receivedObject.VarDouble}, {receivedObject.VarInt}");
        }

    }
}