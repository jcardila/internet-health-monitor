using InternetHealth.Core.Model;
using InternetHealth.Core.Network;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Diagnosis;

/// <summary>
/// Umbrales de clasificación. Los de extremo a extremo siguen las recomendaciones de red de
/// Microsoft Teams (RTT &lt; 100 ms, jitter &lt; 30 ms, pérdida &lt; 1 %), con margen para el ruido
/// propio de las mediciones con ping y así evitar falsas alarmas.
/// </summary>
public static class Thresholds
{
    public const int MinSamples = 5;

    // --- Extremo a extremo (internet) ---
    public const double GoodMos = 4.0;
    public const double FairMos = 3.5;
    public const double E2eFairLoss = 4;     // % (≥ 2 de 30 muestras)
    public const double E2ePoorLoss = 10;
    public const double E2eFairJitter = 30;
    public const double E2ePoorJitter = 60;
    public const double E2eFairRtt = 150;
    public const double E2ePoorRtt = 300;
    public const double E2eDownLoss = 90;

    // --- Red local (router) — los routers dan baja prioridad al ping: umbrales holgados ---
    public const double RouterFairLoss = 4;
    public const double RouterPoorLoss = 10;
    public const double RouterFairMedian = 40;
    public const double RouterPoorMedian = 100;
    public const double RouterFairJitter = 25;
    public const double RouterPoorJitter = 50;

    // --- Primer salto del proveedor ---
    public const double ProviderFairLoss = 4;
    public const double ProviderPoorLoss = 10;
    public const double ProviderFairMedian = 60;
    public const double ProviderPoorMedian = 150;
    public const double ProviderFairJitter = 30;
    public const double ProviderPoorJitter = 60;

    // --- Enlace local ---
    public const int WifiPoorSignal = 35;
    public const int WifiFairSignal = 55;
    public const double WifiPoorRate = 12;
    public const double WifiFairRate = 40;
    public const double EthernetFairRate = 10;

    // --- Uso intensivo del propio equipo ---
    public const double HeavyTxMbps = 5;
    public const double HeavyRxMbps = 60;

    public static Health EndToEnd(LinkStats s, out double? mos)
    {
        mos = null;
        if (s.Sent < MinSamples) return Health.Unknown;
        if (s.LossPct >= E2eDownLoss) { mos = 1; return Health.Down; }
        mos = CallQuality.Mos(s);
        double jitter = s.JitterMs ?? 0, rtt = s.MeanMs ?? 0;
        if (s.LossPct >= E2ePoorLoss || jitter > E2ePoorJitter || rtt > E2ePoorRtt || mos < FairMos) return Health.Poor;
        if (s.LossPct >= E2eFairLoss || jitter > E2eFairJitter || rtt > E2eFairRtt || mos < GoodMos) return Health.Fair;
        return Health.Good;
    }

    public static Health Router(LinkStats s)
    {
        if (s.Sent < MinSamples) return Health.Unknown;
        if (s.LossPct >= E2eDownLoss) return Health.Down;
        double med = s.MedianMs ?? 0, jit = s.JitterMs ?? 0;
        if (s.LossPct >= RouterPoorLoss || med > RouterPoorMedian || jit > RouterPoorJitter) return Health.Poor;
        if (s.LossPct >= RouterFairLoss || med > RouterFairMedian || jit > RouterFairJitter) return Health.Fair;
        return Health.Good;
    }

    public static Health Provider(LinkStats s)
    {
        if (s.Sent < MinSamples) return Health.Unknown;
        if (s.LossPct >= E2eDownLoss) return Health.Down;
        double med = s.MedianMs ?? 0, jit = s.JitterMs ?? 0;
        if (s.LossPct >= ProviderPoorLoss || med > ProviderPoorMedian || jit > ProviderPoorJitter) return Health.Poor;
        if (s.LossPct >= ProviderFairLoss || med > ProviderFairMedian || jit > ProviderFairJitter) return Health.Fair;
        return Health.Good;
    }

    public static Health Link(NetworkContext ctx)
    {
        if (!ctx.HasConnection) return Health.Down;
        if (ctx.LinkType == LinkType.WiFi && ctx.Wifi is { } w)
        {
            var h = Health.Good;
            if (w.SignalQuality is int q)
                h = h.Worst(q < WifiPoorSignal ? Health.Poor : q < WifiFairSignal ? Health.Fair : Health.Good);
            var rate = w.RxRateMbps ?? ctx.LinkSpeedMbps;
            if (rate is double r && r > 0)
                h = h.Worst(r < WifiPoorRate ? Health.Poor : r < WifiFairRate ? Health.Fair : Health.Good);
            return h;
        }
        if (ctx.LinkType == LinkType.WiFi && ctx.LinkSpeedMbps is double wr && wr > 0)
            return wr < WifiPoorRate ? Health.Poor : wr < WifiFairRate ? Health.Fair : Health.Good;
        if (ctx.LinkType == LinkType.Ethernet && ctx.LinkSpeedMbps is double er && er > 0)
            return er <= EthernetFairRate ? Health.Fair : Health.Good;
        return Health.Good;
    }

    public static bool IsHeavyUsage(Throughput t) => t.TxMbps >= HeavyTxMbps || t.RxMbps >= HeavyRxMbps;
}
