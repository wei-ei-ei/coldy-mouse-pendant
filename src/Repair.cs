using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// The cutout shaved the upper-left of the head flat. The right side is still
// a circular arc, so the missing part is filled back out to that same circle.
class Repair
{
    static void Main()
    {
        var cut = new Bitmap(@"assets\coldy.png");
        int w = cut.Width, h = cut.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = cut.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        byte[] px = new byte[Math.Abs(stride) * h];
        Marshal.Copy(data.Scan0, px, 0, px.Length);

        // Fitted to the intact right side of the head.
        double cx = 134.0, cy = 92.0, r = 46.0;
        int filled = 0;
        int y0 = (int)Math.Floor(cy - r);
        for (int y = y0; y <= 96; y++)
        {
            for (int x = 0; x < (int)cx + 8; x++)
            {
                if (x < 0 || y < 0 || y >= h) continue;
                int o = y * stride + x * 4;
                if (px[o + 3] >= 200) continue;
                double dx = x - cx, dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > r) continue;

                int best = 999, nx = -1, ny = -1;
                for (int yy = y - 24; yy <= y + 8; yy++)
                {
                    for (int xx = x - 8; xx <= x + 24; xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        if (px[yy * stride + xx * 4 + 3] < 200) continue;
                        int d2 = (xx - x) * (xx - x) + (yy - y) * (yy - y);
                        if (d2 < best) { best = d2; nx = xx; ny = yy; }
                    }
                }
                if (best > 26 * 26) continue;
                int no = ny * stride + nx * 4;
                double edge = r - dist;
                int alpha = edge >= 1.3 ? 255 : (int)(255 * Math.Max(0, edge / 1.3));
                px[o] = px[no];
                px[o + 1] = px[no + 1];
                px[o + 2] = px[no + 2];
                px[o + 3] = (byte)alpha;
                filled++;
            }
        }

        Marshal.Copy(px, 0, data.Scan0, px.Length);
        cut.UnlockBits(data);
        cut.Save(@"assets\coldy_fixed.png", ImageFormat.Png);
        Console.WriteLine("filled " + filled);

        using (var sil = new Bitmap(160 * 6, 110 * 6))
        using (var g = Graphics.FromImage(sil))
        {
            g.Clear(Color.Black);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int y = 30; y < 140; y++)
                for (int x = 60; x < 220; x++)
                    if (cut.GetPixel(x, y).A >= 128)
                        g.FillRectangle(Brushes.White, (x - 60) * 6, (y - 30) * 6, 6, 6);
            sil.Save(@"assets\head_silhouette.png", ImageFormat.Png);
        }
        cut.Dispose();
    }
}
