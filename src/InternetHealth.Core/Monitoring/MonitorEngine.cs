using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Network;
using InternetHealth.Core.Settings;
using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Monitoring;

public sealed class MonitorDependencies
{
    public required IPinger Pinger { get; init; }
    public required ITcpProber TcpProber { get; init; }
    public required INetworkContextProvider Network { get; init; }
    public required ICaptivePortalChecker CaptivePortal { get; init; }
    public required IThroughputMeter Throughput { get; init; }
    public ICallDetector CallDetector { get; init; } = new NoCallDetector();
    public IClock Clock { get; init; } = SystemClock.Instance;
}

/// <summary>
/// Motor de monitoreo. Corre en segundo plano (nunca en el hilo de la interfaz), ajusta la
/// frecuencia de medición según la situación y publica un <see cref="MonitorSnapshot"/> por ronda.
/// </summary>
public sealed class MonitorEngine : IAsyncDisposable
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    private readonly AppSettings _settings;
    private readonly MonitorDependencies _deps;
    private readonly HistoryStore? _history;
    private readonly IPAddress[] _internetTargets;
    private readonly IPAddress _hopTarget;

    private readonly SampleBuffer _router = new(120);
    private readonly SampleBuffer _provider = new(120);
    private readonly SampleBuffer _internet = new(180);
    private readonly SampleBuffer _cloud = new(60);
    private readonly StatusTracker _tracker = new();
    private readonly SemaphoreSlim _wake = new(0, int.MaxValue);
    private readonly object _stateGate = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile bool _paused;
    private volatile bool _resetRequested = true;
    private DateTimeOffset _resetNotBefore;
    private DateTimeOffset _lastReset;

    private NetworkContext _ctx = NetworkContext.None;
    private ProviderHop? _hop;
    private Task? _hopDiscovery;
    private bool _routerEverResponded;
    private volatile int _generation;
    private int _internetIndex, _cloudIndex;
    private int _cloudFailures, _dnsFailures;
    private double? _cloudLast, _dnsLast;
    private bool? _captive;
    private DateTimeOffset _lastTcp, _lastContext, _lastCall, _lastCaptive, _lastThroughput;
    private CallInfo _call = CallInfo.None;
    private Throughput _throughput = Throughput.Zero;
    private DateTimeOffset? _heavySince;

    public MonitorEngine(AppSettings settings, MonitorDependencies deps, HistoryStore? history = null, LiveLog? log = null)
    {
        _settings = settings;
        _deps = deps;
        _history = history;
        Log = log ?? new LiveLog();
        _internetTargets = settings.InternetTargets.Select(IPAddress.Parse).ToArray();
        _hopTarget = IPAddress.TryParse(settings.HopDiscoveryTarget, out var ht) ? ht : IPAddress.Parse("1.1.1.1");
        Notifications = new NotificationPolicy();
    }

    public LiveLog Log { get; }
    public NotificationPolicy Notifications { get; }
    public MonitorSnapshot? Latest { get; private set; }

    /// <summary>Si la ventana de detalle está abierta, medimos cada segundo para una vista "en vivo".</summary>
    public bool UiVisible
    {
        get => _uiVisible;
        set { _uiVisible = value; if (value) Wake(); }
    }
    private volatile bool _uiVisible;

    public bool CallDetectionEnabled { get; set; } = true;
    public bool DetailedLog { get; set; }

    /// <summary>Se dispara en el hilo del motor. La interfaz debe pasar al hilo de UI.</summary>
    public event Action<MonitorSnapshot>? SnapshotUpdated;
    public event Action<MonitorSnapshot>? StableChanged;
    public event Action<NotificationRequest>? NotificationRequested;

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Pause()
    {
        if (_paused) return;
        _paused = true;
        Log.Add(_deps.Clock.Now, "Monitoreo en pausa (equipo bloqueado o suspendido).");
    }

    public void Resume()
    {
        if (!_paused) return;
        _paused = false;
        RequestReset("Reanudando monitoreo", TimeSpan.FromSeconds(3));
    }

    public void RequestReset(string reason, TimeSpan? delay = null)
    {
        _resetNotBefore = _deps.Clock.Now + (delay ?? TimeSpan.FromSeconds(2));
        _resetRequested = true;
        Log.Add(_deps.Clock.Now, reason + "…");
        Wake();
    }

    public double?[] Recent(HistoryChannel channel, int count) => channel switch
    {
        HistoryChannel.Router => _router.Snapshot(count),
        HistoryChannel.Provider => _provider.Snapshot(count),
        HistoryChannel.Internet => _internet.Snapshot(count),
        _ => _cloud.Snapshot(count),
    };

    // Windows dispara estos eventos con frecuencia (p. ej. cambios de IPv6). Solo pedimos releer
    // el contexto; la ronda decide si de verdad cambió la red (router/adaptador) y reinicia.
    private void OnNetworkChanged(object? sender, EventArgs e) => RequestContextCheck();

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => RequestContextCheck();

    private void RequestContextCheck()
    {
        _lastContext = default;
        Wake();
    }

    private void Wake()
    {
        try { _wake.Release(); } catch (SemaphoreFullException) { /* ya despierto */ }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_paused)
                {
                    await _wake.WaitAsync(ct).ConfigureAwait(false);
                    continue;
                }

                var start = _deps.Clock.Now;
                var snapshot = await RunRoundAsync(ct).ConfigureAwait(false);
                var interval = TimeSpan.FromSeconds(snapshot.Mode == MonitorMode.Active
                    ? _settings.ActiveIntervalSeconds
                    : _settings.RelaxedIntervalSeconds);
                var delay = interval - (_deps.Clock.Now - start);
                if (delay < TimeSpan.FromMilliseconds(200)) delay = TimeSpan.FromMilliseconds(200);
                if (_resetRequested)
                {
                    var untilReset = _resetNotBefore - _deps.Clock.Now;
                    if (untilReset > TimeSpan.Zero && untilReset < delay) delay = untilReset;
                }

                await _wake.WaitAsync(delay, ct).ConfigureAwait(false);
                while (_wake.CurrentCount > 0) _wake.Wait(0, CancellationToken.None);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Add(_deps.Clock.Now, "Error interno del monitor: " + ex.Message);
                try { await Task.Delay(2000, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ResetAsync(CancellationToken ct)
    {
        _resetRequested = false;
        _generation++;
        var now = _deps.Clock.Now;
        _lastReset = now;
        _router.Clear(); _provider.Clear(); _internet.Clear(); _cloud.Clear();
        _routerEverResponded = false;
        _cloudFailures = 0; _dnsFailures = 0; _cloudLast = null; _dnsLast = null;
        _captive = null; _lastCaptive = default; _lastTcp = default; _heavySince = null;
        lock (_stateGate) _tracker.Reset(now);

        _ctx = SafeGetContext(includeWifi: true);
        _lastContext = now;
        Log.Add(now, DescribeContext(_ctx));

        _hop = null;
        if (_ctx.HasConnection)
            _hopDiscovery = DiscoverHopAsync(ct);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private NetworkContext SafeGetContext(bool includeWifi)
    {
        try { return _deps.Network.GetContext(includeWifi); }
        catch (Exception ex)
        {
            Log.Add(_deps.Clock.Now, "No se pudo leer la configuración de red: " + ex.Message);
            return NetworkContext.None;
        }
    }

    private async Task DiscoverHopAsync(CancellationToken ct)
    {
        int generation = _generation;
        try
        {
            for (int ttl = 1; ttl <= 8 && !ct.IsCancellationRequested; ttl++)
            {
                var r = await _deps.Pinger.PingAsync(_hopTarget, 1000, ttl, ct).ConfigureAwait(false);
                if (r.Status == IPStatus.Success) break;
                if (r.ReplyFrom is { } addr && r.Status is IPStatus.TtlExpired or IPStatus.TimeExceeded
                    && IpClassification.IsProviderSide(addr))
                {
                    if (generation != _generation) return; // la red cambió mientras buscábamos
                    _hop = new ProviderHop(addr, ttl);
                    Log.Add(_deps.Clock.Now, $"Primer salto del proveedor: {addr} (salto {ttl}).");
                    return;
                }
            }
            Log.Add(_deps.Clock.Now, "No se identificó el primer salto del proveedor (algunas redes no lo permiten).");
        }
        catch (OperationCanceledException) { /* cierre */ }
        catch (Exception ex)
        {
            Log.Add(_deps.Clock.Now, "Descubrimiento de salto falló: " + ex.Message);
        }
    }

    /// <summary>Una ronda de mediciones + evaluación. Expuesto para pruebas.</summary>
    internal async Task<MonitorSnapshot> RunRoundAsync(CancellationToken ct)
    {
        if (_resetRequested && _deps.Clock.Now >= _resetNotBefore)
            await ResetAsync(ct).ConfigureAwait(false);

        var now = _deps.Clock.Now;
        bool active = IsActive(now);

        // Contexto (Wi-Fi, adaptador): cada 5 s en modo activo, 15 s en reposo.
        if (now - _lastContext >= TimeSpan.FromSeconds(active ? 5 : 15))
        {
            var ctx = SafeGetContext(includeWifi: true);
            _lastContext = now;
            if (!Equals(ctx.Gateway, _ctx.Gateway) || ctx.AdapterId != _ctx.AdapterId || ctx.HasConnection != _ctx.HasConnection)
            {
                _ctx = ctx;
                RequestReset("La red cambió", TimeSpan.Zero);
            }
            else _ctx = ctx;
        }

        var ctxNow = _ctx;

        // Pings en paralelo: router, un destino de internet (rotando) y el salto del proveedor.
        Task<PingOutcome>? routerTask = ctxNow.Gateway is { } gw
            ? _deps.Pinger.PingAsync(gw, _settings.RouterTimeoutMs, null, ct)
            : null;
        var target = _internetTargets[_internetIndex++ % _internetTargets.Length];
        Task<PingOutcome>? internetTask = ctxNow.HasConnection
            ? _deps.Pinger.PingAsync(target, _settings.InternetTimeoutMs, null, ct)
            : null;
        var hop = _hop;
        Task<PingOutcome>? hopTask = hop is not null && ctxNow.HasConnection
            ? _deps.Pinger.PingAsync(_hopTarget, _settings.InternetTimeoutMs, hop.Ttl, ct)
            : null;

        // Conexión real (DNS + TCP 443) a Microsoft 365.
        bool icmpBlocked = Latest?.Stable.IcmpBlocked == true;
        var tcpEvery = icmpBlocked ? TimeSpan.FromSeconds(2) : active ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(30);
        Task<TcpProbeOutcome>? tcpTask = null;
        if (ctxNow.HasConnection && _settings.CloudTargets.Count > 0 && now - _lastTcp >= tcpEvery)
        {
            _lastTcp = now;
            var ct0 = _settings.CloudTargets[_cloudIndex++ % _settings.CloudTargets.Count];
            tcpTask = _deps.TcpProber.ProbeAsync(ct0.Host, ct0.Port, 3000, ct);
        }

        // Portal cautivo: tras un cambio de red y cuando hay fallas.
        Task<bool?>? captiveTask = null;
        bool failing = Latest is { } l && (l.Raw.Severity == Health.Down || _cloudFailures >= 2);
        if (ctxNow.HasConnection && ((_captive is null && now - _lastReset > TimeSpan.FromSeconds(3)) || (failing && now - _lastCaptive > TimeSpan.FromSeconds(60)))
            && now - _lastCaptive > TimeSpan.FromSeconds(20))
        {
            _lastCaptive = now;
            captiveTask = _deps.CaptivePortal.CheckAsync(ct);
        }

        var routerOut = routerTask is null ? (PingOutcome?)null : await routerTask.ConfigureAwait(false);
        var internetOut = internetTask is null ? (PingOutcome?)null : await internetTask.ConfigureAwait(false);
        var hopOut = hopTask is null ? (PingOutcome?)null : await hopTask.ConfigureAwait(false);
        var tcpOut = tcpTask is null ? (TcpProbeOutcome?)null : await tcpTask.ConfigureAwait(false);
        if (captiveTask is not null)
        {
            var c = await captiveTask.ConfigureAwait(false);
            if (c is not null)
            {
                if (c != _captive && c == true) Log.Add(now, "La red requiere iniciar sesión (portal cautivo).");
                _captive = c;
            }
        }

        var sampleTime = _deps.Clock.Now;
        if (routerOut is { } ro)
        {
            _router.Add(sampleTime, ro.RttMs);
            if (ro.IsSuccess) _routerEverResponded = true;
            _history?.AddSample(HistoryChannel.Router, sampleTime, ro.RttMs);
        }
        if (internetOut is { } io)
        {
            _internet.Add(sampleTime, io.RttMs);
            _history?.AddSample(HistoryChannel.Internet, sampleTime, io.RttMs);
        }
        if (hopOut is { } ho)
        {
            double? v = ho.Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TimeExceeded ? ho.RttMs : null;
            _provider.Add(sampleTime, v);
            _history?.AddSample(HistoryChannel.Provider, sampleTime, v);
        }
        if (tcpOut is { } to)
        {
            if (to.ConnectOk) { _cloudFailures = 0; _cloudLast = to.ConnectMs; }
            else _cloudFailures++;
            if (to.DnsOk) { _dnsFailures = 0; _dnsLast = to.DnsMs; }
            else _dnsFailures++;
            _cloud.Add(sampleTime, to.ConnectOk ? to.ConnectMs : null);
            _history?.AddSample(HistoryChannel.Cloud, sampleTime, to.ConnectOk ? to.ConnectMs : null);
            if (!to.ConnectOk && _cloudFailures == 3)
                Log.Add(sampleTime, "Microsoft 365 no responde: " + to.Error);
        }

        // Uso de red del propio equipo (cada 2 s como mínimo).
        if (now - _lastThroughput >= TimeSpan.FromSeconds(2))
        {
            _lastThroughput = now;
            try { _throughput = _deps.Throughput.Sample(ctxNow.AdapterId, now); } catch { _throughput = Throughput.Zero; }
            if (Thresholds.IsHeavyUsage(_throughput)) _heavySince ??= now;
            else _heavySince = null;
        }
        bool heavy = _heavySince is { } hs && now - hs >= TimeSpan.FromSeconds(8);

        // Detección de llamadas (micrófono en uso) cada 5 s.
        if (CallDetectionEnabled && now - _lastCall >= TimeSpan.FromSeconds(5))
        {
            _lastCall = now;
            CallInfo call;
            try { call = _deps.CallDetector.Detect(); } catch { call = CallInfo.None; }
            if (call.InCall != _call.InCall)
                Log.Add(now, call.InCall ? $"Llamada detectada ({call.AppName ?? "micrófono en uso"}): monitoreo intensivo." : "Llamada finalizada.");
            _call = call;
        }
        else if (!CallDetectionEnabled) _call = CallInfo.None;

        // Evaluación.
        var window = TimeSpan.FromSeconds(active ? 60 : 180);
        var routerStats = _router.Compute(sampleTime, window, 30);
        var providerStats = _provider.Compute(sampleTime, window, 30);
        var internetStats = _internet.Compute(sampleTime, window, 30);
        var cloudStats = _cloud.Compute(sampleTime, TimeSpan.FromMinutes(icmpBlocked ? 2 : 10), 30);

        var assessment = new Assessment(
            ctxNow, routerStats, _routerEverResponded, _hop, providerStats, internetStats, cloudStats,
            _cloudFailures, _dnsFailures, _throughput, heavy, _captive, _call);
        var raw = DiagnosisEngine.Diagnose(assessment);

        bool changed;
        Diagnosis.Diagnosis stable;
        DateTimeOffset stableSince;
        lock (_stateGate)
        {
            changed = _tracker.Update(raw, sampleTime);
            stable = _tracker.Stable;
            stableSince = _tracker.StableSince;
        }

        var mode = _paused ? MonitorMode.Paused
            : IsActive(now, raw, stable) ? MonitorMode.Active : MonitorMode.Relaxed;

        var snapshot = new MonitorSnapshot(sampleTime, ctxNow, routerStats, _routerEverResponded, _hop, providerStats,
            internetStats, cloudStats, _cloudLast, _dnsLast, _throughput, _call, _captive, raw, stable, stableSince, mode);
        Latest = snapshot;
        _history?.Observe(snapshot);

        if (DetailedLog)
        {
            Log.Add(sampleTime,
                $"Router {Fmt(routerOut)} | Internet {target} {Fmt(internetOut)} | Proveedor {Fmt(hopOut)}" +
                (tcpOut is { } t2 ? $" | M365 {(t2.ConnectOk ? t2.ConnectMs!.Value.ToString("0", Es) + " ms" : "falla")}" : "") +
                $" | MOS {raw.Mos?.ToString("0.0", Es) ?? "-"} | {raw.Code}");
        }

        if (changed)
        {
            Log.Add(sampleTime, $"Estado: {stable.Severity.ToLabel()} — {stable.Text.Title}");
            _history?.RecordEvent(snapshot);
            SafeInvoke(StableChanged, snapshot);
        }

        var notification = Notifications.Evaluate(stable, stableSince, _call.InCall, sampleTime);
        if (notification is not null)
        {
            Log.Add(sampleTime, "Notificación: " + notification.Title);
            try { NotificationRequested?.Invoke(notification); } catch { /* ignorar */ }
        }

        SafeInvoke(SnapshotUpdated, snapshot);
        return snapshot;
    }

    private bool IsActive(DateTimeOffset now, Diagnosis.Diagnosis? raw = null, Diagnosis.Diagnosis? stable = null)
    {
        if (_uiVisible || _call.InCall) return true;
        if (now - _lastReset < TimeSpan.FromSeconds(30)) return true;
        var r = raw ?? Latest?.Raw;
        var s = stable ?? Latest?.Stable;
        if (r is null || s is null) return true;
        if (s.Code == DiagnosisCode.Checking) return true;
        return r.Severity >= Health.Fair || s.Severity >= Health.Fair;
    }

    private void SafeInvoke(Action<MonitorSnapshot>? handler, MonitorSnapshot s)
    {
        if (handler is null) return;
        foreach (var d in handler.GetInvocationList())
        {
            try { ((Action<MonitorSnapshot>)d)(s); }
            catch (Exception ex) { Log.Add(s.Time, "Error en suscriptor: " + ex.Message); }
        }
    }

    private static string Fmt(PingOutcome? o) =>
        o is null ? "-" : o.Value.RttMs is double ms ? ms.ToString("0", Es) + " ms" : "sin respuesta";

    private static string DescribeContext(NetworkContext c)
    {
        if (!c.HasConnection) return "Sin conexión de red activa.";
        var parts = new List<string> { $"Conexión: {c.LinkTypeLabel}" };
        if (c.Wifi?.Ssid is { } ssid) parts.Add($"red «{ssid}»");
        if (c.Gateway is not null) parts.Add($"router {c.Gateway}");
        if (c.VpnActive) parts.Add($"VPN activa ({c.VpnName})");
        return string.Join(", ", parts) + ".";
    }

    public async ValueTask DisposeAsync()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            if (_loop is not null)
            {
                try { await _loop.ConfigureAwait(false); } catch { /* ignorar */ }
            }
            _cts.Dispose();
        }
        _history?.Flush();
        _wake.Dispose();
    }
}
