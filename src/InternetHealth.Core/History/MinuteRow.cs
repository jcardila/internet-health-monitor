using InternetHealth.Core.Model;

namespace InternetHealth.Core.History;

/// <summary>
/// Resumen de un minuto. Es la unidad del historial local y de los archivos que se comparten
/// (pensado para poder consolidar muchos equipos/sedes en Power BI).
/// </summary>
public sealed record MinuteRow
{
    public DateTimeOffset Minute { get; init; }          // hora local, inicio del minuto
    public string Device { get; init; } = "";
    public string User { get; init; } = "";
    public string AppVersion { get; init; } = "";
    public string ConnectionType { get; init; } = "";
    public string? Adapter { get; init; }
    public string? Ssid { get; init; }
    public string? Bssid { get; init; }
    public string? GatewayIp { get; init; }
    public string? GatewayMac { get; init; }
    public string? ProviderHop { get; init; }
    public bool Vpn { get; init; }
    public int? WifiSignalPct { get; init; }
    public int? WifiRssiDbm { get; init; }
    public string? WifiBand { get; init; }
    public int? WifiChannel { get; init; }
    public double? LinkRateMbps { get; init; }

    public ChannelSummary Router { get; init; } = ChannelSummary.Empty;
    public ChannelSummary Provider { get; init; } = ChannelSummary.Empty;
    public ChannelSummary Internet { get; init; } = ChannelSummary.Empty;
    public ChannelSummary Cloud { get; init; } = ChannelSummary.Empty;

    public double? MosAvg { get; init; }
    public double? MosMin { get; init; }
    public double RxMbpsAvg { get; init; }
    public double TxMbpsAvg { get; init; }
    public bool InCall { get; init; }
    public Health WorstSeverity { get; init; }
    public DiagnosisCode WorstDiagnosis { get; init; }
    public int SecondsGood { get; init; }
    public int SecondsFair { get; init; }
    public int SecondsPoor { get; init; }
    public int SecondsDown { get; init; }
}

public sealed record ChannelSummary(int Sent, int Lost, double? AvgMs, double? MaxMs, double? JitterMs)
{
    public static readonly ChannelSummary Empty = new(0, 0, null, null, null);
    public double? LossPct => Sent > 0 ? 100.0 * Lost / Sent : null;
}
