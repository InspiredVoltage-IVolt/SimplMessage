using System;
using System.Net;
using SimplSockets;

namespace SimplSocketsClient
{
    public class ClientWaitForReplyBytes
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

                // Reply to the message with the same message (echo)
                server.Reply(receivedMessage, receivedMessage);
                Console.WriteLine($"Server replied to message");


                // We cannot not dispose the received message directly, since it may still need to be send
                // Instead we use the ReturnAfterSend, which will return the message to the pool after sending
                receivedMessage.ReturnAfterSend();

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
            var messageToSend  = new byte[1000];

            // Send it
            Console.WriteLine("Client will send message and wait for reply");
            var receivedMessage = client.SendReceive(messageToSend);
            if (receivedMessage == null)
            {
                Console.WriteLine("Client received no answer"); 
            }
            else
            {
                Console.WriteLine($"Client received answer of {receivedMessage.Length} bytes");

                // Return message to pool
                receivedMessage.Return();
            }    
                       

        }
    }
}