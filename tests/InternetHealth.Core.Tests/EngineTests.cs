using System.Net;
using System.Net.NetworkInformation;
using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.Export;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;
using InternetHealth.Core.Monitoring;
using InternetHealth.Core.Network;
using InternetHealth.Core.Settings;

namespace InternetHealth.Core.Tests;

internal sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset Now { get; set; } = start;
}

/// <summary>Red simulada: cada destino responde según una función configurable.</summary>
internal sealed class FakePinger : IPinger
{
    public Func<IPAddress, int?, PingOutcome> Behavior { get; set; } = (_, _) => new PingOutcome(IPStatus.Success, 10, null);
    public int Calls;

    public Task<PingOutcome> PingAsync(IPAddress target, int timeoutMs, int? ttl, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(Behavior(target, ttl));
    }
}

internal sealed class FakeTcp : ITcpProber
{
    public bool Ok { get; set; } = true;
    public Task<TcpProbeOutcome> ProbeAsync(string host, int port, int timeoutMs, CancellationToken ct) =>
        Task.FromResult(Ok ? new TcpProbeOutcome(true, 2, true, 35, null) : new TcpProbeOutcome(true, 2, false, null, "timeout"));
}

internal sealed class FakeNetwork(NetworkContext ctx) : INetworkContextProvider
{
    public NetworkContext Context { get; set; } = ctx;
    public NetworkContext GetContext(bool includeWifiDetails) => Context;
}

internal sealed class FakeCaptive : ICaptivePortalChecker
{
    public Task<bool?> CheckAsync(CancellationToken ct) => Task.FromResult<bool?>(false);
}

internal sealed class FakeThroughput : IThroughputMeter
{
    public Throughput Sample(string? adapterId, DateTimeOffset now) => new(1, 0.1);
}

internal sealed class FakeCall(bool inCall) : ICallDetector
{
    public CallInfo Detect() => inCall ? new CallInfo(true, "Teams") : CallInfo.None;
}

public class EngineTests
{
    private static readonly IPAddress Isp = IPAddress.Parse("181.49.1.1");

    private static (MonitorEngine Engine, FakeClock Clock, FakePinger Pinger, FakeTcp Tcp, AppPaths Paths) Create(bool inCall = false)
    {
        var clock = new FakeClock(Build.T0);
        var pinger = new FakePinger();
        pinger.Behavior = (target, ttl) => ttl is not null
            ? new PingOutcome(IPStatus.TtlExpired, 8, ttl >= 2 ? Isp : IPAddress.Parse("192.168.1.1"))
            : new PingOutcome(IPStatus.Success, target.ToString().StartsWith("192.168", StringComparison.Ordinal) ? 3 : 25, target);
        var tcp = new FakeTcp();
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), "ihm-tests-" + Guid.NewGuid().ToString("N")));
        var history = new HistoryStore(paths, "2.0.0-test");
        var engine = new MonitorEngine(new AppSettings(), new MonitorDependencies
        {
            Pinger = pinger,
            TcpProber = tcp,
            Network = new FakeNetwork(Build.Wifi()),
            CaptivePortal = new FakeCaptive(),
            Throughput = new FakeThroughput(),
            CallDetector = new FakeCall(inCall),
            Clock = clock,
        }, history);
        return (engine, clock, pinger, tcp, paths);
    }

    private static async Task<MonitorSnapshot> Rounds(MonitorEngine e, FakeClock clock, int n, double stepSeconds = 1)
    {
        MonitorSnapshot? last = null;
        for (int i = 0; i < n; i++)
        {
            clock.Now = clock.Now.AddSeconds(stepSeconds);
            last = await e.RunRoundAsync(CancellationToken.None);
        }
        return last!;
    }

    [Test]
    public async Task Healthy_network_reaches_all_good_and_relaxes()
    {
        var (e, clock, _, _, paths) = Create();
        var s = await Rounds(e, clock, 8);
        Assert.Equal(DiagnosisCode.AllGood, s.Stable.Code);
        clock.Now = clock.Now.AddSeconds(40); // pasada la ventana de arranque
        s = await Rounds(e, clock, 1);
        Assert.Equal(MonitorMode.Relaxed, s.Mode);
        Assert.True(s.RouterMeasurable);
        Directory.Delete(paths.DataRoot, true);
    }

    [Test]
    public async Task Call_forces_active_mode()
    {
        var (e, clock, _, _, _) = Create(inCall: true);
        clock.Now = clock.Now.AddSeconds(40);
        var s = await Rounds(e, clock, 6);
        Assert.True(s.Call.InCall);
        Assert.Equal(MonitorMode.Active, s.Mode);
    }

    [Test]
    public async Task Outage_is_detected_and_notified()
    {
        var (e, clock, pinger, tcp, _) = Create();
        NotificationRequest? note = null;
        e.NotificationRequested += n => note = n;
        await Rounds(e, clock, 10);

        // Se cae el proveedor: el router responde, internet no.
        pinger.Behavior = (target, ttl) => target.ToString().StartsWith("192.168", StringComparison.Ordinal) && ttl is null
            ? new PingOutcome(IPStatus.Success, 3, target)
            : PingOutcome.Failed(IPStatus.TimedOut);
        tcp.Ok = false;
        var s = await Rounds(e, clock, 80);
        Assert.Equal(Health.Down, s.Stable.Severity);
        Assert.Equal(DiagnosisCode.NoInternet, s.Stable.Code);
        Assert.NotNull(note);
    }

    [Test]
    public async Task History_rows_are_written_per_minute_and_readable()
    {
        var (e, clock, _, _, paths) = Create();
        await Rounds(e, clock, 150); // ~2.5 minutos
        var rows = HistoryStore.Read(paths, Build.T0.AddMinutes(-1), Build.T0.AddMinutes(5));
        Assert.True(rows.Count >= 2, $"filas: {rows.Count}");
        var r = rows[0];
        Assert.Equal("Oficina", r.Ssid);
        Assert.True(r.Internet.Sent > 0);
        Assert.Near(25, r.Internet.AvgMs, 0.5);
        Assert.Equal(Health.Good, r.WorstSeverity);

        var zip = Path.Combine(paths.DataRoot, "export.zip");
        DiagnosticExporter.Create(zip, paths, e.Latest, e.Log, "2.0.0-test", clock.Now);
        List<string> names;
        using (var archive = System.IO.Compression.ZipFile.OpenRead(zip))
            names = archive.Entries.Select(x => x.FullName).ToList();
        Assert.True(names.Contains("resumen.html"));
        Assert.True(names.Any(n => n.StartsWith("historial/", StringComparison.Ordinal)));
        Assert.True(names.Contains("eventos.csv"));
        Directory.Delete(paths.DataRoot, true);
    }

    [Test]
    public async Task History_written_in_another_time_zone_reads_back_at_the_same_instant()
    {
        // Pasó en GitHub Actions (servidor en UTC): las filas se leían corridas 5 h y el filtro las
        // descartaba. Aquí se escribe con UTC+3 para que falle en cualquier equipo que no esté en +3.
        var (e, clock, _, _, paths) = Create();
        clock.Now = Build.T0.ToOffset(TimeSpan.FromHours(3)); // mismo instante, otra zona horaria
        await Rounds(e, clock, 150);
        var rows = HistoryStore.Read(paths, Build.T0.AddMinutes(-1), Build.T0.AddMinutes(5));
        Assert.True(rows.Count >= 2, $"filas: {rows.Count}");
        Assert.Equal(TimeSpan.FromHours(3), rows[0].Minute.Offset);
        Assert.True(rows[0].Minute >= Build.T0 && rows[0].Minute <= Build.T0.AddMinutes(3), $"minuto {rows[0].Minute}");
        await e.DisposeAsync();
        Directory.Delete(paths.DataRoot, true);
    }

    [Test]
    public async Task Provider_hop_is_discovered_via_ttl()
    {
        var (e, clock, _, _, _) = Create();
        await Rounds(e, clock, 1);
        await Task.Delay(50); // el descubrimiento corre en paralelo
        var s = await Rounds(e, clock, 1);
        Assert.NotNull(s.ProviderHop);
        Assert.Equal(Isp, s.ProviderHop!.Address);
        Assert.Equal(2, s.ProviderHop.Ttl);
    }
}

public class SettingsAndCsvTests
{
    [Test]
    public void Settings_layers_merge_and_normalize()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ihm-set-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var a = Path.Combine(dir, "a.json");
        var b = Path.Combine(dir, "b.json");
        File.WriteAllText(a, """
            // comentario permitido
            { "supportName": "Mesa de ayuda Ardisa", "internetTargets": ["1.1.1.1", "no-es-ip"], "activeIntervalSeconds": 0, }
            """);
        File.WriteAllText(b, """{ "SupportUrl": "https://soporte.ejemplo.com" }""");
        var s = AppSettings.Load([a, b, Path.Combine(dir, "no-existe.json")]);
        Assert.Equal("Mesa de ayuda Ardisa", s.SupportName);
        Assert.Equal("https://soporte.ejemplo.com", s.SupportUrl);
        Assert.Equal(1, s.InternetTargets.Count);
        Assert.Equal(1, s.ActiveIntervalSeconds);
        Assert.Equal(2, s.CloudTargets.Count);
        Directory.Delete(dir, true);
    }

    [Test]
    public void Csv_escaping_roundtrips()
    {
        var fields = InternetHealth.Core.History.Csv.Split("a,\"b,c\",\"d \"\"e\"\"\",,f");
        Assert.Equal(5, fields.Count);
        Assert.Equal("b,c", fields[1]);
        Assert.Equal("d \"e\"", fields[2]);
        Assert.Equal("", fields[3]);
        Assert.Equal("\"x,y\"", InternetHealth.Core.History.Csv.Escape("x,y"));
    }

    [Test]
    public void Report_html_renders_without_data()
    {
        var html = ReportBuilder.Build(null, [], [], "2.0.0", Build.T0);
        Assert.Contains("Diagnóstico de conexión", html);
        Assert.Contains("Todavía no hay historial", html);
    }
}
