using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using InternetHealth.Core.Model;
using InternetHealth.Core.Network;
using InternetHealth.Mac.Interop;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Contexto de red en Mac: la detección multiplataforma de Core más el Wi-Fi (CoreWLAN), el nombre
/// del puerto de red ("Wi-Fi", "Ethernet Adapter (en4)"…), la VPN y la MAC del router (ARP), que
/// identifica la red o sede sin pedir permisos.
/// </summary>
internal sealed class MacNetworkContextProvider : BasicNetworkContextProvider
{
    private readonly CoreWlanClient _wlan = new();
    private readonly object _gate = new();
    private IPAddress? _macFor;
    private string? _mac;
    private DateTimeOffset _macAttempt;
    private WifiInfo? _lastWifi;
    private string? _lastWifiAdapter;
    private Dictionary<string, string> _portNames = new(StringComparer.Ordinal);
    private DateTimeOffset _portNamesRead;

    public override NetworkContext GetContext(bool includeWifiDetails)
    {
        var ctx = base.GetContext(includeWifiDetails);
        var (vpn, vpnName) = DetectVpn();
        ctx = ctx with { VpnActive = vpn || ctx.VpnActive, VpnName = vpnName ?? ctx.VpnName };
        if (!ctx.HasConnection) return ctx;

        lock (_gate)
        {
            string? mac = ResolveGatewayMac(ctx.Gateway);
            string? adapter = ctx.AdapterId is { } id ? $"{PortName(id)} ({id})" : ctx.AdapterName;
            WifiInfo? wifi = null;
            if (ctx.LinkType == LinkType.WiFi)
            {
                // En Mac se lee siempre: el motor pide la lectura completa solo cada 30 min (pensado para
                // Windows, donde cuenta como uso de ubicación) y aquí la señal debe seguir al día.
                {
                    try { wifi = _wlan.Query(ctx.AdapterId); } catch { wifi = null; }
                    _lastWifi = wifi;
                    _lastWifiAdapter = ctx.AdapterId;
                }
            }
            return ctx with { GatewayMac = mac, Wifi = wifi, AdapterName = adapter };
        }
    }

    /// <summary>
    /// En Mac todas las VPN (y también servicios del sistema como iCloud) usan interfaces utun. Las
    /// del sistema solo tienen IPv6 local; una VPN real recibe una dirección IPv4.
    /// </summary>
    private static (bool Active, string? Name) DetectVpn()
    {
        try
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus is not (OperationalStatus.Up or OperationalStatus.Unknown)) continue;
                if (!n.Name.StartsWith("utun", StringComparison.Ordinal) && !n.Name.StartsWith("ipsec", StringComparison.Ordinal)
                    && !n.Name.StartsWith("ppp", StringComparison.Ordinal)) continue;
                bool hasIpv4 = n.GetIPProperties().UnicastAddresses.Any(u =>
                    u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address));
                if (hasIpv4) return (true, $"VPN ({n.Name})");
            }
        }
        catch { /* ignorar */ }
        return (false, null);
    }

    /// <summary>Nombre del puerto de red como lo muestra macOS en Ajustes ("Wi-Fi", "Thunderbolt Ethernet"…).</summary>
    private string PortName(string device)
    {
        if (_portNames.TryGetValue(device, out var name)) return name;
        if (DateTimeOffset.Now - _portNamesRead > TimeSpan.FromMinutes(1))
        {
            _portNamesRead = DateTimeOffset.Now;
            var output = Run("/usr/sbin/networksetup", "-listallhardwareports");
            if (output is not null)
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                string? port = null;
                foreach (var line in output.Split('\n'))
                {
                    if (line.StartsWith("Hardware Port: ", StringComparison.Ordinal)) port = line[15..].Trim();
                    else if (line.StartsWith("Device: ", StringComparison.Ordinal) && port is not null) map[line[8..].Trim()] = port;
                }
                _portNames = map;
                if (map.TryGetValue(device, out name)) return name;
            }
        }
        return "Adaptador de red";
    }

    private string? ResolveGatewayMac(IPAddress? gateway)
    {
        if (gateway is null) return null;
        if (gateway.Equals(_macFor) && _mac is not null) return _mac;
        if (gateway.Equals(_macFor) && DateTimeOffset.Now - _macAttempt < TimeSpan.FromMinutes(1)) return null;

        _macFor = gateway;
        _macAttempt = DateTimeOffset.Now;
        _mac = null;
        // "? (10.0.4.1) at 70:a7:41:70:73:3c on en0 ifscope [ethernet]" → "70-A7-41-70-73-3C" (mismo formato que Windows)
        var output = Run("/usr/sbin/arp", "-n", gateway.ToString());
        if (output is not null)
        {
            int at = output.IndexOf(" at ", StringComparison.Ordinal);
            if (at > 0)
            {
                var raw = output[(at + 4)..].Split(' ', 2)[0];
                var parts = raw.Split(':');
                if (parts.Length == 6 && parts.All(p => p.Length is 1 or 2 && p.All(Uri.IsHexDigit)))
                    _mac = string.Join("-", parts.Select(p => p.PadLeft(2, '0').ToUpperInvariant()));
            }
        }
        return _mac;
    }

    private static string? Run(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { /* ignorar */ } return null; }
            return output;
        }
        catch
        {
            return null;
        }
    }
}
