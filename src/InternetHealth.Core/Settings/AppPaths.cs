namespace InternetHealth.Core.Settings;

/// <summary>Rutas de datos locales. Todo queda en el perfil del usuario; nada sale del equipo sin que él lo comparta.</summary>
public sealed class AppPaths
{
    public const string AppFolderName = "InternetHealthMonitor";

    public AppPaths(string? dataRoot = null)
    {
        DataRoot = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName + "Data");
    }

    public string DataRoot { get; }
    public string HistoryDir => Path.Combine(DataRoot, "historial");
    public string EventsFile => Path.Combine(DataRoot, "eventos.csv");
    public string PreferencesFile => Path.Combine(DataRoot, "preferencias.json");
    public string ErrorLogFile => Path.Combine(DataRoot, "errores.log");

    /// <summary>Capas de configuración de la organización, de menor a mayor prioridad.</summary>
    public static IEnumerable<string> SettingsLayers()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "defaults.json");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderName, "defaults.json");
    }

    public void AppendError(string message)
    {
        try
        {
            Directory.CreateDirectory(DataRoot);
            var fi = new FileInfo(ErrorLogFile);
            if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
            File.AppendAllText(ErrorLogFile, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch { /* último recurso */ }
    }
}
