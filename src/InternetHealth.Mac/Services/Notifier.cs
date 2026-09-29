using System.Diagnostics;
using System.Runtime.InteropServices;
using InternetHealth.Mac.Interop;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Avisos del sistema. Dentro del paquete .app usa el Centro de notificaciones de macOS
/// (UserNotifications): el aviso sale con el nombre y el ícono de la app y al hacer clic abre el panel.
/// Sin paquete (desarrollo con dotnet run) usa AppleScript como respaldo.
/// </summary>
internal sealed class Notifier
{
    private static Notifier? s_current;
    private readonly IntPtr _center;
    private volatile bool _useCenter;

    public Notifier()
    {
        s_current = this;
        if (ObjC.MainBundleIdentifier() is null || !ObjC.LoadFramework("UserNotifications")) return;
        try
        {
            _center = ObjC.WithPool(() =>
            {
                var center = ObjC.Send(ObjC.objc_getClass("UNUserNotificationCenter"), "currentNotificationCenter");
                if (center == IntPtr.Zero) return IntPtr.Zero;
                ObjC.Send(center, "retain");
                ObjC.SendVoid(center, "setDelegate:", CreateDelegate());
                // alerta (4) + sonido (2)
                ObjC.SendVoid(center, "requestAuthorizationWithOptions:completionHandler:", (nuint)6, AuthorizationBlock);
                return center;
            });
            _useCenter = _center != IntPtr.Zero;
        }
        catch (Exception ex)
        {
            _useCenter = false;
            StartupError = ex.Message;
        }
    }

    public string? StartupError { get; }

    /// <summary>Para el registro técnico: cómo se muestran los avisos y si macOS los permitió.</summary>
    public event Action<string>? Status;

    public string Mode => _useCenter ? "Centro de notificaciones de macOS" : "AppleScript (respaldo)";

    /// <summary>El usuario hizo clic en un aviso. Se dispara fuera del hilo de la interfaz.</summary>
    public event Action? Clicked;

    public void Show(string title, string message)
    {
        if (_useCenter)
        {
            try
            {
                ObjC.WithPool(() =>
                {
                    var content = ObjC.Send(ObjC.Send(ObjC.objc_getClass("UNMutableNotificationContent"), "alloc"), "init");
                    ObjC.SendVoid(content, "setTitle:", ObjC.NSString(title));
                    ObjC.SendVoid(content, "setBody:", ObjC.NSString(message));
                    ObjC.SendVoid(content, "setSound:", ObjC.Send(ObjC.objc_getClass("UNNotificationSound"), "defaultSound"));
                    var request = ObjC.Send(ObjC.objc_getClass("UNNotificationRequest"), "requestWithIdentifier:content:trigger:",
                        ObjC.NSString(Guid.NewGuid().ToString()), content, IntPtr.Zero);
                    ObjC.SendVoid(_center, "addNotificationRequest:withCompletionHandler:", request, IntPtr.Zero);
                    ObjC.Send(content, "release");
                    return 0;
                });
                return;
            }
            catch { /* respaldo */ }
        }
        ShowWithAppleScript(title, message);
    }

    private static void ShowWithAppleScript(string title, string message)
    {
        static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/osascript") { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add($"display notification {Quote(message)} with title {Quote(title)}");
            using var _ = Process.Start(psi);
        }
        catch { /* ignorar */ }
    }

    // ---------- Delegado de Objective-C creado en tiempo de ejecución ----------

    private static unsafe IntPtr CreateDelegate()
    {
        var cls = ObjC.objc_getClass("IHMNotificationDelegate");
        if (cls == IntPtr.Zero)
        {
            cls = ObjC.objc_allocateClassPair(ObjC.objc_getClass("NSObject"), "IHMNotificationDelegate", 0);
            ObjC.class_addMethod(cls, ObjC.Sel("userNotificationCenter:willPresentNotification:withCompletionHandler:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&WillPresent, "v@:@@@?");
            ObjC.class_addMethod(cls, ObjC.Sel("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidReceive, "v@:@@@?");
            var protocol = ObjC.objc_getProtocol("UNUserNotificationCenterDelegate");
            if (protocol != IntPtr.Zero) ObjC.class_addProtocol(cls, protocol);
            ObjC.objc_registerClassPair(cls);
        }
        return ObjC.Send(ObjC.Send(cls, "alloc"), "init"); // se conserva toda la vida del proceso
    }

    private static readonly unsafe IntPtr AuthorizationBlock =
        ObjC.GlobalBlock((IntPtr)(delegate* unmanaged<IntPtr, byte, IntPtr, void>)&AuthorizationDone);

    [UnmanagedCallersOnly]
    private static void AuthorizationDone(IntPtr block, byte granted, IntPtr error)
    {
        // Si macOS no permite avisos a esta app (p. ej. por la firma), se usa AppleScript.
        // Si la persona los rechazó, se respeta: no se muestran avisos.
        if (s_current is not { } n) return;
        if (error != IntPtr.Zero)
        {
            n._useCenter = false;
            var text = ObjC.ToManagedString(ObjC.Send(error, "localizedDescription"));
            n.Status?.Invoke($"macOS no permitió los avisos de la app ({text}); se usará AppleScript.");
        }
        else n.Status?.Invoke(granted != 0 ? "Avisos permitidos por macOS." : "La persona no permitió los avisos en macOS.");
    }

    [UnmanagedCallersOnly]
    private static void WillPresent(IntPtr self, IntPtr cmd, IntPtr center, IntPtr notification, IntPtr handler)
    {
        // Mostrar el aviso aunque la app esté activa: lista (8) + banner (16) + sonido (2).
        ObjC.InvokeBlock(handler, 8 | 16 | 2);
    }

    [UnmanagedCallersOnly]
    private static void DidReceive(IntPtr self, IntPtr cmd, IntPtr center, IntPtr response, IntPtr handler)
    {
        try { s_current?.Clicked?.Invoke(); } catch { /* ignorar */ }
        ObjC.InvokeBlock(handler);
    }
}
