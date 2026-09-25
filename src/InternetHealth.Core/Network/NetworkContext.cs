using System.Net;
using InternetHealth.Core.Model;

namespace InternetHealth.Core.Network;

/// <summary>Datos del Wi-Fi conectado. Cualquier campo puede ser null si Windows no lo entrega.</summary>
public sealed record WifiInfo(
    string? Ssid,
    string? Bssid,
    int? SignalQuality,     // 0-100 (%)
    int? RssiDbm,           // p. ej. -55
    double? RxRateMbps,
    double? TxRateMbps,
    string? Band,           // "2.4 GHz", "5 GHz", "6 GHz"
    int? Channel,
    string? PhyType,        // "Wi-Fi 6 (802.11ax)", etc.
    bool LocationPermissionMissing);

/// <summary>Fotografía del entorno de red del equipo en un momento dado.</summary>
public sealed record NetworkContext(
    bool HasConnection,
    LinkType LinkType,
    string? AdapterName,
    string? AdapterId,
    IPAddress? LocalAddress,
    IPAddress? Gateway,
    string? GatewayMac,
    double? LinkSpeedMbps,
    WifiInfo? Wifi,
    bool VpnActive,
    string? VpnName)
{
    public static readonly NetworkContext None =
        new(false, LinkType.Unknown, null, null, null, null, null, null, null, false, null);

    /// <summary>Identificador estable de la red (útil para agrupar por sede): SSID o MAC del router.</summary>
    public string NetworkKey =>
        string.Join('|', new[] { Wifi?.Ssid, GatewayMac, Gateway?.ToString() }.Where(s => !string.IsNullOrEmpty(s)));

    public string LinkTypeLabel => LinkType switch
    {
        LinkType.WiFi => "Wi-Fi",
        LinkType.Ethernet => "Cable",
        LinkType.Cellular => "Datos móviles",
        LinkType.Other => "Otra",
        _ => "Desconocida",
    };

    /// <summary>Velocidad efectiva del enlace local (en Wi-Fi usa la tasa de recepción).</summary>
    public double? EffectiveLinkRateMbps => Wifi?.RxRateMbps ?? LinkSpeedMbps;
}

public sealed record ProviderHop(IPAddress Address, int Ttl);

public sealed record CallInfo(bool InCall, string? AppName)
{
    public static readonly CallInfo None = new(false, null);
}

public readonly record struct Throughput(double RxMbps, double TxMbps)
{
    public static readonly Throughput Zero = new(0, 0);
}
