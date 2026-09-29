using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using InternetHealth.Core.Model;
using InternetHealth.Mac.Controls;
using InternetHealth.Mac.Interop;

namespace InternetHealth.Mac.Services;

/// <summary>
/// Ícono y menú en la barra de menús del Mac (el equivalente del ícono junto al reloj en Windows).
/// La primera línea del menú dice el estado en palabras, para que no dependa del color del ícono.
/// </summary>
internal sealed class MenuBarIcon : IDisposable
{
    private readonly bool _demo;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _status;
    private readonly NativeMenuItem _dnd;
    private readonly NativeMenuItem _startup;
    private readonly Dictionary<(Health, bool), WindowIcon> _icons = [];
    private readonly DispatcherTimer _appearanceTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
    private Health _health = Health.Unknown;
    private bool _dark;

    public MenuBarIcon(bool demo)
    {
        _demo = demo;
        _status = new NativeMenuItem(Prefix + "Verificando la conexión…") { IsEnabled = false };
        var open = new NativeMenuItem("Abrir panel");
        open.Click += (_, _) => OpenPanel?.Invoke();
        var details = new NativeMenuItem("Ver detalles e historial");
        details.Click += (_, _) => OpenDetails?.Invoke();
        var share = new NativeMenuItem("Compartir diagnóstico…");
        share.Click += (_, _) => Share?.Invoke();
        _dnd = new NativeMenuItem("No molestar por 1 hora");
        _dnd.Click += (_, _) => ToggleDoNotDisturb?.Invoke();
        _startup = new NativeMenuItem("Abrir al iniciar sesión") { ToggleType = NativeMenuItemToggleType.CheckBox };
        _startup.Click += (_, _) => ToggleStartup?.Invoke();
        var quit = new NativeMenuItem("Salir");
        quit.Click += (_, _) => Exit?.Invoke();

        var menu = new NativeMenu();
        foreach (var item in new NativeMenuItemBase[]
                 {
                     _status, new NativeMenuItemSeparator(), open, details, share, new NativeMenuItemSeparator(),
                     _dnd, _startup, new NativeMenuItemSeparator(), quit,
                 })
            menu.Add(item);

        _tray = new TrayIcon
        {
            Icon = IconFor(Health.Unknown),
            ToolTipText = TooltipPrefix + "verificando…",
            Menu = menu,
            IsVisible = true,
        };
        TrayIcon.SetIcons(Application.Current!, [_tray]);
        // El color de la barra cambia con el fondo de pantalla, sin avisar: se revisa cada 5 s (es muy barato).
        _appearanceTimer.Tick += (_, _) => RefreshIcons();
        _appearanceTimer.Start();
        Dispatcher.UIThread.Post(RefreshIcons, DispatcherPriority.Background);
    }

    public event Action? OpenPanel;
    public event Action? OpenDetails;
    public event Action? Share;
    public event Action? ToggleDoNotDisturb;
    public event Action? ToggleStartup;
    public event Action? Exit;

    private string Prefix => _demo ? "[Demo] " : "";
    private string TooltipPrefix => _demo ? "DEMO · Monitor de Conexión — " : "Monitor de Conexión — ";

    public void Update(Health health, string text)
    {
        _health = health;
        _tray.Icon = IconFor(health);
        _tray.ToolTipText = TooltipPrefix + text;
        _status.Header = $"{Prefix}{HealthToGlyphConverter.Glyph(health)}  {text}";
    }

    public void SetMenuState(bool dndActive, bool startupEnabled)
    {
        _dnd.Header = dndActive ? "Reactivar avisos" : "No molestar por 1 hora";
        _startup.IsChecked = startupEnabled;
    }

    /// <summary>La barra de menús cambió entre clara y oscura: el globo cambia de color.</summary>
    public void RefreshIcons()
    {
        bool dark = DarkMenuBar();
        if (dark == _dark) return;
        _dark = dark;
        _tray.Icon = IconFor(_health);
    }

    private static bool DarkMenuBar()
    {
        try { if (MenuBarAppearance.IsDark() is bool d) return d; } catch { /* usar el tema de la app */ }
        return Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
    }

    private WindowIcon IconFor(Health health)
    {
        if (!_icons.TryGetValue((health, _dark), out var icon))
            _icons[(health, _dark)] = icon = new WindowIcon(Render(health, _dark));
        return icon;
    }

    /// <summary>
    /// Globo monocromo del color del texto de la barra de menús y, en la esquina, la insignia de estado
    /// con su símbolo (✓ ! ✕ …), recortada del globo como en OneDrive o Teams. Se dibuja a 2× (Retina).
    /// </summary>
    internal static RenderTargetBitmap Render(Health health, bool darkMenuBar, int size = 36)
    {
        var bmp = new RenderTargetBitmap(new PixelSize(size, size), new Vector(192, 192));
        double s = size / 2.0; // unidades lógicas (96 dpi) → 2× píxeles
        using (var dc = bmp.CreateDrawingContext())
        {
            var globe = new SolidColorBrush(darkMenuBar ? Colors.White : Color.FromRgb(0x1A, 0x1A, 0x19));
            double cx = s * .44, cy = s * .46, r = s * .38;
            var pen = new Pen(globe, s * .075);
            // Recorte alrededor de la insignia: se dibuja el globo solo fuera de ese círculo.
            double bx = s * .73, by = s * .73, br = s * .27, gap = s * .07;
            var outside = new CombinedGeometry(GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(0, 0, s, s)),
                new EllipseGeometry(new Rect(bx - br - gap, by - br - gap, 2 * (br + gap), 2 * (br + gap))));
            using (dc.PushGeometryClip(outside))
            {
                dc.DrawEllipse(null, pen, new Point(cx, cy), r, r);
                dc.DrawEllipse(null, pen, new Point(cx, cy), r * .45, r);
                dc.DrawLine(pen, new Point(cx - r, cy), new Point(cx + r, cy));
            }

            var (fill, ink) = health switch
            {
                Health.Good => (Color.FromRgb(0x0C, 0xA3, 0x0C), Colors.White),
                Health.Fair => (Color.FromRgb(0xFA, 0xB2, 0x19), Color.FromRgb(0x1A, 0x1A, 0x19)),
                Health.Poor => (Color.FromRgb(0xEC, 0x83, 0x5A), Color.FromRgb(0x1A, 0x1A, 0x19)),
                Health.Down => (Color.FromRgb(0xD0, 0x3B, 0x3B), Colors.White),
                _ => (Color.FromRgb(0x89, 0x87, 0x81), Colors.White),
            };
            StatusBadge.Draw(dc, health, new Rect(bx - br, by - br, 2 * br, 2 * br), new SolidColorBrush(fill), new SolidColorBrush(ink));
        }
        return bmp;
    }

    public void Dispose()
    {
        _appearanceTimer.Stop();
        _tray.IsVisible = false;
        _tray.Dispose();
    }
}
