namespace SimplSocketsServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //var socketServer = new SocketServer();
            //socketServer.Start();
            var messageServer = new MessageServer();
            messageServer.Start();
        }
    }
}
