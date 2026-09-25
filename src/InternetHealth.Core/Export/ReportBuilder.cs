using System.Globalization;
using System.Net;
using System.Text;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Export;

/// <summary>Reporte HTML autocontenido (sin scripts ni recursos externos) de las últimas 24 horas.</summary>
public static class ReportBuilder
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Build(
        MonitorSnapshot? s,
        IReadOnlyList<MinuteRow> rows,
        IReadOnlyList<(DateTimeOffset Time, Health Severity, DiagnosisCode Code, string Title)> events,
        string appVersion,
        DateTimeOffset now)
    {
        var sb = new StringBuilder(64 * 1024);
        sb.Append("""
            <!DOCTYPE html>
            <html lang="es"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Diagnóstico de conexión</title>
            <style>
            :root{color-scheme:light;--page:#f9f9f7;--surface:#fcfcfb;--ink:#0b0b0b;--ink2:#52514e;--muted:#898781;
              --grid:#e1e0d9;--axis:#c3c2b7;--ring:rgba(11,11,11,.10);--series:#2a78d6;
              --good:#0ca30c;--fair:#fab219;--poor:#ec835a;--down:#d03b3b;--unknown:#898781}
            @media (prefers-color-scheme:dark){:root{color-scheme:dark;--page:#0d0d0d;--surface:#1a1a19;--ink:#fff;--ink2:#c3c2b7;
              --grid:#2c2c2a;--axis:#383835;--ring:rgba(255,255,255,.10);--series:#3987e5}}
            *{box-sizing:border-box}
            body{margin:0;background:var(--page);color:var(--ink);font:15px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif}
            main{max-width:1000px;margin:0 auto;padding:24px 16px 48px}
            h1{font-size:24px;margin:0 0 4px}h2{font-size:17px;margin:32px 0 12px}
            .meta{color:var(--ink2);font-size:13px}
            .card{background:var(--surface);border:1px solid var(--ring);border-radius:12px;padding:16px 20px;margin-top:16px}
            .pill{display:inline-flex;align-items:center;gap:6px;font-weight:600;font-size:13px;padding:2px 10px;border-radius:99px;border:1px solid var(--ring)}
            .dot{width:10px;height:10px;border-radius:50%;display:inline-block;flex:none}
            .title{font-size:20px;font-weight:600;margin:8px 0 4px}
            ol{padding-left:20px;margin:8px 0 0}li{margin:4px 0}
            .chain{display:grid;grid-template-columns:repeat(5,1fr);gap:8px}
            .node{background:var(--surface);border:1px solid var(--ring);border-radius:10px;padding:10px}
            .node b{display:block;font-size:14px}.node span{color:var(--ink2);font-size:12px}
            .node.culprit{outline:2px solid var(--ink);outline-offset:1px}
            .tiles{display:grid;grid-template-columns:repeat(4,1fr);gap:8px}
            .tile{background:var(--surface);border:1px solid var(--ring);border-radius:10px;padding:12px}
            .tile .v{font-size:22px;font-weight:600}.tile .l{color:var(--ink2);font-size:12px}
            svg{display:block;width:100%;height:auto}
            svg text{fill:var(--muted);font:11px system-ui,-apple-system,"Segoe UI",sans-serif}
            .legend{display:flex;gap:14px;flex-wrap:wrap;font-size:12px;color:var(--ink2);margin-top:6px}
            .legend span{display:inline-flex;align-items:center;gap:6px}
            table{width:100%;border-collapse:collapse;font-size:13px;font-variant-numeric:tabular-nums}
            th,td{text-align:left;padding:6px 8px;border-bottom:1px solid var(--grid)}th{color:var(--ink2);font-weight:600}
            td.n,th.n{text-align:right}
            details summary{cursor:pointer;color:var(--ink2);margin-top:8px}
            footer{margin-top:40px;color:var(--muted);font-size:12px}
            @media (max-width:640px){.chain,.tiles{grid-template-columns:repeat(2,1fr)}}
            </style></head><body><main>
            """);

        sb.Append("<h1>Diagnóstico de conexión</h1>");
        sb.Append($"<div class=\"meta\">Equipo <b>{H(Environment.MachineName)}</b> · Usuario {H(Environment.UserName)} · Generado el {H(now.ToString("dd 'de' MMMM 'de' yyyy, HH:mm", Es))} · Versión {H(appVersion)}</div>");

        if (s is not null) AppendCurrent(sb, s);
        AppendLast24h(sb, rows, now);
        AppendEvents(sb, events);
        if (s is not null) AppendNetwork(sb, s);

        sb.Append("<footer>Este reporte solo contiene datos de calidad de conexión (latencia, pérdida, señal). No incluye archivos, sitios visitados ni contenido de ningún tipo.</footer>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void AppendCurrent(StringBuilder sb, MonitorSnapshot s)
    {
        var d = s.Stable;
        sb.Append("<div class=\"card\">");
        sb.Append($"<span class=\"pill\"><span class=\"dot\" style=\"background:{Var(d.Severity)}\"></span>{Icon(d.Severity)} {H(d.Severity.ToLabel())}</span>");
        sb.Append($"<div class=\"title\">{H(d.Text.Title)}</div><div>{H(d.Text.Summary)}</div>");
        if (d.Mos is double mos)
            sb.Append($"<div class=\"meta\" style=\"margin-top:6px\">Calidad estimada para videollamadas: <b>{H(CallQuality.Label(mos))}</b> ({mos.ToString("0.0", Es)} de 5)</div>");
        if (d.Text.Steps.Count > 0)
        {
            sb.Append("<ol>");
            foreach (var step in d.Text.Steps) sb.Append($"<li>{H(step)}</li>");
            sb.Append("</ol>");
        }
        sb.Append("</div>");

        if (d.Chain.Count > 0)
        {
            sb.Append("<h2>Cadena de conexión</h2><div class=\"chain\">");
            foreach (var node in d.Chain)
            {
                bool culprit = d.Culprit == node.Segment && d.Severity > Health.Good;
                var h = node.Measured || node.Health != Health.Unknown ? node.Health : Health.Unknown;
                sb.Append($"<div class=\"node{(culprit ? " culprit" : "")}\"><span class=\"pill\"><span class=\"dot\" style=\"background:{Var(h)}\"></span>{Icon(h)} {H(h == Health.Unknown ? "Sin datos" : h.ToLabel())}</span>");
                sb.Append($"<b style=\"margin-top:6px\">{H(node.Label)}</b><span>{H(node.Detail)}</span>");
                if (culprit) sb.Append("<span style=\"display:block;font-weight:600;color:var(--ink)\">Aquí falla</span>");
                sb.Append("</div>");
            }
            sb.Append("</div>");
        }
    }

    private static void AppendLast24h(StringBuilder sb, IReadOnlyList<MinuteRow> rows, DateTimeOffset now)
    {
        sb.Append("<h2>Últimas 24 horas</h2>");
        if (rows.Count == 0)
        {
            sb.Append("<div class=\"card\">Todavía no hay historial guardado.</div>");
            return;
        }

        int good = rows.Sum(r => r.SecondsGood), fair = rows.Sum(r => r.SecondsFair);
        int poor = rows.Sum(r => r.SecondsPoor), down = rows.Sum(r => r.SecondsDown);
        int measured = Math.Max(1, good + fair + poor + down);
        var avgs = rows.Where(r => r.Internet.AvgMs is not null).Select(r => r.Internet.AvgMs!.Value).ToList();
        int sent = rows.Sum(r => r.Internet.Sent), lost = rows.Sum(r => r.Internet.Lost);
        var worst = rows.Where(r => r.MosMin is not null).OrderBy(r => r.MosMin).FirstOrDefault();

        sb.Append("<div class=\"tiles\">");
        Tile(sb, FormatDuration(poor + down), "con problemas o sin conexión");
        Tile(sb, FormatDuration(fair), "inestable");
        Tile(sb, avgs.Count > 0 ? $"{avgs.Average():0} ms" : "–", "latencia promedio a internet");
        Tile(sb, sent > 0 ? $"{(100.0 * lost / sent).ToString("0.#", Es)} %" : "–", "pérdida promedio");
        sb.Append("</div>");
        sb.Append($"<div class=\"meta\" style=\"margin-top:6px\">Tiempo medido: {FormatDuration(measured)}.");
        if (worst is not null && worst.MosMin < 3.5)
            sb.Append($" Peor momento: {worst.Minute.ToString("HH:mm", Es)} (calidad {CallQuality.Label(worst.MosMin)}).");
        sb.Append("</div>");

        // Gráfica 1: latencia promedio por minuto.
        const double W = 960, Hh = 170, L = 44, R = 12, T = 10, B = 24;
        var start = now.AddHours(-24);
        double X(DateTimeOffset t) => L + (t - start).TotalMinutes / (24 * 60) * (W - L - R);
        double yMax = NiceMax(avgs.Count > 0 ? Percentile(avgs, 0.98) * 1.15 : 50);
        double Y(double v) => T + (1 - Math.Min(v, yMax) / yMax) * (Hh - T - B);

        sb.Append("<div class=\"card\"><b>Latencia hacia internet</b><div class=\"meta\">Promedio por minuto, en milisegundos. Menos es mejor.</div>");
        sb.Append($"<svg viewBox=\"0 0 {W} {Hh}\" role=\"img\" aria-label=\"Latencia hacia internet en las últimas 24 horas\">");
        for (int i = 0; i <= 4; i++)
        {
            double v = yMax * i / 4, y = Y(v);
            sb.Append($"<line x1=\"{L}\" x2=\"{W - R}\" y1=\"{F(y)}\" y2=\"{F(y)}\" stroke=\"var(--{(i == 0 ? "axis" : "grid")})\" stroke-width=\"1\"/>");
            sb.Append($"<text x=\"{L - 6}\" y=\"{F(y + 4)}\" text-anchor=\"end\">{v:0}</text>");
        }
        AppendTimeAxis(sb, start, now, X, Hh - 6);

        var path = new StringBuilder();
        DateTimeOffset? prev = null;
        foreach (var r in rows.OrderBy(r => r.Minute))
        {
            if (r.Internet.AvgMs is not double v) { prev = null; continue; }
            bool gap = prev is null || (r.Minute - prev.Value).TotalMinutes > 3;
            path.Append(gap ? 'M' : 'L').Append(F(X(r.Minute))).Append(' ').Append(F(Y(v))).Append(' ');
            prev = r.Minute;
        }
        sb.Append($"<path d=\"{path}\" fill=\"none\" stroke=\"var(--series)\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");

        // Capa de ayuda al pasar el mouse: bloques de 15 minutos con su resumen.
        foreach (var g in rows.GroupBy(r => new DateTimeOffset(r.Minute.Year, r.Minute.Month, r.Minute.Day, r.Minute.Hour, r.Minute.Minute / 15 * 15, 0, r.Minute.Offset)))
        {
            var gAvg = g.Where(r => r.Internet.AvgMs is not null).Select(r => r.Internet.AvgMs!.Value).DefaultIfEmpty().Average();
            int gs = g.Sum(r => r.Internet.Sent), gl = g.Sum(r => r.Internet.Lost);
            double x0 = X(g.Key), x1 = X(g.Key.AddMinutes(15));
            var tip = $"{g.Key:HH:mm}–{g.Key.AddMinutes(15):HH:mm}: {gAvg:0} ms, pérdida {(gs > 0 ? 100.0 * gl / gs : 0).ToString("0.#", Es)} %";
            sb.Append($"<rect x=\"{F(x0)}\" y=\"{T}\" width=\"{F(Math.Max(1, x1 - x0))}\" height=\"{Hh - T - B}\" fill=\"transparent\"><title>{H(tip)}</title></rect>");
        }
        sb.Append("</svg></div>");

        // Gráfica 2: estado por minuto (franja de color con icono en la leyenda).
        const double SH = 58;
        sb.Append("<div class=\"card\"><b>Estado de la conexión</b><div class=\"meta\">El peor estado registrado en cada minuto.</div>");
        sb.Append($"<svg viewBox=\"0 0 {W} {SH}\" role=\"img\" aria-label=\"Estado de la conexión por minuto\">");
        foreach (var r in rows)
        {
            if (r.WorstSeverity == Health.Unknown) continue;
            double x0 = X(r.Minute), x1 = X(r.Minute.AddMinutes(1));
            sb.Append($"<rect x=\"{F(x0)}\" y=\"6\" width=\"{F(Math.Max(1, x1 - x0))}\" height=\"24\" fill=\"var(--{Key(r.WorstSeverity)})\"><title>{r.Minute:HH:mm} — {H(r.WorstSeverity.ToLabel())}</title></rect>");
        }
        AppendTimeAxis(sb, start, now, X, SH - 6);
        sb.Append("</svg><div class=\"legend\">");
        foreach (var h in new[] { Health.Good, Health.Fair, Health.Poor, Health.Down })
            sb.Append($"<span><span class=\"dot\" style=\"background:{Var(h)}\"></span>{Icon(h)} {H(h.ToLabel())}</span>");
        sb.Append("</div></div>");

        // Vista de tabla por hora.
        sb.Append("<details><summary>Ver tabla por hora</summary><table><thead><tr><th>Hora</th><th class=\"n\">Latencia prom.</th><th class=\"n\">Variación</th><th class=\"n\">Pérdida</th><th class=\"n\">Min. con problemas</th><th>Conexión</th></tr></thead><tbody>");
        foreach (var g in rows.GroupBy(r => new DateTimeOffset(r.Minute.Year, r.Minute.Month, r.Minute.Day, r.Minute.Hour, 0, 0, r.Minute.Offset)).OrderByDescending(g => g.Key))
        {
            var la = g.Where(r => r.Internet.AvgMs is not null).Select(r => r.Internet.AvgMs!.Value).ToList();
            var ja = g.Where(r => r.Internet.JitterMs is not null).Select(r => r.Internet.JitterMs!.Value).ToList();
            int gs = g.Sum(r => r.Internet.Sent), gl = g.Sum(r => r.Internet.Lost);
            int bad = g.Count(r => r.WorstSeverity >= Health.Poor);
            var conn = g.Select(r => string.IsNullOrEmpty(r.Ssid) ? r.ConnectionType : $"{r.ConnectionType} «{r.Ssid}»").Distinct();
            sb.Append($"<tr><td>{g.Key:dd/MM HH}:00</td><td class=\"n\">{(la.Count > 0 ? $"{la.Average():0} ms" : "–")}</td><td class=\"n\">{(ja.Count > 0 ? $"{ja.Average():0} ms" : "–")}</td><td class=\"n\">{(gs > 0 ? (100.0 * gl / gs).ToString("0.#", Es) + " %" : "–")}</td><td class=\"n\">{bad}</td><td>{H(string.Join(", ", conn))}</td></tr>");
        }
        sb.Append("</tbody></table></details>");
    }

    private static void AppendEvents(StringBuilder sb, IReadOnlyList<(DateTimeOffset Time, Health Severity, DiagnosisCode Code, string Title)> events)
    {
        sb.Append("<h2>Cambios de estado</h2>");
        if (events.Count == 0) { sb.Append("<div class=\"card\">Sin cambios de estado registrados en las últimas 24 horas.</div>"); return; }
        sb.Append("<div class=\"card\"><table><thead><tr><th>Hora</th><th>Estado</th><th>Diagnóstico</th></tr></thead><tbody>");
        foreach (var e in events.OrderByDescending(e => e.Time).Take(200))
            sb.Append($"<tr><td>{e.Time:dd/MM HH:mm:ss}</td><td><span class=\"dot\" style=\"background:{Var(e.Severity)}\"></span> {Icon(e.Severity)} {H(e.Severity.ToLabel())}</td><td>{H(e.Title)}</td></tr>");
        sb.Append("</tbody></table></div>");
    }

    private static void AppendNetwork(StringBuilder sb, MonitorSnapshot s)
    {
        var c = s.Context;
        var rows = new List<(string, string?)>
        {
            ("Tipo de conexión", c.LinkTypeLabel),
            ("Adaptador", c.AdapterName),
            ("Red Wi-Fi", c.Wifi?.Ssid),
            ("Señal Wi-Fi", c.Wifi?.SignalQuality is int q ? $"{q} %" + (c.Wifi.RssiDbm is int rssi ? $" ({rssi} dBm)" : "") : null),
            ("Banda / canal", c.Wifi?.Band is null ? null : c.Wifi.Band + (c.Wifi.Channel is int ch ? $" · canal {ch}" : "")),
            ("Estándar Wi-Fi", c.Wifi?.PhyType),
            ("Velocidad del enlace", c.EffectiveLinkRateMbps is double r ? $"{r:0} Mbps" : null),
            ("IP del equipo", c.LocalAddress?.ToString()),
            ("Router", c.Gateway is null ? null : c.Gateway + (c.GatewayMac is null ? "" : $" ({c.GatewayMac})")),
            ("Primer salto del proveedor", s.ProviderHop?.Address.ToString()),
            ("VPN", c.VpnActive ? $"Activa ({c.VpnName})" : "No"),
            ("Microsoft 365 (conexión)", s.CloudLastMs is double cm ? $"{cm:0} ms" : null),
        };
        sb.Append("<h2>Datos de la red</h2><div class=\"card\"><table><tbody>");
        foreach (var (k, v) in rows.Where(x => !string.IsNullOrEmpty(x.Item2)))
            sb.Append($"<tr><th>{H(k)}</th><td>{H(v)}</td></tr>");
        sb.Append("</tbody></table></div>");
    }

    private static void AppendTimeAxis(StringBuilder sb, DateTimeOffset start, DateTimeOffset end, Func<DateTimeOffset, double> x, double y)
    {
        var t = new DateTimeOffset(start.Year, start.Month, start.Day, start.Hour, 0, 0, start.Offset).AddHours(1);
        while (t.Hour % 3 != 0) t = t.AddHours(1);
        for (; t < end; t = t.AddHours(3))
            sb.Append($"<text x=\"{F(x(t))}\" y=\"{F(y)}\" text-anchor=\"middle\">{t:HH}:00</text>");
    }

    private static void Tile(StringBuilder sb, string value, string label) =>
        sb.Append($"<div class=\"tile\"><div class=\"v\">{H(value)}</div><div class=\"l\">{H(label)}</div></div>");

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60) return $"{seconds} s";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours} h {ts.Minutes} min" : $"{ts.Minutes} min";
    }

    private static double NiceMax(double v)
    {
        double[] steps = [20, 40, 60, 80, 100, 150, 200, 300, 400, 600, 800, 1000, 1500, 2000];
        foreach (var s in steps) if (v <= s) return s;
        return Math.Ceiling(v / 1000) * 1000;
    }

    private static double Percentile(List<double> values, double p)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[(int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
    }

    private static string Key(Health h) => h switch
    {
        Health.Good => "good", Health.Fair => "fair", Health.Poor => "poor", Health.Down => "down", _ => "unknown",
    };

    private static string Var(Health h) => $"var(--{Key(h)})";

    private static string Icon(Health h) => h switch
    {
        Health.Good => "✓", Health.Fair => "!", Health.Poor => "!!", Health.Down => "✕", _ => "?",
    };

    private static string F(double v) => v.ToString("0.#", Inv);
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
}
