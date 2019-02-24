using System;
using System.Collections.Generic;
using System.Net;
#if (WINDOWS_UWP)
using Windows.Networking;
using Windows.Networking.Connectivity;
#else
using System.Linq;
#endif

namespace SimplSockets
{
    class NetworkHelper
    {
#if (WINDOWS_UWP)
        public static List<IPAddress> GetCurrentIpv4Addresses()
        {
            var ipAdresses                              = new List<IPAddress>();
            var icp                                     = NetworkInformation.GetInternetConnectionProfile();
            if (icp                                    != null
                && icp.NetworkAdapter                  != null
                && icp.NetworkAdapter.NetworkAdapterId != null)
            {
                var name      = icp.ProfileName;
                var hostnames = NetworkInformation.GetHostNames();

                foreach (var hn in hostnames)
                {
                    if (hn.IPInformation                                    != null
                        && hn.IPInformation.NetworkAdapter                  != null
                        && hn.IPInformation.NetworkAdapter.NetworkAdapterId != null
                        && hn.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId
                        && hn.Type                                          == HostNameType.Ipv4)
                    {
                        ipAdresses.Add(IPAddress.Parse(hn.CanonicalName));
                    }
                }
            }

            return ipAdresses;
        }
#else
        public static List<IPAddress> GetCurrentIpv4Addresses()
        {
            return  Dns.GetHostAddresses(Dns.GetHostName()).ToList();
        }
#endif
    }
}

