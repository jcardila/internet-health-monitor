namespace InternetHealth.Core.Stats;

/// <summary>
/// Estadísticas de un tramo calculadas sobre una ventana reciente de muestras.
/// Las latencias están en milisegundos. Null significa "sin datos suficientes".
/// </summary>
public readonly record struct LinkStats(
    int Sent,
    int Received,
    double LossPct,
    double? MeanMs,
    double? MedianMs,
    double? P95Ms,
    double? MinMs,
    double? MaxMs,
    double? JitterMs,
    double? LastMs,
    int TrailingLosses)
{
    public static readonly LinkStats Empty = new(0, 0, 0, null, null, null, null, null, null, null, 0);

    public bool HasData => Sent > 0;
    public int Lost => Sent - Received;
    public bool AllLost => Sent > 0 && Received == 0;
}

/// <summary>
/// Buffer circular de tamaño fijo con marcas de tiempo. Sin asignaciones por muestra.
/// Thread-safe: el motor escribe y la interfaz puede leer en paralelo.
/// </summary>
public sealed class SampleBuffer
{
    private readonly long[] _ticks;
    private readonly double[] _values; // NaN = muestra perdida
    private readonly double[] _scratch;
    private readonly object _gate = new();
    private int _head; // próxima posición de escritura
    private int _count;

    public SampleBuffer(int capacity = 120)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        _ticks = new long[capacity];
        _values = new double[capacity];
        _scratch = new double[capacity];
    }

    public int Capacity => _values.Length;

    public int Count
    {
        get { lock (_gate) return _count; }
    }

    public void Add(DateTimeOffset time, double? rttMs)
    {
        lock (_gate)
        {
            _ticks[_head] = time.UtcTicks;
            _values[_head] = rttMs ?? double.NaN;
            _head = (_head + 1) % _values.Length;
            if (_count < _values.Length) _count++;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _head = 0;
            _count = 0;
        }
    }

    /// <summary>
    /// Calcula estadísticas con las últimas <paramref name="maxCount"/> muestras que no sean más
    /// antiguas que <paramref name="maxAge"/>.
    /// </summary>
    public LinkStats Compute(DateTimeOffset now, TimeSpan maxAge, int maxCount)
    {
        lock (_gate)
        {
            if (_count == 0) return LinkStats.Empty;
            long minTicks = now.UtcTicks - maxAge.Ticks;
            int take = Math.Min(maxCount, _count);

            // Recorremos de la más reciente a la más antigua.
            int sent = 0, received = 0, trailing = 0;
            bool trailingOpen = true;
            double sum = 0, min = double.MaxValue, max = double.MinValue;
            double? last = null;
            double jitterSum = 0;
            int jitterN = 0;
            double prev = double.NaN;

            for (int i = 0; i < take; i++)
            {
                int idx = (_head - 1 - i + _values.Length) % _values.Length;
                if (_ticks[idx] < minTicks) break;
                double v = _values[idx];
                sent++;
                if (double.IsNaN(v))
                {
                    if (trailingOpen) trailing++;
                    continue;
                }

                trailingOpen = false;
                last ??= v;
                _scratch[received++] = v;
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
                if (!double.IsNaN(prev))
                {
                    jitterSum += Math.Abs(prev - v);
                    jitterN++;
                }
                prev = v;
            }

            if (sent == 0) return LinkStats.Empty;
            double lossPct = 100.0 * (sent - received) / sent;
            if (received == 0)
                return new LinkStats(sent, 0, lossPct, null, null, null, null, null, null, null, trailing);

            var span = _scratch.AsSpan(0, received);
            span.Sort();
            double median = Percentile(span, 0.5);
            double p95 = Percentile(span, 0.95);
            double? jitter = jitterN > 0 ? jitterSum / jitterN : null;
            return new LinkStats(sent, received, lossPct, sum / received, median, p95, min, max, jitter, last, trailing);
        }
    }

    /// <summary>Copia las últimas muestras en orden cronológico (null = perdida). Para gráficas.</summary>
    public double?[] Snapshot(int maxCount)
    {
        lock (_gate)
        {
            int take = Math.Min(maxCount, _count);
            var result = new double?[take];
            for (int i = 0; i < take; i++)
            {
                int idx = (_head - take + i + _values.Length) % _values.Length;
                double v = _values[idx];
                result[i] = double.IsNaN(v) ? null : v;
            }
            return result;
        }
    }

    private static double Percentile(ReadOnlySpan<double> sorted, double p)
    {
        if (sorted.Length == 1) return sorted[0];
        double pos = p * (sorted.Length - 1);
        int lo = (int)Math.Floor(pos);
        int hi = (int)Math.Ceiling(pos);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
