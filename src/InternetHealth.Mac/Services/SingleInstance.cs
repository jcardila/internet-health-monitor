using System.IO;
using System.IO.Pipes;
using InternetHealth.Core.Settings;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Garantiza una sola instancia por usuario. Abrir la app de nuevo muestra la ventana existente.
/// En Mac se usa un archivo bloqueado (flock) y un socket local para pedirle a la otra instancia que se muestre.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string PipeName = "InternetHealthMonitor.Show";

    private readonly FileStream _lock;
    private readonly CancellationTokenSource _cts = new();

    private SingleInstance(FileStream lockFile) => _lock = lockFile;

    public static SingleInstance? TryAcquire()
    {
        var dir = new AppPaths().DataRoot;
        Directory.CreateDirectory(dir);
        try
        {
            var fs = new FileStream(Path.Combine(dir, ".instancia.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new SingleInstance(fs);
        }
        catch (IOException)
        {
            // Ya hay una instancia: le pedimos que se muestre y salimos.
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1000);
                client.WriteByte(1);
            }
            catch { /* ignorar */ }
            return null;
        }
    }

    public void ListenForActivation(Action onActivate)
    {
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            // Si la app se cerró mal, el socket anterior puede seguir ahí: somos la única instancia, se puede borrar.
            try { File.Delete(Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + PipeName)); } catch { /* ignorar */ }
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                    onActivate();
                }
                catch (OperationCanceledException) { return; }
                catch { await Task.Delay(2000, ct).ConfigureAwait(false); }
            }
        }, ct);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _lock.Dispose();
    }
}
