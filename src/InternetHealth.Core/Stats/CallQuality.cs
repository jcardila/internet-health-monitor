namespace InternetHealth.Core.Stats;

/// <summary>
/// Estimación de calidad de voz/video a partir de latencia, jitter y pérdida
/// (modelo E simplificado, ITU-T G.107). Devuelve un MOS entre 1 y 5.
/// </summary>
public static class CallQuality
{
    public static double RFactor(double rttMs, double jitterMs, double lossPct)
    {
        double effective = rttMs + jitterMs * 2 + 10;
        double r = effective < 160
            ? 93.2 - effective / 40
            : 93.2 - (effective - 120) / 10;
        r -= lossPct * 2.5;
        return Math.Clamp(r, 0, 100);
    }

    public static double Mos(double rFactor)
    {
        if (rFactor <= 0) return 1;
        if (rFactor >= 100) return 4.5;
        double mos = 1 + 0.035 * rFactor + 7e-6 * rFactor * (rFactor - 60) * (100 - rFactor);
        return Math.Clamp(mos, 1, 5);
    }

    public static double? Mos(LinkStats stats)
    {
        if (!stats.HasData) return null;
        if (stats.AllLost) return 1;
        double rtt = stats.MeanMs ?? 0;
        double jitter = stats.JitterMs ?? 0;
        return Mos(RFactor(rtt, jitter, stats.LossPct));
    }

    public static string Label(double? mos) => mos switch
    {
        null => "Midiendo…",
        >= 4.2 => "Excelente",
        >= 3.9 => "Buena",
        >= 3.5 => "Aceptable",
        >= 3.0 => "Regular",
        _ => "Mala",
    };
}
