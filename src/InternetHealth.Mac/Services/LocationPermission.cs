using System.Diagnostics;
using InternetHealth.Mac.Interop;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Permiso de ubicación: macOS lo exige para que las apps vean el nombre de la red Wi-Fi (SSID).
/// La app no usa la ubicación para nada más. La primera vez muestra la pregunta del sistema; si ya
/// se respondió, abre Ajustes del Sistema en la sección correspondiente.
/// </summary>
internal static class LocationPermission
{
    private const string SettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_LocationServices";
    private static IntPtr s_manager;

    /// <summary>Debe llamarse en el hilo de la interfaz. Devuelve una línea para el registro técnico.</summary>
    public static string Request()
    {
        try
        {
            if (ObjC.MainBundleIdentifier() is null) { Shell.Open(SettingsUrl); return "Ubicación: la app no corre como .app; se abrieron los Ajustes."; }
            if (!ObjC.LoadFramework("CoreLocation")) { Shell.Open(SettingsUrl); return "Ubicación: CoreLocation no disponible."; }
            if (s_manager == IntPtr.Zero)
                s_manager = ObjC.Send(ObjC.Send(ObjC.objc_getClass("CLLocationManager"), "alloc"), "init");
            long status = ObjC.SendLong(s_manager, "authorizationStatus");
            if (status == 0) // aún no se ha preguntado: macOS muestra su pregunta
            {
                ObjC.Send(s_manager, "requestWhenInUseAuthorization");
                ObjC.Send(s_manager, "startUpdatingLocation"); // en Mac es lo que dispara la pregunta de forma confiable
                _ = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => ObjC.Send(s_manager, "stopUpdatingLocation")));
                return "Ubicación: se pidió el permiso a macOS.";
            }
            Shell.Open(SettingsUrl);
            return $"Ubicación: estado {status} (1 restringido, 2 negado, 3-4 permitido); se abrieron los Ajustes.";
        }
        catch (Exception ex)
        {
            Shell.Open(SettingsUrl);
            return "Ubicación: " + ex.Message;
        }
    }
}

/// <summary>Abrir archivos, carpetas y direcciones con la app predeterminada del Mac.</summary>
internal static class Shell
{
    public static void Open(string target, bool reveal = false)
    {
        var psi = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false, CreateNoWindow = true };
        if (reveal) psi.ArgumentList.Add("-R");
        psi.ArgumentList.Add(target);
        using var _ = Process.Start(psi);
    }

    /// <summary>Copia texto al portapapeles (funciona aunque no haya ventanas abiertas).</summary>
    public static bool CopyToClipboard(string text)
    {
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/pbcopy") { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
            psi.Environment["LC_ALL"] = "en_US.UTF-8"; // tildes y ñ
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.StandardInput.Write(text);
            p.StandardInput.Close();
            return p.WaitForExit(3000) && p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
