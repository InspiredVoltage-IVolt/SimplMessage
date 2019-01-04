using System;
using SimplMessage;
using System.Net;
using System.Threading.Tasks;

namespace SimplSocketsClient
{
    public class AsyncMessage
    {
        private SimplMessageServer _server;
        private SimplMessageClient _client;

        public void Start()
        {
            Console.WriteLine($"Starting sync part of application");
            Task.Run(async () => { await StartAsync(); }).GetAwaiter().GetResult();
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"Starting async part of application");

            // Create Client
            var clientCreated = CreateClient();
            var serverCreated = CreateServer();

            // Now await the client being connected to the server. This can only occur if the server has been created
            await clientCreated;

            // Now await the server being connected to a client. 
            await serverCreated;

            // Send data
            await SendData();
        }



        // Define a class to be send
        public class ClassA
        {
            public int    VarInt;
            public double VarDouble;
        }


        async Task CreateServer()
        {
            // Create the server
            _server = new SimplMessageServer();

            // Create a callback for received data of type classA
            _server.AddCallBack<ClassA>((receivedMessage) =>
            {
                // get data from received message cast to ClassA
                var receivedObject = receivedMessage.GetContent<ClassA>();
                Console.WriteLine($"Server: received message {receivedObject.VarDouble}, {receivedObject.VarInt}");

                // Reply to the message with the same content (echo)
                _server.Reply(receivedObject, receivedMessage);
                Console.WriteLine($"Server: replied to message");
            });

            // Start listening for client connections on loopback end point            
            _server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));


            // Wait until a new client has connected
            Console.WriteLine($"Server: awaiting connection from new client");
            var connectedClient = await _server.WaitForNewClientAsync();

            Console.WriteLine($"Server: a client has connected");
        }

        async Task CreateClient()
        {
            // Create the client
            _client = new SimplMessageClient();

            // Make the client connect automatically
            _client.AutoConnect();

            // Wait until the connection is actually made
            // Because this is done asynchronously so the CreateClient 
            // Method will be exited so that the server can be created

            Console.WriteLine($"Client: awaiting connection to server");
            await _client.WaitForConnectionAsync();

            Console.WriteLine($"Client: connected to server ");

      
        }

        async Task SendData()
        {
            // Create an object to send
            var objectToSend = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            //Send the data, but not wait for reply
            Console.WriteLine("Client: send message but do not wait for reply");
            var ReceiveTask = _client.SendReceiveAsync<ClassA, ClassA>(objectToSend);

            // Do a bit of work
            Console.WriteLine("Client: do some work");
            await Task.Delay(1000);

            // Await reply
            Console.WriteLine("Client: await reply");
            var receivedObject = await ReceiveTask;

            if (receivedObject == null)
            {
                Console.WriteLine("Client: received no answer");
            }
            else
            {
                Console.WriteLine($"Client: received answer: {receivedObject.VarDouble}, {receivedObject.VarInt}");
            }
        }
    }
}