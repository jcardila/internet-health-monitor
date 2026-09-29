using InternetHealth.Core.Network;

namespace InternetHealth.Mac.Interop;

/// <summary>
/// Datos del Wi-Fi conectado con CoreWLAN (el framework que usa el propio macOS). La señal, la
/// velocidad y el canal no requieren permisos; el nombre de la red (SSID) sí: macOS lo oculta si la
/// app no tiene acceso a la ubicación, igual que Windows 11 24H2.
/// </summary>
internal sealed class CoreWlanClient
{
    private readonly bool _available = ObjC.LoadFramework("CoreWLAN");

    public WifiInfo? Query(string? interfaceName) => !_available ? null : ObjC.WithPool(() =>
    {
        var client = ObjC.Send(ObjC.objc_getClass("CWWiFiClient"), "sharedWiFiClient");
        var iface = interfaceName is null
            ? ObjC.Send(client, "interface")
            : ObjC.Send(client, "interfaceWithName:", ObjC.NSString(interfaceName));
        if (iface == IntPtr.Zero || !ObjC.SendBool(iface, "powerOn")) return null;

        long rssi = ObjC.SendLong(iface, "rssiValue");
        if (rssi is >= 0 or < -120) return null; // encendido pero sin asociar a una red
        double tx = ObjC.SendDouble(iface, "transmitRate");
        string? ssid = ObjC.ToManagedString(ObjC.Send(iface, "ssid"));
        string? bssid = ObjC.ToManagedString(ObjC.Send(iface, "bssid"));

        int? channel = null;
        string? band = null;
        var ch = ObjC.Send(iface, "wlanChannel");
        if (ch != IntPtr.Zero)
        {
            long number = ObjC.SendLong(ch, "channelNumber");
            channel = number > 0 ? (int)number : null;
            band = ObjC.SendLong(ch, "channelBand") switch
            {
                1 => "2.4 GHz",
                2 => "5 GHz",
                3 => "6 GHz",
                _ => null,
            };
        }

        return new WifiInfo(
            Ssid: string.IsNullOrEmpty(ssid) ? null : ssid,
            Bssid: string.IsNullOrEmpty(bssid) ? null : bssid.ToUpperInvariant(),
            // Misma escala que Windows: 0 % a -100 dBm, 100 % a -50 dBm.
            SignalQuality: (int)Math.Clamp(2 * (rssi + 100), 0, 100),
            RssiDbm: (int)rssi,
            // macOS solo informa la tasa de transmisión: es la "velocidad del enlace" que muestra el sistema.
            RxRateMbps: tx > 0 ? tx : null,
            TxRateMbps: tx > 0 ? tx : null,
            Band: band,
            Channel: channel,
            PhyType: PhyName(ObjC.SendLong(iface, "activePHYMode")),
            LocationPermissionMissing: string.IsNullOrEmpty(ssid));
    });

    private static string? PhyName(long phy) => phy switch
    {
        1 => "802.11a",
        2 => "802.11b",
        3 => "802.11g",
        4 => "Wi-Fi 4 (802.11n)",
        5 => "Wi-Fi 5 (802.11ac)",
        6 => "Wi-Fi 6/6E (802.11ax)",
        7 => "Wi-Fi 7 (802.11be)",
        _ => null,
    };
}
