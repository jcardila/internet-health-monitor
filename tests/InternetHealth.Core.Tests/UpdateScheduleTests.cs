using InternetHealth.Core.Settings;

namespace InternetHealth.Core.Tests;

public class UpdateScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 8, 0, 0, TimeSpan.FromHours(-5));

    private static IEnumerable<Random> Randoms() => Enumerable.Range(0, 50).Select(i => new Random(i));

    [Test]
    public void Startup_check_is_spread_between_2_and_20_minutes()
    {
        // 400 equipos que se encienden a las 8 a. m. no deben consultar en el mismo minuto.
        var delays = Randoms().Select(r => UpdateSchedule.NextDelay(Now, null, 0, atStartup: true, r)).ToList();
        Assert.True(delays.All(d => d >= TimeSpan.FromMinutes(2) && d <= TimeSpan.FromMinutes(20)), "fuera de 2–20 min");
        Assert.True(delays.Distinct().Count() > 40, "los tiempos deben variar entre equipos");
    }

    [Test]
    public void Restart_after_successful_check_waits_until_next_day()
    {
        foreach (var r in Randoms())
        {
            var d = UpdateSchedule.NextDelay(Now, Now.AddHours(-1), 0, atStartup: true, r);
            Assert.True(d >= TimeSpan.FromHours(23) && d <= TimeSpan.FromHours(24), $"espera {d}");
        }
    }

    [Test]
    public void Success_more_than_a_day_ago_checks_soon_after_startup()
    {
        var d = UpdateSchedule.NextDelay(Now, Now.AddDays(-3), 0, atStartup: true, new Random(1));
        Assert.True(d <= TimeSpan.FromMinutes(20), $"espera {d}");
    }

    [Test]
    public void Failures_back_off_up_to_8_hours()
    {
        foreach (var r in Randoms())
        {
            var first = UpdateSchedule.NextDelay(Now, null, 1, atStartup: false, r);
            Assert.True(first >= TimeSpan.FromMinutes(48) && first <= TimeSpan.FromMinutes(72), $"1er reintento {first}");
            var many = UpdateSchedule.NextDelay(Now, null, 10, atStartup: false, r);
            Assert.True(many >= TimeSpan.FromHours(6.4) && many <= TimeSpan.FromHours(9.6), $"reintento largo {many}");
        }
    }

    [Test]
    public void Clock_going_back_never_blocks_updates()
    {
        var d = UpdateSchedule.NextDelay(Now, Now.AddDays(5), 0, atStartup: false, new Random(1));
        Assert.Equal(UpdateSchedule.StartupMin, d);
    }
}
