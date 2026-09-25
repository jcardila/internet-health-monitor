namespace InternetHealth.Core.Model;

/// <summary>Salud de un tramo o del conjunto. El orden numérico es el orden de gravedad.</summary>
public enum Health
{
    Unknown = 0,
    Good = 1,
    Fair = 2,
    Poor = 3,
    Down = 4,
}

/// <summary>Eslabones de la cadena de conexión, en el orden en que viaja el tráfico.</summary>
public enum Segment
{
    Device = 0,
    Link = 1,      // Wi-Fi o cable
    Router = 2,    // puerta de enlace de la red local
    Provider = 3,  // primer salto público (proveedor de internet)
    Internet = 4,  // internet y servicios en la nube (Microsoft 365)
}

public enum LinkType
{
    Unknown = 0,
    WiFi,
    Ethernet,
    Cellular,
    Other,
}

public enum MonitorMode
{
    Relaxed,
    Active,
    Paused,
}

public enum DiagnosisCode
{
    Checking,
    AllGood,
    GoodButWeakLink,
    CloudUnreachable,
    DnsIssue,
    WeakWifi,
    CableIssue,
    LocalNetwork,
    RouterUnreachable,
    DeviceBusy,
    ProviderIssue,
    ExternalIssue,
    Degraded,
    NoInternet,
    NoConnection,
    CaptivePortal,
}

public static class HealthExtensions
{
    public static Health Worst(this Health a, Health b) => (Health)Math.Max((int)a, (int)b);

    public static bool IsProblem(this Health h) => h >= Health.Fair;

    /// <summary>Etiqueta corta para mostrar al usuario.</summary>
    public static string ToLabel(this Health h) => h switch
    {
        Health.Good => "Buena",
        Health.Fair => "Inestable",
        Health.Poor => "Con problemas",
        Health.Down => "Sin conexión",
        _ => "Verificando",
    };
}
