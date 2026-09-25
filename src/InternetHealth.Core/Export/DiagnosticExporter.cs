using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using InternetHealth.Core.History;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Settings;

namespace InternetHealth.Core.Export;

/// <summary>
/// Crea el paquete "Compartir diagnóstico": un .zip con un reporte HTML legible y los datos
/// en CSV (mismo formato para todos los equipos, listo para consolidar en Power BI).
/// </summary>
public static class DiagnosticExporter
{
    public static string DefaultFileName(DateTimeOffset now) =>
        $"Diagnostico-conexion_{Sanitize(Environment.MachineName)}_{now:yyyyMMdd-HHmm}.zip";

    public static string Create(string zipPath, AppPaths paths, MonitorSnapshot? snapshot, LiveLog? log,
        string appVersion, DateTimeOffset now, int days = 7)
    {
        var from = now.AddDays(-days);
        var rows = HistoryStore.Read(paths, now.AddHours(-24), now);
        var events = HistoryStore.ReadEvents(paths, now.AddHours(-24));
        var html = ReportBuilder.Build(snapshot, rows, events, appVersion, now);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddText(zip, "resumen.html", html);
            if (snapshot is not null)
                AddText(zip, "resumen.txt", SummaryText.Build(snapshot, rows.Where(r => r.Minute >= now.AddHours(-1)).ToList()));

            if (Directory.Exists(paths.HistoryDir))
            {
                for (var day = from.Date; day <= now.Date; day = day.AddDays(1))
                {
                    var file = Path.Combine(paths.HistoryDir, day.ToString("yyyy-MM-dd") + ".csv");
                    if (File.Exists(file)) AddFileShared(zip, file, "historial/" + Path.GetFileName(file));
                }
            }
            if (File.Exists(paths.EventsFile)) AddFileShared(zip, paths.EventsFile, "eventos.csv");

            if (log is not null)
                AddText(zip, "registro.txt", string.Join(Environment.NewLine, log.Snapshot().Select(e => e.ToString())));

            AddText(zip, "equipo.json", DeviceInfo(snapshot, appVersion, now));
            AddText(zip, "LEEME.txt", Readme);
        }
        return zipPath;
    }

    private const string Readme =
        """
        Diagnóstico de conexión — contenido del paquete

        resumen.html   Reporte legible (ábrelo con el navegador).
        resumen.txt    Resumen corto del estado en el momento de compartir.
        historial/     Un archivo CSV por día, con un resumen por minuto de todas las mediciones.
        eventos.csv    Cambios de estado (cuándo empezó y terminó cada problema).
        registro.txt   Registro técnico reciente.
        equipo.json    Datos del equipo y de la red al momento de compartir.

        Los CSV tienen el mismo formato en todos los equipos: se pueden combinar en Power BI
        (Obtener datos → Carpeta) para analizar varias sedes o personas a la vez.
        Este paquete solo contiene datos de conexión; no incluye archivos, sitios visitados ni contenido.
        """;

    private static string DeviceInfo(MonitorSnapshot? s, string appVersion, DateTimeOffset now)
    {
        var ctx = s?.Context;
        var info = new Dictionary<string, object?>
        {
            ["generado"] = now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            ["version_app"] = appVersion,
            ["equipo"] = Environment.MachineName,
            ["usuario"] = Environment.UserName,
            ["sistema"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["conexion"] = ctx?.LinkTypeLabel,
            ["adaptador"] = ctx?.AdapterName,
            ["ip_local"] = ctx?.LocalAddress?.ToString(),
            ["router_ip"] = ctx?.Gateway?.ToString(),
            ["router_mac"] = ctx?.GatewayMac,
            ["proveedor_salto"] = s?.ProviderHop?.Address.ToString(),
            ["wifi_ssid"] = ctx?.Wifi?.Ssid,
            ["wifi_bssid"] = ctx?.Wifi?.Bssid,
            ["wifi_senal_pct"] = ctx?.Wifi?.SignalQuality,
            ["wifi_rssi_dbm"] = ctx?.Wifi?.RssiDbm,
            ["wifi_banda"] = ctx?.Wifi?.Band,
            ["wifi_canal"] = ctx?.Wifi?.Channel,
            ["wifi_estandar"] = ctx?.Wifi?.PhyType,
            ["velocidad_enlace_mbps"] = ctx?.EffectiveLinkRateMbps,
            ["vpn"] = ctx?.VpnActive,
            ["vpn_nombre"] = ctx?.VpnName,
            ["estado"] = s?.Stable.Severity.ToString(),
            ["diagnostico"] = s?.Stable.Code.ToString(),
            ["diagnostico_titulo"] = s?.Stable.Text.Title,
            ["mos"] = s?.Stable.Mos,
        };
        return JsonSerializer.Serialize(info, ExportJsonContext.Default.DictionaryStringObject);
    }

    private static void AddText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(true));
        writer.Write(content);
    }

    private static void AddFileShared(ZipArchive zip, string source, string name)
    {
        // El archivo puede estar abierto por el propio monitor: lo leemos en modo compartido.
        using var fs = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var target = entry.Open();
        fs.CopyTo(target);
    }

    private static string Sanitize(string s) =>
        new(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c).ToArray());

    public static string AppVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(DiagnosticExporter).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info)) return asm.GetName().Version?.ToString(3) ?? "0.0.0";
        int plus = info.IndexOf('+');
        return plus > 0 ? info[..plus] : info;
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, object?>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
[System.Text.Json.Serialization.JsonSerializable(typeof(int))]
[System.Text.Json.Serialization.JsonSerializable(typeof(double))]
[System.Text.Json.Serialization.JsonSerializable(typeof(bool))]
internal sealed partial class ExportJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
