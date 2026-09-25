using System.Globalization;
using System.Windows;
using System.Windows.Media;
using InternetHealth.Core.History;
using InternetHealth.Core.Model;

namespace InternetHealth.App.Controls;

/// <summary>
/// Historial: latencia promedio por minuto (línea) y, debajo, una franja con el estado de cada
/// minuto. Un solo eje Y. Al pasar el mouse muestra el detalle del minuto.
/// </summary>
public sealed class HistoryChart : FrameworkElement
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(IReadOnlyList<MinuteRow>), typeof(HistoryChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((HistoryChart)d).Reindex()));

    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(DateTimeOffset), typeof(HistoryChart),
        new FrameworkPropertyMetadata(default(DateTimeOffset), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(DateTimeOffset), typeof(HistoryChart),
        new FrameworkPropertyMetadata(default(DateTimeOffset), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeriesBrushProperty = Reg(nameof(SeriesBrush), Brushes.SteelBlue);
    public static readonly DependencyProperty GridBrushProperty = Reg(nameof(GridBrush), Brushes.LightGray);
    public static readonly DependencyProperty AxisBrushProperty = Reg(nameof(AxisBrush), Brushes.Gray);
    public static readonly DependencyProperty TextBrushProperty = Reg(nameof(TextBrush), Brushes.Gray);
    public static readonly DependencyProperty TooltipBackgroundProperty = Reg(nameof(TooltipBackground), Brushes.White);
    public static readonly DependencyProperty TooltipForegroundProperty = Reg(nameof(TooltipForeground), Brushes.Black);

    private static DependencyProperty Reg(string name, Brush def) => DependencyProperty.Register(
        name, typeof(Brush), typeof(HistoryChart), new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");
    private readonly Typeface _font = new("Segoe UI");
    private MinuteRow[] _sorted = [];
    private Point? _mouse;

    public HistoryChart()
    {
        ClipToBounds = true;
        MouseMove += (_, e) => { _mouse = e.GetPosition(this); InvalidateVisual(); };
        MouseLeave += (_, _) => { _mouse = null; InvalidateVisual(); };
    }

    public IReadOnlyList<MinuteRow>? Rows { get => (IReadOnlyList<MinuteRow>?)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public DateTimeOffset From { get => (DateTimeOffset)GetValue(FromProperty); set => SetValue(FromProperty, value); }
    public DateTimeOffset To { get => (DateTimeOffset)GetValue(ToProperty); set => SetValue(ToProperty, value); }
    public Brush SeriesBrush { get => (Brush)GetValue(SeriesBrushProperty); set => SetValue(SeriesBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush AxisBrush { get => (Brush)GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public Brush TooltipBackground { get => (Brush)GetValue(TooltipBackgroundProperty); set => SetValue(TooltipBackgroundProperty, value); }
    public Brush TooltipForeground { get => (Brush)GetValue(TooltipForegroundProperty); set => SetValue(TooltipForegroundProperty, value); }

    private void Reindex() => _sorted = Rows?.OrderBy(r => r.Minute).ToArray() ?? [];

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        var from = From; var to = To;
        if (to <= from) return;
        const double left = 44, right = 8, top = 8, xAxisH = 20, stripH = 16, gap = 10;
        double plotBottom = h - xAxisH - stripH - gap;
        double plotH = Math.Max(10, plotBottom - top);
        double plotW = Math.Max(10, w - left - right);
        double totalMin = (to - from).TotalMinutes;
        double X(DateTimeOffset t) => left + (t - from).TotalMinutes / totalMin * plotW;

        var gridPen = Frozen(new Pen(GridBrush, 1));
        var axisPen = Frozen(new Pen(AxisBrush, 1));
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var avgs = _sorted.Where(r => r.Internet.AvgMs is not null).Select(r => r.Internet.AvgMs!.Value).OrderBy(v => v).ToArray();
        double yMax = NiceMax(avgs.Length > 0 ? avgs[(int)Math.Round(0.98 * (avgs.Length - 1))] * 1.15 : 50);
        double Y(double v) => top + (1 - Math.Min(v, yMax) / yMax) * plotH;

        // Grilla y eje Y
        for (int i = 0; i <= 4; i++)
        {
            double v = yMax * i / 4, y = Math.Round(Y(v)) + 0.5;
            dc.DrawLine(i == 0 ? axisPen : gridPen, new Point(left, y), new Point(w - right, y));
            var ft = Text($"{v:0} ms", 11, TextBrush, dpi);
            dc.DrawText(ft, new Point(left - 6 - ft.Width, y - ft.Height / 2));
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
            var ft = Text(t.ToString(fmt, Es), 11, TextBrush, dpi);
            double x = X(t) - ft.Width / 2;
            if (x < lastRight + 8) continue;
            dc.DrawText(ft, new Point(x, h - xAxisH + 3));
            lastRight = x + ft.Width;
        }

        // Línea de latencia
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            MinuteRow? prev = null;
            foreach (var r in _sorted)
            {
                if (r.Minute < from || r.Minute > to || r.Internet.AvgMs is not double v) { prev = null; continue; }
                var p = new Point(X(r.Minute), Y(v));
                if (prev is null || (r.Minute - prev.Minute).TotalMinutes > 3) ctx.BeginFigure(p, false, false);
                else ctx.LineTo(p, true, true);
                prev = r;
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, Frozen(new Pen(SeriesBrush, 2) { LineJoin = PenLineJoin.Round }), geo);

        // Franja de estado por minuto (con 2 px de separación visual entre bloques largos)
        double stripTop = plotBottom + gap;
        double minuteW = Math.Max(1, plotW / totalMin);
        foreach (var r in _sorted)
        {
            if (r.WorstSeverity == Health.Unknown || r.Minute < from || r.Minute > to) continue;
            dc.DrawRectangle(HealthToBrushConverter.BrushFor(r.WorstSeverity), null,
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
                dc.DrawLine(axisPen, new Point(x, top), new Point(x, stripTop + stripH));
                if (nearest.Internet.AvgMs is double hv)
                    dc.DrawEllipse(SeriesBrush, Frozen(new Pen(TooltipBackground, 2)), new Point(x, Y(hv)), 4, 4);

                var lines = new List<string>
                {
                    nearest.Minute.ToString("dddd d, HH:mm", Es),
                    nearest.Internet.AvgMs is double a ? $"Latencia: {a:0} ms" : "Latencia: sin respuesta",
                };
                if (nearest.Internet.LossPct is double lp) lines.Add($"Pérdida: {lp.ToString("0.#", Es)} %");
                if (nearest.Internet.JitterMs is double j) lines.Add($"Variación: {j:0} ms");
                lines.Add($"Estado: {nearest.WorstSeverity.ToLabel()}");
                if (!string.IsNullOrEmpty(nearest.Ssid)) lines.Add($"Red: {nearest.Ssid}");
                var ft = Text(string.Join("\n", lines), 12, TooltipForeground, dpi);
                double bw = ft.Width + 20, bh = ft.Height + 14;
                double bx = x + 12 + bw > w ? x - 12 - bw : x + 12;
                var box = new Rect(Math.Max(0, bx), top + 4, bw, bh);
                dc.DrawRoundedRectangle(TooltipBackground, gridPen, box, 6, 6);
                dc.DrawText(ft, new Point(box.X + 10, box.Y + 7));
            }
        }
    }

    private FormattedText Text(string s, double size, Brush brush, double dpi) =>
        new(s, Es, FlowDirection.LeftToRight, _font, size, brush, dpi);

    private static Pen Frozen(Pen p) { if (p.CanFreeze) p.Freeze(); return p; }

    private static double NiceMax(double v)
    {
        double[] steps = [20, 40, 60, 80, 100, 150, 200, 300, 400, 600, 800, 1000, 1500, 2000];
        foreach (var s in steps) if (v <= s) return s;
        return Math.Ceiling(v / 1000) * 1000;
    }
}
