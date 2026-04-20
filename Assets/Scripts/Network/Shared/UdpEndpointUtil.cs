using System.Net;

namespace TheLostHill.Network.Shared
{

    public static class UdpEndpointUtil
    {
        public static IPAddress NormalizeAddress(IPAddress ip)
        {
            if (ip == null) return null;
            return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        }

        public static IPEndPoint NormalizeEndPoint(IPEndPoint ep)
        {
            if (ep == null) return null;
            return new IPEndPoint(NormalizeAddress(ep.Address), ep.Port);
        }

        public static bool AddressesMatch(IPAddress a, IPAddress b)
        {
            if (a == null || b == null) return false;
            if (a.Equals(b)) return true;

            var na = NormalizeAddress(a);
            var nb = NormalizeAddress(b);
            if (na.Equals(nb)) return true;

            return IPAddress.IsLoopback(na) && IPAddress.IsLoopback(nb);
        }

        public static bool EndPointsMatch(IPEndPoint a, IPEndPoint b)
        {
            if (a == null || b == null) return false;
            if (a.Port != b.Port) return false;
            return AddressesMatch(a.Address, b.Address);
        }
    }
}
