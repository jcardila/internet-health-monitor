using System.Globalization;
using InternetHealth.Core.Model;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Diagnosis;

/// <summary>
/// Motor de diagnóstico. Principio: primero se evalúa la experiencia de extremo a extremo
/// (lo que el usuario vive en una llamada). Solo si esa experiencia es mala se busca el eslabón
/// culpable. Así, un router que responde lento al ping no genera alarmas si todo funciona.
/// </summary>
public static class DiagnosisEngine
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    public static Diagnosis Diagnose(Assessment a)
    {
        var ctx = a.Context;
        if (!ctx.HasConnection)
        {
            var chainOff = BuildChain(a, Health.Down, Health.Unknown, Health.Unknown, Health.Unknown, Health.Down, DiagnosisCode.NoConnection, Segment.Link, Health.Down);
            return new Diagnosis(DiagnosisCode.NoConnection, Health.Down, Segment.Link, chainOff,
                Messages.Build(DiagnosisCode.NoConnection, a), 1, false);
        }

        Health link = Thresholds.Link(ctx);

        bool icmpBlocked = a.Internet.Sent >= Thresholds.MinSamples && a.Internet.AllLost
                           && a.Cloud.Received > 0 && a.CloudConsecutiveFailures == 0;
        var e2eStats = icmpBlocked ? a.Cloud : a.Internet;
        Health e2e = Thresholds.EndToEnd(e2eStats, out var mos);

        Health router = a.RouterMeasurable ? Thresholds.Router(a.Router) : Health.Unknown;
        Health provider = a.ProviderHop is not null ? Thresholds.Provider(a.Provider) : Health.Unknown;

        // Si internet funciona, un salto que no responde simplemente filtra el ping.
        if (e2e is Health.Good or Health.Fair)
        {
            if (provider == Health.Down) provider = Health.Unknown;
            if (router == Health.Down) router = Health.Unknown;
        }

        // Si la experiencia de extremo a extremo es buena, las pérdidas o demoras de ping en
        // saltos intermedios son solo baja prioridad del ICMP en esos equipos: no son un problema.
        if (e2e == Health.Good)
        {
            if (router > Health.Good) router = Health.Good;
            if (provider > Health.Good) provider = Health.Good;
        }

        Health cloud = a.Cloud.Sent == 0 ? Health.Unknown
            : a.CloudConsecutiveFailures >= 3 ? Health.Down
            : Health.Good;
        bool dnsFailing = a.DnsConsecutiveFailures >= 3;

        DiagnosisCode code;
        Segment? culprit;
        Health severity;

        if (a.CaptivePortal == true)
        {
            code = DiagnosisCode.CaptivePortal;
            culprit = Segment.Router;
            severity = Health.Down;
        }
        else if (e2e == Health.Unknown)
        {
            code = DiagnosisCode.Checking;
            culprit = null;
            severity = Health.Unknown;
        }
        else if (e2e == Health.Down)
        {
            severity = Health.Down;
            if (a.RouterMeasurable && router == Health.Down)
            {
                bool weakWifi = ctx.LinkType == LinkType.WiFi && link >= Health.Poor;
                code = weakWifi ? DiagnosisCode.WeakWifi : DiagnosisCode.RouterUnreachable;
                culprit = weakWifi ? Segment.Link : Segment.Router;
            }
            else
            {
                code = DiagnosisCode.NoInternet;
                culprit = Segment.Provider;
            }
        }
        else if (e2e == Health.Good)
        {
            if (cloud == Health.Down)
            {
                code = DiagnosisCode.CloudUnreachable; culprit = Segment.Internet; severity = Health.Fair;
            }
            else if (dnsFailing)
            {
                code = DiagnosisCode.DnsIssue; culprit = Segment.Internet; severity = Health.Fair;
            }
            else if (link >= Health.Poor)
            {
                code = DiagnosisCode.GoodButWeakLink; culprit = Segment.Link; severity = Health.Good;
            }
            else
            {
                code = DiagnosisCode.AllGood; culprit = null; severity = Health.Good;
            }
        }
        else
        {
            severity = e2e;
            bool linkIsCulprit = (link >= Health.Poor && router != Health.Good)
                                 || (link == Health.Fair && router >= Health.Fair);
            if (linkIsCulprit)
            {
                code = ctx.LinkType == LinkType.WiFi ? DiagnosisCode.WeakWifi : DiagnosisCode.CableIssue;
                culprit = Segment.Link;
            }
            else if (router >= Health.Fair)
            {
                code = DiagnosisCode.LocalNetwork; culprit = Segment.Router;
            }
            else if (a.HeavyUsage)
            {
                code = DiagnosisCode.DeviceBusy; culprit = Segment.Device;
            }
            else if (provider >= Health.Fair)
            {
                code = DiagnosisCode.ProviderIssue; culprit = Segment.Provider;
            }
            else if (router == Health.Good && provider == Health.Good)
            {
                code = DiagnosisCode.ExternalIssue; culprit = Segment.Internet;
            }
            else if (router == Health.Good)
            {
                code = DiagnosisCode.ProviderIssue; culprit = Segment.Provider;
            }
            else
            {
                code = DiagnosisCode.Degraded; culprit = null;
            }
        }

        Health internetNode = e2e.Worst(cloud == Health.Down ? Health.Fair : Health.Unknown);
        if (dnsFailing) internetNode = internetNode.Worst(Health.Fair);
        var chain = BuildChain(a, link, router, provider, internetNode, e2e, code, culprit, severity);
        return new Diagnosis(code, severity, culprit, chain, Messages.Build(code, a), mos, icmpBlocked);
    }

    private static List<SegmentState> BuildChain(
        Assessment a, Health link, Health router, Health provider, Health internet, Health e2e,
        DiagnosisCode code, Segment? culprit, Health severity)
    {
        var ctx = a.Context;

        Health Mark(Segment s, Health h) => culprit == s && severity > Health.Good ? h.Worst(severity) : h;

        // Equipo
        string deviceDetail = a.HeavyUsage
            ? $"Usando ↓{a.Throughput.RxMbps.ToString("0.#", Es)} ↑{a.Throughput.TxMbps.ToString("0.#", Es)} Mbps"
            : a.Call.InCall ? "En llamada" : "Funcionando";
        var device = new SegmentState(Segment.Device, Mark(Segment.Device, ctx.HasConnection ? Health.Good : Health.Unknown),
            true, "Tu equipo", deviceDetail);

        // Enlace
        string linkLabel = ctx.LinkType switch
        {
            LinkType.WiFi => "Wi-Fi",
            LinkType.Ethernet => "Cable",
            LinkType.Cellular => "Datos móviles",
            _ => "Conexión",
        };
        string linkDetail;
        if (!ctx.HasConnection) linkDetail = "Desconectado";
        else if (ctx.LinkType == LinkType.WiFi && ctx.Wifi is { } w)
        {
            var parts = new List<string>();
            if (w.SignalQuality is int q) parts.Add($"Señal {q} %");
            if (!string.IsNullOrEmpty(w.Band)) parts.Add(w.Band!);
            if (parts.Count == 0 && ctx.EffectiveLinkRateMbps is double r0) parts.Add($"{r0:0} Mbps");
            linkDetail = parts.Count > 0 ? string.Join(" · ", parts) : "Conectado";
        }
        else linkDetail = ctx.EffectiveLinkRateMbps is double r ? $"{r:0} Mbps" : "Conectado";
        var linkState = new SegmentState(Segment.Link, Mark(Segment.Link, link), ctx.HasConnection, linkLabel, linkDetail);

        // Router
        SegmentState routerState;
        if (code == DiagnosisCode.NoConnection)
            routerState = new SegmentState(Segment.Router, Health.Unknown, false, "Router", "Sin datos");
        else if (!a.RouterMeasurable)
            routerState = new SegmentState(Segment.Router, Mark(Segment.Router, Health.Unknown), false, "Router",
                "No responde a las pruebas (normal en algunos equipos)");
        else
            routerState = new SegmentState(Segment.Router, Mark(Segment.Router, router), true, "Router", StatsDetail(a.Router));

        // Proveedor
        SegmentState providerState = a.ProviderHop is null || provider == Health.Unknown
            ? new SegmentState(Segment.Provider, Mark(Segment.Provider, Health.Unknown), false, "Proveedor",
                code == DiagnosisCode.NoInternet ? "Sin salida a internet" : "Sin datos")
            : new SegmentState(Segment.Provider, Mark(Segment.Provider, provider), true, "Proveedor", StatsDetail(a.Provider));

        // Internet
        string internetDetail = e2e == Health.Down ? "Sin respuesta" : StatsDetail(a.Internet.AllLost ? a.Cloud : a.Internet);
        var internetState = new SegmentState(Segment.Internet, Mark(Segment.Internet, internet), e2e != Health.Unknown,
            "Internet", internetDetail);

        return [device, linkState, routerState, providerState, internetState];
    }

    private static string StatsDetail(LinkStats s)
    {
        if (!s.HasData) return "Midiendo…";
        if (s.AllLost) return "Sin respuesta";
        var ms = s.MedianMs ?? s.MeanMs ?? 0;
        return s.LossPct >= 1
            ? $"{ms:0} ms · pérdida {s.LossPct.ToString("0", Es)} %"
            : $"{ms:0} ms";
    }
}
