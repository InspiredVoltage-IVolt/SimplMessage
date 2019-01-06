![logo](SimplMessage.png)
===========

*SimplMessage* is build on top of SimplSocket and facilitates sending and receiving objects of any class type. It also comes with auto-discovery and auto-connection functionality 

*SimplSockets* is a socket library that provides highly efficient, scalable, simple socket communication that allows you to send, receive and reply with binary messages.


Quickstart
===========

To give a sense of what the library does, let's look at a basic example of sending an object from a client to the server

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
            // (you could also implement this as a lambda function)
            server.AddCallBack<ClassA>(ServerReceivedClassACallback);            

            // Start listening for client connections on loopback end point
            server.Listen(new IPEndPoint(IPAddress.Loopback, 5000));
        }

        void CreateClient()
        {
            // Create the client
            var client = new SimplMessageClient();

            // Automatically discover the server and connect
            client.AutoConnect();

            // Wait until the connection is actually made
            client.WaitForConnection();

            // Create an object to send
            var objectToSend  = new ClassA() { VarInt = 2, VarDouble = 2.5 };

            // Send the object
            client.Send(objectToSend);
            
            // Done!
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

That's it! The Wiki goes into more depth on different topics:
* [Connecting clients to server](../../wiki/Connecting) 
* [Sending raw bytes using SimplSockets](../../wiki/Sending-bytes) 
* [Receiving raw bytes using SimplSockets](../../wiki/Receiving-bytes) 
* [Sending objects using SimplMessage](../../wiki/Sending-objects) 
* [Receiving objects using SimplMessage](../../wiki/Receiving-objects) 
* [Using SimplMessage & SimplSockets in Unity](../../wiki/Unity-3D-support) 
* [Asynchronous usage with async and await](../../Asynchronous-usage-with-async-and-await) 
Credits
===========

This library is forked from [SimplSockets](https://github.com/haneytron/simplsockets) (MIT) by [David Haney](https://github.com/haneytron). It has been significantly refactored, optimized and extended, but a lot, if not most, of the code is based on that library. 

The serialization and deserialization of objects is done using [msgpack-cli](https://github.com/msgpack/msgpack-cli) (Apache) by [Yusuke Fujiwara](https://github.com/yfakariya).

The server discovery code is done using [Beacon](https://github.com/rix0rrr/beacon) (MIT) by [Rico Huijbers](https://github.com/rix0rrr)




