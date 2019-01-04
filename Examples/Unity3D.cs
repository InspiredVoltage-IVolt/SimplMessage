using System;
using SimplMessage;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace SimplSocketsClient
{
    public class Unity3D
    {
        private ClientMonoBehaviour     _clientMonoBehaviour;
        private GameObjectMonoBehaviour _gameObjectMonoBehaviour;

        public void Start()
        {
            CreateClient();
            CreateServer();
        }

        /// Simulates calling of the Unity Monobehaviour classes
        async void CreateClient()
        {
            // Create the Unity objects
            _clientMonoBehaviour     = new ClientMonoBehaviour();
            _gameObjectMonoBehaviour = new GameObjectMonoBehaviour();

            // Simulate Unity start call
            _clientMonoBehaviour.Start();
            _gameObjectMonoBehaviour.Start();

            // Simulate Unity update call loop
            await Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(16); // 60 FPS
                    _clientMonoBehaviour.Update();
                    _gameObjectMonoBehaviour.Update();
                }
            });
        }

        void CreateServer()
        {
            // Create the server
            var server = new SimplMessageServer();

            // Start listening for client connections on loopback end point
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));

            // Wait until a new client has connected
            var connectedClient = server.WaitForNewClient();

            // Create an object to send
            var objectToSend = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            // Send it with an implicit descriptor (which is the class name)
            Console.WriteLine($"server sending received message: {objectToSend.VarDouble}, {objectToSend.VarInt} with implicit descriptor {typeof(ClassA).Name}");
            server.Send(objectToSend, connectedClient);
        }
    }

    // Define a class to be send
    public class ClassA
    {
        public int VarInt;
        public double VarDouble;
    }

    /// Example of a Unity class that derives from MonoBehaviour,
    /// This class implements a global client
    public class ClientMonoBehaviour
    {
        private SimplMessageClient _client;

        public void Start()
        {
            // Get the singleton instance of SimplMessageClient
            _client = SimplMessageClient.Instance;

            // Make the client connect automatically
            _client.AutoConnect();
        }

        public void Update()
        {
            // Go through all messages that are queued and process them during the Update loopfunction
            _client.UpdateCallbacks();
        }
    }

    /// Example of a Unity class that derives from MonoBehaviour,
    /// This object is typically connected to a GameObject
    public class GameObjectMonoBehaviour
    {
        private SimplMessageClient _client;

        public void Start()
        {
            // Get the singleton instance of SimplMessageClient
            _client = SimplMessageClient.Instance;

            // This callback is triggered when a the UpdateCallbacks() function is being run
            _client.AddUpdateCallBack<ClassA>((receivedMessage) =>
            {
                // Get data from received message cast to ClassA
                var receivedObject = receivedMessage.GetContent<ClassA>();
                Console.WriteLine($"Client received message: {receivedObject.VarDouble}, {receivedObject.VarInt}");
            });
        }

        public void Update()
        {
            // Do something a game object does
        }
    }
}