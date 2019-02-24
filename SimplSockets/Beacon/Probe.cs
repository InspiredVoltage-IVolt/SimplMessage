using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SimplSockets
{
    /// <summary>
    /// Counterpart of the beacon, searches for beacons
    /// </summary>
    /// <remarks>
    /// The beacon list event will not be raised on your main thread!
    /// </remarks>
    public class Probe : IDisposable
    {
        /// <summary>
        /// Remove beacons older than this
        /// </summary>
        private static readonly TimeSpan BeaconTimeout = new TimeSpan(0, 0, 0, 5); // seconds

        public event Action<IEnumerable<BeaconLocation>> BeaconsUpdated;

        private  Task                        task                = null;
        private readonly AsyncAutoResetEvent asyncAutoResetEvent = new AsyncAutoResetEvent();
        private readonly UdpClient           udp                 = new UdpClient();
        private IEnumerable<BeaconLocation>  currentBeacons      = Enumerable.Empty<BeaconLocation>();
        private bool                         running             = false;

        public Probe(string beaconType)
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            BeaconType = beaconType;

            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
#if (!WINDOWS_UWP)
            try 
            {
                udp.AllowNatTraversal(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error switching on NAT traversal: " + ex.Message);
            }
#endif
            var dummy = PollResponses();
        }

        private async Task PollResponses()
        {
            while (true)
            {
                var result = await udp.ReceiveAsync();
                ResponseReceived(result);
            }
        }

        public void Start()
        {
            if (running) return;
            running = true;
            task    = BackgroundLoopAsync();
        }

        private void ResponseReceived(UdpReceiveResult ar)
        {
            var bytes     = ar.Buffer;
            var remote    = ar.RemoteEndPoint;
            Debug.WriteLine($"P: Received response from {remote.Address}");
            var typeBytes = Beacon.Encode(BeaconType).ToList();
            Debug.WriteLine(string.Join(", ", typeBytes.Select(_ => (char)_)));
            if (Beacon.HasPrefix(bytes, typeBytes))
            {
                try
                {
                    var portBytes = bytes.Skip(typeBytes.Count()).Take(2).ToArray();
                    var port      = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(portBytes, 0));
                    var payload   = Beacon.Decode(bytes.Skip(typeBytes.Count() + 2));
                    Debug.WriteLine($"P: New beacon {remote.Address},{port}");
                    NewBeacon(new BeaconLocation(new IPEndPoint(remote.Address, port), payload, DateTime.Now));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        public string BeaconType { get; private set; }

        private async Task BackgroundLoopAsync()
        {
            while (running)
            {
                try
                {
                    await BroadcastProbeAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
                
				await asyncAutoResetEvent.WaitAsync(2000); 
                PruneBeacons();
            }
        }

        private async Task BroadcastProbeAsync()
        {
            Debug.WriteLine($"P: Broadcasting probe with discovery port {Beacon.DiscoveryPort}");
            var probe = Beacon.Encode(BeaconType).ToArray();
            await udp.SendAsync(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, Beacon.DiscoveryPort));
        }

        private void PruneBeacons()
        {
            var cutOff     = DateTime.Now - BeaconTimeout;
            var oldBeacons = currentBeacons.ToList();
            var newBeacons = oldBeacons.Where(_ => _.LastAdvertised >= cutOff).ToList();
            if (EnumsEqual(oldBeacons, newBeacons)) return;

            var u = BeaconsUpdated;
            if (u != null) u(newBeacons);
            currentBeacons = newBeacons;
        }

        private void NewBeacon(BeaconLocation newBeacon)
        {
            var newBeacons = currentBeacons
                .Where  (_ => !_.Equals(newBeacon))
                .Concat (new [] { newBeacon })
                .OrderBy(_ => _.Data)
                .ThenBy (_ => _.Address, IPEndPointComparer.Instance)
                .ToList ();
            var u = BeaconsUpdated;
            if (u != null) u(newBeacons);
            currentBeacons = newBeacons;
        }

        private static bool EnumsEqual<T>(IEnumerable<T> xs, IEnumerable<T> ys)
        {
            return xs.Zip(ys, (x, y) => x.Equals(y)).Count() == xs.Count();
        }

        public async Task Stop()
        {
            running = false;

            asyncAutoResetEvent.Set();
            await task;
        }

        public void Dispose()
        {
            try
            {
                running = false;
                asyncAutoResetEvent.Set();

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}
