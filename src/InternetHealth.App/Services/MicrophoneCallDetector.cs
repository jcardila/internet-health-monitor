using System.IO;
using InternetHealth.Core.Network;
using Microsoft.Win32;

namespace InternetHealth.App.Services;

/// <summary>
/// Detecta si alguna aplicación está usando el micrófono en este momento (señal de llamada o
/// reunión), leyendo el registro de privacidad de Windows. No accede al audio ni a ninguna app.
/// </summary>
internal sealed class MicrophoneCallDetector : ICallDetector
{
    private const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
    private static readonly string OwnExe = Path.GetFileName(Environment.ProcessPath ?? "InternetHealthMonitor.exe");

    public CallInfo Detect()
    {
        using var root = Registry.CurrentUser.OpenSubKey(Root);
        if (root is null) return CallInfo.None;

        foreach (var name in root.GetSubKeyNames())
        {
            if (name.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
            {
                using var np = root.OpenSubKey(name);
                if (np is null) continue;
                foreach (var app in np.GetSubKeyNames())
                {
                    if (app.EndsWith(OwnExe, StringComparison.OrdinalIgnoreCase)) continue;
                    using var k = np.OpenSubKey(app);
                    if (IsActive(k)) return new CallInfo(true, FriendlyName(app));
                }
            }
            else
            {
                using var k = root.OpenSubKey(name);
                if (IsActive(k)) return new CallInfo(true, FriendlyName(name));
            }
        }
        return CallInfo.None;
    }

    private static bool IsActive(RegistryKey? k)
    {
        if (k is null) return false;
        var start = k.GetValue("LastUsedTimeStart") is long s ? s : 0;
        var stop = k.GetValue("LastUsedTimeStop") is long e ? e : -1;
        return start > 0 && stop == 0;
    }

    private static string FriendlyName(string key)
    {
        var k = key.ToLowerInvariant();
        if (k.Contains("teams")) return "Teams";
        if (k.Contains("zoom")) return "Zoom";
        if (k.Contains("webex")) return "Webex";
        if (k.Contains("slack")) return "Slack";
        if (k.Contains("whatsapp")) return "WhatsApp";
        if (k.Contains("chrome") || k.Contains("msedge") || k.Contains("firefox") || k.Contains("brave")) return "navegador";
        var file = key.Split('#').LastOrDefault() ?? key;
        return Path.GetFileNameWithoutExtension(file.Split('_').First());
    }
}
