using Microsoft.Win32;

namespace InternetHealth.App.Services;

/// <summary>Inicio automático con Windows para el usuario actual (sin permisos de administrador).</summary>
internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "InternetHealthMonitor";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
            {
                var exe = LauncherPath();
                if (exe is not null) key.SetValue(ValueName, $"\"{exe}\" --background");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch { /* políticas de la empresa pueden bloquearlo */ }
    }

    /// <summary>
    /// Instalado con Velopack, el ejecutable vive en ...\current\ (la ruta no cambia entre versiones).
    /// </summary>
    private static string? LauncherPath() => Environment.ProcessPath;
}
