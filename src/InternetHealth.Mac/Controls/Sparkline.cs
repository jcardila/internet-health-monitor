using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace InternetHealth.Mac.Controls;

/// <summary>
/// Minigráfica de latencia reciente. Dibujo directo con StreamGeometry (sin un objeto por punto).
/// Las muestras perdidas se marcan como pequeñas barras en la base.
/// </summary>
public sealed class Sparkline : Control
{
    public static readonly StyledProperty<IReadOnlyList<double?>?> ValuesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<double?>?>(nameof(Values));
    public static readonly StyledProperty<IBrush> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush>(nameof(Stroke), Brushes.SteelBlue);
    public static readonly StyledProperty<IBrush> LossBrushProperty =
        AvaloniaProperty.Register<Sparkline, IBrush>(nameof(LossBrush), Brushes.IndianRed);
    public static readonly StyledProperty<IBrush> GridBrushProperty =
        AvaloniaProperty.Register<Sparkline, IBrush>(nameof(GridBrush), Brushes.LightGray);
    public static readonly StyledProperty<IBrush> TextBrushProperty =
        AvaloniaProperty.Register<Sparkline, IBrush>(nameof(TextBrush), Brushes.Gray);

    private int _hover = -1;

    static Sparkline()
    {
        AffectsRender<Sparkline>(ValuesProperty, StrokeProperty, LossBrushProperty, GridBrushProperty, TextBrushProperty);
        ClipToBoundsProperty.OverrideDefaultValue<Sparkline>(true);
    }

    public IReadOnlyList<double?>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IBrush Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush LossBrush { get => GetValue(LossBrushProperty); set => SetValue(LossBrushProperty, value); }
    public IBrush GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _hover = IndexAt(e.GetPosition(this).X);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    private int IndexAt(double x)
    {
        var v = Values;
        if (v is null || v.Count < 2 || Bounds.Width <= 0) return -1;
        return (int)Math.Clamp(Math.Round(x / Bounds.Width * (v.Count - 1)), 0, v.Count - 1);
    }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // área de hover

        var values = Values;
        var gridPen = new Pen(GridBrush, 1);
        context.DrawLine(gridPen, new Point(0, h - 0.5), new Point(w, h - 0.5));
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
                if (values[i] is not double v)
                {
                    if (open) ctx.EndFigure(false);
                    open = false;
                    continue;
                }
                var p = new Point(i * step, 2 + plotH - v / max * plotH);
                if (!open) { ctx.BeginFigure(p, false); open = true; }
                else ctx.LineTo(p);
            }
            if (open) ctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(Stroke, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);

        for (int i = 0; i < values.Count; i++)
            if (values[i] is null)
                context.DrawRectangle(LossBrush, null, new Rect(Math.Max(0, i * step - 1.5), h - lossBand, 3, lossBand), 1, 1);

        if (_hover >= 0 && _hover < values.Count)
        {
            double x = _hover * step;
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            string label = values[_hover] is double hv ? $"{hv:0} ms" : "sin respuesta";
            var ft = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 11, TextBrush);
            double tx = Math.Clamp(x + 6, 0, Math.Max(0, w - ft.Width - 2));
            context.DrawText(ft, new Point(tx, 0));
            if (values[_hover] is double pv)
                context.DrawEllipse(Stroke, null, new Point(x, 2 + plotH - pv / max * plotH), 3.5, 3.5);
        }
    }
}
