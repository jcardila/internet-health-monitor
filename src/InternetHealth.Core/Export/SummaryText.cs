using System.Globalization;
using System.Text;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Export;

/// <summary>Resumen corto en texto plano para pegar en un chat de Teams o en un correo.</summary>
public static class SummaryText
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    public static string Build(MonitorSnapshot s, IReadOnlyList<MinuteRow>? lastHour = null)
    {
        var sb = new StringBuilder();
        var d = s.Stable;
        var ctx = s.Context;
        sb.AppendLine($"Diagnóstico de conexión — {Environment.MachineName} — {s.Time.ToString("dd/MM/yyyy HH:mm", Es)}");
        sb.AppendLine($"Estado: {d.Severity.ToLabel()} — {d.Text.Title}");
        if (d.Mos is double mos)
            sb.AppendLine($"Calidad para videollamadas: {CallQuality.Label(mos)} ({mos.ToString("0.0", Es)}/5)");
        sb.AppendLine("Internet: " + Describe(s.Internet.AllLost && s.Cloud.HasData ? s.Cloud : s.Internet));
        if (ctx.Gateway is not null)
            sb.AppendLine($"Router ({ctx.Gateway}): " + (s.RouterMeasurable ? Describe(s.Router) : "no responde a ping"));
        if (s.ProviderHop is not null)
            sb.AppendLine($"Proveedor ({s.ProviderHop.Address}): " + Describe(s.Provider));
        if (s.CloudLastMs is double c)
            sb.AppendLine($"Microsoft 365: conexión en {c.ToString("0", Es)} ms");

        var link = new List<string> { ctx.LinkTypeLabel };
        if (ctx.Wifi is { } w)
        {
            if (w.Ssid is not null) link.Add($"red «{w.Ssid}»");
            if (w.SignalQuality is int q) link.Add($"señal {q} %");
            if (w.Band is not null) link.Add(w.Band);
        }
        if (ctx.EffectiveLinkRateMbps is double r) link.Add($"{r.ToString("0", Es)} Mbps");
        if (ctx.VpnActive) link.Add("VPN activa");
        sb.AppendLine("Conexión: " + string.Join(", ", link));

        if (lastHour is { Count: > 0 })
        {
            int badMinutes = lastHour.Count(m => m.WorstSeverity >= Health.Poor);
            int fairMinutes = lastHour.Count(m => m.WorstSeverity == Health.Fair);
            sb.AppendLine($"Última hora: {badMinutes} min con problemas, {fairMinutes} min inestable.");
        }
        return sb.ToString().TrimEnd();
    }

    private static string Describe(LinkStats st)
    {
        if (!st.HasData) return "sin datos";
        if (st.AllLost) return "sin respuesta";
        return $"{(st.MeanMs ?? 0).ToString("0", Es)} ms, variación {(st.JitterMs ?? 0).ToString("0", Es)} ms, pérdida {st.LossPct.ToString("0", Es)} %";
    }
}
