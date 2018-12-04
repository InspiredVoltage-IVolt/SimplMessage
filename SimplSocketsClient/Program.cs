namespace SimplSocketsClient
{
    class Program
    {
        static void Main(string[] args)
        {
            //var socketClient = new SocketClient();
            //socketClient.Start();
            var messageClient = new MessageClient();
            messageClient.Start();
        }
    }
}
