using InternetHealth.Core.Model;
using InternetHealth.Core.Network;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Diagnosis;

/// <summary>Todo lo que el motor de diagnóstico necesita saber en un instante.</summary>
public sealed record Assessment(
    NetworkContext Context,
    LinkStats Router,
    bool RouterMeasurable,
    ProviderHop? ProviderHop,
    LinkStats Provider,
    LinkStats Internet,
    LinkStats Cloud,
    int CloudConsecutiveFailures,
    int DnsConsecutiveFailures,
    Throughput Throughput,
    bool HeavyUsage,
    bool? CaptivePortal,
    CallInfo Call);

/// <summary>Estado de un eslabón para dibujar la cadena.</summary>
public sealed record SegmentState(Segment Segment, Health Health, bool Measured, string Label, string Detail);

public sealed record DiagnosisText(string Title, string Summary, IReadOnlyList<string> Steps, string TrayText);

public sealed record Diagnosis(
    DiagnosisCode Code,
    Health Severity,
    Segment? Culprit,
    IReadOnlyList<SegmentState> Chain,
    DiagnosisText Text,
    double? Mos,
    bool IcmpBlocked)
{
    public static Diagnosis Checking { get; } = new(
        DiagnosisCode.Checking, Health.Unknown, null, [],
        Messages.Build(DiagnosisCode.Checking, null), null, false);

    public bool SameAs(Diagnosis other) => Code == other.Code && Severity == other.Severity;
}
