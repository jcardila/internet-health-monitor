using System.Net;
using System.Net.NetworkInformation;

namespace InternetHealth.Core.Network;

public readonly record struct PingOutcome(IPStatus Status, double? RttMs, IPAddress? ReplyFrom)
{
    public bool IsSuccess => Status == IPStatus.Success;
    public static PingOutcome Failed(IPStatus status = IPStatus.Unknown) => new(status, null, null);
}

public readonly record struct TcpProbeOutcome(bool DnsOk, double? DnsMs, bool ConnectOk, double? ConnectMs, string? Error);

public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset Now => DateTimeOffset.Now;
}

/// <summary>Envía pings ICMP. Si <c>ttl</c> se indica, un "TtlExpired" desde un salto intermedio cuenta como respuesta.</summary>
public interface IPinger
{
    Task<PingOutcome> PingAsync(IPAddress target, int timeoutMs, int? ttl, CancellationToken ct);
}

/// <summary>Resuelve DNS y abre una conexión TCP, midiendo ambos tiempos.</summary>
public interface ITcpProber
{
    Task<TcpProbeOutcome> ProbeAsync(string host, int port, int timeoutMs, CancellationToken ct);
}

public interface INetworkContextProvider
{
    /// <param name="includeWifiDetails">Si es false, puede reutilizar el último dato de Wi-Fi (más barato).</param>
    NetworkContext GetContext(bool includeWifiDetails);
}

public interface ICallDetector
{
    CallInfo Detect();
}

public interface ICaptivePortalChecker
{
    /// <returns>true si hay portal cautivo, false si no, null si no se pudo determinar.</returns>
    Task<bool?> CheckAsync(CancellationToken ct);
}

public interface IThroughputMeter
{
    Throughput Sample(string? adapterId, DateTimeOffset now);
}

public sealed class NoCallDetector : ICallDetector
{
    public CallInfo Detect() => CallInfo.None;
}
