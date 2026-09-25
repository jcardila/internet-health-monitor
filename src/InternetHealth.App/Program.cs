using System.Globalization;
using InternetHealth.App.Services;
using Velopack;

namespace InternetHealth.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool firstRun = false;

        // Debe ejecutarse antes que cualquier otra cosa: maneja instalación, actualización y desinstalación.
        VelopackApp.Build()
            .OnFirstRun(_ => firstRun = true)
            .OnBeforeUninstallFastCallback(_ => StartupManager.Set(false))
            .Run();

        using var instance = SingleInstance.TryAcquire();
        if (instance is null) return 0; // ya estaba abierta: se le pidió mostrarse

        // Textos y formatos en español de Colombia aunque Windows esté en otro idioma.
        var culture = CultureInfo.GetCultureInfo("es-CO");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var app = new App();
        app.InitializeComponent();
        // Tema Fluent de Windows 11 para los controles estándar (se asigna en código: asignarlo en XAML
        // junto con Application.Resources puede descartar el diccionario Fluent).
        try { app.ThemeMode = System.Windows.ThemeMode.System; } catch { /* Windows 10 antiguo: tema clásico */ }
        var host = new AppHost(app, instance, args, firstRun);
        app.Startup += (_, _) => host.Start();
        app.Exit += (_, _) => host.Shutdown();
        return app.Run();
    }
}
