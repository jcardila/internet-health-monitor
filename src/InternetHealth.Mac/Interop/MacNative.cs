using System.Runtime.InteropServices;

namespace InternetHealth.Mac.Interop;

/// <summary>Funciones C de macOS: CoreFoundation, CoreAudio (micrófono) y CoreGraphics (pantalla bloqueada).</summary>
internal static unsafe class MacNative
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreAudio = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    private const uint Utf8Encoding = 0x08000100;

    [DllImport(CoreFoundation)] public static extern void CFRelease(IntPtr cf);
    [DllImport(CoreFoundation)] private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, uint encoding);
    [DllImport(CoreFoundation)] private static extern IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);
    [DllImport(CoreFoundation)] private static extern byte CFBooleanGetValue(IntPtr boolean);
    [DllImport(CoreFoundation)] private static extern IntPtr CFGetTypeID(IntPtr cf);
    [DllImport(CoreFoundation)] private static extern IntPtr CFBooleanGetTypeID();
    [DllImport(CoreFoundation)] private static extern IntPtr CFStringGetTypeID();
    [DllImport(CoreFoundation)] private static extern nint CFStringGetLength(IntPtr str);
    [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(IntPtr str, byte* buffer, nint size, uint encoding);

    public static string? CFStringToManaged(IntPtr str)
    {
        if (str == IntPtr.Zero || CFGetTypeID(str) != CFStringGetTypeID()) return null;
        nint size = CFStringGetLength(str) * 4 + 1;
        var buffer = new byte[size];
        fixed (byte* p = buffer)
        {
            if (!CFStringGetCString(str, p, size, Utf8Encoding)) return null;
            return Marshal.PtrToStringUTF8((IntPtr)p);
        }
    }

    // ---------- Pantalla bloqueada ----------

    [DllImport(CoreGraphics)] private static extern IntPtr CGSessionCopyCurrentDictionary();

    private static readonly IntPtr ScreenLockedKey =
        CFStringCreateWithCString(IntPtr.Zero, "CGSSessionScreenIsLocked", Utf8Encoding);

    /// <summary>true si la pantalla está bloqueada; null si no se pudo saber.</summary>
    public static bool? IsScreenLocked()
    {
        var dict = CGSessionCopyCurrentDictionary();
        if (dict == IntPtr.Zero) return null;
        try
        {
            var value = CFDictionaryGetValue(dict, ScreenLockedKey);
            return value != IntPtr.Zero && CFGetTypeID(value) == CFBooleanGetTypeID() && CFBooleanGetValue(value) != 0;
        }
        finally
        {
            CFRelease(dict);
        }
    }

    // ---------- CoreAudio ----------

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyAddress
    {
        public uint Selector, Scope, Element;
        public PropertyAddress(string selector, string scope) { Selector = Code(selector); Scope = Code(scope); Element = 0; }
    }

    private static uint Code(string s) => (uint)(s[0] << 24 | s[1] << 16 | s[2] << 8 | s[3]);

    [DllImport(CoreAudio)]
    private static extern int AudioObjectGetPropertyDataSize(uint objectId, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, out uint size);

    [DllImport(CoreAudio)]
    private static extern int AudioObjectGetPropertyData(uint objectId, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, ref uint size, void* data);

    private const uint SystemObject = 1;

    private static uint[]? GetIds(uint objectId, string selector, string scope = "glob")
    {
        var addr = new PropertyAddress(selector, scope);
        if (AudioObjectGetPropertyDataSize(objectId, ref addr, 0, IntPtr.Zero, out uint size) != 0) return null;
        var ids = new uint[size / 4];
        if (ids.Length == 0) return ids;
        fixed (uint* p = ids)
        {
            if (AudioObjectGetPropertyData(objectId, ref addr, 0, IntPtr.Zero, ref size, p) != 0) return null;
        }
        return ids;
    }

    private static uint? GetUInt(uint objectId, string selector)
    {
        var addr = new PropertyAddress(selector, "glob");
        uint value = 0, size = 4;
        return AudioObjectGetPropertyData(objectId, ref addr, 0, IntPtr.Zero, ref size, &value) == 0 ? value : null;
    }

    private static string? GetCFString(uint objectId, string selector)
    {
        var addr = new PropertyAddress(selector, "glob");
        IntPtr value = IntPtr.Zero;
        uint size = (uint)IntPtr.Size;
        if (AudioObjectGetPropertyData(objectId, ref addr, 0, IntPtr.Zero, ref size, &value) != 0 || value == IntPtr.Zero) return null;
        try { return CFStringToManaged(value); }
        finally { CFRelease(value); }
    }

    /// <summary>
    /// Procesos que están capturando audio del micrófono ahora mismo (macOS 14.2 o superior), con su
    /// identificador de app. Solo lee el estado que macOS también usa para el punto naranja: no accede al audio.
    /// </summary>
    /// <returns>null si esta versión de macOS no ofrece la información por proceso.</returns>
    public static List<(int Pid, string? BundleId)>? ProcessesUsingMicrophone()
    {
        var processes = GetIds(SystemObject, "prs#");
        if (processes is null) return null;
        var result = new List<(int, string?)>();
        foreach (var p in processes)
        {
            if (GetUInt(p, "piri") is not > 0) continue; // kAudioProcessPropertyIsRunningInput
            int pid = (int)(GetUInt(p, "ppid") ?? 0);
            result.Add((pid, GetCFString(p, "pbid")));
        }
        return result;
    }

    /// <summary>Alternativa para macOS antiguos: ¿algún dispositivo con entrada de audio está en uso?</summary>
    public static bool AnyInputDeviceRunning()
    {
        var devices = GetIds(SystemObject, "dev#");
        if (devices is null) return false;
        foreach (var d in devices)
        {
            var streams = GetIds(d, "stm#", "inpt");
            if (streams is null || streams.Length == 0) continue; // sin entrada (solo parlantes)
            if (GetUInt(d, "gone") is > 0) return true; // kAudioDevicePropertyDeviceIsRunningSomewhere
        }
        return false;
    }
}
