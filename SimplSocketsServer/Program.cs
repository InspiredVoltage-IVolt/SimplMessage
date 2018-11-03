using SimplSockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DemoServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await RunViaSockets();
        }

        static async Task RunViaSockets()
        {
            using (var server = new SimplSocketServer(
                () => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                { NoDelay = true }))
            {
                server.MessageReceived += (s, e) =>
                {
                    var blob = e.ReceivedMessage.Message;
                    if (server != null)
                    {
                        server.Reply(blob, e.ReceivedMessage);                        
                    }
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
