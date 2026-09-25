using System.Globalization;
using InternetHealth.Core.Model;

namespace InternetHealth.Core.Diagnosis;

/// <summary>
/// Textos para el usuario. Reglas: español claro, segunda persona, frases cortas, sin jerga,
/// sin culpar, y neutros respecto a si la persona está en casa o en una sede de la empresa.
/// </summary>
public static class Messages
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Nombre del equipo de soporte, configurable (p. ej. "Soporte TI de Ardisa").</summary>
    public static string SupportName { get; set; } = "soporte TI";

    private static string ShareStep =>
        $"Si el problema continúa, usa «Compartir diagnóstico» y envíaselo a {SupportName}.";

    private const string RouterRestartStep =
        "Si tienes acceso al router o módem (por ejemplo, en casa), reinícialo: desconéctalo de la corriente 15 segundos, vuelve a conectarlo y espera 2 a 3 minutos.";

    private static string NoRouterAccessStep =>
        $"Si no tienes acceso al router (por ejemplo, en una sede de la empresa), avisa a {SupportName} usando «Compartir diagnóstico».";

    public static DiagnosisText Build(DiagnosisCode code, Assessment? a)
    {
        var ctx = a?.Context;
        var wifi = ctx?.Wifi;
        bool isWifi = ctx?.LinkType == LinkType.WiFi;
        string signal = wifi?.SignalQuality is int q ? $" ({q} %)" : "";

        DiagnosisText text = code switch
        {
            DiagnosisCode.Checking => new(
                "Verificando tu conexión…",
                "Estamos tomando las primeras mediciones. Esto toma unos segundos.",
                [],
                "Verificando la conexión…"),

            DiagnosisCode.AllGood => new(
                "Todo bien para tus reuniones",
                "Tu conexión está estable. Las videollamadas deberían funcionar sin problemas.",
                [],
                "Conexión estable"),

            DiagnosisCode.GoodButWeakLink when isWifi => new(
                "Funciona bien, pero tu señal Wi-Fi es débil",
                $"Por ahora todo funciona, pero la señal Wi-Fi que llega a tu equipo es baja{signal}. Si se debilita un poco más podrías tener cortes.",
                [
                    "Si puedes, acércate al punto de acceso Wi-Fi o conéctate por cable antes de una reunión importante.",
                ],
                "Estable, pero con señal Wi-Fi débil"),

            DiagnosisCode.GoodButWeakLink => new(
                "Funciona bien, pero el cable conecta a baja velocidad",
                "Por ahora todo funciona, pero el cable de red está conectando a una velocidad muy baja. Suele indicar un cable o conector dañado.",
                [
                    "Revisa que el cable esté bien conectado en ambos extremos.",
                    "Si puedes, prueba con otro cable o en otro puerto de red.",
                ],
                "Estable, pero el cable conecta lento"),

            DiagnosisCode.CloudUnreachable => new(
                "Microsoft 365 no responde desde esta red",
                "Tienes internet, pero no logramos conectarnos con Microsoft 365 (Teams, Outlook). Puede ser una falla temporal del servicio o un bloqueo en esta red.",
                [
                    "Espera unos minutos y vuelve a intentarlo.",
                    "Prueba abrir teams.microsoft.com en el navegador.",
                    ShareStep,
                ],
                "Microsoft 365 no responde"),

            DiagnosisCode.DnsIssue => new(
                "Hay problemas para encontrar sitios web",
                "Tu conexión funciona, pero el servicio que traduce los nombres de los sitios (DNS) está fallando. Algunas páginas o aplicaciones podrían no abrir.",
                [
                    "Desconéctate de la red y vuelve a conectarte.",
                    RouterRestartStep,
                    NoRouterAccessStep,
                ],
                "Problemas de DNS"),

            DiagnosisCode.WeakWifi => new(
                "Tu señal Wi-Fi está causando problemas",
                $"Tu equipo muestra conexión Wi-Fi, pero la señal que llega es débil{signal} y se están perdiendo datos. Por eso las llamadas se cortan o se congelan, aunque veas las rayitas del Wi-Fi.",
                WeakWifiSteps(wifi),
                "Problemas por señal Wi-Fi débil"),

            DiagnosisCode.CableIssue => new(
                "Tu conexión por cable tiene problemas",
                "Se están perdiendo datos entre tu equipo y la red por el cable. Suele ser un cable o conector dañado, o un puerto defectuoso.",
                [
                    "Revisa que el cable esté bien conectado en ambos extremos (debe hacer «clic»).",
                    "Prueba con otro cable o en otro puerto de red.",
                    ShareStep,
                ],
                "Problemas en la conexión por cable"),

            DiagnosisCode.LocalNetwork => new(
                "Hay problemas entre tu equipo y el router",
                "La comunicación entre tu equipo y el router de esta red es inestable. El problema está en la red local, no en el proveedor de internet.",
                isWifi
                    ?
                    [
                        "Acércate al punto de acceso Wi-Fi o, si puedes, conéctate por cable.",
                        "Desconéctate de la red Wi-Fi y vuelve a conectarte.",
                        RouterRestartStep,
                        NoRouterAccessStep,
                    ]
                    :
                    [
                        "Revisa que el cable de red esté bien conectado.",
                        RouterRestartStep,
                        NoRouterAccessStep,
                    ],
                "Problemas en la red local"),

            DiagnosisCode.RouterUnreachable => new(
                "Tu equipo no logra comunicarse con el router",
                "Tu equipo está conectado a la red, pero el router no responde. Sin esa comunicación no hay internet.",
                [
                    isWifi ? "Desconéctate de la red Wi-Fi y vuelve a conectarte." : "Desconecta el cable de red y vuelve a conectarlo.",
                    "Si otros equipos o celulares en el mismo lugar tampoco tienen internet, el problema es del router.",
                    RouterRestartStep,
                    NoRouterAccessStep,
                ],
                "El router no responde"),

            DiagnosisCode.DeviceBusy => new(
                "Tu equipo está usando mucho internet",
                a is null
                    ? "Tu equipo está enviando o recibiendo muchos datos y eso está saturando la conexión."
                    : $"Tu equipo está enviando o recibiendo muchos datos (subida {a.Throughput.TxMbps.ToString("0.#", Es)} Mbps, bajada {a.Throughput.RxMbps.ToString("0.#", Es)} Mbps) y eso está saturando la conexión. Suele pasar con OneDrive, descargas o actualizaciones.",
                [
                    "Pausa la sincronización de OneDrive: clic en el ícono de la nube junto al reloj → engranaje → «Pausar sincronización».",
                    "Pausa descargas y actualizaciones en curso.",
                    "Cierra videos o transmisiones que tengas abiertos en otras pestañas.",
                ],
                "Tu equipo está saturando la conexión"),

            DiagnosisCode.ProviderIssue => new(
                "La salida a internet de esta red está inestable",
                "Tu equipo y su conexión con el router funcionan bien. El problema está entre el router y el proveedor de internet.",
                [
                    RouterRestartStep,
                    "Si estás en casa y el problema continúa más de 10 minutos, llama a tu proveedor de internet. Usa «Compartir diagnóstico» para tener la evidencia a mano.",
                    $"Si estás en una sede de la empresa, avisa a {SupportName} usando «Compartir diagnóstico».",
                ],
                "Problemas en la salida a internet"),

            DiagnosisCode.ExternalIssue => new(
                "El problema no está en tu equipo ni en tu red",
                "Tu equipo, tu conexión y la red local funcionan bien. La lentitud viene de más allá: del proveedor de internet o del servicio en línea.",
                [
                    "Espera unos minutos; estos problemas suelen ser temporales.",
                    "Si varias personas tienen el mismo problema, es probable que sea del proveedor o del servicio.",
                    ShareStep,
                ],
                "Problema externo (no es tu equipo)"),

            DiagnosisCode.Degraded => new(
                "Tu conexión a internet está inestable",
                "Detectamos pérdida de datos o demoras hacia internet. No podemos precisar el punto exacto porque el router de esta red no responde a nuestras pruebas.",
                [
                    isWifi ? "Si estás en Wi-Fi, acércate al punto de acceso o conéctate por cable." : "Revisa que el cable de red esté bien conectado.",
                    RouterRestartStep,
                    ShareStep,
                ],
                "Conexión inestable"),

            DiagnosisCode.NoInternet => new(
                "Sin salida a internet",
                "Tu equipo está conectado a la red, pero la red no tiene salida a internet en este momento.",
                [
                    "Revisa si otros equipos o celulares en el mismo lugar tienen internet.",
                    RouterRestartStep,
                    "Si estás en casa y sigue sin internet, llama a tu proveedor de internet.",
                    $"Si estás en una sede de la empresa, avisa a {SupportName}.",
                ],
                "Sin internet"),

            DiagnosisCode.NoConnection => new(
                "Tu equipo no está conectado a ninguna red",
                "No hay conexión Wi-Fi ni por cable activa.",
                [
                    "Revisa que el Wi-Fi esté activado y que no esté puesto el modo avión.",
                    "Conéctate a tu red Wi-Fi o conecta el cable de red.",
                ],
                "Sin conexión a una red"),

            DiagnosisCode.CaptivePortal => new(
                "Esta red pide iniciar sesión",
                "Estás conectado a una red que requiere aceptar condiciones o iniciar sesión antes de dar internet (común en hoteles, aeropuertos y redes de invitados).",
                [
                    "Abre el navegador: debería mostrarse la página de inicio de sesión de la red.",
                    "Si no aparece, usa el botón «Abrir página de inicio de sesión».",
                ],
                "La red pide iniciar sesión"),

            _ => new("Estado desconocido", "", [], "Monitor de conexión"),
        };

        if (a is not null && ctx is not null && ctx.VpnActive && code is not (DiagnosisCode.AllGood or DiagnosisCode.Checking
                or DiagnosisCode.NoConnection or DiagnosisCode.CaptivePortal))
        {
            var steps = text.Steps.ToList();
            steps.Insert(Math.Max(0, steps.Count - 1),
                "Estás conectado a una VPN. Si no la necesitas en este momento, desconéctala y revisa si mejora.");
            text = text with { Steps = steps };
        }

        return text;
    }

    private static List<string> WeakWifiSteps(Network.WifiInfo? wifi)
    {
        var steps = new List<string>
        {
            "Acércate al punto de acceso Wi-Fi o quita obstáculos entre ambos (paredes, muebles metálicos, electrodomésticos).",
            "Si puedes, conéctate por cable de red: es la opción más estable para reuniones.",
        };
        if (wifi?.Band == "2.4 GHz")
            steps.Add("Si hay una red de 5 GHz disponible (su nombre suele terminar en «5G»), conéctate a ella.");
        steps.Add("Desconéctate de la red Wi-Fi y vuelve a conectarte.");
        steps.Add(ShareStep);
        return steps;
    }
}
