using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;

namespace InternetHealth.Mac.Controls;

/// <summary>
/// Historial: latencia promedio por minuto (línea) y, debajo, una franja con el estado de cada
/// minuto. Un solo eje Y. Al pasar el mouse muestra el detalle del minuto.
/// </summary>
public sealed class HistoryChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<MinuteRow>?> RowsProperty =
        AvaloniaProperty.Register<HistoryChart, IReadOnlyList<MinuteRow>?>(nameof(Rows));
    public static readonly StyledProperty<DateTimeOffset> FromProperty =
        AvaloniaProperty.Register<HistoryChart, DateTimeOffset>(nameof(From));
    public static readonly StyledProperty<DateTimeOffset> ToProperty =
        AvaloniaProperty.Register<HistoryChart, DateTimeOffset>(nameof(To));
    public static readonly StyledProperty<IBrush> SeriesBrushProperty =
        AvaloniaProperty.Register<HistoryChart, IBrush>(nameof(SeriesBrush), Brushes.SteelBlue);
    public static readonly StyledProperty<IBrush> GridBrushProperty =
        AvaloniaProperty.Register<HistoryChart, IBrush>(nameof(GridBrush), Brushes.LightGray);
    public static readonly StyledProperty<IBrush> AxisBrushProperty =
        AvaloniaProperty.Register<HistoryChart, IBrush>(nameof(AxisBrush), Brushes.Gray);
    public static readonly StyledProperty<IBrush> TextBrushProperty =
        AvaloniaProperty.Register<HistoryChart, IBrush>(nameof(TextBrush), Brushes.Gray);
    public static readonly StyledProperty<IBrush> TooltipBackgroundProperty =
        AvaloniaProperty.Register<HistoryChart, IBrush>(nameof(TooltipBackground), Brushes.White);
    public static readonly StyledProperty<IBrush> TooltipForegroundProperty =
        AvaloniaProperty.Register<HistoryChart, IBrush>(nameof(TooltipForeground), Brushes.Black);

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");
    private MinuteRow[] _sorted = [];
    private Point? _mouse;

    static HistoryChart()
    {
        AffectsRender<HistoryChart>(RowsProperty, FromProperty, ToProperty, SeriesBrushProperty, GridBrushProperty,
            AxisBrushProperty, TextBrushProperty, TooltipBackgroundProperty, TooltipForegroundProperty);
        ClipToBoundsProperty.OverrideDefaultValue<HistoryChart>(true);
        RowsProperty.Changed.AddClassHandler<HistoryChart>((c, _) => c.Reindex());
    }

    public HistoryChart()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual(); // la franja usa colores del tema
    }

    public IReadOnlyList<MinuteRow>? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public DateTimeOffset From { get => GetValue(FromProperty); set => SetValue(FromProperty, value); }
    public DateTimeOffset To { get => GetValue(ToProperty); set => SetValue(ToProperty, value); }
    public IBrush SeriesBrush { get => GetValue(SeriesBrushProperty); set => SetValue(SeriesBrushProperty, value); }
    public IBrush GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush TooltipBackground { get => GetValue(TooltipBackgroundProperty); set => SetValue(TooltipBackgroundProperty, value); }
    public IBrush TooltipForeground { get => GetValue(TooltipForegroundProperty); set => SetValue(TooltipForegroundProperty, value); }

    private void Reindex() => _sorted = Rows?.OrderBy(r => r.Minute).ToArray() ?? [];

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _mouse = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _mouse = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        var from = From; var to = To;
        if (to <= from) return;
        const double left = 44, right = 8, top = 8, xAxisH = 20, stripH = 16, gap = 10;
        double plotBottom = h - xAxisH - stripH - gap;
        double plotH = Math.Max(10, plotBottom - top);
        double plotW = Math.Max(10, w - left - right);
        double totalMin = (to - from).TotalMinutes;
        double X(DateTimeOffset t) => left + (t - from).TotalMinutes / totalMin * plotW;

        var gridPen = new Pen(GridBrush, 1);
        var axisPen = new Pen(AxisBrush, 1);

        var avgs = _sorted.Where(r => r.Internet.AvgMs is not null).Select(r => r.Internet.AvgMs!.Value).OrderBy(v => v).ToArray();
        double yMax = NiceMax(avgs.Length > 0 ? avgs[(int)Math.Round(0.98 * (avgs.Length - 1))] * 1.15 : 50);
        double Y(double v) => top + (1 - Math.Min(v, yMax) / yMax) * plotH;

        // Grilla y eje Y
        for (int i = 0; i <= 4; i++)
        {
            double v = yMax * i / 4, y = Math.Round(Y(v)) + 0.5;
            context.DrawLine(i == 0 ? axisPen : gridPen, new Point(left, y), new Point(w - right, y));
            var ft = Text($"{v:0} ms", 11, TextBrush);
            context.DrawText(ft, new Point(left - 6 - ft.Width, y - ft.Height / 2));
        }

        // Eje X
        var span = to - from;
        TimeSpan tick = span.TotalHours <= 1.5 ? TimeSpan.FromMinutes(15)
            : span.TotalHours <= 7 ? TimeSpan.FromHours(1)
            : span.TotalHours <= 26 ? TimeSpan.FromHours(3)
            : TimeSpan.FromDays(1);
        var t0 = new DateTimeOffset(from.Year, from.Month, from.Day, 0, 0, 0, from.Offset);
        while (t0 < from) t0 += tick;
        string fmt = tick >= TimeSpan.FromDays(1) ? "ddd d" : "HH:mm";
        double lastRight = double.MinValue;
        for (var t = t0; t < to; t += tick)
        {
            var ft = Text(t.ToString(fmt, Es), 11, TextBrush);
            double x = X(t) - ft.Width / 2;
            if (x < lastRight + 8) continue;
            context.DrawText(ft, new Point(x, h - xAxisH + 3));
            lastRight = x + ft.Width;
        }

        // Línea de latencia
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            MinuteRow? prev = null;
            bool open = false;
            foreach (var r in _sorted)
            {
                if (r.Minute < from || r.Minute > to || r.Internet.AvgMs is not double v)
                {
                    prev = null;
                    continue;
                }
                var p = new Point(X(r.Minute), Y(v));
                if (prev is null || (r.Minute - prev.Minute).TotalMinutes > 3)
                {
                    if (open) ctx.EndFigure(false);
                    ctx.BeginFigure(p, false);
                    open = true;
                }
                else ctx.LineTo(p);
                prev = r;
            }
            if (open) ctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(SeriesBrush, 2, lineJoin: PenLineJoin.Round), geo);

        // Franja de estado por minuto
        double stripTop = plotBottom + gap;
        double minuteW = Math.Max(1, plotW / totalMin);
        foreach (var r in _sorted)
        {
            if (r.WorstSeverity == Health.Unknown || r.Minute < from || r.Minute > to) continue;
            context.DrawRectangle(HealthToBrushConverter.BrushFor(r.WorstSeverity), null,
                new Rect(X(r.Minute), stripTop, minuteW + 0.5, stripH));
        }

        // Capa de hover
        if (_mouse is Point m && m.X >= left && m.X <= w - right && _sorted.Length > 0)
        {
            var t = from + TimeSpan.FromMinutes((m.X - left) / plotW * totalMin);
            var nearest = _sorted.MinBy(r => Math.Abs((r.Minute - t).TotalMinutes));
            if (nearest is not null && Math.Abs((nearest.Minute - t).TotalMinutes) <= Math.Max(2, totalMin / plotW * 6))
            {
                double x = X(nearest.Minute);
                context.DrawLine(axisPen, new Point(x, top), new Point(x, stripTop + stripH));
                if (nearest.Internet.AvgMs is double hv)
                    context.DrawEllipse(SeriesBrush, new Pen(TooltipBackground, 2), new Point(x, Y(hv)), 4, 4);

                var lines = new List<string>
                {
                    nearest.Minute.ToLocalTime().ToString("dddd d, HH:mm", Es),
                    nearest.Internet.AvgMs is double a ? $"Latencia: {a:0} ms" : "Latencia: sin respuesta",
                };
                if (nearest.Internet.LossPct is double lp) lines.Add($"Pérdida: {lp.ToString("0.#", Es)} %");
                if (nearest.Internet.JitterMs is double j) lines.Add($"Variación: {j:0} ms");
                lines.Add($"Estado: {nearest.WorstSeverity.ToLabel()}");
                if (!string.IsNullOrEmpty(nearest.Ssid)) lines.Add($"Red: {nearest.Ssid}");
                var ft = Text(string.Join("\n", lines), 12, TooltipForeground);
                double bw = ft.Width + 20, bh = ft.Height + 14;
                double bx = x + 12 + bw > w ? x - 12 - bw : x + 12;
                var box = new Rect(Math.Max(0, bx), top + 4, bw, bh);
                context.DrawRectangle(TooltipBackground, gridPen, box, 6, 6);
                context.DrawText(ft, new Point(box.X + 10, box.Y + 7));
            }
        }
    }

    private static FormattedText Text(string s, double size, IBrush brush) =>
        new(s, Es, FlowDirection.LeftToRight, Typeface.Default, size, brush);

    private static double NiceMax(double v)
    {
        double[] steps = [20, 40, 60, 80, 100, 150, 200, 300, 400, 600, 800, 1000, 1500, 2000];
        foreach (var s in steps) if (v <= s) return s;
        return Math.Ceiling(v / 1000) * 1000;
    }
}
