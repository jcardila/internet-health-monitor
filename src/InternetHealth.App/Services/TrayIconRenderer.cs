using System.Drawing;
using System.Drawing.Drawing2D;
using InternetHealth.App.Interop;
using InternetHealth.Core.Model;

namespace InternetHealth.App.Services;

/// <summary>
/// Dibuja los íconos de la bandeja al estilo de OneDrive o Teams: un globo monocromo del color de
/// la barra de tareas y, en la esquina, una insignia de estado con símbolo (✓ ! ✕ …) para que el
/// estado no dependa solo del color (accesible para personas con daltonismo).
/// Se dibuja a 4× y se reduce: a 16 px los trazos quedan nítidos y sin dientes.
/// </summary>
internal static class TrayIconRenderer
{
    private const int Oversample = 4;

    public static Icon Create(Health health, int size, bool lightTaskbar)
    {
        int big = size * Oversample;
        using var hi = new Bitmap(big, big, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(hi))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float s = big;

            // Globo: contorno, meridiano y ecuador.
            var globeColor = lightTaskbar ? Color.FromArgb(0x1A, 0x1A, 0x19) : Color.White;
            float cx = s * .44f, cy = s * .44f, r = s * .39f;
            using (var pen = new Pen(globeColor, s * .075f))
            {
                g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
                g.DrawEllipse(pen, cx - r * .45f, cy - r, r * .9f, 2 * r);
                g.DrawLine(pen, cx - r, cy, cx + r, cy);
            }

            // Recorte alrededor de la insignia: separa la insignia del globo, como en OneDrive.
            float bx = s * .73f, by = s * .73f, br = s * .27f, gap = s * .07f;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var clear = new SolidBrush(Color.Transparent))
                g.FillEllipse(clear, bx - br - gap, by - br - gap, 2 * (br + gap), 2 * (br + gap));
            g.CompositingMode = CompositingMode.SourceOver;

            var (fill, ink) = health switch
            {
                Health.Good => (Color.FromArgb(0x0C, 0xA3, 0x0C), Color.White),
                Health.Fair => (Color.FromArgb(0xFA, 0xB2, 0x19), Color.FromArgb(0x1A, 0x1A, 0x19)),
                Health.Poor => (Color.FromArgb(0xEC, 0x83, 0x5A), Color.FromArgb(0x1A, 0x1A, 0x19)),
                Health.Down => (Color.FromArgb(0xD0, 0x3B, 0x3B), Color.White),
                _ => (Color.FromArgb(0x89, 0x87, 0x81), Color.White),
            };
            using (var brush = new SolidBrush(fill))
                g.FillEllipse(brush, bx - br, by - br, 2 * br, 2 * br);

            // Símbolo dentro de la insignia, en coordenadas relativas a su caja.
            float x0 = bx - br, y0 = by - br, d = 2 * br;
            PointF P(float fx, float fy) => new(x0 + d * fx, y0 + d * fy);
            using var sym = new Pen(ink, d * .17f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var dot = new SolidBrush(ink);
            switch (health)
            {
                case Health.Good:
                    g.DrawLines(sym, [P(.26f, .52f), P(.43f, .69f), P(.74f, .35f)]);
                    break;
                case Health.Fair:
                case Health.Poor:
                    g.DrawLine(sym, P(.5f, .25f), P(.5f, .55f));
                    g.FillEllipse(dot, x0 + d * .41f, y0 + d * .66f, d * .18f, d * .18f);
                    break;
                case Health.Down:
                    g.DrawLine(sym, P(.32f, .32f), P(.68f, .68f));
                    g.DrawLine(sym, P(.68f, .32f), P(.32f, .68f));
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                        g.FillEllipse(dot, x0 + d * (.2f + i * .23f), y0 + d * .42f, d * .16f, d * .16f);
                    break;
            }
        }

        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(hi, new Rectangle(0, 0, size, size));
        }

        IntPtr h = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(h);
        }
    }
}
