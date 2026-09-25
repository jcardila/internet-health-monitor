using InternetHealth.Core.Model;

namespace InternetHealth.Core.Diagnosis;

/// <summary>
/// Histéresis: convierte diagnósticos "crudos" (que cambian en cada medición) en un estado
/// estable. Empeorar exige que la condición se mantenga unos segundos; mejorar exige más tiempo,
/// para no mostrar "todo bien" en medio de cortes intermitentes.
/// </summary>
public sealed class StatusTracker
{
    public static readonly TimeSpan WorsenDelay = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan DownDelay = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan ImproveDelay = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan CodeChangeDelay = TimeSpan.FromSeconds(10);

    private int _direction; // +1 empeorando, -1 mejorando, 0 igual con otro código
    private DateTimeOffset _since;
    private DiagnosisCode? _pendingCode;

    public Diagnosis Stable { get; private set; } = Diagnosis.Checking;
    public DateTimeOffset StableSince { get; private set; }

    public void Reset(DateTimeOffset now)
    {
        Stable = Diagnosis.Checking;
        StableSince = now;
        _direction = 0;
        _pendingCode = null;
    }

    /// <returns>true si cambió el estado estable (gravedad o causa).</returns>
    public bool Update(Diagnosis raw, DateTimeOffset now)
    {
        // Mientras verificamos, aceptamos el primer diagnóstico real de inmediato.
        if (Stable.Code == DiagnosisCode.Checking)
        {
            if (raw.Code == DiagnosisCode.Checking) { Stable = raw; return false; }
            return Promote(raw, now);
        }

        if (raw.Code == DiagnosisCode.Checking)
        {
            // Datos insuficientes (p. ej. tras un reinicio de buffers): mantenemos lo que había.
            ClearPending();
            return false;
        }

        if (raw.SameAs(Stable))
        {
            Stable = raw; // refresca métricas y textos
            ClearPending();
            return false;
        }

        int direction = Math.Sign((int)raw.Severity - (int)Stable.Severity);
        if (direction != _direction || (direction == 0 && _pendingCode != raw.Code) || _pendingCode is null)
        {
            _direction = direction;
            _pendingCode = raw.Code;
            _since = now;
        }
        else
        {
            _pendingCode = raw.Code;
        }

        TimeSpan required = direction switch
        {
            > 0 => raw.Severity == Health.Down ? DownDelay : WorsenDelay,
            < 0 => ImproveDelay,
            _ => CodeChangeDelay,
        };

        return now - _since >= required && Promote(raw, now);
    }

    private bool Promote(Diagnosis raw, DateTimeOffset now)
    {
        bool changed = !raw.SameAs(Stable);
        Stable = raw;
        if (changed) StableSince = now;
        ClearPending();
        return changed;
    }

    private void ClearPending()
    {
        _pendingCode = null;
        _direction = 0;
    }
}
