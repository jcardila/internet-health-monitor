using System.Windows;
using InternetHealth.App.Services;

namespace InternetHealth.App.Views;

/// <summary>Ventana de detalle. Se crea al abrirla y se destruye al cerrarla (no consume memoria mientras está cerrada).</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    public event Action? ShareRequested;
    public event Action? CopySummaryRequested;
    public event Action? WifiSettingsRequested;
    public event Action? CaptivePortalRequested;
    public event Action? SupportRequested;
    public event Action? CopyLogRequested;
    public event Action? DataFolderRequested;
    public event Action? LocationSettingsRequested;
    public event Action? CheckUpdatesRequested;

    private void Share_Click(object sender, RoutedEventArgs e) => ShareRequested?.Invoke();
    private void CopySummary_Click(object sender, RoutedEventArgs e) => CopySummaryRequested?.Invoke();
    private void WifiSettings_Click(object sender, RoutedEventArgs e) => WifiSettingsRequested?.Invoke();
    private void Captive_Click(object sender, RoutedEventArgs e) => CaptivePortalRequested?.Invoke();
    private void Support_Click(object sender, RoutedEventArgs e) => SupportRequested?.Invoke();
    private void CopyLog_Click(object sender, RoutedEventArgs e) => CopyLogRequested?.Invoke();
    private void DataFolder_Click(object sender, RoutedEventArgs e) => DataFolderRequested?.Invoke();
    private void Location_Click(object sender, RoutedEventArgs e) => LocationSettingsRequested?.Invoke();
    private void CheckUpdates_Click(object sender, RoutedEventArgs e) => CheckUpdatesRequested?.Invoke();
}
