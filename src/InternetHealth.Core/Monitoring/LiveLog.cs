namespace InternetHealth.Core.Monitoring;

public readonly record struct LogEntry(DateTimeOffset Time, string Message)
{
    public override string ToString() => $"[{Time:HH:mm:ss}] {Message}";
}

/// <summary>Registro en memoria de tamaño fijo para la pestaña "Avanzado".</summary>
public sealed class LiveLog
{
    private readonly LogEntry[] _items;
    private readonly object _gate = new();
    private int _head, _count;

    public LiveLog(int capacity = 400) => _items = new LogEntry[capacity];

    public event Action<LogEntry>? Added;

    public void Add(DateTimeOffset time, string message)
    {
        var entry = new LogEntry(time, message);
        lock (_gate)
        {
            _items[_head] = entry;
            _head = (_head + 1) % _items.Length;
            if (_count < _items.Length) _count++;
        }
        Added?.Invoke(entry);
    }

    public LogEntry[] Snapshot()
    {
        lock (_gate)
        {
            var result = new LogEntry[_count];
            for (int i = 0; i < _count; i++)
                result[i] = _items[(_head - _count + i + _items.Length) % _items.Length];
            return result;
        }
    }

    public void Clear()
    {
        lock (_gate) { _head = 0; _count = 0; }
    }
}
