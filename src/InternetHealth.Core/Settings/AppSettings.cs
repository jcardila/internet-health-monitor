using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace InternetHealth.Core.Settings;

public sealed class CloudTarget
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 443;
}

/// <summary>
/// Configuración de la organización. Se toma de los valores por defecto, luego de
/// <c>defaults.json</c> junto al ejecutable y por último de
/// <c>%ProgramData%\InternetHealthMonitor\defaults.json</c> (para configurar un equipo o sede).
/// </summary>
public sealed class AppSettings
{
    public List<string> InternetTargets { get; set; } = ["1.1.1.1", "8.8.8.8", "9.9.9.9"];

    public List<CloudTarget> CloudTargets { get; set; } =
    [
        new() { Name = "Microsoft Teams", Host = "teams.microsoft.com", Port = 443 },
        new() { Name = "Outlook / Microsoft 365", Host = "outlook.office365.com", Port = 443 },
    ];

    /// <summary>Destino usado para descubrir el primer salto del proveedor (traceroute corto).</summary>
    public string HopDiscoveryTarget { get; set; } = "1.1.1.1";

    public int RelaxedIntervalSeconds { get; set; } = 5;
    public int ActiveIntervalSeconds { get; set; } = 1;
    public int RouterTimeoutMs { get; set; } = 1000;
    public int InternetTimeoutMs { get; set; } = 2000;
    public int HistoryRetentionDays { get; set; } = 30;

    public string SupportName { get; set; } = "soporte TI";
    public string? SupportUrl { get; set; }
    public string? SupportEmail { get; set; }

    /// <summary>URL de actualizaciones (Velopack). Vacío = sin actualizaciones automáticas.</summary>
    public string? UpdateFeedUrl { get; set; }

    public static AppSettings Load(IEnumerable<string> layerPaths)
    {
        var merged = JsonSerializer.SerializeToNode(new AppSettings(), SettingsJsonContext.Default.AppSettings)!.AsObject();
        foreach (var path in layerPaths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var node = JsonNode.Parse(File.ReadAllText(path),
                    documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (node is JsonObject obj) Merge(merged, obj);
            }
            catch
            {
                // Un archivo inválido no debe impedir que la app arranque.
            }
        }

        var result = merged.Deserialize(SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        result.Normalize();
        return result;
    }

    private void Normalize()
    {
        InternetTargets = InternetTargets.Where(t => System.Net.IPAddress.TryParse(t, out _)).Distinct().ToList();
        if (InternetTargets.Count == 0) InternetTargets = ["1.1.1.1", "8.8.8.8", "9.9.9.9"];
        CloudTargets = CloudTargets.Where(t => !string.IsNullOrWhiteSpace(t.Host) && t.Port is > 0 and < 65536).ToList();
        RelaxedIntervalSeconds = Math.Clamp(RelaxedIntervalSeconds, 2, 60);
        ActiveIntervalSeconds = Math.Clamp(ActiveIntervalSeconds, 1, 10);
        RouterTimeoutMs = Math.Clamp(RouterTimeoutMs, 200, 5000);
        InternetTimeoutMs = Math.Clamp(InternetTimeoutMs, 300, 5000);
        HistoryRetentionDays = Math.Clamp(HistoryRetentionDays, 1, 365);
        if (string.IsNullOrWhiteSpace(SupportName)) SupportName = "soporte TI";
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            var existingKey = target.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) ?? key;
            if (value is JsonObject so && target[existingKey] is JsonObject to) Merge(to, so);
            else target[existingKey] = value?.DeepClone();
        }
    }
}

/// <summary>Preferencias del usuario (se guardan en su perfil).</summary>
public sealed class UserPreferences
{
    public bool StartWithWindows { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public bool CallDetectionEnabled { get; set; } = true;
    public bool DetailedLog { get; set; }
    public DateTimeOffset? DoNotDisturbUntil { get; set; }
    public bool FirstRunCompleted { get; set; }

    /// <summary>Última búsqueda de actualizaciones que respondió bien (ver <see cref="UpdateSchedule"/>).</summary>
    public DateTimeOffset? LastUpdateCheck { get; set; }

    public static UserPreferences Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJsonContext.Default.UserPreferences) ?? new();
        }
        catch { /* archivo dañado: usar valores por defecto */ }
        return new();
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, SettingsJsonContext.Default.UserPreferences));
            File.Move(tmp, path, overwrite: true);
        }
        catch { /* no crítico */ }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(UserPreferences))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
