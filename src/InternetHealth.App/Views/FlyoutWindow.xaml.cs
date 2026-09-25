using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using InternetHealth.App.Services;

namespace InternetHealth.App.Views;

/// <summary>Panel compacto que aparece junto a la bandeja del sistema.</summary>
public partial class FlyoutWindow : Window
{
    private DateTime _hiddenAt;

    public FlyoutWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            ThemeManager.ApplyTitleBar(this);
            ThemeManager.ApplyRoundCorners(this);
        };
        Deactivated += (_, _) => HideFlyout();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) HideFlyout();
        };
    }

    public event Action<int>? DetailsRequested;
    public event Action? ShareRequested;
    public event Action? CaptivePortalRequested;
    public event Action? LocationSettingsRequested;

    /// <summary>
    /// Alterna la visibilidad. Si se acaba de ocultar por perder el foco (el propio clic en el
    /// ícono de la bandeja lo provoca), no lo volvemos a abrir.
    /// </summary>
    public void Toggle()
    {
        if (IsVisible) { HideFlyout(); return; }
        if ((DateTime.UtcNow - _hiddenAt).TotalMilliseconds < 300) return;
        ShowNearTray();
    }

    public void ShowNearTray()
    {
        // Se crea el identificador de ventana y se ubica ANTES de mostrarla: sin parpadeos.
        new WindowInteropHelper(this).EnsureHandle();
        if (!_measured)
        {
            Measure(new Size(Width, double.PositiveInfinity));
            _measured = true;
        }
        Show();
        UpdateLayout();
        PositionNearTray();
        Activate();
        Focus();
    }

    private bool _measured;

    public void HideFlyout()
    {
        if (!IsVisible) return;
        _hiddenAt = DateTime.UtcNow;
        Hide();
    }

    private void PositionNearTray()
    {
        // Todo en píxeles físicos: funciona en configuraciones con varios monitores y distinta escala.
        var hwnd = new WindowInteropHelper(this).Handle;
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursor);
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;

        // 1) Llevar la ventana al monitor de la bandeja (Windows ajusta la escala si cambia el DPI).
        SetWindowPos(hwnd, IntPtr.Zero, work.Left, work.Top, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        UpdateLayout();

        // 2) Ubicarla en la esquina junto a la barra de tareas.
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        int h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        int m = (int)(12 * dpi.DpiScaleX);
        int x, y;
        if (work.Top > bounds.Top) { x = work.Right - w - m; y = work.Top + m; }          // barra arriba
        else if (work.Left > bounds.Left) { x = work.Left + m; y = work.Bottom - h - m; } // barra a la izquierda
        else { x = work.Right - w - m; y = work.Bottom - h - m; }                         // abajo o a la derecha
        x = Math.Max(work.Left + m, x);
        y = Math.Max(work.Top + m, y);
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private void Close_Click(object sender, RoutedEventArgs e) => HideFlyout();

    private void Settings_Click(object sender, RoutedEventArgs e) { HideFlyout(); DetailsRequested?.Invoke(4); }

    private void Details_Click(object sender, RoutedEventArgs e) { HideFlyout(); DetailsRequested?.Invoke(0); }

    private void Steps_Click(object sender, RoutedEventArgs e) { HideFlyout(); DetailsRequested?.Invoke(2); }

    private void Share_Click(object sender, RoutedEventArgs e) { HideFlyout(); ShareRequested?.Invoke(); }

    private void Captive_Click(object sender, RoutedEventArgs e) { HideFlyout(); CaptivePortalRequested?.Invoke(); }

    private void Location_Click(object sender, RoutedEventArgs e) { HideFlyout(); LocationSettingsRequested?.Invoke(); }
}
