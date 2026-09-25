using System.Runtime.InteropServices;
using System.Text;
using InternetHealth.Core.Network;
using static InternetHealth.App.Interop.NativeMethods;

namespace InternetHealth.App.Interop;

/// <summary>
/// Lectura de datos del Wi-Fi conectado mediante la Native Wifi API.
/// En Windows 11 24H2 o posterior, Windows exige permiso de ubicación para leer estos datos;
/// si no está concedido, se informa <see cref="WifiInfo.LocationPermissionMissing"/>.
/// </summary>
internal sealed class WlanClient : IDisposable
{
    private IntPtr _handle;
    private bool _unavailable;

    public WifiInfo? Query(string? adapterId)
    {
        if (_unavailable || adapterId is null || !Guid.TryParse(adapterId, out var guid)) return null;
        if (_handle == IntPtr.Zero)
        {
            try
            {
                if (WlanOpenHandle(2, IntPtr.Zero, out _, out _handle) != ERROR_SUCCESS)
                {
                    _unavailable = true;
                    return null;
                }
            }
            catch (DllNotFoundException)
            {
                _unavailable = true; // equipos sin servicio WLAN
                return null;
            }
        }

        uint rc = WlanQueryInterface(_handle, ref guid, WLAN_OPCODE_CURRENT_CONNECTION, IntPtr.Zero, out uint size, out IntPtr data, out _);
        if (rc == ERROR_ACCESS_DENIED)
            return new WifiInfo(null, null, null, null, null, null, null, null, null, LocationPermissionMissing: true);
        if (rc != ERROR_SUCCESS || data == IntPtr.Zero) return null;

        string? ssid, bssid, phy;
        int signal;
        double rx, tx;
        byte[] bssidBytes = new byte[6];
        try
        {
            if (size < 588) return null;
            // WLAN_CONNECTION_ATTRIBUTES: isState(4) mode(4) profileName(512) → association attributes en 520.
            const int assoc = 520;
            int ssidLen = Math.Clamp(Marshal.ReadInt32(data, assoc), 0, 32);
            var ssidBytes = new byte[ssidLen];
            Marshal.Copy(data + assoc + 4, ssidBytes, 0, ssidLen);
            ssid = ssidLen > 0 ? Encoding.UTF8.GetString(ssidBytes) : null;
            Marshal.Copy(data + assoc + 40, bssidBytes, 0, 6);
            bssid = string.Join(":", bssidBytes.Select(b => b.ToString("X2")));
            int phyType = Marshal.ReadInt32(data, assoc + 48);
            signal = Marshal.ReadInt32(data, assoc + 56);
            rx = (uint)Marshal.ReadInt32(data, assoc + 60) / 1000.0; // kbps → Mbps
            tx = (uint)Marshal.ReadInt32(data, assoc + 64) / 1000.0;
            phy = PhyName(phyType);
        }
        finally
        {
            WlanFreeMemory(data);
        }

        int? channel = QueryInt(guid, WLAN_OPCODE_CHANNEL_NUMBER);
        int? rssi = QueryInt(guid, WLAN_OPCODE_RSSI);
        if (rssi is < -110 or > -5) rssi = null;
        var (freqKhz, bssRssi) = QueryBss(guid, bssidBytes);
        rssi ??= bssRssi;

        string? band = freqKhz switch
        {
            >= 2_400_000 and < 2_500_000 => "2.4 GHz",
            >= 4_900_000 and < 5_925_000 => "5 GHz",
            >= 5_925_000 and < 7_200_000 => "6 GHz",
            _ => channel switch
            {
                >= 1 and <= 14 when phy is null || !(phy.Contains("6E") || phy.Contains("7")) => "2.4 GHz",
                >= 32 and <= 177 => "5 GHz",
                _ => null,
            },
        };

        return new WifiInfo(
            ssid, bssid,
            signal is >= 0 and <= 100 ? signal : null,
            rssi,
            rx > 0 ? rx : null,
            tx > 0 ? tx : null,
            band, channel, phy, LocationPermissionMissing: false);
    }

    private int? QueryInt(Guid guid, uint opcode)
    {
        try
        {
            if (WlanQueryInterface(_handle, ref guid, opcode, IntPtr.Zero, out uint size, out IntPtr data, out _) != ERROR_SUCCESS
                || data == IntPtr.Zero)
                return null;
            try { return size >= 4 ? Marshal.ReadInt32(data) : null; }
            finally { WlanFreeMemory(data); }
        }
        catch { return null; }
    }

    /// <summary>Busca el punto de acceso actual en la última lista de escaneo para obtener la frecuencia exacta.</summary>
    private (int? FreqKhz, int? Rssi) QueryBss(Guid guid, byte[] bssid)
    {
        const int entrySize = 360;
        try
        {
            if (WlanGetNetworkBssList(_handle, ref guid, IntPtr.Zero, 1, false, IntPtr.Zero, out IntPtr list) != ERROR_SUCCESS
                || list == IntPtr.Zero)
                return (null, null);
            try
            {
                int total = Marshal.ReadInt32(list, 0);
                int count = Marshal.ReadInt32(list, 4);
                if (count <= 0 || total < 8 + (long)count * entrySize) return (null, null); // estructura inesperada: no arriesgar
                var mac = new byte[6];
                for (int i = 0; i < count; i++)
                {
                    IntPtr entry = list + 8 + i * entrySize;
                    Marshal.Copy(entry + 40, mac, 0, 6);
                    if (!mac.AsSpan().SequenceEqual(bssid)) continue;
                    int rssi = Marshal.ReadInt32(entry, 56);
                    int freq = Marshal.ReadInt32(entry, 92);
                    return (freq is > 2_000_000 and < 8_000_000 ? freq : null, rssi is > -110 and < -5 ? rssi : null);
                }
            }
            finally
            {
                WlanFreeMemory(list);
            }
        }
        catch { /* ignorar */ }
        return (null, null);
    }

    private static string? PhyName(int phy) => phy switch
    {
        4 => "802.11a",
        5 => "802.11b",
        6 => "802.11g",
        7 => "Wi-Fi 4 (802.11n)",
        8 => "Wi-Fi 5 (802.11ac)",
        10 => "Wi-Fi 6/6E (802.11ax)",
        11 => "Wi-Fi 7 (802.11be)",
        _ => null,
    };

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            WlanCloseHandle(_handle, IntPtr.Zero);
            _handle = IntPtr.Zero;
        }
    }
}
