using System.Runtime.InteropServices;

namespace InternetHealth.Mac.Interop;

/// <summary>
/// Acceso mínimo al runtime de Objective-C (objc_msgSend) para usar frameworks de macOS
/// (CoreWLAN, UserNotifications, CoreLocation) sin dependencias externas.
/// </summary>
internal static unsafe class ObjC
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";
    private static readonly IntPtr MsgSend = NativeLibrary.GetExport(NativeLibrary.Load(Lib), "objc_msgSend");

    [DllImport(Lib)] public static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Lib)] public static extern IntPtr objc_getProtocol([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Lib)] public static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Lib)] public static extern IntPtr objc_allocateClassPair(IntPtr superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nint extraBytes);
    [DllImport(Lib)] public static extern void objc_registerClassPair(IntPtr cls);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool class_addMethod(IntPtr cls, IntPtr sel, IntPtr imp, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool class_addProtocol(IntPtr cls, IntPtr protocol);
    [DllImport(Lib)] public static extern IntPtr objc_autoreleasePoolPush();
    [DllImport(Lib)] public static extern void objc_autoreleasePoolPop(IntPtr pool);

    /// <summary>Carga un framework del sistema (necesario para que sus clases existan en el runtime).</summary>
    public static bool LoadFramework(string name) =>
        NativeLibrary.TryLoad($"/System/Library/Frameworks/{name}.framework/{name}", out _);

    public static IntPtr Sel(string name) => sel_registerName(name);

    public static IntPtr Send(IntPtr target, string selector) =>
        target == IntPtr.Zero ? IntPtr.Zero : ((delegate* unmanaged<IntPtr, IntPtr, IntPtr>)MsgSend)(target, Sel(selector));

    public static IntPtr Send(IntPtr target, string selector, IntPtr a) =>
        target == IntPtr.Zero ? IntPtr.Zero : ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr>)MsgSend)(target, Sel(selector), a);

    public static IntPtr Send(IntPtr target, string selector, IntPtr a, IntPtr b, IntPtr c) =>
        target == IntPtr.Zero ? IntPtr.Zero : ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)MsgSend)(target, Sel(selector), a, b, c);

    public static void SendVoid(IntPtr target, string selector, IntPtr a)
    {
        if (target != IntPtr.Zero) ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)MsgSend)(target, Sel(selector), a);
    }

    public static void SendVoid(IntPtr target, string selector, IntPtr a, IntPtr b)
    {
        if (target != IntPtr.Zero) ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)MsgSend)(target, Sel(selector), a, b);
    }

    public static void SendVoid(IntPtr target, string selector, nuint a, IntPtr b)
    {
        if (target != IntPtr.Zero) ((delegate* unmanaged<IntPtr, IntPtr, nuint, IntPtr, void>)MsgSend)(target, Sel(selector), a, b);
    }

    public static long SendLong(IntPtr target, string selector) =>
        target == IntPtr.Zero ? 0 : ((delegate* unmanaged<IntPtr, IntPtr, long>)MsgSend)(target, Sel(selector));

    public static double SendDouble(IntPtr target, string selector) =>
        target == IntPtr.Zero ? 0 : ((delegate* unmanaged<IntPtr, IntPtr, double>)MsgSend)(target, Sel(selector));

    public static bool SendBool(IntPtr target, string selector) =>
        target != IntPtr.Zero && ((delegate* unmanaged<IntPtr, IntPtr, byte>)MsgSend)(target, Sel(selector)) != 0;

    public static bool RespondsTo(IntPtr target, string selector) =>
        target != IntPtr.Zero && ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>)MsgSend)(target, Sel("respondsToSelector:"), Sel(selector)) != 0;

    /// <summary>NSString (autoliberado) a partir de un texto .NET.</summary>
    public static IntPtr NSString(string value)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try { return Send(objc_getClass("NSString"), "stringWithUTF8String:", utf8); }
        finally { Marshal.FreeCoTaskMem(utf8); }
    }

    public static string? ToManagedString(IntPtr nsString) =>
        nsString == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(nsString, "UTF8String"));

    /// <summary>Identificador del paquete .app (null si la app no corre dentro de un paquete, p. ej. con dotnet run).</summary>
    public static string? MainBundleIdentifier()
    {
        var pool = objc_autoreleasePoolPush();
        try { return ToManagedString(Send(Send(objc_getClass("NSBundle"), "mainBundle"), "bundleIdentifier")); }
        finally { objc_autoreleasePoolPop(pool); }
    }

    /// <summary>Ejecuta código que usa objetos autoliberados fuera del hilo principal sin fugas de memoria.</summary>
    public static T WithPool<T>(Func<T> action)
    {
        var pool = objc_autoreleasePoolPush();
        try { return action(); }
        finally { objc_autoreleasePoolPop(pool); }
    }

    // ---------- Bloques (closures de Objective-C) ----------

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public nuint Reserved;
        public nuint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public BlockDescriptor* Descriptor;
    }

    private static readonly IntPtr GlobalBlockIsa =
        NativeLibrary.GetExport(NativeLibrary.Load("/usr/lib/libSystem.dylib"), "_NSConcreteGlobalBlock");

    /// <summary>
    /// Crea un bloque global (sin capturas) que llama a <paramref name="invoke"/>. Vive toda la vida
    /// del proceso: se usa para un puñado de callbacks fijos.
    /// </summary>
    public static IntPtr GlobalBlock(IntPtr invoke)
    {
        var descriptor = (BlockDescriptor*)NativeMemory.AllocZeroed((nuint)sizeof(BlockDescriptor));
        descriptor->Size = (nuint)sizeof(BlockLiteral);
        var block = (BlockLiteral*)NativeMemory.AllocZeroed((nuint)sizeof(BlockLiteral));
        block->Isa = GlobalBlockIsa;
        block->Flags = 1 << 28; // BLOCK_IS_GLOBAL
        block->Invoke = invoke;
        block->Descriptor = descriptor;
        return (IntPtr)block;
    }

    /// <summary>Llama a un bloque recibido de macOS que no recibe argumentos: void (^)(void).</summary>
    public static void InvokeBlock(IntPtr block)
    {
        if (block == IntPtr.Zero) return;
        var invoke = ((BlockLiteral*)block)->Invoke;
        ((delegate* unmanaged<IntPtr, void>)invoke)(block);
    }

    /// <summary>Llama a un bloque recibido de macOS con un argumento entero: void (^)(NSUInteger).</summary>
    public static void InvokeBlock(IntPtr block, nuint arg)
    {
        if (block == IntPtr.Zero) return;
        var invoke = ((BlockLiteral*)block)->Invoke;
        ((delegate* unmanaged<IntPtr, nuint, void>)invoke)(block, arg);
    }
}

/// <summary>
/// Apariencia real de la barra de menús. En macOS 26 la barra es transparente y su texto es claro u
/// oscuro según el fondo de pantalla, no según el tema claro/oscuro de las apps.
/// </summary>
internal static class MenuBarAppearance
{
    /// <returns>true si los íconos de la barra se ven claros (barra oscura); null si no se pudo saber.</returns>
    public static bool? IsDark() => ObjC.WithPool<bool?>(() =>
    {
        var app = ObjC.Send(ObjC.objc_getClass("NSApplication"), "sharedApplication");
        var windows = ObjC.Send(app, "windows");
        long count = ObjC.SendLong(windows, "count");
        for (long i = 0; i < count; i++)
        {
            var w = ObjC.Send(windows, "objectAtIndex:", (IntPtr)i);
            var cls = ObjC.ToManagedString(ObjC.Send(ObjC.Send(w, "className"), "description"));
            if (cls is null || !cls.Contains("StatusBar", StringComparison.Ordinal)) continue;
            var name = ObjC.ToManagedString(ObjC.Send(ObjC.Send(w, "effectiveAppearance"), "name"));
            if (name is not null) return name.Contains("Dark", StringComparison.Ordinal);
        }
        return null;
    });
}
