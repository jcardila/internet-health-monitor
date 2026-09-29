using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using InternetHealth.Mac.Services;

namespace InternetHealth.Mac;

public partial class App : Application
{
    private readonly SingleInstance? _instance;
    private readonly string[] _args = [];
    private readonly bool _firstRun;
    private MacHost? _host;

    /// <summary>Usado por el diseñador de Avalonia.</summary>
    public App() { }

    internal App(SingleInstance instance, string[] args, bool firstRun)
    {
        _instance = instance;
        _args = args;
        _firstRun = firstRun;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && _instance is not null)
        {
            _host = new MacHost(this, desktop, _instance, _args, _firstRun);
            desktop.Exit += (_, _) => _host.Shutdown();
            _host.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
