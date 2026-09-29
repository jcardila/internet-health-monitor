using InternetHealth.Core.Network;
using InternetHealth.Mac.Interop;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Detecta si alguna aplicación está usando el micrófono en este momento (señal de llamada o
/// reunión). Lee el mismo estado que macOS usa para el punto naranja de la barra de menús: no accede
/// al audio ni pide permiso de micrófono.
/// </summary>
internal sealed class MicrophoneCallDetector : ICallDetector
{
    private readonly int _ownPid = Environment.ProcessId;

    public CallInfo Detect()
    {
        var processes = MacNative.ProcessesUsingMicrophone();
        if (processes is null) // macOS anterior a 14.2: solo se sabe si hay un micrófono en uso
            return MacNative.AnyInputDeviceRunning() ? new CallInfo(true, null) : CallInfo.None;

        bool unknownApp = false;
        foreach (var (pid, bundleId) in processes)
        {
            if (pid == _ownPid) continue;
            var name = FriendlyName(bundleId);
            if (name is not null) return new CallInfo(true, name);
            if (!IsSystemListener(bundleId)) unknownApp = true;
        }
        return unknownApp ? new CallInfo(true, null) : CallInfo.None;
    }

    /// <summary>Servicios de macOS que escuchan de fondo ("Oye Siri", dictado, reconocimiento de sonidos): no son llamadas.</summary>
    private static bool IsSystemListener(string? bundleId) =>
        bundleId is not null && bundleId.StartsWith("com.apple.", StringComparison.OrdinalIgnoreCase);

    internal static string? FriendlyName(string? bundleId)
    {
        if (string.IsNullOrEmpty(bundleId)) return null;
        var k = bundleId.ToLowerInvariant();
        if (k.Contains("teams")) return "Teams";
        if (k.Contains("zoom")) return "Zoom";
        if (k.Contains("webex") || k.Contains("cisco")) return "Webex";
        if (k.Contains("slack")) return "Slack";
        if (k.Contains("whatsapp")) return "WhatsApp";
        if (k.Contains("facetime")) return "FaceTime";
        if (k.Contains("chrome") || k.Contains("edgemac") || k.Contains("firefox") || k.Contains("brave")
            || k.Contains("safari") || k.Contains("com.apple.webkit")) return "navegador";
        return null;
    }
}
