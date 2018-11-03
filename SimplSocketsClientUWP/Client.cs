using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Windows.System.Threading;
using SimplSockets;

namespace SimplSocketsClientUWP
{
    internal class Client
    {
        private readonly MainPage _mainPage;

        public Client(MainPage mainPage)
        {
            _mainPage = mainPage;
            
        }

        public void Log(string output)
        {
            var task = ThreadPool.RunAsync(operation => UIDispatcher.Execute(() => _mainPage.Log(output)));
        }

        void RunBenchmarks()
        {
            // Fill randomData array


            var client = new SimplSocketClient(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true });
            {
                // subscribe to broadcasts
                // client.MessageReceived += async (s, e) => await WriteLineAsync('*', e.ReceivedMessage.Message);
                SendBenchmark(client);
                SendFromPoolBenchmark(client);
                SendFromPoolAndReceiveBenchmark(client);
                SendAndReceiveBenchmark(client);
                
            }
        }

        private void SendBenchmark(SimplSocketClient client)
        {

            Log("*** SendBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new int[] { 1, 10, 100, 1000 };
            for (var test = 0; test < 4; test++)
            {
                byte[] randomData = new byte[bufferSizes[test] * 512];
                rnd.NextBytes(randomData);

                var countPerIteration = 100;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < countPerIteration; i++)
                {
                    if (!client.IsConnected())
                    {
                        client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    }

                    client.Send(randomData);
                }

                watch.Stop();
                var speed = (countPerIteration * randomData.Length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{randomData.Length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
            }
        }

        private void SendFromPoolBenchmark(SimplSocketClient client)
        {

            Log("*** SendFromPoolBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new int[] { 1, 10, 100, 1000 };
            for (var test = 0; test < 4; test++)
            {
                var randomData = Message.Rent(bufferSizes[test] * 512);
                rnd.NextBytes(randomData.Content);

                var countPerIteration = 100;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < countPerIteration; i++)
                {
                    if (!client.IsConnected())
                    {
                        client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    }

                    client.Send(randomData);
                }

                watch.Stop();
                var speed = (countPerIteration * randomData.Length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{randomData.Length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
            }
        }
        private void SendAndReceiveBenchmark(SimplSocketClient client)
        {
            Log("*** SendAndReceiveBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new int[] { 1, 10, 100, 1000 };
            for (var test = 0; test < 4; test++)
            {
                byte[] randomData = new byte[bufferSizes[test] * 512];
                rnd.NextBytes(randomData);

                var countPerIteration = 100;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < countPerIteration; i++)
                {
                    if (!client.IsConnected())
                    {
                        client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    }

                    var response = client.SendReceive(randomData);
                }

                watch.Stop();
                var speed = (countPerIteration * randomData.Length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{randomData.Length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
            }
        }

        private void SendFromPoolAndReceiveBenchmark(SimplSocketClient client)
        {
            Log("*** SendFromPoolAndReceiveBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new int[] { 1, 10, 100, 1000 };
            for (var test = 0; test < 4; test++)
            {
                var randomData = Message.Rent(bufferSizes[test] * 512);
                rnd.NextBytes(randomData.Content);

                var countPerIteration = 100;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < countPerIteration; i++)
                {
                    if (!client.IsConnected())
                    {
                        client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    }

                    var response = client.SendReceive(randomData);
                }

                watch.Stop();
                var speed = (countPerIteration * randomData.Length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{randomData.Length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
            }
        }

        private static string ScaledSpeed(double bps)
        {
            const int KB = 1024;
            const int MB = 1024 * 1024;
            const int GB = 1024 * 1024 * 1024;
            if (bps > GB) { return $"{bps / GB:0.000} GB/s"; }
            if (bps > MB) { return $"{bps / MB:0.000} MB/s"; }
            if (bps > KB) { return $"{bps / KB:0.000} KB/s"; }
            return $"{bps:0.0} B/s";
        }

        public void Start()
        {
            RunBenchmarks();
        }
    }
}