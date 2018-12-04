using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SimplSockets;

namespace SimplSocketsServer
{
    class SocketServer
    {
        public async void Start() => await RunEchoServer();

        static async Task RunEchoServer()
        {

            
            using (var server = new SimplSocketServer(
                () => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    { NoDelay = true }))
            {
                server.ClientConnected += (s, e) =>
                {
                    Console.Out.WriteLine($"client connected: {e.ConnectedClient.IPEndPoint.Address}");
                };

                server.ClientDisconnected += (s, e) =>
                {
                    Console.Out.WriteLine($"client disconnected");
                };


                server.MessageReceived += (s, e) =>
                {
                    var receivedMessage = e.ReceivedMessage;
                    if (server != null)
                    {
                        server.Reply(receivedMessage, receivedMessage);
                    }
                    // We will not dispose the received image directly, since it may still need to be send
                    // Instead we use the ReturnAfterSend, which will dispose the image after sending
                    receivedMessage.ReturnAfterSend();
                    //Console.Out.WriteLine($"messages still rented out: {PooledMessage.GetNoRentedMessages()}");
                };

                server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
                await Console.Out.WriteLineAsync("Server running; type 'q' to exit, anything else to broadcast");

                string line;
                while ((line = await Console.In.ReadLineAsync()) != null)
                {
                    if (line == "q") break;
                }
            }
        }
    }
}
