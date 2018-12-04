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
            Console.ReadLine();            
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
            server.AddCallBack<ClassA>(ServerReceivedClassACallback);

            // You could also implement this as a lambda function:
            /*
            server.AddCallBack<ClassA>((receivedMessage) =>
            {
                // get data from received message cast to ClassA
                var receivedObject = receivedMessage.GetContent<ClassA>();

                // Notify that the server received data
                Console.WriteLine($"Server received message: {receivedObject.VarDouble}, {receivedObject.VarInt}");
            });
            */
            // Start listening for client connections on loopback end poiny
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

            // Send it
            client.Send(objectToSend);            
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