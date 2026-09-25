using System.Net;
using System.Net.Sockets;

namespace InternetHealth.Core.Network;

public static class IpClassification
{
    /// <summary>Direcciones privadas de red local (RFC 1918), loopback y link-local.</summary>
    public static bool IsPrivateLan(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || IPAddress.IsLoopback(ip);
        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || b[0] == 127
            || (b[0] == 169 && b[1] == 254);
    }

    /// <summary>CGNAT (100.64.0.0/10): ya es red del proveedor.</summary>
    public static bool IsCgnat(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 100 && b[1] >= 64 && b[1] <= 127;
    }

    /// <summary>Un salto pertenece al proveedor si no es una dirección de red local.</summary>
    public static bool IsProviderSide(IPAddress ip) => !IsPrivateLan(ip);
}
