SimplMessage & SimplSockets
===========

SimplSockets is a socket library that provides highly efficient, scalable, simple socket communication that allows you to send, receive and reply with binary messages. It also comes with auto-discovery and auto-connection functionality 

SimplMessage is build on top of SimplSocket and facilitates sending and receiving objects of any class type.


Quickstart
===========

Let's start with a basic example of sending an object from a client to the server

```csharp
using System;
using SimplMessage;
using System.Net;

namespace SimplSocketsClient
{
    public class ClientSendMessage
    {
        public void Start()
        {
            CreateServer();
            CreateClient();
            Console.ReadLine();            
        }

        // Define a class to be send
        public class ClassA
        {
            public int    VarInt;
            public double VarDouble;
        }
   
        void CreateServer()
        {
            // Create the server
            var server = new SimplMessageServer();

            // Create a callback for received data of type classA
            server.AddCallBack<ClassA>(ServerReceivedClassACallback);

            // You could also implement this as a lambda function:

            // Start listening for client connections on loopback end poiny
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
        }

        void CreateClient()
        {
            // Create the client
            var client = new SimplMessageClient();

            // Make the client connect automatically
            client.AutoConnect();

            // Wait until the connection is actually made
            client.WaitForConnection();

            // Create an object to send
            var objectToSend  = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            // Send it
            client.Send(objectToSend);            
        }

        // Function is called when an object of type ClassA is received 
        private void ServerReceivedClassACallback(ReceivedMessage receivedMessage)
        {
            // get data from received message
            var receivedObject = receivedMessage.GetContent<ClassA>();

            // Notify that the server received data
            Console.WriteLine($"Server received message: {receivedObject.VarDouble}, {receivedObject.VarInt}");
        }

    }
}
```


Credits
===========

This work is forked from simplSockets, A spinoff library of Dache that provides highly efficient, scalable, simple socket communication. http://www.dache.io

It has been significantly refactored, optimized and extended, but still a lot of the code is based on this library.
