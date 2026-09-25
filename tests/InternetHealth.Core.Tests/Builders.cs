using System.Net;
using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.Model;
using InternetHealth.Core.Network;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Tests;

/// <summary>Constructores de escenarios para las pruebas de diagnóstico.</summary>
internal static class Build
{
    public static readonly DateTimeOffset T0 = new(2026, 9, 24, 10, 0, 0, TimeSpan.FromHours(-5));

    /// <summary>Estadísticas a partir de una secuencia de RTT (null = perdido).</summary>
    public static LinkStats Stats(params double?[] samples)
    {
        var b = new SampleBuffer(200);
        for (int i = 0; i < samples.Length; i++) b.Add(T0.AddSeconds(i), samples[i]);
        return b.Compute(T0.AddSeconds(samples.Length), TimeSpan.FromMinutes(10), 200);
    }

    public static LinkStats Steady(double ms, int count = 30, int lost = 0, double wobble = 0)
    {
        var list = new List<double?>();
        for (int i = 0; i < count; i++)
        {
            bool isLost = lost > 0 && i % Math.Max(1, count / lost) == 0 && list.Count(x => x is null) < lost;
            list.Add(isLost ? null : ms + (i % 2 == 0 ? wobble : -wobble));
        }
        return Stats(list.ToArray());
    }

    public static LinkStats AllLost(int count = 30) => Stats(Enumerable.Repeat<double?>(null, count).ToArray());

    public static NetworkContext Wifi(int signal = 90, double rate = 400, string band = "5 GHz", bool vpn = false) =>
        new(true, LinkType.WiFi, "Intel Wi-Fi 6", "id-wifi", IPAddress.Parse("192.168.1.20"), IPAddress.Parse("192.168.1.1"),
            "AA-BB-CC-DD-EE-FF", rate, new WifiInfo("Oficina", "11:22:33:44:55:66", signal, -50, rate, rate, band, 36, "Wi-Fi 6", false),
            vpn, vpn ? "FortiClient VPN" : null);

    public static NetworkContext Cable(double speed = 1000) =>
        new(true, LinkType.Ethernet, "Realtek PCIe GbE", "id-eth", IPAddress.Parse("10.0.0.20"), IPAddress.Parse("10.0.0.1"),
            null, speed, null, false, null);

    public static Assessment Assess(
        NetworkContext? ctx = null,
        LinkStats? router = null,
        bool routerMeasurable = true,
        LinkStats? provider = null,
        bool hopKnown = true,
        LinkStats? internet = null,
        LinkStats? cloud = null,
        int cloudFailures = 0,
        int dnsFailures = 0,
        bool heavy = false,
        bool? captive = false,
        bool inCall = false) =>
        new(ctx ?? Wifi(),
            router ?? Steady(3),
            routerMeasurable,
            hopKnown ? new ProviderHop(IPAddress.Parse("181.49.1.1"), 3) : null,
            provider ?? Steady(9),
            internet ?? Steady(25),
            cloud ?? Steady(40, 6),
            cloudFailures,
            dnsFailures,
            heavy ? new Throughput(20, 12) : new Throughput(1, 0.2),
            heavy,
            captive,
            inCall ? new CallInfo(true, "Teams") : CallInfo.None);
}
