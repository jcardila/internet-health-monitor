using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.Model;
using InternetHealth.Core.Network;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Monitoring;

/// <summary>Estado completo publicado por el motor después de cada ronda de mediciones.</summary>
public sealed record MonitorSnapshot(
    DateTimeOffset Time,
    NetworkContext Context,
    LinkStats Router,
    bool RouterMeasurable,
    ProviderHop? ProviderHop,
    LinkStats Provider,
    LinkStats Internet,
    LinkStats Cloud,
    double? CloudLastMs,
    double? DnsLastMs,
    Throughput Throughput,
    CallInfo Call,
    bool? CaptivePortal,
    Diagnosis.Diagnosis Raw,
    Diagnosis.Diagnosis Stable,
    DateTimeOffset StableSince,
    MonitorMode Mode)
{
    public Health Severity => Stable.Severity;
    public double? Mos => Stable.Mos;
}
