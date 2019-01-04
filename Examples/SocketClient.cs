using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SimplSockets;

namespace SimplSocketsClient
{
    public class SocketClient
    {
        public void Start()
        {
            RunSocketBenchmarks();
        }

        void RunSocketBenchmarks()
        {
            // Fill randomData array

            using (var client = new SimplSocketClient(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            { NoDelay = true }))
            {
                // subscribe to broadcasts
                // client.MessageReceived += async (s, e) => await WriteLineAsync('*', e.ReceivedMessage.PooledMessage);
                client.MessageReceived += (s, e) => { e.ReceivedMessage.Dispose(); };


                client.Connected    += (s, e) =>
                {
                    Console.WriteLine("The client has connected!");
                };
                client.Disconnected += (s, e) =>
                {
                    Console.WriteLine("The client has disconnected!");
                };


                SendAndReceiveCheck(client);
                WaitUntilRentedMessagesReturn();

                SendFromPoolAndReceiveCheck(client);
                WaitUntilRentedMessagesReturn();

                SendBenchmark(client);
                WaitUntilRentedMessagesReturn();
                
                SendFromPoolBenchmark(client);
                WaitUntilRentedMessagesReturn();
                SendFromPoolAndReceiveBenchmark(client);
                WaitUntilRentedMessagesReturn();
                SendAndReceiveBenchmark(client);
                WaitUntilRentedMessagesReturn();
                SendBenchmark(client);
                WaitUntilRentedMessagesReturn();
                SendFromPoolBenchmark(client);
                WaitUntilRentedMessagesReturn();
                SendFromPoolAndReceiveBenchmark(client);
                WaitUntilRentedMessagesReturn();
                SendAndReceiveBenchmark(client);
                WaitUntilRentedMessagesReturn();

                Console.ReadLine();
            }
        }

        private void WaitUntilRentedMessagesReturn()
        {
            var prevNo = 0;
            var no = PooledMessage.GetNoRentedMessages();
            while (no>0)
            {
                no = PooledMessage.GetNoRentedMessages();
                if (no==prevNo) break;
                prevNo = no;
                Log($"messages rented out: {no}");                                
                Thread.Sleep(2000);
            }
            
            Log(PooledMessage.GetNoRentedMessages()==0?$"All messages returned to pool": $"messages remains rented out: {no}");
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
                length += bufferSizes[test] * 512;

                var countPerIteration = 100;
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < countPerIteration; i++)
                {
                    var randomData = PooledMessage.Rent(bufferSizes[test] * 512);
                    if (!client.IsConnected())
                    {
                        client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
                    }

                    client.Send(randomData);
                    randomData.Return();
                }

                watch.Stop();
                
                var speed = (countPerIteration * length) / (watch.ElapsedMilliseconds / 1000.0);
                var scaledSpeed = ScaledSpeed(speed);
                Log($"{countPerIteration}x{length}: {watch.ElapsedMilliseconds}ms = {scaledSpeed} ");
                
            }
        }

        private void SendAndReceiveCheck(SimplSocketClient client)
        {
            Log("*** SendAndReceiveCheck ***");
            var rnd = new Random();

            byte[] randomData = new byte[1000 * 512];
            rnd.NextBytes(randomData);

            if (!client.IsConnected())
            {
                client.Connect(new IPEndPoint(IPAddress.Loopback, 5000));
            }

            var outputData = client.SendReceive(randomData);
            if (outputData == null) { Log("No answer received"); return; }

            var same = true;
            for (int i = 0; i < outputData.Length; i++)
            {
                if (outputData.Content[i] != randomData[i])
                {
                    same = false;
                    break;
                }
            }
            // We need to return the output data to pool
            outputData.Return();
            Log(same?"data is same":"Data is not the same");           
        }

        private void SendFromPoolAndReceiveCheck(SimplSocketClient client)
        {
            Log("*** SendFromPoolAndReceiveCheck ***");
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
                if (outputData.Content[i] != randomData.Content[i])
                {
                    same = false;
                    break;
                }
            }
            randomData.Return();
            outputData.Return();
            Log(same ? "data is same" : "Data is not the same");
        }

        private void SendAndReceiveBenchmark(SimplSocketClient client)
        {
            Log("*** SendAndReceiveBenchmark ***");
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
                    if (response == null) { Log("No response "); } else { response.Return(); }
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
                    if (response==null) { Log("No response ");} else { response.Return();}
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