using System.IO;
using System.Security;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Inicio automático al iniciar sesión, con un LaunchAgent del usuario (~/Library/LaunchAgents).
/// No requiere permisos de administrador. macOS muestra una vez el aviso "Se agregó un ítem de inicio".
/// </summary>
internal static class LaunchAgent
{
    public const string Label = "com.jcardila.internethealthmonitor";

    private static string PlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", Label + ".plist");

    public static void Set(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                if (File.Exists(PlistPath)) File.Delete(PlistPath);
                return;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            var plist = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                  <key>Label</key>
                  <string>{Label}</string>
                  <key>ProgramArguments</key>
                  <array>
                    <string>{SecurityElement.Escape(exe)}</string>
                    <string>--background</string>
                  </array>
                  <key>RunAtLoad</key>
                  <true/>
                  <key>ProcessType</key>
                  <string>Interactive</string>
                </dict>
                </plist>
                """;
            if (File.Exists(PlistPath) && File.ReadAllText(PlistPath) == plist) return;
            Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
            File.WriteAllText(PlistPath, plist);
        }
        catch
        {
            // No crítico: la app funciona igual, solo no arrancará sola.
        }
    }
}
