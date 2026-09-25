using Velopack;

namespace InternetHealth.App.Services;

/// <summary>
/// Actualizaciones automáticas y silenciosas (Velopack). Se descargan en segundo plano y se
/// aplican solo cuando el usuario no está en una llamada.
/// </summary>
/// <remarks>
/// El feed es una URL de archivos estáticos (SimpleWebSource). Para GitHub Releases se usa
/// https://github.com/{dueño}/{repo}/releases/latest/download: son descargas directas, sin el
/// límite de 60 consultas por hora por IP de la API de GitHub (que sí usaría GithubSource).
/// </remarks>
internal sealed class UpdateService
{
    private readonly UpdateManager? _manager;
    private UpdateInfo? _pending;

    public UpdateService(string? feedUrl)
    {
        if (string.IsNullOrWhiteSpace(feedUrl)) return;
        try
        {
            _manager = new UpdateManager(feedUrl.TrimEnd('/'));
        }
        catch
        {
            _manager = null;
        }
    }

    public bool IsConfigured => _manager is not null;
    public bool IsInstalled => _manager?.IsInstalled == true;
    public bool HasPendingUpdate => _pending is not null;
    public string? PendingVersion => _pending?.TargetFullRelease.Version.ToString();

    /// <returns>Si la consulta respondió (haya o no versión nueva) y un mensaje para el usuario.</returns>
    public async Task<(bool Ok, string Message)> CheckAndDownloadAsync()
    {
        if (_manager is null) return (false, "Las actualizaciones automáticas no están configuradas.");
        if (!_manager.IsInstalled) return (false, "La app no está instalada (modo portátil o desarrollo): no se buscan actualizaciones.");
        try
        {
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null) return (true, "Tienes la versión más reciente.");
            await _manager.DownloadUpdatesAsync(info).ConfigureAwait(false);
            _pending = info;
            return (true, $"Versión {info.TargetFullRelease.Version} descargada. Se instalará automáticamente.");
        }
        catch (Exception ex)
        {
            return (false, "No se pudo buscar actualizaciones: " + ex.Message);
        }
    }

    /// <summary>Aplica la actualización descargada y reinicia la app en segundo plano.</summary>
    public void ApplyAndRestart()
    {
        if (_manager is null || _pending is null) return;
        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease, ["--background"]);
    }
}
