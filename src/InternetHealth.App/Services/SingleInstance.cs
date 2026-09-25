namespace InternetHealth.App.Services;

/// <summary>Garantiza una sola instancia por usuario. Abrir la app de nuevo muestra la ventana existente.</summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\InternetHealthMonitor.Instance";
    private const string EventName = @"Local\InternetHealthMonitor.Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent)
    {
        _mutex = mutex;
        _showEvent = showEvent;
    }

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
        var evt = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        if (created) return new SingleInstance(mutex, evt);

        // Ya hay una instancia: le pedimos que se muestre y salimos.
        try { evt.Set(); } catch { /* ignorar */ }
        evt.Dispose();
        mutex.Dispose();
        return null;
    }

    public void ListenForActivation(Action onActivate)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _showEvent.Dispose();
        try { _mutex.ReleaseMutex(); } catch { /* ignorar */ }
        _mutex.Dispose();
    }
}
