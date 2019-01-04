using System;
using System.Net;
using SimplSockets;

namespace SimplSocketsClient
{
    public class ServerSendBytes
    {
        public void Start()
        {            
            CreateClient();
            CreateServer();
        }

        void CreateServer()
        {
            // Create the server
            var server = new SimplSocketServer();

            // Start listening for client connections on loopback end poiny
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));

            // Wait until a new client has connected
            var connectedClient = server.WaitForNewClient();
            
            // Create a byte array to send
            var arrayToSend1 = new byte[1000];
            // Send it
            Console.WriteLine($"Server is going to send message of {arrayToSend1.Length} bytes");
            server.Send(arrayToSend1, connectedClient);


            // Get a byte array from the memory pool
            var arrayToSend2 = PooledMessage.Rent(1000);
            Console.WriteLine($"Server is going to send a pooled message of {arrayToSend1.Length} bytes");
            server.Send(arrayToSend2, connectedClient);

            // Return message to pool
            arrayToSend2.ReturnAfterSend();
        }

        void CreateClient()
        {
            // Create the client
            var client = new SimplSocketClient();

            // Make the client connect automatically
            client.AutoConnect();

            // Create a callback for received data 
            client.MessageReceived += (s, e) =>
            {
                // Get the message
                var receivedMessage = e.ReceivedMessage;
                Console.WriteLine($"Client received message of {receivedMessage.Length} bytes");

                // Return the message to the pool
                receivedMessage.Return();
            };            
        }
    }
}