using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using SimplSockets;

namespace SimplSocketsClient
{
    public class Client
    {
        public void Start()
        {
            RunBenchmarks();
        }

        void RunBenchmarks()
        {
            // Fill randomData array

            using (var client = new SimplSocketClient(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            { NoDelay = true }))
            {
                // subscribe to broadcasts
                // client.MessageReceived += async (s, e) => await WriteLineAsync('*', e.ReceivedMessage.PooledMessage);

                SendAndReceiveCheck(client);
                SendFromPoolAndReceiveCheck(client);

                SendBenchmark(client);
                SendFromPoolBenchmark(client);
                SendFromPoolAndReceiveBenchmark(client);
                SendAndReceiveBenchmark(client);

                SendBenchmark(client);
                SendFromPoolBenchmark(client);
                SendFromPoolAndReceiveBenchmark(client);
                SendAndReceiveBenchmark(client);
                Console.ReadLine();
            }
        }


        private void SendBenchmark(SimplSocketClient client)
        {

            Log("*** SendBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new[] { 1, 10, 100, 1000 };
            var length = 0;
            for (var test = 0; test < 4; test++)
            {
                byte[] randomData = new byte[bufferSizes[test] * 512];
                length += randomData.Length;
                rnd.NextBytes(randomData);

                var countPerIteration = 100;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < countPerIteration; i++)
                {
                    randomData = new byte[bufferSizes[test] * 512];

                    if (!client.IsConnected())
                    {
                        client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    }

                    client.Send(randomData);
                }

                watch.Stop();
                var speed = (countPerIteration * length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
            }
        }

        private void SendFromPoolBenchmark(SimplSocketClient client)
        {

            Log("*** SendFromPoolBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new[] { 1, 10, 100, 1000 };
            var length = 0;            
            for (var test = 0; test < 4; test++)
            {
                var randomData = PooledMessage.Rent(bufferSizes[test] * 512);
                length += randomData.Length;
                rnd.NextBytes(randomData.Content);

                var countPerIteration = 100;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < countPerIteration; i++)
                {
                    randomData = PooledMessage.Rent(bufferSizes[test] * 512);
                    if (!client.IsConnected())
                    {
                        client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    }

                    client.Send(randomData);
                    randomData.ReturnAfterSend();
                }

                watch.Stop();
                

                var speed = (countPerIteration * length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
                
            }
        }

        private void SendAndReceiveCheck(SimplSocketClient client)
        {
            Console.Out.WriteLine("*** SendAndReceiveCheck ***");
            var rnd = new Random();

            byte[] randomData = new byte[1000 * 512];
            rnd.NextBytes(randomData);

            if (!client.IsConnected())
            {
                client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
            }

            var outputData = client.SendReceive(randomData);
            var same = true;
            for (int i = 0; i < outputData.Length; i++)
            {
                if (outputData[i] != randomData[i])
                {
                    same = false;
                    break;
                }
            }

            Log(same?"data is same":"Data is not the same");           
        }

        private void SendFromPoolAndReceiveCheck(SimplSocketClient client)
        {
            Console.Out.WriteLine("*** SendFromPoolAndReceiveCheck ***");
            var rnd = new Random();

            var randomData = PooledMessage.Rent(1000 * 512);
            rnd.NextBytes(randomData.Content);

            if (!client.IsConnected())
            {
                client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
            }

            var outputData = client.SendReceive(randomData);
            var same = true;
            for (int i = 0; i < outputData.Length; i++)
            {
                if (outputData[i] != randomData.Content[i])
                {
                    same = false;
                    break;
                }
            }
            randomData.Return();
            Log(same ? "data is same" : "Data is not the same");
        }

        private void SendAndReceiveBenchmark(SimplSocketClient client)
        {
            Console.Out.WriteLine("*** SendAndReceiveBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new[] { 1, 10, 100, 1000 };
            var length = 0;
            for (var test = 0; test < 4; test++)
            {                
                byte[] randomData = new byte[bufferSizes[test] * 512];
                length += randomData.Length;
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
                var speed = (countPerIteration * length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
            }
        }

        private void SendFromPoolAndReceiveBenchmark(SimplSocketClient client)
        {
            Log("*** SendFromPoolAndReceiveBenchmark ***");
            var rnd = new Random();
            var bufferSizes = new[] { 1, 10, 100, 1000 };
            var length = 0;
            for (var test = 0; test < 4; test++)
            {                
                var randomData = PooledMessage.Rent(bufferSizes[test] * 512);
                length += randomData.Length;
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
                var speed = (countPerIteration * length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
                randomData.Return();
            }
        }

        private string ScaledSpeed(double bps)
        {
            const int KB = 1024;
            const int MB = 1024 * 1024;
            const int GB = 1024 * 1024 * 1024;
            if (bps > GB) { return $"{bps / GB:0.000} GB/s"; }
            if (bps > MB) { return $"{bps / MB:0.000} MB/s"; }
            if (bps > KB) { return $"{bps / KB:0.000} KB/s"; }
            return $"{bps:0.0} B/s";
        }

        public void Log(string output)
        {
            Console.Out.WriteLine(output);
        }


    }
}