using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using InternetHealth.App.ViewModels;
using InternetHealth.App.Views;
using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.Export;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Network;
using InternetHealth.Core.Settings;
using Microsoft.Win32;

namespace InternetHealth.App.Services;

/// <summary>Arma y coordina la aplicación: motor, bandeja, ventanas, preferencias y actualizaciones.</summary>
internal sealed class AppHost
{
    private readonly App _app;
    private readonly SingleInstance _instance;
    private readonly bool _background;
    private readonly bool _demo;
    private readonly bool _firstRun;
    private readonly string _version = DiagnosticExporter.AppVersion();

    private AppPaths _paths = null!;
    private AppSettings _settings = null!;
    private UserPreferences _prefs = null!;
    private HistoryStore _history = null!;
    private MonitorEngine _engine = null!;
    private TrayIcon _tray = null!;
    private UpdateService _updates = null!;
    private DashboardViewModel _dashboard = null!;
    private HistoryViewModel _historyVm = null!;
    private SettingsViewModel _settingsVm = null!;
    private MainWindowViewModel _mainVm = null!;
    private FlyoutWindow? _flyout;
    private MainWindow? _main;
    private DispatcherTimer? _updateTimer;
    private readonly List<IDisposable> _disposables = [];
    private Health _lastTrayHealth = (Health)(-1);
    private string _lastTrayText = "";
    private bool _uiRefreshQueued;
    private int _updateFailures;

    public AppHost(App app, SingleInstance instance, string[] args, bool firstRun)
    {
        _app = app;
        _instance = instance;
        _background = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        _demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        _firstRun = firstRun;
    }

    public void Start()
    {
        _paths = new AppPaths(_demo ? Path.Combine(Path.GetTempPath(), "InternetHealthMonitorDemo") : null);
        HookGlobalErrors();
        ThemeManager.Initialize();

        _settings = AppSettings.Load(AppPaths.SettingsLayers());
        _prefs = UserPreferences.Load(_paths.PreferencesFile);
        Messages.SupportName = _settings.SupportName;
        HistoryStore.ApplyRetention(_paths, _settings.HistoryRetentionDays, DateTimeOffset.Now);
        _history = new HistoryStore(_paths, _version);

        _engine = new MonitorEngine(_settings, CreateDependencies(), _history);
        _engine.CallDetectionEnabled = _prefs.CallDetectionEnabled;
        _engine.DetailedLog = _prefs.DetailedLog;
        _engine.Notifications.Enabled = _prefs.NotificationsEnabled;
        _engine.Notifications.DoNotDisturbUntil = _prefs.DoNotDisturbUntil;
        _engine.Log.Add(DateTimeOffset.Now, $"Monitor de Conexión {_version} iniciado{(_demo ? " (modo demostración)" : "")}.");

        _dashboard = new DashboardViewModel { IsDemo = _demo };
        _historyVm = new HistoryViewModel(_paths);
        _settingsVm = new SettingsViewModel(_prefs, _settings, _version, OnPreferencesChanged);
        _mainVm = new MainWindowViewModel(_dashboard, _historyVm, _settingsVm);

        _tray = new TrayIcon(_demo);
        _tray.LeftClick += ToggleFlyout;
        _tray.OpenDetails += () => ShowMain(0);
        _tray.Share += ShareDiagnostic;
        _tray.ToggleDoNotDisturb += ToggleDnd;
        _tray.ToggleStartup += () => { _settingsVm.StartWithWindows = !_settingsVm.StartWithWindows; };
        _tray.Exit += () => _app.Shutdown();
        _tray.NotificationClicked += () => ShowFlyout();
        ThemeManager.ThemeChanged += () =>
        {
            _tray.ApplyTheme(ThemeManager.IsDark);
            _dashboard.RefreshThemeBindings();
        };
        ThemeManager.TaskbarThemeChanged += () => _tray.RefreshIcons();

        _engine.SnapshotUpdated += OnSnapshot;
        _engine.NotificationRequested += n => _app.Dispatcher.BeginInvoke(() => OnNotification(n));
        _engine.Log.Added += e =>
        {
            if (_main is not null) _app.Dispatcher.BeginInvoke(() => _mainVm.AddLog(e));
        };
        _history.MinuteWritten += _row =>
        {
            if (_main is not null && _mainVm.SelectedTab == 1)
                _app.Dispatcher.BeginInvoke(() => { _ = _historyVm.LoadAsync(); });
        };
        _mainVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedTab) && _mainVm.SelectedTab == 1) _ = _historyVm.LoadAsync();
        };

        _instance.ListenForActivation(() => _app.Dispatcher.BeginInvoke(() => ShowMain(0)));
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        if (!_demo) StartupManager.Set(_prefs.StartWithWindows);
        SyncTrayMenu();
        _engine.Start();

        SetupUpdates();

        if (!_prefs.FirstRunCompleted || _firstRun)
        {
            _prefs.FirstRunCompleted = true;
            _prefs.Save(_paths.PreferencesFile);
            _tray.ShowNotification("Monitor de Conexión está activo",
                "Lo encontrarás junto al reloj. Te avisaremos si algo en tu conexión puede afectar tus reuniones.", true);
            _app.Dispatcher.BeginInvoke(() => ShowFlyout(), DispatcherPriority.ApplicationIdle);
        }
        else if (!_background)
        {
            _app.Dispatcher.BeginInvoke(() => ShowMain(0), DispatcherPriority.ApplicationIdle);
        }
    }

    private MonitorDependencies CreateDependencies()
    {
        if (_demo)
        {
            var demo = new DemoNetwork();
            return new MonitorDependencies
            {
                Pinger = demo, TcpProber = demo, Network = demo, CaptivePortal = demo, Throughput = demo, CallDetector = demo,
            };
        }

        var pinger = new SystemPinger();
        var network = new WindowsNetworkContextProvider();
        var captive = new HttpCaptivePortalChecker();
        _disposables.Add(pinger);
        _disposables.Add(network);
        _disposables.Add(captive);
        return new MonitorDependencies
        {
            Pinger = pinger,
            TcpProber = new SystemTcpProber(),
            Network = network,
            CaptivePortal = captive,
            Throughput = new SystemThroughputMeter(),
            CallDetector = new MicrophoneCallDetector(),
        };
    }

    // ---------- Actualización de la interfaz ----------

    private void OnSnapshot(MonitorSnapshot s)
    {
        // Se llama en el hilo del motor. Evitamos encolar trabajo si la UI aún no procesó el anterior.
        if (_uiRefreshQueued) return;
        _uiRefreshQueued = true;
        _app.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _uiRefreshQueued = false;
            var latest = _engine.Latest ?? s;
            UpdateTray(latest);
            bool flyoutVisible = _flyout?.IsVisible == true;
            bool mainVisible = _main is not null && _main.IsVisible && _main.WindowState != WindowState.Minimized;
            if (flyoutVisible || mainVisible)
                _dashboard.Apply(latest, _engine, includeDetails: mainVisible);
        });
    }

    private void UpdateTray(MonitorSnapshot s)
    {
        var d = s.Stable;
        string text = d.Code == DiagnosisCode.Checking ? d.Text.TrayText
            : s.Call.InCall ? $"{d.Text.TrayText} · en llamada" : d.Text.TrayText;
        if (d.Severity == _lastTrayHealth && text == _lastTrayText) return;
        _lastTrayHealth = d.Severity;
        _lastTrayText = text;
        _tray.Update(d.Severity, text);
    }

    private void OnNotification(NotificationRequest n)
    {
        if (_main?.IsActive == true && _main.WindowState != WindowState.Minimized) return; // ya lo está viendo
        _tray.ShowNotification(n.Title, n.Message, n.IsRecovery);
    }

    // ---------- Ventanas ----------

    private FlyoutWindow EnsureFlyout()
    {
        if (_flyout is not null) return _flyout;
        _flyout = new FlyoutWindow { DataContext = _dashboard };
        _flyout.DetailsRequested += ShowMain;
        _flyout.ShareRequested += ShareDiagnostic;
        _flyout.CaptivePortalRequested += OpenCaptivePortal;
        _flyout.LocationSettingsRequested += () => OpenUri("ms-settings:privacy-location");
        _flyout.IsVisibleChanged += (_, _) => UpdateUiVisibility();
        return _flyout;
    }

    private void ToggleFlyout()
    {
        var f = EnsureFlyout();
        if (!f.IsVisible) RefreshNow(includeDetails: false);
        f.Toggle();
    }

    private void ShowFlyout()
    {
        var f = EnsureFlyout();
        RefreshNow(includeDetails: false);
        if (!f.IsVisible) f.ShowNearTray();
    }

    private void ShowMain(int tab)
    {
        if (_main is null)
        {
            _main = new MainWindow { DataContext = _mainVm };
            _main.ShareRequested += ShareDiagnostic;
            _main.CopySummaryRequested += CopySummary;
            _main.WifiSettingsRequested += () => OpenUri("ms-settings:network-wifi");
            _main.CaptivePortalRequested += OpenCaptivePortal;
            _main.SupportRequested += OpenSupport;
            _main.CopyLogRequested += CopyLog;
            _main.DataFolderRequested += () => OpenUri(_paths.DataRoot, ensureDirectory: true);
            _main.LocationSettingsRequested += () => OpenUri("ms-settings:privacy-location");
            _main.CheckUpdatesRequested += () => _ = CheckUpdatesAsync(userInitiated: true);
            _main.IsVisibleChanged += (_, _) => UpdateUiVisibility();
            _main.StateChanged += (_, _) => UpdateUiVisibility();
            _main.Closed += (_, _) =>
            {
                _main = null;
                _mainVm.Log.Clear();
                UpdateUiVisibility();
                // Liberar la memoria de la ventana: la app vuelve a su mínimo consumo.
                _app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                {
                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                });
            };
            _mainVm.ReloadLog(_engine.Log.Snapshot());
            _settingsVm.Refresh();
        }

        _mainVm.SelectedTab = tab;
        if (tab == 1) _ = _historyVm.LoadAsync();
        RefreshNow(includeDetails: true);
        if (!_main.IsVisible) _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void RefreshNow(bool includeDetails)
    {
        if (_engine.Latest is { } s) _dashboard.Apply(s, _engine, includeDetails);
        _dashboard.DndActive = _prefs.DoNotDisturbUntil is { } dnd && dnd > DateTimeOffset.Now;
    }

    private void UpdateUiVisibility()
    {
        bool mainVisible = _main is not null && _main.IsVisible && _main.WindowState != WindowState.Minimized;
        _engine.UiVisible = mainVisible || _flyout?.IsVisible == true;
    }

    // ---------- Acciones ----------

    private void ShareDiagnostic()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(folder)) folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var now = DateTimeOffset.Now;
            _history.Flush();
            var zip = DiagnosticExporter.Create(Path.Combine(folder, DiagnosticExporter.DefaultFileName(now)),
                _paths, _engine.Latest, _engine.Log, _version, now);

            if (_engine.Latest is { } s)
                TrySetClipboard(SummaryText.Build(s, HistoryStore.Read(_paths, now.AddHours(-1), now)));

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zip}\"") { UseShellExecute = true });
            _tray.ShowNotification("Diagnóstico listo",
                "Se guardó en Descargas y el resumen quedó copiado: pégalo en Teams o en un correo y adjunta el archivo.", true);
            _engine.Log.Add(now, "Diagnóstico exportado: " + zip);
        }
        catch (Exception ex)
        {
            _paths.AppendError("Exportar: " + ex);
            MessageBox.Show("No se pudo crear el diagnóstico:\n" + ex.Message, "Monitor de Conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopySummary()
    {
        if (_engine.Latest is not { } s) return;
        var now = DateTimeOffset.Now;
        if (TrySetClipboard(SummaryText.Build(s, HistoryStore.Read(_paths, now.AddHours(-1), now))))
            _tray.ShowNotification("Resumen copiado", "Pégalo en un chat de Teams o en un correo.", true);
    }

    private void CopyLog() =>
        TrySetClipboard(string.Join(Environment.NewLine, _engine.Log.Snapshot().Select(e => e.ToString())));

    private static bool TrySetClipboard(string text)
    {
        for (int i = 0; i < 3; i++)
        {
            try { Clipboard.SetText(text); return true; }
            catch { Thread.Sleep(50); } // el portapapeles puede estar ocupado por otra app
        }
        return false;
    }

    private void OpenCaptivePortal() => OpenUri("http://www.msftconnecttest.com/redirect");

    private void OpenSupport()
    {
        if (!string.IsNullOrWhiteSpace(_settings.SupportUrl)) OpenUri(_settings.SupportUrl!);
        else if (!string.IsNullOrWhiteSpace(_settings.SupportEmail))
            OpenUri($"mailto:{_settings.SupportEmail}?subject={Uri.EscapeDataString("Problema de conexión - " + Environment.MachineName)}");
    }

    private void OpenUri(string target, bool ensureDirectory = false)
    {
        try
        {
            if (ensureDirectory) Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _paths.AppendError($"Abrir {target}: {ex.Message}");
        }
    }

    private void ToggleDnd()
    {
        bool active = _prefs.DoNotDisturbUntil is { } dnd && dnd > DateTimeOffset.Now;
        _prefs.DoNotDisturbUntil = active ? null : DateTimeOffset.Now.AddHours(1);
        _engine.Notifications.DoNotDisturbUntil = _prefs.DoNotDisturbUntil;
        _prefs.Save(_paths.PreferencesFile);
        _dashboard.DndActive = !active;
        SyncTrayMenu();
    }

    private void OnPreferencesChanged()
    {
        _engine.CallDetectionEnabled = _prefs.CallDetectionEnabled;
        _engine.DetailedLog = _prefs.DetailedLog;
        _engine.Notifications.Enabled = _prefs.NotificationsEnabled;
        if (!_demo) StartupManager.Set(_prefs.StartWithWindows);
        _prefs.Save(_paths.PreferencesFile);
        SyncTrayMenu();
    }

    private void SyncTrayMenu()
    {
        bool dnd = _prefs.DoNotDisturbUntil is { } until && until > DateTimeOffset.Now;
        _tray.SetMenuState(dnd, _prefs.StartWithWindows);
    }

    // ---------- Energía y sesión ----------

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock && _engine.Latest?.Call.InCall != true) _engine.Pause();
        else if (e.Reason == SessionSwitchReason.SessionUnlock) _engine.Resume();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) { _history.Flush(); _engine.Pause(); }
        else if (e.Mode == PowerModes.Resume) _engine.Resume();
    }

    // ---------- Actualizaciones ----------

    private void SetupUpdates()
    {
        _updates = new UpdateService(_settings.UpdateFeedUrl);
        if (!_updates.IsConfigured || !_updates.IsInstalled) return;
        _updateTimer = new DispatcherTimer(DispatcherPriority.Background);
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Stop();
            if (_updates.HasPendingUpdate) TryApplyPendingUpdate(); // ya descargada: solo falta instalarla
            else await CheckUpdatesAsync(userInitiated: false);
            ScheduleNextUpdateCheck(atStartup: false);
        };
        ScheduleNextUpdateCheck(atStartup: true);
    }

    /// <summary>Programa la próxima consulta: al azar al iniciar, cada ~24 h si responde, con espera creciente si falla.</summary>
    private void ScheduleNextUpdateCheck(bool atStartup)
    {
        if (_updateTimer is null) return;
        _updateTimer.Interval = _updates.HasPendingUpdate
            ? TimeSpan.FromMinutes(30) // descargada pero sin instalar (en llamada o con ventanas abiertas)
            : UpdateSchedule.NextDelay(DateTimeOffset.Now, _prefs.LastUpdateCheck, _updateFailures, atStartup, Random.Shared);
        _updateTimer.Start();
    }

    private async Task CheckUpdatesAsync(bool userInitiated)
    {
        if (_updates is null) return;
        _settingsVm.IsChecking = true;
        _settingsVm.UpdateStatus = "Buscando…";
        var (ok, message) = await _updates.CheckAndDownloadAsync();
        _settingsVm.IsChecking = false;
        _settingsVm.UpdateStatus = message;
        _engine.Log.Add(DateTimeOffset.Now, "Actualizaciones: " + message);

        if (ok)
        {
            _updateFailures = 0;
            _prefs.LastUpdateCheck = DateTimeOffset.Now;
            _prefs.Save(_paths.PreferencesFile);
        }
        else if (_updates.IsConfigured && _updates.IsInstalled)
        {
            _updateFailures++;
        }

        if (!userInitiated) TryApplyPendingUpdate();
        else if (_updateTimer is not null)
        {
            // Búsqueda manual: reprogramar según el resultado (p. ej. instalar en 30 min lo descargado).
            _updateTimer.Stop();
            ScheduleNextUpdateCheck(atStartup: false);
        }
    }

    /// <summary>Instala solo si no hay una llamada en curso ni ventanas abiertas (reinicio en ~2 s, invisible).</summary>
    private void TryApplyPendingUpdate()
    {
        if (!_updates.HasPendingUpdate || _engine.Latest?.Call.InCall == true || _main is not null || _flyout?.IsVisible == true) return;
        // ApplyUpdatesAndRestart termina el proceso sin pasar por Application.Exit: cerramos todo antes.
        _history.Flush();
        try { _engine.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3)); } catch { /* ignorar */ }
        _tray.Dispose();
        _updates.ApplyAndRestart();
    }

    // ---------- Errores y cierre ----------

    private void HookGlobalErrors()
    {
        _app.DispatcherUnhandledException += (_, e) =>
        {
            _paths.AppendError("UI: " + e.Exception);
            e.Handled = true; // una app residente no debe cerrarse por un error de interfaz
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _paths.AppendError("Tarea: " + e.Exception);
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => _paths.AppendError("Fatal: " + e.ExceptionObject);
    }

    public void Shutdown()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _updateTimer?.Stop();
        try { _engine?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3)); } catch { /* ignorar */ }
        _tray?.Dispose();
        foreach (var d in _disposables) { try { d.Dispose(); } catch { /* ignorar */ } }
    }
}
