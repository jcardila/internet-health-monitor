using Avalonia.Threading;
using InternetHealth.Mac.Interop;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Pantalla bloqueada y suspensión del Mac. Consulta cada 5 s el estado de la sesión (una llamada
/// muy barata) y detecta que el equipo despertó porque el reloj avanzó mucho más que el intervalo.
/// </summary>
internal sealed class SessionMonitor : IDisposable
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
    private DateTimeOffset _lastTick = DateTimeOffset.UtcNow;
    private bool _locked;

    public SessionMonitor()
    {
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public event Action? Locked;
    public event Action? Unlocked;
    public event Action? Woke;

    private void Tick()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastTick > TimeSpan.FromSeconds(30)) Woke?.Invoke();
        _lastTick = now;

        bool locked;
        try { locked = MacNative.IsScreenLocked() == true; }
        catch { return; }
        if (locked == _locked) return;
        _locked = locked;
        if (locked) Locked?.Invoke();
        else Unlocked?.Invoke();
    }

    public void Dispose() => _timer.Stop();
}
