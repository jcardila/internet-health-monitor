using System.Globalization;
using Avalonia;
using InternetHealth.Mac.Services;
using Velopack;

namespace InternetHealth.Mac;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool firstRun = false;

        // Debe ejecutarse antes que cualquier otra cosa: maneja instalación y actualizaciones.
        VelopackApp.Build()
            .OnFirstRun(_ => firstRun = true)
            .Run();

        using var instance = SingleInstance.TryAcquire();
        if (instance is null) return 0; // ya estaba abierta: se le pidió mostrarse

        // Textos y formatos en español de Colombia aunque el Mac esté en otro idioma.
        var culture = CultureInfo.GetCultureInfo("es-CO");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        return BuildAvaloniaApp(() => new App(instance, args, firstRun))
            .StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
    }

    private static AppBuilder BuildAvaloniaApp(Func<App> factory) =>
        AppBuilder.Configure(factory)
            .UsePlatformDetect()
            // Vive en la barra de menús: sin ícono en el Dock (como en Windows, sin botón en la barra de tareas).
            .With(new MacOSPlatformOptions { ShowInDock = false })
            // Dibujo por CPU: para una interfaz tan simple no se nota, y al cerrar la ventana no quedan
            // ~35 MB de memoria gráfica (Metal) retenidos en un proceso que vive todo el día.
            .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] })
            .LogToTrace();

    /// <summary>Usado por el diseñador de Avalonia.</summary>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(() => new App());
}
