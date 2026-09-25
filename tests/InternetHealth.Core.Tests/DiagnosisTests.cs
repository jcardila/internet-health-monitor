using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.Model;
using InternetHealth.Core.Network;

namespace InternetHealth.Core.Tests;

public class DiagnosisTests
{
    private static Diagnosis.Diagnosis D(Assessment a) => DiagnosisEngine.Diagnose(a);

    [Test]
    public void Healthy_connection_is_all_good()
    {
        var d = D(Build.Assess());
        Assert.Equal(DiagnosisCode.AllGood, d.Code);
        Assert.Equal(Health.Good, d.Severity);
        Assert.Equal(5, d.Chain.Count);
        Assert.True(d.Chain.All(c => c.Health == Health.Good), "todos los eslabones en verde");
    }

    [Test]
    public void Router_slow_to_ping_does_not_raise_alarm_when_internet_is_fine()
    {
        // El error principal de la versión anterior: 1 ping lento/perdido del router = alarma roja.
        var router = Build.Stats(3, 80, null, 4, 95, 3, null, 5, 70, 4, 3, 90);
        var d = D(Build.Assess(router: router));
        Assert.Equal(DiagnosisCode.AllGood, d.Code);
        Assert.Equal(Health.Good, d.Chain[(int)Segment.Router].Health);
    }

    [Test]
    public void Single_lost_internet_ping_is_not_a_problem()
    {
        var d = D(Build.Assess(internet: Build.Steady(25, 30, lost: 1)));
        Assert.Equal(Health.Good, d.Severity);
    }

    [Test]
    public void Weak_wifi_with_local_loss_blames_wifi()
    {
        var ctx = Build.Wifi(signal: 28, rate: 11, band: "2.4 GHz");
        var d = D(Build.Assess(ctx: ctx, router: Build.Steady(20, 30, lost: 5, wobble: 25), internet: Build.Steady(60, 30, lost: 5, wobble: 30)));
        Assert.Equal(DiagnosisCode.WeakWifi, d.Code);
        Assert.Equal(Segment.Link, d.Culprit);
        Assert.True(d.Severity >= Health.Fair);
        Assert.Contains("5 GHz", string.Join(" ", d.Text.Steps)); // sugiere cambiar de banda
        Assert.Contains("(28 %)", d.Text.Summary);
    }

    [Test]
    public void Local_loss_with_good_signal_blames_local_network()
    {
        var d = D(Build.Assess(router: Build.Steady(10, 30, lost: 4, wobble: 5), internet: Build.Steady(40, 30, lost: 4)));
        Assert.Equal(DiagnosisCode.LocalNetwork, d.Code);
        Assert.Equal(Segment.Router, d.Culprit);
    }

    [Test]
    public void Clean_lan_but_lossy_provider_blames_provider()
    {
        var d = D(Build.Assess(provider: Build.Steady(15, 30, lost: 4), internet: Build.Steady(40, 30, lost: 4)));
        Assert.Equal(DiagnosisCode.ProviderIssue, d.Code);
        Assert.Equal(Segment.Provider, d.Culprit);
    }

    [Test]
    public void Clean_lan_and_provider_means_external_problem()
    {
        var d = D(Build.Assess(internet: Build.Steady(40, 30, lost: 4)));
        Assert.Equal(DiagnosisCode.ExternalIssue, d.Code);
        Assert.Equal(Segment.Internet, d.Culprit);
    }

    [Test]
    public void Clean_lan_unknown_provider_blames_exit_to_internet()
    {
        var d = D(Build.Assess(hopKnown: false, internet: Build.Steady(40, 30, lost: 4)));
        Assert.Equal(DiagnosisCode.ProviderIssue, d.Code);
    }

    [Test]
    public void Router_that_ignores_ping_gives_neutral_diagnosis()
    {
        var d = D(Build.Assess(router: Build.AllLost(), routerMeasurable: false, hopKnown: false,
            internet: Build.Steady(40, 30, lost: 4)));
        Assert.Equal(DiagnosisCode.Degraded, d.Code);
        Assert.False(d.Chain[(int)Segment.Router].Measured);
    }

    [Test]
    public void Heavy_local_usage_is_detected()
    {
        var d = D(Build.Assess(heavy: true, internet: Build.Steady(180, 30, wobble: 40)));
        Assert.Equal(DiagnosisCode.DeviceBusy, d.Code);
        Assert.Equal(Segment.Device, d.Culprit);
        Assert.Contains("OneDrive", d.Text.Summary);
    }

    [Test]
    public void No_internet_when_router_ok()
    {
        var d = D(Build.Assess(provider: Build.AllLost(), internet: Build.AllLost(), cloud: Build.AllLost(6), cloudFailures: 6));
        Assert.Equal(DiagnosisCode.NoInternet, d.Code);
        Assert.Equal(Health.Down, d.Severity);
    }

    [Test]
    public void Router_unreachable_when_router_stops_answering()
    {
        var d = D(Build.Assess(router: Build.AllLost(), internet: Build.AllLost(), cloud: Build.AllLost(6), cloudFailures: 6));
        Assert.Equal(DiagnosisCode.RouterUnreachable, d.Code);
        Assert.Equal(Segment.Router, d.Culprit);
    }

    [Test]
    public void Icmp_blocked_networks_use_tcp_measurements()
    {
        // Redes corporativas que bloquean el ping hacia internet: no debe decir "sin internet".
        var d = D(Build.Assess(internet: Build.AllLost(), provider: Build.AllLost(), cloud: Build.Steady(35, 10)));
        Assert.True(d.IcmpBlocked);
        Assert.Equal(DiagnosisCode.AllGood, d.Code);
    }

    [Test]
    public void Microsoft365_unreachable_while_internet_ok()
    {
        var d = D(Build.Assess(cloudFailures: 3));
        Assert.Equal(DiagnosisCode.CloudUnreachable, d.Code);
        Assert.Equal(Health.Fair, d.Severity);
    }

    [Test]
    public void Captive_portal_has_priority()
    {
        var d = D(Build.Assess(captive: true, internet: Build.AllLost(), cloud: Build.AllLost(6), cloudFailures: 6));
        Assert.Equal(DiagnosisCode.CaptivePortal, d.Code);
    }

    [Test]
    public void Disconnected()
    {
        var d = D(Build.Assess(ctx: NetworkContext.None));
        Assert.Equal(DiagnosisCode.NoConnection, d.Code);
        Assert.Equal(Health.Down, d.Severity);
    }

    [Test]
    public void Good_but_weak_wifi_is_informative_only()
    {
        var d = D(Build.Assess(ctx: Build.Wifi(signal: 25)));
        Assert.Equal(DiagnosisCode.GoodButWeakLink, d.Code);
        Assert.Equal(Health.Good, d.Severity);
    }

    [Test]
    public void Too_few_samples_means_checking()
    {
        var d = D(Build.Assess(internet: Build.Stats(20, 21)));
        Assert.Equal(DiagnosisCode.Checking, d.Code);
    }

    [Test]
    public void Vpn_adds_a_hint_when_there_are_problems()
    {
        var d = D(Build.Assess(ctx: Build.Wifi(vpn: true), internet: Build.Steady(40, 30, lost: 4)));
        Assert.Contains("VPN", string.Join(" ", d.Text.Steps));
        var ok = D(Build.Assess(ctx: Build.Wifi(vpn: true)));
        Assert.False(string.Join(" ", ok.Text.Steps).Contains("VPN"));
    }

    [Test]
    public void High_jitter_alone_degrades_call_quality()
    {
        var d = D(Build.Assess(internet: Build.Steady(40, 30, wobble: 35)));
        Assert.True(d.Severity >= Health.Fair, $"severidad {d.Severity}");
    }

    [Test]
    public void Messages_are_neutral_about_home_or_office()
    {
        foreach (var code in Enum.GetValues<DiagnosisCode>())
        {
            var t = Messages.Build(code, Build.Assess());
            var all = t.Title + " " + t.Summary + " " + string.Join(" ", t.Steps);
            Assert.False(all.Contains("tu router", StringComparison.OrdinalIgnoreCase) && !all.Contains("Si tienes acceso"),
                $"{code}: no debe asumir que el router es del usuario");
            Assert.True(t.TrayText.Length <= 60, $"{code}: texto de bandeja muy largo");
        }
    }

    [Test]
    public void Chain_labels_fit_under_each_node()
    {
        // En el panel cada eslabón mide ~75 px: etiquetas largas salían cortadas ("Internet y…").
        var d = D(Build.Assess());
        foreach (var node in d.Chain)
            Assert.True(node.Label.Length <= 10, $"etiqueta muy larga: {node.Label}");
        Assert.Equal("Internet", d.Chain[(int)Segment.Internet].Label);
    }
}
