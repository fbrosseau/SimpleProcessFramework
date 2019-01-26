using System;
using System.Net;

namespace SimpleProcessFramework.Utilities
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
    }
}
