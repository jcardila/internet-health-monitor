using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace InternetHealth.Mac.Views;

/// <summary>Panel rápido que se abre desde la barra de menús, pegado a la esquina superior derecha.</summary>
public partial class PanelWindow : Window
{
    public PanelWindow()
    {
        InitializeComponent();
        // Como un menú: se cierra al hacer clic en otra parte.
        Deactivated += (_, _) => Hide();
    }

    public event Action<int>? DetailsRequested;
    public event Action? ShareRequested;
    public event Action? CaptivePortalRequested;
    public event Action? LocationRequested;

    public void Toggle()
    {
        if (IsVisible) Hide();
        else ShowNearMenuBar();
    }

    public void ShowNearMenuBar()
    {
        Show();
        // La barra de menús está arriba; el panel queda a la derecha, bajo ella (donde están los íconos).
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            var area = screen.WorkingArea;
            double scale = screen.Scaling;
            int width = (int)Math.Ceiling(Bounds.Width * scale);
            Position = new PixelPoint(area.Right - width - (int)(12 * scale), area.Y + (int)(6 * scale));
        }
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Hide();
    private void Settings_Click(object? sender, RoutedEventArgs e) { Hide(); DetailsRequested?.Invoke(4); }
    private void Details_Click(object? sender, RoutedEventArgs e) { Hide(); DetailsRequested?.Invoke(0); }
    private void Steps_Click(object? sender, RoutedEventArgs e) { Hide(); DetailsRequested?.Invoke(2); }
    private void Share_Click(object? sender, RoutedEventArgs e) { Hide(); ShareRequested?.Invoke(); }
    private void Captive_Click(object? sender, RoutedEventArgs e) => CaptivePortalRequested?.Invoke();
    private void Location_Click(object? sender, RoutedEventArgs e) => LocationRequested?.Invoke();
}
