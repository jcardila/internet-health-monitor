using InternetHealth.Core.Diagnosis;
using InternetHealth.Core.Model;

namespace InternetHealth.Core.Tests;

public class TrackerTests
{
    private static readonly Diagnosis.Diagnosis Good = DiagnosisEngine.Diagnose(Build.Assess());
    private static readonly Diagnosis.Diagnosis Bad = DiagnosisEngine.Diagnose(
        Build.Assess(router: Build.Steady(10, 30, lost: 6, wobble: 5), internet: Build.Steady(40, 30, lost: 6)));

    [Test]
    public void First_real_diagnosis_is_accepted_immediately()
    {
        var t = new StatusTracker();
        t.Reset(Build.T0);
        Assert.True(t.Update(Good, Build.T0.AddSeconds(1)));
        Assert.Equal(DiagnosisCode.AllGood, t.Stable.Code);
    }

    [Test]
    public void Brief_glitch_does_not_change_state()
    {
        var t = new StatusTracker();
        t.Update(Good, Build.T0);
        Assert.False(t.Update(Bad, Build.T0.AddSeconds(1)));
        Assert.False(t.Update(Bad, Build.T0.AddSeconds(4)));
        Assert.False(t.Update(Good, Build.T0.AddSeconds(5)));
        Assert.Equal(DiagnosisCode.AllGood, t.Stable.Code);
    }

    [Test]
    public void Sustained_problem_changes_state_after_delay()
    {
        var t = new StatusTracker();
        t.Update(Good, Build.T0);
        for (int i = 1; i < 8; i++) Assert.False(t.Update(Bad, Build.T0.AddSeconds(i)));
        Assert.True(t.Update(Bad, Build.T0.AddSeconds(9)));
        Assert.Equal(Bad.Code, t.Stable.Code);
    }

    [Test]
    public void Recovery_requires_longer_stability()
    {
        var t = new StatusTracker();
        t.Update(Good, Build.T0);
        for (int i = 1; i <= 10; i++) t.Update(Bad, Build.T0.AddSeconds(i));
        Assert.Equal(Bad.Code, t.Stable.Code);
        for (int i = 11; i < 30; i++) Assert.False(t.Update(Good, Build.T0.AddSeconds(i)));
        Assert.True(t.Update(Good, Build.T0.AddSeconds(31)));
    }

    [Test]
    public void Notifies_once_for_persistent_problem_and_once_on_recovery()
    {
        var p = new NotificationPolicy();
        var since = Build.T0;
        Assert.Null(p.Evaluate(Bad, since, inCall: false, Build.T0.AddSeconds(20)));
        Assert.Equal(Health.Poor, Bad.Severity);
        Assert.NotNull(p.Evaluate(Bad, since, false, Build.T0.AddSeconds(50)));
        Assert.Null(p.Evaluate(Bad, since, false, Build.T0.AddSeconds(120)));
        var rec = p.Evaluate(Good, Build.T0.AddSeconds(200), false, Build.T0.AddSeconds(210));
        Assert.NotNull(rec);
        Assert.True(rec!.IsRecovery);
    }

    [Test]
    public void In_call_notifications_are_faster_and_include_fair_problems()
    {
        var fairWifi = DiagnosisEngine.Diagnose(Build.Assess(ctx: Build.Wifi(signal: 30),
            router: Build.Steady(8, 30, lost: 2, wobble: 3), internet: Build.Steady(40, 30, lost: 2)));
        Assert.Equal(Health.Fair, fairWifi.Severity);
        var p = new NotificationPolicy();
        Assert.Null(p.Evaluate(fairWifi, Build.T0, inCall: false, Build.T0.AddMinutes(5)));
        var n = p.Evaluate(fairWifi, Build.T0, inCall: true, Build.T0.AddSeconds(12));
        Assert.NotNull(n);
        Assert.Equal("Tu reunión puede verse afectada", n!.Title);
    }

    [Test]
    public void Do_not_disturb_mutes_notifications()
    {
        var p = new NotificationPolicy { DoNotDisturbUntil = Build.T0.AddHours(1) };
        var down = DiagnosisEngine.Diagnose(Build.Assess(provider: Build.AllLost(), internet: Build.AllLost(), cloud: Build.AllLost(6), cloudFailures: 6));
        Assert.Null(p.Evaluate(down, Build.T0, false, Build.T0.AddMinutes(5)));
        Assert.NotNull(p.Evaluate(down, Build.T0, false, Build.T0.AddMinutes(61)));
    }
}
