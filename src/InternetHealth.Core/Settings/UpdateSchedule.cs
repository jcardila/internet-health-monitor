namespace InternetHealth.Core.Settings;

/// <summary>
/// Decide cuándo buscar actualizaciones. Con ~400 equipos, muchos detrás de la misma IP de una
/// sede, se evita que todos consulten a la vez:
/// - Al iniciar se espera un tiempo al azar (2–20 min): los equipos que se encienden a las 8 a. m.
///   no consultan en el mismo minuto.
/// - Tras una consulta exitosa, la siguiente es en ~24 h (con hasta 1 h al azar). La fecha se
///   guarda, así que reiniciar el equipo no provoca otra consulta.
/// - Si falla (sin internet, servidor caído), se reintenta con espera creciente: 1 h, 2 h, 4 h,
///   máximo 8 h, con ±20 % al azar.
/// </summary>
public static class UpdateSchedule
{
    public static readonly TimeSpan SuccessInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan StartupMin = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan StartupMax = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan FirstRetry = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxRetry = TimeSpan.FromHours(8);

    /// <param name="lastSuccess">Última consulta exitosa (guardada en las preferencias).</param>
    /// <param name="consecutiveFailures">Fallos seguidos desde que inició la app.</param>
    /// <param name="atStartup">true para la primera consulta después de iniciar la app.</param>
    public static TimeSpan NextDelay(DateTimeOffset now, DateTimeOffset? lastSuccess, int consecutiveFailures, bool atStartup, Random random)
    {
        TimeSpan delay;
        if (consecutiveFailures > 0)
        {
            double hours = Math.Min(FirstRetry.TotalHours * Math.Pow(2, consecutiveFailures - 1), MaxRetry.TotalHours);
            delay = TimeSpan.FromHours(hours * (0.8 + 0.4 * random.NextDouble()));
        }
        else if (lastSuccess is { } last && last <= now)
        {
            var due = last + SuccessInterval + TimeSpan.FromMinutes(60 * random.NextDouble());
            delay = due - now;
        }
        else
        {
            delay = TimeSpan.Zero; // nunca ha consultado (o el reloj retrocedió): consultar pronto
        }

        if (atStartup)
        {
            var startup = StartupMin + (StartupMax - StartupMin) * random.NextDouble();
            if (delay < startup) delay = startup;
        }
        return delay < StartupMin ? StartupMin : delay;
    }
}
