using System;
using System.Net;
using System.Net.Sockets;

namespace Spfx.Utilities
{
    internal static class EndpointHelper
    {
        internal static EndPoint ParseEndpoint(string targetEndpoint, int defaultPort = 0)
        {
            Guard.ArgumentNotNullOrEmpty(targetEndpoint, nameof(targetEndpoint));

            var uri = new Uri("dummy://" + targetEndpoint, UriKind.Absolute);
            int port = uri.IsDefaultPort ? defaultPort : uri.Port;
            if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
            {
                return new IPEndPoint(IPAddress.Parse(uri.Host), port);
            }

            return new DnsEndPoint(uri.DnsSafeHost, port);
        }

        internal static string EndpointToString(EndPoint ep)
        {
            if (ep is IPEndPoint ipep)
            {
                if (ipep.AddressFamily == AddressFamily.InterNetwork)
                    return $"{ipep.Address}:{ipep.Port}";
                if (ipep.AddressFamily == AddressFamily.InterNetwork)
                    return $"[{ipep.Address}]:{ipep.Port}";
            }

            if (ep is DnsEndPoint dns)
                return $"{dns.Host}:{dns.Port}";

            if (ep is UnixDomainSocketEndPoint)
                return ep.ToString();

            throw new ArgumentException("Endpoint is not supported: " + ep.GetType().FullName);
        }
    }
}
