using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using InternetHealth.Core.Network;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Ping para Mac. Los pings normales usan <see cref="SystemPinger"/>; los pings con TTL (para
/// descubrir y medir el primer salto del proveedor) usan un socket ICMP propio, porque en macOS el
/// Ping de .NET informa el destino como origen de un "TTL expirado" en lugar del salto que respondió,
/// y el motor tomaría el router por el proveedor. macOS permite estos sockets sin ser administrador.
/// </summary>
internal sealed class MacPinger : IPinger, IDisposable
{
    private readonly SystemPinger _system = new();
    private int _sequence;

    public Task<PingOutcome> PingAsync(IPAddress target, int timeoutMs, int? ttl, CancellationToken ct) =>
        ttl is null ? _system.PingAsync(target, timeoutMs, null, ct) : PingWithTtlAsync(target, timeoutMs, ttl.Value, ct);

    private async Task<PingOutcome> PingWithTtlAsync(IPAddress target, int timeoutMs, int ttl, CancellationToken ct)
    {
        if (target.AddressFamily != AddressFamily.InterNetwork) return PingOutcome.Failed();
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Icmp);
            socket.Ttl = (short)Math.Clamp(ttl, 1, 255);

            ushort id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
            ushort seq = (ushort)Interlocked.Increment(ref _sequence);
            var packet = new byte[8 + 32];
            packet[0] = 8; // eco (echo request)
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(4), id);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(6), seq);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), Checksum(packet));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeoutMs);
            var sw = Stopwatch.StartNew();
            await socket.SendToAsync(packet, SocketFlags.None, new IPEndPoint(target, 0), timeout.Token).ConfigureAwait(false);

            var buffer = new byte[1500];
            while (true)
            {
                SocketReceiveFromResult r;
                try
                {
                    r = await socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), timeout.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    return PingOutcome.Failed(IPStatus.TimedOut);
                }
                var from = ((IPEndPoint)r.RemoteEndPoint).Address;
                var status = Parse(buffer.AsSpan(0, r.ReceivedBytes), id, seq);
                if (status is null) continue; // respuesta de otro ping
                double ms = sw.Elapsed.TotalMilliseconds;
                return status == IPStatus.Success
                    ? new PingOutcome(IPStatus.Success, ms, from)
                    : new PingOutcome(IPStatus.TtlExpired, ms, from);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return PingOutcome.Failed();
        }
    }

    /// <summary>Success si es nuestro eco, TtlExpired si un salto intermedio lo descartó, null si no es nuestro.</summary>
    internal static IPStatus? Parse(ReadOnlySpan<byte> data, ushort id, ushort seq)
    {
        var icmp = SkipIpHeader(data);
        if (icmp.Length < 8) return null;
        if (icmp[0] == 0) // echo reply
            return Matches(icmp, id, seq) ? IPStatus.Success : null;
        if (icmp[0] == 11) // time exceeded: trae la cabecera IP + 8 bytes de nuestro paquete original
        {
            var original = SkipIpHeader(icmp[8..]);
            return original.Length >= 8 && original[0] == 8 && Matches(original, id, seq) ? IPStatus.TtlExpired : null;
        }
        return null;
    }

    private static bool Matches(ReadOnlySpan<byte> icmp, ushort id, ushort seq) =>
        BinaryPrimitives.ReadUInt16BigEndian(icmp[4..]) == id && BinaryPrimitives.ReadUInt16BigEndian(icmp[6..]) == seq;

    /// <summary>macOS entrega la cabecera IPv4 junto con el mensaje ICMP.</summary>
    private static ReadOnlySpan<byte> SkipIpHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 20 && data[0] >> 4 == 4)
        {
            int ihl = (data[0] & 0x0F) * 4;
            return data.Length > ihl ? data[ihl..] : [];
        }
        return data;
    }

    private static ushort Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (int i = 0; i + 1 < data.Length; i += 2) sum += (uint)(data[i] << 8 | data[i + 1]);
        if (data.Length % 2 == 1) sum += (uint)(data[^1] << 8);
        while (sum >> 16 != 0) sum = (sum & 0xFFFF) + (sum >> 16);
        return (ushort)~sum;
    }

    public void Dispose() => _system.Dispose();
}
