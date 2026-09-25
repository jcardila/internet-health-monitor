using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using InternetHealth.Core.Model;

namespace InternetHealth.Core.Network;

/// <summary>Ping ICMP asíncrono con reutilización de objetos <see cref="Ping"/>.</summary>
public sealed class SystemPinger : IPinger, IDisposable
{
    private static readonly byte[] Payload = new byte[32];
    private readonly ConcurrentBag<Ping> _pool = new();

    public async Task<PingOutcome> PingAsync(IPAddress target, int timeoutMs, int? ttl, CancellationToken ct)
    {
        if (!_pool.TryTake(out var ping)) ping = new Ping();
        try
        {
            var options = new PingOptions(ttl ?? 128, dontFragment: false);
            var sw = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(target, TimeSpan.FromMilliseconds(timeoutMs), Payload, options, ct)
                .ConfigureAwait(false);
            sw.Stop();

            if (reply.Status == IPStatus.Success)
            {
                // RoundtripTime tiene resolución de 1 ms; si es 0 usamos el cronómetro (más fino).
                double rtt = reply.RoundtripTime > 0 ? reply.RoundtripTime : Math.Min(sw.Elapsed.TotalMilliseconds, 1);
                return new PingOutcome(IPStatus.Success, rtt, reply.Address);
            }

            if (ttl is not null && reply.Status is IPStatus.TtlExpired or IPStatus.TimeExceeded)
            {
                // Windows no reporta RTT para TtlExpired: usamos el cronómetro.
                return new PingOutcome(reply.Status, sw.Elapsed.TotalMilliseconds, reply.Address);
            }

            return new PingOutcome(reply.Status, null, reply.Address);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Un Ping que falló puede quedar en mal estado: no lo devolvemos al pool.
            ping.Dispose();
            ping = null;
            return PingOutcome.Failed();
        }
        finally
        {
            if (ping is not null) _pool.Add(ping);
        }
    }

    public void Dispose()
    {
        while (_pool.TryTake(out var p)) p.Dispose();
    }
}

public sealed class SystemTcpProber : ITcpProber
{
    public async Task<TcpProbeOutcome> ProbeAsync(string host, int port, int timeoutMs, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        var sw = Stopwatch.StartNew();
        IPAddress[] addresses;
        double dnsMs;
        try
        {
            addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cts.Token).ConfigureAwait(false);
            dnsMs = sw.Elapsed.TotalMilliseconds;
            if (addresses.Length == 0) return new TcpProbeOutcome(false, null, false, null, "DNS sin resultados");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TcpProbeOutcome(false, null, false, null, "DNS: " + ex.Message);
        }

        sw.Restart();
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await socket.ConnectAsync(addresses[0], port, cts.Token).ConfigureAwait(false);
            double connectMs = sw.Elapsed.TotalMilliseconds;
            try { socket.Shutdown(SocketShutdown.Both); } catch { /* ignorar */ }
            return new TcpProbeOutcome(true, dnsMs, true, connectMs, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new TcpProbeOutcome(true, dnsMs, false, null, "Tiempo de espera agotado");
        }
        catch (Exception ex)
        {
            return new TcpProbeOutcome(true, dnsMs, false, null, ex.Message);
        }
    }
}

/// <summary>Detecta portales cautivos usando el mismo método que Windows (NCSI).</summary>
public sealed class HttpCaptivePortalChecker : ICaptivePortalChecker, IDisposable
{
    private const string ProbeUrl = "http://www.msftconnecttest.com/connecttest.txt";
    private const string Expected = "Microsoft Connect Test";
    private readonly HttpClient _http;

    public HttpCaptivePortalChecker()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            UseCookies = false,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
    }

    public async Task<bool?> CheckAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(ProbeUrl, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400) return true;
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return !body.Trim().Equals(Expected, StringComparison.Ordinal);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Mide el tráfico del adaptador activo (todo el equipo, no por aplicación).</summary>
public sealed class SystemThroughputMeter : IThroughputMeter
{
    private string? _adapterId;
    private NetworkInterface? _nic;
    private long _lastRx, _lastTx;
    private DateTimeOffset _lastTime;

    public Throughput Sample(string? adapterId, DateTimeOffset now)
    {
        if (adapterId is null) return Throughput.Zero;
        if (_nic is null || _nic.Id != adapterId)
        {
            try { _nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == adapterId); }
            catch { _nic = null; }
        }
        var nic = _nic;
        if (nic is null) return Throughput.Zero;

        IPInterfaceStatistics stats;
        try { stats = nic.GetIPStatistics(); }
        catch { return Throughput.Zero; }

        long rx = stats.BytesReceived, tx = stats.BytesSent;
        if (adapterId != _adapterId || _lastTime == default)
        {
            _adapterId = adapterId;
            _lastRx = rx; _lastTx = tx; _lastTime = now;
            return Throughput.Zero;
        }

        double seconds = (now - _lastTime).TotalSeconds;
        if (seconds < 0.5) return Throughput.Zero;
        double rxMbps = Math.Max(0, rx - _lastRx) * 8 / 1_000_000.0 / seconds;
        double txMbps = Math.Max(0, tx - _lastTx) * 8 / 1_000_000.0 / seconds;
        _lastRx = rx; _lastTx = tx; _lastTime = now;
        return new Throughput(rxMbps, txMbps);
    }
}

/// <summary>
/// Detección de red multiplataforma basada en <see cref="NetworkInterface"/>.
/// La app de Windows la complementa con la ruta real, Wi-Fi y MAC del router.
/// </summary>
public class BasicNetworkContextProvider : INetworkContextProvider
{
    private static readonly string[] VirtualHints =
        ["virtual", "hyper-v", "vmware", "virtualbox", "loopback", "bluetooth", "wsl", "docker", "npcap", "pseudo"];

    private static readonly string[] VpnHints =
        ["vpn", "wireguard", "openvpn", "tap-windows", "fortinet", "forticlient", "anyconnect", "globalprotect",
         "pangp", "zscaler", "cloudflare warp", "nordlynx", "tailscale", "zerotier", "juniper", "pulse secure", "sonicwall", "checkpoint"];

    public virtual NetworkContext GetContext(bool includeWifiDetails)
    {
        NetworkInterface[] all;
        try { all = NetworkInterface.GetAllNetworkInterfaces(); }
        catch { return NetworkContext.None; }

        var up = all.Where(n => n.OperationalStatus == OperationalStatus.Up).ToArray();
        var vpn = up.FirstOrDefault(IsVpn);
        var physical = ChoosePhysical(up, PreferredInterfaceIndex());
        if (physical is null)
            return NetworkContext.None with { VpnActive = vpn is not null, VpnName = vpn?.Description };

        return BuildContext(physical, vpn);
    }

    /// <summary>Índice IPv4 de la interfaz que Windows usaría para salir a internet (si se conoce).</summary>
    protected virtual int? PreferredInterfaceIndex() => null;

    protected NetworkContext BuildContext(NetworkInterface nic, NetworkInterface? vpn)
    {
        var props = nic.GetIPProperties();
        var gateway = props.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
        var local = props.UnicastAddresses
            .Select(u => u.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

        var type = nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => LinkType.WiFi,
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => LinkType.Ethernet,
            NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => LinkType.Cellular,
            _ => LinkType.Other,
        };

        double? speed = null;
        try { speed = nic.Speed > 0 ? nic.Speed / 1_000_000.0 : null; } catch { /* ignorar */ }

        return new NetworkContext(
            HasConnection: gateway is not null || local is not null,
            LinkType: type,
            AdapterName: nic.Description,
            AdapterId: nic.Id,
            LocalAddress: local,
            Gateway: gateway,
            GatewayMac: null,
            LinkSpeedMbps: speed,
            Wifi: null,
            VpnActive: vpn is not null,
            VpnName: vpn?.Description);
    }

    protected static bool IsVpn(NetworkInterface n)
    {
        if (n.NetworkInterfaceType is NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel)
            return n.NetworkInterfaceType == NetworkInterfaceType.Ppp || HasHint(n, VpnHints);
        return HasHint(n, VpnHints);
    }

    protected static NetworkInterface? ChoosePhysical(IEnumerable<NetworkInterface> up, int? preferredIndex)
    {
        var candidates = up.Where(n =>
                n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet
                    or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                    or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2
                && !HasHint(n, VirtualHints) && !IsVpn(n)
                && HasIpv4Gateway(n))
            .ToList();
        if (candidates.Count == 0) return null;

        if (preferredIndex is int idx)
        {
            var match = candidates.FirstOrDefault(n => Ipv4Index(n) == idx);
            if (match is not null) return match;
        }

        // Windows prefiere cable sobre Wi-Fi por métrica automática.
        return candidates
            .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0)
            .First();
    }

    protected static int? Ipv4Index(NetworkInterface n)
    {
        try { return n.GetIPProperties().GetIPv4Properties()?.Index; }
        catch { return null; }
    }

    private static bool HasIpv4Gateway(NetworkInterface n)
    {
        try
        {
            return n.GetIPProperties().GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
        }
        catch { return false; }
    }

    private static bool HasHint(NetworkInterface n, string[] hints)
    {
        var text = (n.Name + " " + n.Description).ToLowerInvariant();
        return hints.Any(text.Contains);
    }
}
