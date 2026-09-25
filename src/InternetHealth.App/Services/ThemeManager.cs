using System.Windows;
using System.Windows.Interop;
using InternetHealth.App.Interop;
using Microsoft.Win32;

namespace InternetHealth.App.Services;

/// <summary>Sigue el tema claro/oscuro de Windows y cambia los colores de la app en caliente.</summary>
internal static class ThemeManager
{
    private static ResourceDictionary? _current;

    public static bool IsDark { get; private set; }

    /// <summary>
    /// Tema de la barra de tareas (SystemUsesLightTheme). Es independiente del tema de las apps:
    /// en el modo "personalizado" de Windows la barra puede ser oscura con apps claras.
    /// </summary>
    public static bool TaskbarIsLight { get; private set; }

    public static event Action? ThemeChanged;
    public static event Action? TaskbarThemeChanged;

    public static void Initialize()
    {
        TaskbarIsLight = ReadTaskbarIsLight();
        Apply(ReadSystemIsDark());
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)) return;
            var dark = ReadSystemIsDark();
            var taskbarLight = ReadTaskbarIsLight();
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (dark != IsDark) Apply(dark);
                if (taskbarLight != TaskbarIsLight)
                {
                    TaskbarIsLight = taskbarLight;
                    TaskbarThemeChanged?.Invoke();
                }
            });
        };
    }

    private static void Apply(bool dark)
    {
        IsDark = dark;
        var dict = new ResourceDictionary
        {
            Source = new Uri(dark ? "/InternetHealthMonitor;component/Themes/Dark.xaml" : "/InternetHealthMonitor;component/Themes/Light.xaml", UriKind.Relative),
        };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current is not null) merged.Remove(_current);
        merged.Insert(0, dict);
        _current = dict;

        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
        ThemeChanged?.Invoke();
    }

    /// <summary>Barra de título oscura/clara acorde al tema (Windows 10 20H1+ y Windows 11).</summary>
    public static void ApplyTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int value = IsDark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch { /* versiones antiguas de Windows */ }
    }

    public static void ApplyRoundCorners(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            int pref = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
        catch { /* Windows 10: esquinas rectas */ }
    }

    private static bool ReadSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    private static bool ReadTaskbarIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
        }
        catch { return false; } // por defecto la barra de tareas es oscura
    }
}
