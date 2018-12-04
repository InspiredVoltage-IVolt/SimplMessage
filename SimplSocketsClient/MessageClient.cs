using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SimplMessage;

namespace SimplSocketsClient
{
    public class MessageClient
    {
        public void Start()
        {
            RunMessageTests();
            Console.ReadLine();
        }

        public class TestClassA
        {
            public int    VarInt;
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

        void RunMessageTests()
        {
            // Fill randomData array

            using (var client = new SimplMessageClient(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                { NoDelay = true }))
            {
                client.AddCallBack      <TestClassA>(TestClassACallback);
                client.AddCallBack      <TestClassB>(TestClassBCallback);
                client.AddUpdateCallBack<TestClassC>(TestClassCCallback);
                client.AddCallBack(GenericCallback);

                client.Connected += (s, e) =>
                {
                    Console.WriteLine($"The client has connected to {e.IPEndPoint.Address}!");
                };
                client.Disconnected += (s, e) =>
                {
                    Console.WriteLine("The client has disconnected!");
                };

                client.ConnectionAttempt += (s, e) =>
                {
                    Console.WriteLine($"The client is trying to connect to {e.IPEndPoint.Address}!");
                };



                if (!client.IsConnected())
                {
                    //client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    //client.AutoConnect(new IPEndPoint(IPAddress.Loopback, 5000));
                    client.AutoConnect();
                }

                client.WaitForConnection();


                var testClassA  = new TestClassA() { VarInt = 2, VarDouble = 2.5 };
                
                var outputClass = client.SendReceive<TestClassA, TestClassA>(testClassA);
                if (outputClass == null)
                {
                    Log("No answer received");
                    return;
                }
                else
                {
                    Log($"VarInt: {outputClass.VarInt} VarDouble: {outputClass.VarDouble}");
                } 

                if (testClassA.VarInt != outputClass.VarInt) { Log("class.VarString not same"); }
                if (Math.Abs(testClassA.VarDouble - outputClass.VarDouble) > 1e-6) { Log("class.b not same"); }

                client.Send<TestClassA>(testClassA);

                var testClassB = new TestClassB () { VarString = "TestString" };
                client.Send<TestClassB>(testClassB);
                Thread.Sleep(1000);


                var testClassB2 = new TestClassC() { };
                for (int i = 0; i < 100; i++)
                {
                    testClassB2.VarString = $"object no: {i}";
                    client.Send<TestClassC>(testClassB2);
                }
                Log("Wait for all replies");
                Thread.Sleep(10000);
                Log("Now processing callbacks");
                client.UpdateCallbacks();
                Log("Done");
            }
        }

        private void GenericCallback(ReceivedMessage receivedMessage)
        {
            Log($"Unknown ");
        }

        private void TestClassBCallback(ReceivedMessage receivedMessage)
        {
            var message = receivedMessage.GetContent<TestClassB>();
            Log($"VarString: {message.VarString}");
        }

        private void TestClassACallback(ReceivedMessage receivedMessage)
        {
            var message = receivedMessage.GetContent<TestClassA>();
            Log($"VarInt: {message.VarInt} VarDouble: {message.VarDouble}");
        }

        private void TestClassCCallback(ReceivedMessage receivedMessage)
        {
            var message = receivedMessage.GetContent<TestClassC>();
            Log($"VarString: {message.VarString}");
        }

        public void Log(string output)
        {
            Console.Out.WriteLine(output);
        }
    }
}