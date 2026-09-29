using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.Export;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Network;
using InternetHealth.Core.Settings;
using InternetHealth.Mac.Interop;
using InternetHealth.Mac.Views;
using InternetHealth.Presentation;
using InternetHealth.Presentation.ViewModels;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Arma y coordina la app en Mac: motor, barra de menús, ventanas, preferencias y actualizaciones.
/// Es el equivalente de AppHost en Windows; la lógica de diagnóstico es la misma (Core).
/// </summary>
internal sealed class MacHost
{
    private readonly Application _app;
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
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
    private MenuBarIcon _menuBar = null!;
    private Notifier _notifier = null!;
    private SessionMonitor _session = null!;
    private UpdateService _updates = null!;
    private DashboardViewModel _dashboard = null!;
    private HistoryViewModel _historyVm = null!;
    private SettingsViewModel _settingsVm = null!;
    private MainWindowViewModel _mainVm = null!;
    private PanelWindow? _panel;
    private MainWindow? _main;
    private DispatcherTimer? _updateTimer;
    private readonly List<IDisposable> _disposables = [];
    private Health _lastTrayHealth = (Health)(-1);
    private string _lastTrayText = "";
    private int _uiRefreshQueued;
    private int _updateFailures;
    private bool _shuttingDown;

    public MacHost(Application app, IClassicDesktopStyleApplicationLifetime lifetime, SingleInstance instance, string[] args, bool firstRun)
    {
        _app = app;
        _lifetime = lifetime;
        _instance = instance;
        _background = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        _demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        _firstRun = firstRun;
    }

    public void Start()
    {
        _paths = new AppPaths(_demo ? Path.Combine(Path.GetTempPath(), "InternetHealthMonitorDemo") : null);
        HookGlobalErrors();

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
        _engine.Log.Add(DateTimeOffset.Now, $"Monitor de Conexión {_version} para Mac iniciado{(_demo ? " (modo demostración)" : "")}.");

        _dashboard = new DashboardViewModel { IsDemo = _demo };
        _historyVm = new HistoryViewModel(_paths);
        _settingsVm = new SettingsViewModel(_prefs, _settings, _version, OnPreferencesChanged);
        _mainVm = new MainWindowViewModel(_dashboard, _historyVm, _settingsVm);

        _notifier = new Notifier();
        _notifier.Clicked += () => Dispatcher.UIThread.Post(ShowPanel);
        _notifier.Status += msg => _engine.Log.Add(DateTimeOffset.Now, msg);
        _engine.Log.Add(DateTimeOffset.Now, $"Avisos: {_notifier.Mode}{(_notifier.StartupError is { } err ? $" ({err})" : "")}.");

        _menuBar = new MenuBarIcon(_demo);
        _menuBar.OpenPanel += ShowPanel;
        _menuBar.OpenDetails += () => ShowMain(0);
        _menuBar.Share += ShareDiagnostic;
        _menuBar.ToggleDoNotDisturb += ToggleDnd;
        _menuBar.ToggleStartup += () => { _settingsVm.StartWithWindows = !_settingsVm.StartWithWindows; };
        _menuBar.Exit += () => _lifetime.Shutdown();
        _app.ActualThemeVariantChanged += (_, _) =>
        {
            _menuBar.RefreshIcons();
            _dashboard.RefreshThemeBindings();
        };

        _engine.SnapshotUpdated += OnSnapshot;
        _engine.NotificationRequested += n => Dispatcher.UIThread.Post(() => OnNotification(n));
        _engine.Log.Added += e =>
        {
            if (_main is not null) Dispatcher.UIThread.Post(() => _mainVm.AddLog(e));
        };
        _history.MinuteWritten += row =>
        {
            if (_main is not null && _mainVm.SelectedTab == 1)
                Dispatcher.UIThread.Post(() => { _ = _historyVm.LoadAsync(); });
        };
        _mainVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedTab) && _mainVm.SelectedTab == 1) _ = _historyVm.LoadAsync();
        };

        // Abrir la app otra vez (desde Aplicaciones, Launchpad o Spotlight) muestra la ventana.
        _instance.ListenForActivation(() => Dispatcher.UIThread.Post(() => ShowMain(0)));
        if (_app.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
            activatable.Activated += (_, e) =>
            {
                if (e.Kind == ActivationKind.Reopen && !_shuttingDown) ShowMain(0);
            };

        _session = new SessionMonitor();
        _session.Locked += () => { if (_engine.Latest?.Call.InCall != true) _engine.Pause(); };
        _session.Unlocked += _engine.Resume;
        _session.Woke += () =>
        {
            _history.Flush();
            _engine.RequestReset("El equipo salió de suspensión", TimeSpan.FromSeconds(3));
        };
        _disposables.Add(_session);

        if (!_demo) LaunchAgent.Set(_prefs.StartWithWindows);
        SyncMenu();
        _engine.Start();

        SetupUpdates();

        if (!_prefs.FirstRunCompleted || _firstRun)
        {
            _prefs.FirstRunCompleted = true;
            _prefs.Save(_paths.PreferencesFile);
            Notify("Monitor de Conexión está activo",
                "Lo encontrarás en la barra de menús, arriba a la derecha. Te avisaremos si algo en tu conexión puede afectar tus reuniones.");
            Dispatcher.UIThread.Post(ShowPanel, DispatcherPriority.ApplicationIdle);
        }
        else if (!_background)
        {
            Dispatcher.UIThread.Post(() => ShowMain(0), DispatcherPriority.ApplicationIdle);
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

        var pinger = new MacPinger();
        var captive = new HttpCaptivePortalChecker();
        _disposables.Add(pinger);
        _disposables.Add(captive);
        return new MonitorDependencies
        {
            Pinger = pinger,
            TcpProber = new SystemTcpProber(),
            Network = new MacNetworkContextProvider(),
            CaptivePortal = captive,
            Throughput = new SystemThroughputMeter(),
            CallDetector = new MicrophoneCallDetector(),
        };
    }

    // ---------- Actualización de la interfaz ----------

    private void OnSnapshot(MonitorSnapshot s)
    {
        // Se llama en el hilo del motor. Evitamos encolar trabajo si la UI aún no procesó el anterior.
        if (Interlocked.Exchange(ref _uiRefreshQueued, 1) == 1) return;
        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _uiRefreshQueued, 0);
            var latest = _engine.Latest ?? s;
            UpdateMenuBar(latest);
            bool panelVisible = _panel?.IsVisible == true;
            bool mainVisible = IsMainVisible;
            if (panelVisible || mainVisible)
                _dashboard.Apply(latest, _engine, includeDetails: mainVisible);
        }, DispatcherPriority.Background);
    }

    private bool IsMainVisible => _main is not null && _main.IsVisible && _main.WindowState != WindowState.Minimized;

    private void UpdateMenuBar(MonitorSnapshot s)
    {
        var d = s.Stable;
        string text = d.Code == DiagnosisCode.Checking ? d.Text.TrayText
            : s.Call.InCall ? $"{d.Text.TrayText} · en llamada" : d.Text.TrayText;
        if (d.Severity == _lastTrayHealth && text == _lastTrayText) return;
        _lastTrayHealth = d.Severity;
        _lastTrayText = text;
        _menuBar.Update(d.Severity, text);
    }

    private void OnNotification(NotificationRequest n)
    {
        if (_main?.IsActive == true && _main.WindowState != WindowState.Minimized) return; // ya lo está viendo
        Notify(n.Title, n.Message);
    }

    private void Notify(string title, string message) => _notifier.Show(_demo ? "[Demo] " + title : title, message);

    // ---------- Ventanas ----------

    private PanelWindow EnsurePanel()
    {
        if (_panel is not null) return _panel;
        _panel = new PanelWindow { DataContext = _dashboard };
        _panel.DetailsRequested += ShowMain;
        _panel.ShareRequested += ShareDiagnostic;
        _panel.CaptivePortalRequested += OpenCaptivePortal;
        _panel.LocationRequested += RequestLocation;
        _panel.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty) UpdateUiVisibility();
        };
        return _panel;
    }

    private void ShowPanel()
    {
        var p = EnsurePanel();
        RefreshNow(includeDetails: false);
        if (!p.IsVisible) p.ShowNearMenuBar();
        else p.Activate();
    }

    private void ShowMain(int tab)
    {
        if (_main is null)
        {
            _main = new MainWindow { DataContext = _mainVm };
            _main.ShareRequested += ShareDiagnostic;
            _main.CopySummaryRequested += CopySummary;
            _main.WifiSettingsRequested += () => OpenUri("x-apple.systempreferences:com.apple.wifi-settings-extension");
            _main.CaptivePortalRequested += OpenCaptivePortal;
            _main.SupportRequested += OpenSupport;
            _main.CopyLogRequested += CopyLog;
            _main.DataFolderRequested += () => OpenUri(_paths.DataRoot, ensureDirectory: true);
            _main.LocationRequested += RequestLocation;
            _main.CheckUpdatesRequested += () => _ = CheckUpdatesAsync(userInitiated: true);
            _main.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty || e.Property == Window.WindowStateProperty) UpdateUiVisibility();
            };
            _main.Closed += (_, _) =>
            {
                _main = null;
                _mainVm.Log.Clear();
                UpdateUiVisibility();
                Dock.Hide();
                // Liberar la memoria de la ventana: la app vuelve a su mínimo consumo.
                Dispatcher.UIThread.Post(() => GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true),
                    DispatcherPriority.ApplicationIdle);
            };
            _mainVm.ReloadLog(_engine.Log.Snapshot());
            _settingsVm.Refresh();
        }

        _mainVm.SelectedTab = tab;
        if (tab == 1) _ = _historyVm.LoadAsync();
        RefreshNow(includeDetails: true);
        // Con la ventana abierta la app aparece en el Dock y en Cmd+Tab, como cualquier ventana.
        Dock.Show();
        if (!_main.IsVisible) _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void RefreshNow(bool includeDetails)
    {
        if (_engine.Latest is { } s) _dashboard.Apply(s, _engine, includeDetails);
        _dashboard.DndActive = _prefs.DoNotDisturbUntil is { } dnd && dnd > DateTimeOffset.Now;
    }

    private void UpdateUiVisibility() => _engine.UiVisible = IsMainVisible || _panel?.IsVisible == true;

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
                Shell.CopyToClipboard(SummaryText.Build(s, HistoryStore.Read(_paths, now.AddHours(-1), now)));

            Shell.Open(zip, reveal: true); // lo muestra seleccionado en el Finder
            Notify("Diagnóstico listo",
                "Se guardó en Descargas y el resumen quedó copiado: pégalo en Teams o en un correo y adjunta el archivo.");
            _engine.Log.Add(now, "Diagnóstico exportado: " + zip);
        }
        catch (Exception ex)
        {
            _paths.AppendError("Exportar: " + ex);
            Notify("No se pudo crear el diagnóstico", ex.Message);
        }
    }

    private void CopySummary()
    {
        if (_engine.Latest is not { } s) return;
        var now = DateTimeOffset.Now;
        if (Shell.CopyToClipboard(SummaryText.Build(s, HistoryStore.Read(_paths, now.AddHours(-1), now))))
            Notify("Resumen copiado", "Pégalo en un chat de Teams o en un correo.");
    }

    private void CopyLog() =>
        Shell.CopyToClipboard(string.Join(Environment.NewLine, _engine.Log.Snapshot().Select(e => e.ToString())));

    private void RequestLocation() => _engine.Log.Add(DateTimeOffset.Now, LocationPermission.Request());

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
            Shell.Open(target);
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
        SyncMenu();
    }

    private void OnPreferencesChanged()
    {
        _engine.CallDetectionEnabled = _prefs.CallDetectionEnabled;
        _engine.DetailedLog = _prefs.DetailedLog;
        _engine.Notifications.Enabled = _prefs.NotificationsEnabled;
        if (!_demo) LaunchAgent.Set(_prefs.StartWithWindows);
        _prefs.Save(_paths.PreferencesFile);
        SyncMenu();
    }

    private void SyncMenu()
    {
        bool dnd = _prefs.DoNotDisturbUntil is { } until && until > DateTimeOffset.Now;
        _menuBar.SetMenuState(dnd, _prefs.StartWithWindows);
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
            _updateTimer.Stop();
            ScheduleNextUpdateCheck(atStartup: false);
        }
    }

    /// <summary>Instala solo si no hay una llamada en curso ni ventanas abiertas (reinicio en ~2 s, invisible).</summary>
    private void TryApplyPendingUpdate()
    {
        if (!_updates.HasPendingUpdate || _engine.Latest?.Call.InCall == true || _main is not null || _panel?.IsVisible == true) return;
        // ApplyUpdatesAndRestart termina el proceso sin pasar por el cierre normal: cerramos todo antes.
        _history.Flush();
        try { _engine.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3)); } catch { /* ignorar */ }
        _menuBar.Dispose();
        _instance.Dispose(); // libera el bloqueo para que la versión nueva pueda arrancar
        _updates.ApplyAndRestart();
    }

    // ---------- Errores y cierre ----------

    private void HookGlobalErrors()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
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
        _shuttingDown = true;
        _updateTimer?.Stop();
        try { _history?.Flush(); } catch { /* ignorar */ }
        try { _engine?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3)); } catch { /* ignorar */ }
        _menuBar?.Dispose();
        foreach (var d in _disposables) { try { d.Dispose(); } catch { /* ignorar */ } }
    }
}

/// <summary>Muestra la app en el Dock solo mientras la ventana de detalle está abierta.</summary>
internal static class Dock
{
    public static void Show() => SetPolicy(0);  // NSApplicationActivationPolicyRegular
    public static void Hide() => SetPolicy(1);  // NSApplicationActivationPolicyAccessory

    private static void SetPolicy(nint policy)
    {
        try
        {
            var app = ObjC.Send(ObjC.objc_getClass("NSApplication"), "sharedApplication");
            ObjC.SendVoid(app, "setActivationPolicy:", policy);
            if (policy == 0) ObjC.SendVoid(app, "activateIgnoringOtherApps:", 1);
        }
        catch { /* no crítico */ }
    }
}
