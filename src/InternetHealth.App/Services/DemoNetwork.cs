using System.Net;
using System.Net.NetworkInformation;
using InternetHealth.Core.Model;
using InternetHealth.Core.Network;

namespace InternetHealth.App.Services;

/// <summary>
/// Modo demostración (<c>--demo</c>): simula una red que pasa por varios escenarios para ver la
/// interfaz, capacitar al equipo o tomar capturas sin tener que dañar una conexión real.
/// </summary>
internal sealed class DemoNetwork : IPinger, ITcpProber, INetworkContextProvider, ICaptivePortalChecker, IThroughputMeter, ICallDetector
{
    private static readonly IPAddress Gateway = IPAddress.Parse("192.168.1.1");
    private static readonly IPAddress Isp = IPAddress.Parse("181.49.100.1");
    private readonly DateTimeOffset _start = DateTimeOffset.Now;
    private readonly Random _rnd = Random.Shared;

    private enum Scenario { Good, WeakWifi, Provider, Offline, Busy }

    private static readonly (Scenario S, int Seconds, string Name)[] Timeline =
    [
        (Scenario.Good, 50, "todo bien"),
        (Scenario.WeakWifi, 60, "Wi-Fi débil"),
        (Scenario.Good, 40, "todo bien"),
        (Scenario.Provider, 60, "falla del proveedor"),
        (Scenario.Busy, 50, "equipo saturando la red"),
        (Scenario.Offline, 40, "sin internet"),
    ];

    private Scenario Current
    {
        get
        {
            int total = Timeline.Sum(t => t.Seconds);
            int t = (int)(DateTimeOffset.Now - _start).TotalSeconds % total;
            foreach (var (s, secs, _) in Timeline)
            {
                if (t < secs) return s;
                t -= secs;
            }
            return Scenario.Good;
        }
    }

    public async Task<PingOutcome> PingAsync(IPAddress target, int timeoutMs, int? ttl, CancellationToken ct)
    {
        await Task.Delay(5, ct).ConfigureAwait(false);
        var s = Current;
        bool isRouter = target.Equals(Gateway);
        double Jit(double baseMs, double spread) => Math.Max(0.5, baseMs + (_rnd.NextDouble() - 0.5) * 2 * spread);
        bool Lose(double p) => _rnd.NextDouble() < p;

        if (ttl is not null)
        {
            if (ttl == 1) return new PingOutcome(IPStatus.TtlExpired, Jit(2, 1), Gateway);
            return s switch
            {
                Scenario.Offline => PingOutcome.Failed(IPStatus.TimedOut),
                Scenario.Provider when Lose(0.25) => PingOutcome.Failed(IPStatus.TimedOut),
                Scenario.Provider => new PingOutcome(IPStatus.TtlExpired, Jit(60, 45), Isp),
                Scenario.WeakWifi when Lose(0.15) => PingOutcome.Failed(IPStatus.TimedOut),
                Scenario.WeakWifi => new PingOutcome(IPStatus.TtlExpired, Jit(40, 35), Isp),
                _ => new PingOutcome(IPStatus.TtlExpired, Jit(8, 2), Isp),
            };
        }

        if (isRouter)
        {
            return s switch
            {
                Scenario.WeakWifi when Lose(0.15) => PingOutcome.Failed(IPStatus.TimedOut),
                Scenario.WeakWifi => new PingOutcome(IPStatus.Success, Jit(35, 30), target),
                _ => new PingOutcome(IPStatus.Success, Jit(3, 1.5), target),
            };
        }

        return s switch
        {
            Scenario.Offline => PingOutcome.Failed(IPStatus.TimedOut),
            Scenario.Provider when Lose(0.25) => PingOutcome.Failed(IPStatus.TimedOut),
            Scenario.Provider => new PingOutcome(IPStatus.Success, Jit(90, 60), target),
            Scenario.WeakWifi when Lose(0.15) => PingOutcome.Failed(IPStatus.TimedOut),
            Scenario.WeakWifi => new PingOutcome(IPStatus.Success, Jit(70, 45), target),
            Scenario.Busy => new PingOutcome(IPStatus.Success, Jit(220, 60), target),
            _ => new PingOutcome(IPStatus.Success, Jit(24, 3), target),
        };
    }

    public Task<TcpProbeOutcome> ProbeAsync(string host, int port, int timeoutMs, CancellationToken ct) =>
        Task.FromResult(Current == Scenario.Offline
            ? new TcpProbeOutcome(false, null, false, null, "Sin conexión")
            : new TcpProbeOutcome(true, 3, true, Current == Scenario.Good ? 38 : 120, null));

    public NetworkContext GetContext(bool includeWifiDetails)
    {
        bool weak = Current == Scenario.WeakWifi;
        return new NetworkContext(true, LinkType.WiFi, "Adaptador Wi-Fi (demostración)", "demo", IPAddress.Parse("192.168.1.34"), Gateway,
            "3C-84-6A-12-34-56", weak ? 26 : 866,
            new WifiInfo("Red de demostración", "3C:84:6A:12:34:57", weak ? 24 : 88, weak ? -82 : -48, weak ? 6.5 : 866, weak ? 6.5 : 866,
                weak ? "2.4 GHz" : "5 GHz", weak ? 6 : 44, "Wi-Fi 5 (802.11ac)", false),
            false, null);
    }

    public Task<bool?> CheckAsync(CancellationToken ct) => Task.FromResult<bool?>(false);

    public Throughput Sample(string? adapterId, DateTimeOffset now) =>
        Current == Scenario.Busy ? new Throughput(85, 18) : new Throughput(0.4 + _rnd.NextDouble(), 0.1);

    public CallInfo Detect() => (DateTimeOffset.Now - _start).TotalSeconds % 300 < 150 ? new CallInfo(true, "Teams") : CallInfo.None;
}
