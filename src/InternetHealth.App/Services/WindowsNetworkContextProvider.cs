using System.Net;
using System.Net.NetworkInformation;
using InternetHealth.App.Interop;
using InternetHealth.Core.Model;
using InternetHealth.Core.Network;

namespace InternetHealth.App.Services;

/// <summary>
/// Contexto de red en Windows: usa la ruta real hacia internet (GetBestInterface), los datos del
/// Wi-Fi (Native Wifi API) y la MAC del router (ARP), que identifica la red/sede sin pedir permisos.
/// </summary>
internal sealed class WindowsNetworkContextProvider : BasicNetworkContextProvider, IDisposable
{
    private static readonly uint InternetProbe = BitConverter.ToUInt32(IPAddress.Parse("1.1.1.1").GetAddressBytes(), 0);
    private readonly WlanClient _wlan = new();
    private readonly object _gate = new();
    private IPAddress? _macFor;
    private string? _mac;
    private DateTimeOffset _macAttempt;
    private WifiInfo? _lastWifi;
    private string? _lastWifiAdapter;
    private IPAddress? _lastWifiGateway;

    protected override int? PreferredInterfaceIndex()
    {
        try { return NativeMethods.GetBestInterface(InternetProbe, out uint idx) == 0 ? (int)idx : null; }
        catch { return null; }
    }

    public override NetworkContext GetContext(bool includeWifiDetails)
    {
        var ctx = base.GetContext(includeWifiDetails);
        if (!ctx.HasConnection) return ctx;

        lock (_gate)
        {
            string? mac = ResolveGatewayMac(ctx.Gateway);
            WifiInfo? wifi = null;
            if (ctx.LinkType == LinkType.WiFi)
            {
                // Lectura liviana (señal y canal): no usa la ubicación, se hace en cada llamada.
                (int? rssi, int? channel) = (null, null);
                // (También funciona sin el permiso de ubicación: así la señal se ve aunque falte.)
                if (!includeWifiDetails && _lastWifi is not null && _lastWifiAdapter == ctx.AdapterId)
                {
                    try { (rssi, channel) = _wlan.QuerySignal(ctx.AdapterId); } catch { /* ignorar */ }
                }

                // Lectura completa (nombre de la red, punto de acceso): cuenta como uso de ubicación.
                // Solo si el motor la pide, si cambió el adaptador o si cambió el canal (el equipo
                // probablemente se pasó a otro punto de acceso).
                bool full = includeWifiDetails || _lastWifi is null || _lastWifiAdapter != ctx.AdapterId
                    || !Equals(_lastWifiGateway, ctx.Gateway)
                    || (channel is int ch && _lastWifi.Channel is int lastCh && ch != lastCh);
                if (full)
                {
                    try { wifi = _wlan.Query(ctx.AdapterId); } catch { wifi = null; }
                    _lastWifi = wifi;
                    _lastWifiAdapter = ctx.AdapterId;
                    _lastWifiGateway = ctx.Gateway;
                }
                else
                {
                    // Velocidades null: se usa la del adaptador (LinkSpeedMbps), que está al día.
                    wifi = _lastWifi! with
                    {
                        RssiDbm = rssi ?? _lastWifi!.RssiDbm,
                        SignalQuality = rssi is int r ? WifiInfo.QualityFromRssi(r) : _lastWifi!.SignalQuality,
                        Channel = channel ?? _lastWifi!.Channel,
                        Band = _lastWifi!.Band ?? WlanClient.Band(null, channel, null),
                        RxRateMbps = null,
                        TxRateMbps = null,
                    };
                }
            }
            return ctx with { GatewayMac = mac, Wifi = wifi };
        }
    }

    private string? ResolveGatewayMac(IPAddress? gateway)
    {
        if (gateway is null) return null;
        if (gateway.Equals(_macFor) && _mac is not null) return _mac;
        if (gateway.Equals(_macFor) && DateTimeOffset.Now - _macAttempt < TimeSpan.FromMinutes(1)) return null;

        _macFor = gateway;
        _macAttempt = DateTimeOffset.Now;
        _mac = null;
        try
        {
            var buf = new byte[6];
            uint len = (uint)buf.Length;
            uint dest = BitConverter.ToUInt32(gateway.GetAddressBytes(), 0);
            if (NativeMethods.SendARP(dest, 0, buf, ref len) == 0 && len == 6)
                _mac = string.Join("-", buf.Select(b => b.ToString("X2")));
        }
        catch { /* ignorar */ }
        return _mac;
    }

    public void Dispose() => _wlan.Dispose();
}
