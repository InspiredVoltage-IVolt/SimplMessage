using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SimplMessage;

namespace SimplSocketsServer
{
    public class TestClassA
    {
        public int VarInt;
        public double VarDouble;
    }

    public class TestClassB
    {
        public string VarString;
    }

    public class TestClassC
    {
        public string VarString;
    }

    public class MessageServer
    {
        private static SimplMessageServer _server;

        public async void Start() => await RunEchoServer();

        async Task RunEchoServer()
        {
            _server = new SimplMessageServer(
                () => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    { NoDelay = true });
            {
                _server.ClientConnected += (s, e) =>
                {
                    Console.Out.WriteLine($"client connected           : {e.ConnectedClient.IPEndPoint.Address}");
                    Console.Out.WriteLine($"Number of connected clients: {_server.ConnectedClients.Count}");
                };

                _server.ClientDisconnected += (s, e) =>
                {
                    Console.Out.WriteLine($"client disconnected");
                    Console.Out.WriteLine($"Number of connected clients: {_server.ConnectedClients.Count}");
                };

                _server.AddCallBack<TestClassA>(TestClassACallback);
                _server.AddCallBack<TestClassB>(TestClassBCallback);
                _server.AddCallBack<TestClassC>(TestClassCCallback);
                _server.AddCallBack(GenericCallback);

                _server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
                await Console.Out.WriteLineAsync("Server running; type 'q' to exit, anything else to broadcast");

                string line;
                while ((line = await Console.In.ReadLineAsync()) != null)
                {
                    if (line == "q") break;
                }
            }
        }

        private void GenericCallback(ReceivedMessage receivedMessage)
        {
            Log($"Unknown ");
            _server.Reply(receivedMessage, receivedMessage);
        }
      
        private void TestClassACallback(ReceivedMessage receivedMessage)
        {
            var message = receivedMessage.GetContent<TestClassA>();
            Log($"VarInt: {message.VarInt} VarDouble: {message.VarDouble}");
            _server.Reply(message, receivedMessage);
        }

        private void TestClassBCallback(ReceivedMessage receivedMessage)
        {
            var message = receivedMessage.GetContent<TestClassB>();
            //var message = SimplMessageServer.GetMessage<TestClassB>(receivedMessage.Bytes);
            Log($"VarString: {message.VarString}");
            _server.Reply(message, receivedMessage);
        }

        private void TestClassCCallback(ReceivedMessage receivedMessage)
        {
            var message = receivedMessage.GetContent<TestClassC>();
            Log($"VarString: {message.VarString}");
            _server.Reply(message, receivedMessage);
        }

        public void Log(string output)
        {
            Console.Out.WriteLine(output);
        }
    }
}
