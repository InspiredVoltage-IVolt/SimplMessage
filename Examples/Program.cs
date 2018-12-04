namespace SimplSocketsClient
{
    class Program
    {
        static void Main(string[] args)
        {

            var example = new ClientSendMessage();
            //var example = new EchoServerMessage();
            example.Start();


        }
    }
}
