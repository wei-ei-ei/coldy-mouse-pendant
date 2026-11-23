using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Turns the white-background Coldy picture into a transparent PNG and
// reports where the antenna ball sits, so the rope can be tied there.
class MakeCutout
{
    static int Main(string[] args)
    {
        string srcPath = args.Length > 0 ? args[0] : @"assets\coldy_lowres.png";
        string dstPath = args.Length > 1 ? args[1] : @"assets\coldy.png";

        using (var src = new Bitmap(srcPath))
        using (var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
                g.DrawImage(src, 0, 0, src.Width, src.Height);

            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            byte[] px = new byte[stride * h];
            Marshal.Copy(data.Scan0, px, 0, px.Length);

            bool[] bg = new bool[w * h];
            var q = new Queue<int>();
            for (int x = 0; x < w; x++) { TrySeed(px, bg, q, stride, w, h, x, 0); TrySeed(px, bg, q, stride, w, h, x, h - 1); }
            for (int y = 0; y < h; y++) { TrySeed(px, bg, q, stride, w, h, 0, y); TrySeed(px, bg, q, stride, w, h, w - 1, y); }

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                int x = i % w, y = i / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (bg[ni]) continue;
                    if (!IsBackground(px, stride, w, h, nx, ny)) continue;
                    bg[ni] = true;
                    q.Enqueue(ni);
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int o = y * stride + x * 4;
                    if (bg[y * w + x]) { px[o + 3] = 0; continue; }
                    // Soften the white fringe left where the background met the fur.
                    if (!TouchesBackground(bg, w, h, x, y)) continue;
                    byte b = px[o], gg = px[o + 1], r = px[o + 2];
                    int m = Math.Min(r, Math.Min(gg, b));
                    if (m > 236)
                    {
                        int a = (255 - m) * 255 / (255 - 236);
                        if (a < 0) a = 0;
                        px[o + 3] = (byte)a;
                    }
                }
            }

            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);

            int top = h;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (bmp.GetPixel(x, y).A > 128 && y < top) top = y;

            // The antenna ball is the topmost solid part of the picture.
            long sx = 0, sy = 0, n = 0;
            int ballBottom = Math.Min(h - 1, top + 16);
            for (int y = top; y <= ballBottom; y++)
                for (int x = 0; x < w; x++)
                {
                    if (bmp.GetPixel(x, y).A < 160) continue;
                    sx += x; sy += y; n++;
                }

            bmp.Save(dstPath, ImageFormat.Png);

            using (var preview = new Bitmap(w, h))
            using (var g = Graphics.FromImage(preview))
            {
                g.Clear(Color.FromArgb(70, 150, 210));
                g.DrawImage(bmp, 0, 0);
                if (n > 0)
                {
                    float ax = sx / (float)n, ay = sy / (float)n;
                    g.FillEllipse(Brushes.Red, ax - 3, ay - 3, 6, 6);
                    Console.WriteLine("anchor {0:0.0} {1:0.0} samples {2} top {3}", ax, ay, n, top);
                }
                preview.Save(@"assets\coldy_preview.png", ImageFormat.Png);
            }

            Console.WriteLine("wrote {0} {1}x{2}", dstPath, w, h);
        }
        return 0;
    }

    // The backdrop is a flat near-white. The fur is the same brightness in
    // places, but it is textured, so a locally flat neighborhood is background.
    static bool IsBackground(byte[] px, int stride, int w, int h, int x, int y)
    {
        int lo = 255, hi = 0;
        for (int yy = y - 1; yy <= y + 1; yy++)
        {
            for (int xx = x - 1; xx <= x + 1; xx++)
            {
                int sx = xx < 0 ? 0 : (xx >= w ? w - 1 : xx);
                int sy = yy < 0 ? 0 : (yy >= h ? h - 1 : yy);
                int o = sy * stride + sx * 4;
                int r = px[o + 2], g = px[o + 1], b = px[o];
                if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) > 16) return false;
                int m = Math.Min(r, Math.Min(g, b));
                int M = Math.Max(r, Math.Max(g, b));
                if (m < lo) lo = m;
                if (M > hi) hi = M;
            }
        }
        return lo >= 214 && hi - lo <= 14;
    }

    static void TrySeed(byte[] px, bool[] bg, Queue<int> q, int stride, int w, int h, int x, int y)
    {
        int i = y * w + x;
        if (bg[i] || !IsBackground(px, stride, w, h, x, y)) return;
        bg[i] = true;
        q.Enqueue(i);
    }

    static bool TouchesBackground(bool[] bg, int w, int h, int x, int y)
    {
        for (int yy = y - 1; yy <= y + 1; yy++)
            for (int xx = x - 1; xx <= x + 1; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= w || yy >= h) return true;
                if (bg[yy * w + xx]) return true;
            }
        return false;
    }
}
