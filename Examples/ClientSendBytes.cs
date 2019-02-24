using System;
using System.Net;
using SimplSockets;

namespace SimplSocketsClient
{
    public class ClientSendBytes
    {
        public void Start()
        {
            CreateServer();
            CreateClient();     
        }
   
        void CreateServer()
        {
            // Create the server
            var server = new SimplSocketServer();

            // Create a callback for received array
            server.MessageReceived += (s, e) =>
            {
                // Get the message
                var receivedMessage = e.ReceivedMessage;
                Console.WriteLine($"Server received message of {receivedMessage.Length} bytes");

                // Return the message to the pool
                receivedMessage.Return();

            };
            // Start listening for client connections on loopback endpoint
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
        }

        void CreateClient()
        {
            // Create the client
            var client = new SimplSocketClient();

            // Make the client connect automatically
            client.AutoConnect();

            // Wait until the connection is actually made
            client.WaitForConnection();

            // Create a byte array to send
            var arrayToSend1  = new byte[1000];
            // Send it
            client.Send(arrayToSend1);

            // Get a byte array from the memory pool
            var arrayToSend2 = PooledMessage.Rent(1000);
            client.Send(arrayToSend2);

            // We will not dispose the message directly, since it may still need to be send
            // Instead we use the ReturnAfterSend, which will return the message to the pool after sending
            arrayToSend2.ReturnAfterSend();
        }


    }
}