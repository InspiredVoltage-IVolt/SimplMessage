using System;
using SimplMessage;
using System.Net;

namespace SimplSocketsClient
{
    public class ClientWaitForReplyMessage
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
            server.AddCallBack<ClassA>((receivedMessage) =>
            {
                // get data from received message cast to ClassA
                var receivedObject = receivedMessage.GetContent<ClassA>();
                Console.WriteLine($"Server received message: {receivedObject.VarDouble}, {receivedObject.VarInt}");

                // Reply to the message with the same content (echo)
                server.Reply(receivedObject, receivedMessage);
                Console.WriteLine($"Server replied to message");
            });

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
            var objectToSend = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            //Send it
            Console.WriteLine("Client will send message and wait for reply");
            var receivedObject = client.SendReceive<ClassA, ClassA>(objectToSend);
            if (receivedObject == null)
            {
                Console.WriteLine("Client received no answer");
            }
            else
            {
                Console.WriteLine($"Client received answer: {receivedObject.VarDouble}, {receivedObject.VarInt}");
            }        
        }
    }
}