using System.Drawing;
using System.Windows.Forms;
using InternetHealth.Core.Model;

namespace InternetHealth.App.Services;

/// <summary>Ícono en la bandeja del sistema: estado de un vistazo, clic para el panel, menú con clic derecho.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly Dictionary<Health, Icon> _icons = new();
    private readonly ToolStripMenuItem _dndItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ContextMenuStrip _menu;
    private readonly bool _demo;
    private Health _current = (Health)(-1);

    public event Action? LeftClick;
    public event Action? OpenDetails;
    public event Action? Share;
    public event Action? ToggleDoNotDisturb;
    public event Action? ToggleStartup;
    public event Action? Exit;
    public event Action? NotificationClicked;

    /// <param name="demo">Modo demostración: el texto del ícono y los avisos lo indican siempre.</param>
    public TrayIcon(bool demo)
    {
        _demo = demo;
        RenderIcons();

        _menu = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Segoe UI", 9.5f) };
        _menu.Items.Add(new ToolStripMenuItem("Abrir panel", null, (_, _) => LeftClick?.Invoke()) { Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold) });
        _menu.Items.Add(new ToolStripMenuItem("Ver detalles e historial", null, (_, _) => OpenDetails?.Invoke()));
        _menu.Items.Add(new ToolStripMenuItem("Compartir diagnóstico…", null, (_, _) => Share?.Invoke()));
        _menu.Items.Add(new ToolStripSeparator());
        _dndItem = new ToolStripMenuItem("No molestar por 1 hora", null, (_, _) => ToggleDoNotDisturb?.Invoke());
        _startupItem = new ToolStripMenuItem("Iniciar con Windows", null, (_, _) => ToggleStartup?.Invoke());
        _menu.Items.Add(_dndItem);
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Salir", null, (_, _) => Exit?.Invoke()));
        ApplyTheme(ThemeManager.IsDark);

        _notify = new NotifyIcon
        {
            Icon = _icons[Health.Unknown],
            Text = Prefix + "verificando…",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) LeftClick?.Invoke();
        };
        _notify.BalloonTipClicked += (_, _) => NotificationClicked?.Invoke();
    }

    public void Update(Health health, string tooltip)
    {
        if (health != _current)
        {
            _notify.Icon = _icons[health];
            _current = health;
        }
        // Límite de Windows para el texto de la bandeja: 127 caracteres.
        var text = Prefix + tooltip;
        _notify.Text = text.Length > 127 ? text[..126] + "…" : text;
    }

    private string Prefix => _demo ? "DEMO · Monitor de Conexión — " : "Monitor de Conexión — ";

    /// <summary>Redibuja los íconos con el color actual de la barra de tareas (clara u oscura).</summary>
    public void RefreshIcons()
    {
        var old = _icons.Values.ToList();
        RenderIcons();
        if (_current >= 0) _notify.Icon = _icons[_current];
        else _notify.Icon = _icons[Health.Unknown];
        foreach (var i in old) i.Dispose();
    }

    private void RenderIcons()
    {
        int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
        bool light = ThemeManager.TaskbarIsLight;
        foreach (var h in Enum.GetValues<Health>()) _icons[h] = TrayIconRenderer.Create(h, size, light);
    }

    public void SetMenuState(bool dndActive, bool startupEnabled)
    {
        _dndItem.Text = dndActive ? "Reactivar notificaciones" : "No molestar por 1 hora";
        _startupItem.Checked = startupEnabled;
    }

    public void ShowNotification(string title, string text, bool isRecovery)
    {
        if (_demo) title = "[Demo] " + title;
        _notify.ShowBalloonTip(8000, title, text,isRecovery ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    public void ApplyTheme(bool dark)
    {
        _menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors(dark)) { RoundedEdges = true };
        _menu.ForeColor = dark ? Color.FromArgb(0xF3, 0xF3, 0xF3) : Color.FromArgb(0x1A, 0x1A, 0x19);
        _menu.BackColor = dark ? Color.FromArgb(0x2B, 0x2B, 0x2A) : Color.FromArgb(0xF9, 0xF9, 0xF9);
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _menu.Dispose();
        foreach (var i in _icons.Values) i.Dispose();
    }

    private sealed class MenuColors(bool dark) : ProfessionalColorTable
    {
        private readonly Color _bg = dark ? Color.FromArgb(0x2B, 0x2B, 0x2A) : Color.FromArgb(0xF9, 0xF9, 0xF9);
        private readonly Color _hover = dark ? Color.FromArgb(0x3A, 0x3A, 0x38) : Color.FromArgb(0xE9, 0xE9, 0xE6);
        private readonly Color _border = dark ? Color.FromArgb(0x45, 0x45, 0x43) : Color.FromArgb(0xD6, 0xD5, 0xCF);

        public override Color ToolStripDropDownBackground => _bg;
        public override Color MenuBorder => _border;
        public override Color MenuItemBorder => _hover;
        public override Color MenuItemSelected => _hover;
        public override Color MenuItemSelectedGradientBegin => _hover;
        public override Color MenuItemSelectedGradientEnd => _hover;
        public override Color ImageMarginGradientBegin => _bg;
        public override Color ImageMarginGradientMiddle => _bg;
        public override Color ImageMarginGradientEnd => _bg;
        public override Color SeparatorDark => _border;
        public override Color SeparatorLight => _bg;
        public override Color CheckBackground => _hover;
        public override Color CheckSelectedBackground => _hover;
        public override Color CheckPressedBackground => _hover;
    }
}
