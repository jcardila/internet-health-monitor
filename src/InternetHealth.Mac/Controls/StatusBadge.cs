using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using InternetHealth.Core.Model;

namespace InternetHealth.Mac.Controls;

/// <summary>
/// Distintivo de estado: círculo de color + símbolo (✓ ! ✕ …) dibujado con trazos, igual que la
/// insignia del ícono de la barra de menús. El estado nunca depende solo del color.
/// </summary>
public sealed class StatusBadge : Control
{
    public static readonly StyledProperty<Health> HealthProperty =
        AvaloniaProperty.Register<StatusBadge, Health>(nameof(Health));

    static StatusBadge()
    {
        AffectsRender<StatusBadge>(HealthProperty);
    }

    public StatusBadge()
    {
        // Al cambiar entre claro y oscuro, los colores se vuelven a leer del tema.
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public Health Health { get => GetValue(HealthProperty); set => SetValue(HealthProperty, value); }

    public override void Render(DrawingContext context)
    {
        double d = Math.Min(Bounds.Width, Bounds.Height);
        if (d <= 0) return;
        var origin = new Point((Bounds.Width - d) / 2, (Bounds.Height - d) / 2);
        Draw(context, Health, new Rect(origin, new Size(d, d)),
            HealthToBrushConverter.BrushFor(Health), HealthToBrushConverter.BrushFor(Health, "Ink"));
    }

    /// <summary>Dibuja la insignia en una caja cuadrada. Lo usa también el ícono de la barra de menús.</summary>
    public static void Draw(DrawingContext dc, Health health, Rect box, IBrush fill, IBrush ink)
    {
        double d = box.Width;
        dc.DrawEllipse(fill, null, box.Center, d / 2, d / 2);
        Point P(double fx, double fy) => new(box.X + d * fx, box.Y + d * fy);
        var pen = new Pen(ink, d * .15, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        switch (health)
        {
            case Health.Good:
                var check = new StreamGeometry();
                using (var g = check.Open())
                {
                    g.BeginFigure(P(.28, .52), false);
                    g.LineTo(P(.44, .68));
                    g.LineTo(P(.73, .36));
                    g.EndFigure(false);
                }
                dc.DrawGeometry(null, pen, check);
                break;
            case Health.Fair:
            case Health.Poor:
                dc.DrawLine(pen, P(.5, .26), P(.5, .54));
                dc.DrawEllipse(ink, null, P(.5, .73), d * .085, d * .085);
                break;
            case Health.Down:
                dc.DrawLine(pen, P(.33, .33), P(.67, .67));
                dc.DrawLine(pen, P(.67, .33), P(.33, .67));
                break;
            default:
                for (int i = 0; i < 3; i++) dc.DrawEllipse(ink, null, P(.28 + i * .22, .5), d * .075, d * .075);
                break;
        }
    }
}
