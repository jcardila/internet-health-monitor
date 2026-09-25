using Velopack;
using Velopack.Sources;

namespace InternetHealth.App.Services;

/// <summary>
/// Actualizaciones automáticas y silenciosas (Velopack). Se descargan en segundo plano y se
/// aplican solo cuando el usuario no está en una llamada.
/// </summary>
internal sealed class UpdateService
{
    private readonly UpdateManager? _manager;
    private UpdateInfo? _pending;

    public UpdateService(string? feedUrl)
    {
        if (string.IsNullOrWhiteSpace(feedUrl)) return;
        try
        {
            _manager = feedUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
                ? new UpdateManager(new GithubSource(feedUrl, null, false))
                : new UpdateManager(feedUrl);
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

    /// <returns>Mensaje para el usuario.</returns>
    public async Task<string> CheckAndDownloadAsync(CancellationToken ct = default)
    {
        if (_manager is null) return "Las actualizaciones automáticas no están configuradas.";
        if (!_manager.IsInstalled) return "La app no está instalada (modo portátil o desarrollo): no se buscan actualizaciones.";
        try
        {
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null) return "Tienes la versión más reciente.";
            await _manager.DownloadUpdatesAsync(info).ConfigureAwait(false);
            _pending = info;
            return $"Versión {info.TargetFullRelease.Version} descargada. Se instalará automáticamente.";
        }
        catch (Exception ex)
        {
            return "No se pudo buscar actualizaciones: " + ex.Message;
        }
    }

    /// <summary>Aplica la actualización descargada y reinicia la app en segundo plano.</summary>
    public void ApplyAndRestart()
    {
        if (_manager is null || _pending is null) return;
        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease, ["--background"]);
    }
}
