using InternetHealth.Core.Stats;

namespace InternetHealth.Core.Tests;

public class StatsTests
{
    [Test]
    public void Loss_mean_median_and_jitter_are_computed()
    {
        var s = Build.Stats(10, 20, null, 30, 10);
        Assert.Equal(5, s.Sent);
        Assert.Equal(4, s.Received);
        Assert.Near(20, s.LossPct, 0.001);
        Assert.Near(17.5, s.MeanMs, 0.001);
        Assert.Near(15, s.MedianMs, 0.001);
        // Diferencias consecutivas entre respuestas: |10-30|, |30-20|, |20-10| -> promedio 13.33
        Assert.Near(13.333, s.JitterMs, 0.01);
        Assert.Equal(0, s.TrailingLosses);
    }

    [Test]
    public void Trailing_losses_count_only_the_most_recent_run()
    {
        var s = Build.Stats(10, null, 12, null, null);
        Assert.Equal(2, s.TrailingLosses);
        Assert.Near(12, s.LastMs, 0.001);
    }

    [Test]
    public void Window_respects_age_and_count()
    {
        var b = new SampleBuffer(10);
        for (int i = 0; i < 25; i++) b.Add(Build.T0.AddSeconds(i), i);
        var s = b.Compute(Build.T0.AddSeconds(25), TimeSpan.FromSeconds(5), 100);
        Assert.Equal(5, s.Sent); // capacidad 10, pero solo 5 s de antigüedad
        var s2 = b.Compute(Build.T0.AddSeconds(25), TimeSpan.FromHours(1), 3);
        Assert.Equal(3, s2.Sent);
        Assert.Near(23, s2.MeanMs, 0.001);
    }

    [Test]
    public void Snapshot_is_chronological()
    {
        var b = new SampleBuffer(4);
        for (int i = 1; i <= 6; i++) b.Add(Build.T0.AddSeconds(i), i == 5 ? null : i);
        var snap = b.Snapshot(10);
        Assert.Equal(4, snap.Length);
        Assert.Equal(3.0, snap[0]!.Value);
        Assert.Null(snap[2]);
        Assert.Equal(6.0, snap[3]!.Value);
    }

    [Test]
    public void All_lost_has_no_latency()
    {
        var s = Build.AllLost(10);
        Assert.True(s.AllLost);
        Assert.Null(s.MeanMs);
        Assert.Near(100, s.LossPct, 0.001);
    }

    [Test]
    public void Mos_is_high_for_a_clean_connection_and_low_for_a_bad_one()
    {
        var good = CallQuality.Mos(CallQuality.RFactor(30, 3, 0));
        var fair = CallQuality.Mos(CallQuality.RFactor(250, 40, 1));
        var bad = CallQuality.Mos(CallQuality.RFactor(150, 25, 15));
        Assert.True(good >= 4.3, $"MOS bueno fue {good}");
        Assert.True(fair is > 3.3 and < 4.0, $"MOS regular fue {fair}");
        Assert.True(bad < 3.0, $"MOS malo fue {bad}");
        Assert.Equal("Excelente", CallQuality.Label(good));
        Assert.Equal("Mala", CallQuality.Label(bad));
    }
}
