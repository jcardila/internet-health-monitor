using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Stats;

namespace InternetHealth.App.ViewModels;

public sealed class ChainNodeViewModel : ObservableObject
{
    private Health _health;
    private string _label = "";
    private string _detail = "";
    private bool _isCulprit;
    private bool _measured;
    private Geometry? _icon;

    public Segment Segment { get; init; }
    public bool IsLast { get; init; }
    public bool IsFirst => Segment == Segment.Device;
    public Health Health { get => _health; set => Set(ref _health, value); }
    public string Label { get => _label; set => Set(ref _label, value); }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public bool IsCulprit { get => _isCulprit; set => Set(ref _isCulprit, value); }
    public bool Measured { get => _measured; set => Set(ref _measured, value); }
    public Geometry? Icon { get => _icon; set => Set(ref _icon, value); }
    public string AutomationName => $"{Label}: {Health.ToLabel()}. {Detail}";
    public void RaiseHealth() => Raise(nameof(Health));

    public void Update(Health health, string label, string detail, bool culprit, bool measured, Geometry? icon)
    {
        Health = health; Label = label; Detail = detail; IsCulprit = culprit; Measured = measured; Icon = icon;
        Raise(nameof(AutomationName));
    }
}

public sealed class StepViewModel : ObservableObject
{
    private bool _done;
    public int Number { get; init; }
    public string Text { get; init; } = "";
    public bool Done { get => _done; set => Set(ref _done, value); }
}

public sealed class MetricCardViewModel : ObservableObject
{
    private string _primary = "–";
    private string _secondary = "";
    private string _tertiary = "";
    private Health _health;
    private double?[]? _values;

    public string Title { get; init; } = "";
    public string Primary { get => _primary; set => Set(ref _primary, value); }
    public string Secondary { get => _secondary; set => Set(ref _secondary, value); }
    public string Tertiary { get => _tertiary; set => Set(ref _tertiary, value); }
    public Health Health { get => _health; set => Set(ref _health, value); }
    public double?[]? Values { get => _values; set => Set(ref _values, value); }
    public bool HasChart { get; init; }
    public void RaiseHealth() => Raise(nameof(Health));
}

public sealed record KeyValueItem(string Key, string Value);

/// <summary>Estado presentado en el panel de la bandeja y en la ventana de detalle.</summary>
public sealed class DashboardViewModel : ObservableObject
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    private Health _severity = Health.Unknown;
    private string _title = "Verificando tu conexión…";
    private string _summary = "Estamos tomando las primeras mediciones.";
    private string _severityLabel = "Verificando";
    private double? _mos;
    private string _mosLabel = "Midiendo…";
    private string _mosText = "";
    private double _mosFraction;
    private string _latencyText = "–";
    private string _jitterText = "–";
    private string _lossText = "–";
    private bool _inCall;
    private string _callText = "";
    private string _updatedText = "";
    private bool _isCaptivePortal;
    private string _modeText = "";
    private string _firstStep = "";
    private bool _hasSteps;
    private bool _locationHint;
    private DiagnosisCode _code = DiagnosisCode.Checking;
    private bool _dndActive;
    private bool _isImproving;

    public DashboardViewModel()
    {
        foreach (var seg in Enum.GetValues<Segment>())
            Chain.Add(new ChainNodeViewModel
            {
                Segment = seg, IsLast = seg == Segment.Internet, Label = DefaultLabel(seg), Detail = "Midiendo…",
                Icon = IconFor(seg, LinkType.WiFi),
            });
    }

    public ObservableCollection<ChainNodeViewModel> Chain { get; } = [];
    public ObservableCollection<StepViewModel> Steps { get; } = [];
    public ObservableCollection<KeyValueItem> Details { get; } = [];

    public MetricCardViewModel InternetCard { get; } = new() { Title = "Internet", HasChart = true };
    public MetricCardViewModel RouterCard { get; } = new() { Title = "Router (red local)", HasChart = true };
    public MetricCardViewModel ProviderCard { get; } = new() { Title = "Proveedor de internet", HasChart = true };
    public MetricCardViewModel CloudCard { get; } = new() { Title = "Microsoft 365", HasChart = true };
    public MetricCardViewModel LinkCard { get; } = new() { Title = "Wi-Fi / cable" };
    public MetricCardViewModel DeviceCard { get; } = new() { Title = "Tu equipo" };

    public Health Severity { get => _severity; private set => Set(ref _severity, value); }
    public string SeverityLabel { get => _severityLabel; private set => Set(ref _severityLabel, value); }
    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public double? Mos { get => _mos; private set => Set(ref _mos, value); }
    public string MosLabel { get => _mosLabel; private set => Set(ref _mosLabel, value); }
    public string MosText { get => _mosText; private set => Set(ref _mosText, value); }
    public double MosFraction { get => _mosFraction; private set => Set(ref _mosFraction, value); }
    public string LatencyText { get => _latencyText; private set => Set(ref _latencyText, value); }
    public string JitterText { get => _jitterText; private set => Set(ref _jitterText, value); }
    public string LossText { get => _lossText; private set => Set(ref _lossText, value); }
    public bool InCall { get => _inCall; private set => Set(ref _inCall, value); }
    public string CallText { get => _callText; private set => Set(ref _callText, value); }
    public string UpdatedText { get => _updatedText; private set => Set(ref _updatedText, value); }
    public bool IsCaptivePortal { get => _isCaptivePortal; private set => Set(ref _isCaptivePortal, value); }
    public string ModeText { get => _modeText; private set => Set(ref _modeText, value); }
    public string FirstStep { get => _firstStep; private set => Set(ref _firstStep, value); }
    public bool HasSteps { get => _hasSteps; private set => Set(ref _hasSteps, value); }
    public bool LocationHint { get => _locationHint; private set => Set(ref _locationHint, value); }
    public bool DndActive { get => _dndActive; set => Set(ref _dndActive, value); }
    public DiagnosisCode Code => _code;

    /// <summary>Modo demostración: la franja azul lo indica siempre.</summary>
    public bool IsDemo { get; init; }

    /// <summary>
    /// Las mediciones ya mejoraron pero el estado aún no cambia (la histéresis exige 20 s estables).
    /// Evita la confusión de ver cifras buenas con un estado rojo.
    /// </summary>
    public bool IsImproving { get => _isImproving; private set => Set(ref _isImproving, value); }

    /// <summary>Tras cambiar entre tema claro y oscuro, fuerza a recalcular los colores de estado.</summary>
    public void RefreshThemeBindings()
    {
        Raise(nameof(Severity));
        foreach (var n in Chain) n.RaiseHealth();
        foreach (var c in new[] { InternetCard, RouterCard, ProviderCard, CloudCard, LinkCard, DeviceCard }) c.RaiseHealth();
    }

    public void Apply(MonitorSnapshot s, MonitorEngine engine, bool includeDetails)
    {
        var d = s.Stable;
        Severity = d.Severity;
        SeverityLabel = d.Severity.ToLabel();
        Title = d.Text.Title;
        Summary = d.Text.Summary;
        IsCaptivePortal = d.Code == DiagnosisCode.CaptivePortal;
        IsImproving = d.Severity > Health.Good && s.Raw.Code != DiagnosisCode.Checking && s.Raw.Severity < d.Severity;

        if (d.Code != _code || Steps.Count != d.Text.Steps.Count || (Steps.Count > 0 && Steps[0].Text != d.Text.Steps[0]))
        {
            _code = d.Code;
            Steps.Clear();
            int n = 1;
            foreach (var step in d.Text.Steps) Steps.Add(new StepViewModel { Number = n++, Text = step });
        }
        HasSteps = Steps.Count > 0 && (d.Severity >= Health.Fair || d.Code == DiagnosisCode.GoodButWeakLink);
        FirstStep = d.Text.Steps.Count > 0 ? d.Text.Steps[0] : "";

        Mos = d.Mos;
        MosLabel = CallQuality.Label(d.Mos);
        MosText = d.Mos is double m ? $"{m.ToString("0.0", Es)} de 5" : "";
        MosFraction = d.Mos is double m2 ? Math.Clamp((m2 - 1) / 3.5, 0, 1) : 0;

        var e2e = d.IcmpBlocked ? s.Cloud : s.Internet;
        // Sin conexión, el promedio de la ventana aún guarda respuestas viejas: no se muestran.
        bool noResponse = e2e.AllLost || d.Severity == Health.Down;
        LatencyText = noResponse ? "sin respuesta" : e2e.MeanMs is double lat ? $"{lat:0} ms" : "–";
        JitterText = !noResponse && e2e.JitterMs is double j ? $"{j:0} ms" : "–";
        LossText = e2e.HasData ? $"{e2e.LossPct.ToString("0.#", Es)} %" : "–";

        InCall = s.Call.InCall;
        CallText = s.Call.InCall ? $"En llamada ({s.Call.AppName ?? "micrófono en uso"}) · medición cada segundo" : "";
        ModeText = s.Mode switch
        {
            MonitorMode.Active => "Medición cada segundo",
            MonitorMode.Paused => "En pausa",
            _ => "Medición en reposo (cada pocos segundos)",
        };
        UpdatedText = "Última medición: " + s.Time.ToString("HH:mm:ss", Es);
        LocationHint = s.Context.Wifi?.LocationPermissionMissing == true;

        ApplyChain(s);
        if (includeDetails) ApplyDetails(s, engine);
    }

    private void ApplyChain(MonitorSnapshot s)
    {
        var d = s.Stable;
        var chain = d.Chain.Count == 5 ? d.Chain : null;
        foreach (var node in Chain)
        {
            var src = chain?[(int)node.Segment];
            string label = src?.Label ?? DefaultLabel(node.Segment);
            var icon = IconFor(node.Segment, s.Context.LinkType);
            node.Update(
                src?.Health ?? Health.Unknown,
                label,
                src?.Detail ?? "Midiendo…",
                d.Culprit == node.Segment && d.Severity > Health.Good,
                src?.Measured ?? false,
                icon);
        }
    }

    private void ApplyDetails(MonitorSnapshot s, MonitorEngine engine)
    {
        const int points = 90;
        var chain = s.Stable.Chain;
        Health H(Segment seg) => chain.Count == 5 ? chain[(int)seg].Health : Health.Unknown;

        Fill(InternetCard, s.Internet, H(Segment.Internet), engine.Recent(HistoryChannel.Internet, points),
            s.Stable.IcmpBlocked ? "Esta red bloquea el ping: se mide con conexiones reales" : "Promedio de las últimas mediciones");
        if (s.RouterMeasurable)
            Fill(RouterCard, s.Router, H(Segment.Router), engine.Recent(HistoryChannel.Router, points),
                s.Context.Gateway is null ? "" : $"Router {s.Context.Gateway}");
        else
        {
            RouterCard.Primary = "No responde";
            RouterCard.Secondary = "Este router no responde a las pruebas de ping (es normal en algunos equipos).";
            RouterCard.Tertiary = s.Context.Gateway?.ToString() ?? "";
            RouterCard.Health = Health.Unknown;
            RouterCard.Values = null;
        }
        if (s.ProviderHop is { } hop)
            Fill(ProviderCard, s.Provider, H(Segment.Provider), engine.Recent(HistoryChannel.Provider, points), $"Primer salto: {hop.Address}");
        else
        {
            ProviderCard.Primary = "–";
            ProviderCard.Secondary = "No fue posible identificar el primer equipo del proveedor en esta red.";
            ProviderCard.Tertiary = "";
            ProviderCard.Health = Health.Unknown;
            ProviderCard.Values = null;
        }

        CloudCard.Primary = s.CloudLastMs is double c ? $"{c:0} ms" : s.Cloud.AllLost ? "Sin conexión" : "–";
        CloudCard.Secondary = s.Cloud.HasData
            ? $"Conexiones exitosas: {s.Cloud.Received} de {s.Cloud.Sent}"
            : "Midiendo…";
        CloudCard.Tertiary = s.DnsLastMs is double dns ? $"Tiempo de conexión a Teams/Outlook · DNS {dns:0} ms" : "Tiempo de conexión a Teams/Outlook";
        CloudCard.Health = s.Cloud.HasData ? (s.Cloud.AllLost ? Health.Down : s.Cloud.LossPct > 0 ? Health.Fair : Health.Good) : Health.Unknown;
        CloudCard.Values = engine.Recent(HistoryChannel.Cloud, 30);

        var ctx = s.Context;
        var w = ctx.Wifi;
        if (ctx.LinkType == LinkType.WiFi)
        {
            LinkCard.Primary = w?.SignalQuality is int q ? $"Señal {q} %" : "Wi-Fi";
            var parts = new List<string>();
            if (w?.Ssid is { } ssid) parts.Add($"Red «{ssid}»");
            if (w?.Band is { } band) parts.Add(band + (w.Channel is int ch ? $" · canal {ch}" : ""));
            LinkCard.Secondary = string.Join(" · ", parts);
            var t = new List<string>();
            if (ctx.EffectiveLinkRateMbps is double r) t.Add($"Velocidad del enlace {r:0} Mbps");
            if (w?.RssiDbm is int rssi) t.Add($"{rssi} dBm");
            if (w?.PhyType is { } phy) t.Add(phy);
            LinkCard.Tertiary = string.Join(" · ", t);
        }
        else
        {
            LinkCard.Primary = ctx.HasConnection ? ctx.LinkTypeLabel : "Sin conexión";
            LinkCard.Secondary = ctx.AdapterName ?? "";
            LinkCard.Tertiary = ctx.EffectiveLinkRateMbps is double r ? $"Velocidad del enlace {r:0} Mbps" : "";
        }
        LinkCard.Health = H(Segment.Link);

        DeviceCard.Primary = $"↓ {s.Throughput.RxMbps.ToString("0.0", Es)}  ↑ {s.Throughput.TxMbps.ToString("0.0", Es)} Mbps";
        DeviceCard.Secondary = "Uso de red de todo el equipo en este momento";
        DeviceCard.Tertiary = string.Join(" · ", new[]
        {
            ctx.VpnActive ? $"VPN activa ({ctx.VpnName})" : "Sin VPN",
            s.Call.InCall ? $"En llamada ({s.Call.AppName})" : null,
        }.Where(x => x is not null));
        DeviceCard.Health = H(Segment.Device);

        var details = new List<KeyValueItem>
        {
            new("Estado", $"{s.Stable.Severity.ToLabel()} — {s.Stable.Code}"),
            new("Tipo de conexión", ctx.LinkTypeLabel),
            new("Adaptador", ctx.AdapterName ?? "–"),
            new("IP del equipo", ctx.LocalAddress?.ToString() ?? "–"),
            new("Router (puerta de enlace)", ctx.Gateway is null ? "–" : $"{ctx.Gateway}{(ctx.GatewayMac is null ? "" : $"  ({ctx.GatewayMac})")}"),
            new("Primer salto del proveedor", s.ProviderHop is null ? "–" : $"{s.ProviderHop.Address} (salto {s.ProviderHop.Ttl})"),
            new("Red Wi-Fi (SSID / BSSID)", w is null ? "–" : $"{w.Ssid ?? "–"} / {w.Bssid ?? "–"}"),
            new("VPN", ctx.VpnActive ? ctx.VpnName ?? "Sí" : "No"),
            new("Ping bloqueado hacia internet", s.Stable.IcmpBlocked ? "Sí (se usan conexiones TCP)" : "No"),
            new("Portal cautivo", s.CaptivePortal switch { true => "Sí", false => "No", _ => "Sin verificar" }),
            new("Calidad estimada (MOS)", s.Stable.Mos is double mm ? mm.ToString("0.00", Es) : "–"),
            new("Modo de medición", ModeText),
        };
        if (Details.Count != details.Count) { Details.Clear(); foreach (var i in details) Details.Add(i); }
        else for (int i = 0; i < details.Count; i++) if (Details[i] != details[i]) Details[i] = details[i];
    }

    private static void Fill(MetricCardViewModel card, LinkStats st, Health health, double?[] values, string tertiary)
    {
        card.Primary = st.MeanMs is double m ? $"{m:0} ms" : st.AllLost ? "Sin respuesta" : "–";
        card.Secondary = st.HasData
            ? $"Variación {(st.JitterMs ?? 0):0} ms · pérdida {st.LossPct.ToString("0.#", Es)} %"
            : "Midiendo…";
        card.Tertiary = tertiary;
        card.Health = health;
        card.Values = values;
    }

    private static string DefaultLabel(Segment s) => s switch
    {
        Segment.Device => "Tu equipo",
        Segment.Link => "Conexión",
        Segment.Router => "Router",
        Segment.Provider => "Proveedor",
        _ => "Internet",
    };

    private static Geometry? IconFor(Segment s, LinkType link)
    {
        var key = s switch
        {
            Segment.Device => "Icon.Device",
            Segment.Link => link == LinkType.Ethernet ? "Icon.Cable" : "Icon.Wifi",
            Segment.Router => "Icon.Router",
            Segment.Provider => "Icon.Provider",
            _ => "Icon.Globe",
        };
        return Application.Current?.TryFindResource(key) as Geometry;
    }
}
