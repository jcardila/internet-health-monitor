using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InternetHealth.App.Controls;

/// <summary>
/// Minigráfica de latencia reciente. Dibujo directo con StreamGeometry (sin un objeto por punto).
/// Las muestras perdidas se marcan como pequeñas barras en la base.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double?>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LossBrushProperty = DependencyProperty.Register(
        nameof(LossBrush), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    private int _hover = -1;

    public Sparkline()
    {
        ClipToBounds = true;
        MouseMove += (_, e) => { _hover = IndexAt(e.GetPosition(this).X); InvalidateVisual(); };
        MouseLeave += (_, _) => { _hover = -1; InvalidateVisual(); };
    }

    public IReadOnlyList<double?>? Values { get => (IReadOnlyList<double?>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush LossBrush { get => (Brush)GetValue(LossBrushProperty); set => SetValue(LossBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    private int IndexAt(double x)
    {
        var v = Values;
        if (v is null || v.Count < 2 || ActualWidth <= 0) return -1;
        return (int)Math.Clamp(Math.Round(x / ActualWidth * (v.Count - 1)), 0, v.Count - 1);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // área de hover

        var values = Values;
        var gridPen = new Pen(GridBrush, 1);
        if (gridPen.CanFreeze) gridPen.Freeze();
        dc.DrawLine(gridPen, new Point(0, h - 0.5), new Point(w, h - 0.5));
        if (values is null || values.Count < 2) return;

        double max = 0;
        foreach (var v in values) if (v is double d && d > max) max = d;
        max = Math.Max(max * 1.15, 10);
        const double lossBand = 4;
        double plotH = h - lossBand - 2;
        double step = w / (values.Count - 1);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            bool open = false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is not double v) { open = false; continue; }
                var p = new Point(i * step, 2 + plotH - v / max * plotH);
                if (!open) { ctx.BeginFigure(p, false, false); open = true; }
                else ctx.LineTo(p, true, true);
            }
        }
        geo.Freeze();
        var pen = new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (pen.CanFreeze) pen.Freeze();
        dc.DrawGeometry(null, pen, geo);

        for (int i = 0; i < values.Count; i++)
            if (values[i] is null)
                dc.DrawRoundedRectangle(LossBrush, null, new Rect(Math.Max(0, i * step - 1.5), h - lossBand, 3, lossBand), 1, 1);

        if (_hover >= 0 && _hover < values.Count)
        {
            double x = _hover * step;
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            string label = values[_hover] is double hv ? $"{hv:0} ms" : "sin respuesta";
            var ft = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, TextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double tx = Math.Clamp(x + 6, 0, Math.Max(0, w - ft.Width - 2));
            dc.DrawText(ft, new Point(tx, 0));
            if (values[_hover] is double pv)
                dc.DrawEllipse(Stroke, null, new Point(x, 2 + plotH - pv / max * plotH), 3.5, 3.5);
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 40 : availableSize.Height);
}
