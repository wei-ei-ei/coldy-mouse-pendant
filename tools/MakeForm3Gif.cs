using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Form 3 is the stage look from COLDY WORLD's Co歌之王.
// Cat-eye sunglasses, a black mic, and the rainbow leads.
// Two or three notes fade in, rise, and fade out.
// The crown stays put so the rope still meets the head.
class MakeForm3Gif
{
    const int W = 340;
    const int H = 320;
    const float DestX = 160f;
    const float DestY = 78f;

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "pack")
        {
            string packDir = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "coldy-sing-pack");
            string gifName = args.Length > 2 ? args[2] : "coldy-form3.gif";
            PackExtracted(packDir, gifName);
            return 0;
        }
        string root = Directory.GetCurrentDirectory();
        string pngPath = Path.Combine(root, "assets", "coldy.png");
        using (var src = new Bitmap(pngPath))
        {
            PointF crown = FindAnchor(src);
            Rectangle glowBox = EyeBox(src);
            PointF glow = new PointF(glowBox.X + glowBox.Width / 2f, glowBox.Y + glowBox.Height / 2f);
            int n = 24;
            var frames = new Bitmap[n];
            for (int i = 0; i < n; i++)
            {
                float wave = (float)Math.Sin(i * Math.PI * 2.0 / n);
                float sy = 1f + wave * 0.015f;
                float sx = 1f - wave * 0.008f;
                float tilt = wave * 7.5f;
                frames[i] = Render(src, crown, glowBox, sx, sy, tilt, i, n);
            }
            string gifPath = Path.Combine(Path.GetTempPath(), "coldy-form3-drawn.gif");
            Gif.Save(gifPath, frames, 12);
            string sheet = Path.Combine(Path.GetTempPath(), "coldy-form3-sheet.png");
            SaveSheet(frames, sheet);
            frames[6].Save(Path.Combine(Path.GetTempPath(), "coldy-form3-frame.png"), ImageFormat.Png);
            frames[18].Save(Path.Combine(Path.GetTempPath(), "coldy-form3-left.png"), ImageFormat.Png);
            PointF hung = FindAnchor(frames[0]);
            Console.WriteLine(gifPath);
            Console.WriteLine(sheet);
            Console.WriteLine("glow " + glow.X.ToString("0.0") + "," + glow.Y.ToString("0.0")
                + " box " + glowBox.X + "," + glowBox.Y + " " + glowBox.Width + "x" + glowBox.Height);
            Console.WriteLine("crown " + crown.X.ToString("0.0") + "," + crown.Y.ToString("0.0")
                + " anchor " + hung.X.ToString("0.0") + "," + hung.Y.ToString("0.0"));
            for (int i = 0; i < frames.Length; i++) frames[i].Dispose();
        }
        return 0;
    }

    static Bitmap Render(Bitmap src, PointF crown, Rectangle glowBox, float sx, float sy, float degrees, int frame, int count)
    {
        var picture = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var painted = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
        {
            using (var pg = Graphics.FromImage(painted))
            {
                pg.Clear(Color.Transparent);
                pg.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            ClearCables(painted);
            using (var pg = Graphics.FromImage(painted))
            {
                pg.SmoothingMode = SmoothingMode.AntiAlias;
                pg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                PaintStage(pg, glowBox, degrees);
            }
            using (var g = Graphics.FromImage(picture))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TranslateTransform(DestX, DestY);
                g.RotateTransform(degrees);
                g.ScaleTransform(sx, sy);
                g.TranslateTransform(-crown.X, -crown.Y);
                g.DrawImage(painted, 0, 0, src.Width, src.Height);
                g.ResetTransform();
                DrawNotes(g, frame, count);
            }
        }
        return picture;
    }

    static void ClearCables(Bitmap b)
    {
        for (int y = 108; y <= 168 && y < b.Height; y++)
        {
            for (int x = 158; x < b.Width; x++)
            {
                Color c = b.GetPixel(x, y);
                if (c.A < 80) continue;
                bool cable = Spread(c) >= 36;
                bool tip = x >= 186 && c.R < 120 && c.G < 120 && c.B < 140;
                if (!cable && !tip) continue;
                Color fur = Color.Empty;
                for (int k = 1; k < 48; k++)
                {
                    int sx = x - k;
                    if (sx < 0) break;
                    Color s = b.GetPixel(sx, y);
                    if (s.A < 160) continue;
                    if (Spread(s) < 26 && s.R > 185 && s.G > 185 && s.B > 185)
                    {
                        fur = s;
                        break;
                    }
                }
                if (fur.A > 0) b.SetPixel(x, y, fur);
            }
        }
    }

    static int Spread(Color c)
    {
        int max = c.R;
        if (c.G > max) max = c.G;
        if (c.B > max) max = c.B;
        int min = c.R;
        if (c.G < min) min = c.G;
        if (c.B < min) min = c.B;
        return max - min;
    }

    static Rectangle EyeBox(Bitmap b)
    {
        Rectangle box = FindGlowBox(b);
        if (box.Width > 40 || box.Height > 48) return new Rectangle(139, 73, 28, 32);
        return box;
    }

    static void PaintStage(Graphics g, Rectangle eye, float sway)
    {
        PointF[] right = {
            new PointF(eye.Left + 4f, eye.Top + 18f),
            new PointF(eye.Right + 9f, eye.Top + 8f),
            new PointF(eye.Left + 18f, eye.Bottom - 2f)
        };
        PointF[] left = {
            new PointF(eye.Left - 2f, eye.Top + 20f),
            new PointF(eye.Left - 36f, eye.Top + 10f),
            new PointF(eye.Left - 14f, eye.Bottom)
        };

        using (var glass = new SolidBrush(Color.FromArgb(165, 12, 16, 24)))
            g.FillPolygon(glass, right);
        using (var dark = new SolidBrush(Color.FromArgb(225, 12, 14, 20)))
            g.FillPolygon(dark, left);
        using (var metal = new Pen(Color.FromArgb(255, 198, 206, 216), 2.1f))
        {
            metal.LineJoin = LineJoin.Round;
            metal.StartCap = LineCap.Round;
            metal.EndCap = LineCap.Round;
            g.DrawPolygon(metal, right);
            g.DrawPolygon(metal, left);
            g.DrawLine(metal, eye.Left - 2f, eye.Top + 20f, eye.Left + 4f, eye.Top + 18f);
        }

        float mouthX = eye.Left - 14f;
        float mouthY = eye.Bottom + 2f;
        using (var mouth = new SolidBrush(Color.FromArgb(245, 16, 18, 24)))
            g.FillEllipse(mouth, mouthX, mouthY, 8f, 11f);

        float gripX = 150f + sway * 0.35f;
        float gripY = eye.Bottom + 16f;
        Color[] leads = {
            Color.FromArgb(255, 214, 52, 64),
            Color.FromArgb(255, 236, 186, 42),
            Color.FromArgb(255, 36, 196, 214),
            Color.FromArgb(255, 42, 112, 214)
        };
        for (int i = 0; i < leads.Length; i++)
        {
            float o = i * 4.6f;
            using (var pen = new Pen(leads[i], 4.2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawBezier(pen,
                    180f, 122f + o * 0.35f,
                    202f + sway * 0.45f, 146f + o * 0.2f,
                    170f + sway * 0.2f, 150f + o * 0.15f,
                    gripX + 4f, gripY + 2f + o * 0.2f);
            }
        }

        g.TranslateTransform(gripX - 2f, gripY + 2f);
        g.RotateTransform(-38f + sway * 0.6f);
        using (var head = new SolidBrush(Color.FromArgb(255, 28, 30, 36)))
            g.FillEllipse(head, -8f, -16f, 16f, 16f);
        using (var mesh = new Pen(Color.FromArgb(220, 150, 156, 166), 0.8f))
        {
            g.DrawEllipse(mesh, -8f, -16f, 16f, 16f);
            g.DrawLine(mesh, -6f, -12f, 6f, -12f);
            g.DrawLine(mesh, -7f, -8f, 7f, -8f);
            g.DrawLine(mesh, -6f, -4f, 6f, -4f);
        }
        using (var handle = new SolidBrush(Color.FromArgb(255, 22, 24, 30)))
            g.FillRectangle(handle, -3.2f, -2f, 6.4f, 28f);
        using (var dot = new SolidBrush(Color.FromArgb(255, 28, 188, 214)))
        {
            g.FillEllipse(dot, -2.1f, 2f, 4.2f, 4.2f);
            g.FillEllipse(dot, -2.1f, 8f, 4.2f, 4.2f);
            g.FillEllipse(dot, -2.1f, 14f, 4.2f, 4.2f);
            g.FillEllipse(dot, -2.1f, 20f, 4.2f, 4.2f);
        }
        g.ResetTransform();
    }

    static Rectangle FindGlowBox(Bitmap b)
    {
        int minX = b.Width;
        int minY = b.Height;
        int maxX = 0;
        int maxY = 0;
        int count = 0;
        for (int y = 0; y < b.Height; y++)
        {
            for (int x = 0; x < b.Width; x++)
            {
                Color c = b.GetPixel(x, y);
                if (c.A < 200) continue;
                if (c.B < 150) continue;
                if (c.B < c.R + 35) continue;
                if (c.G < 90) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                count++;
            }
        }
        if (count == 0) return new Rectangle(139, 73, 28, 32);
        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    static void DrawNotes(Graphics g, int frame, int count)
    {
        float t0 = frame / (float)count;
        float[] phase = { 0f, 0.33f, 0.66f };
        float[] x0 = { 208f, 236f, 188f };
        for (int n = 0; n < 3; n++)
        {
            float life = t0 + phase[n];
            if (life >= 1f) life -= 1f;
            float alpha;
            float rise;
            if (life < 0.22f)
            {
                alpha = life / 0.22f;
                rise = life / 0.22f * 0.08f;
            }
            else if (life < 0.72f)
            {
                alpha = 1f;
                rise = 0.08f + (life - 0.22f) / 0.50f * 0.72f;
            }
            else
            {
                alpha = (1f - life) / 0.28f;
                rise = 0.80f + (life - 0.72f) / 0.28f * 0.20f;
            }
            if (alpha < 0f) alpha = 0f;
            if (alpha > 1f) alpha = 1f;
            int a = (int)(alpha * 235f);
            if (a < 8) continue;
            float x = x0[n] + rise * 10f;
            float y = 158f - rise * 92f;
            DrawNote(g, x, y, a, n == 1 ? -14f : 10f);
        }
    }

    static void DrawNote(Graphics g, float x, float y, int alpha, float lean)
    {
        g.TranslateTransform(x, y);
        g.RotateTransform(lean);
        using (var ink = new SolidBrush(Color.FromArgb(alpha, 186, 230, 244)))
        using (var pen = new Pen(Color.FromArgb(alpha, 186, 230, 244), 1.7f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.FillEllipse(ink, -5.5f, 2f, 11f, 7f);
            g.DrawLine(pen, 4.5f, 4f, 4.5f, -12f);
            g.DrawBezier(pen, 4.5f, -12f, 10f, -10f, 12f, -4f, 6f, -2f);
        }
        g.ResetTransform();
    }

    static PointF FindGlow(Bitmap b)
    {
        long sx = 0;
        long sy = 0;
        int count = 0;
        for (int y = 0; y < b.Height; y++)
        {
            for (int x = 0; x < b.Width; x++)
            {
                Color c = b.GetPixel(x, y);
                if (c.A < 200) continue;
                if (c.B < 150) continue;
                if (c.B < c.R + 35) continue;
                if (c.G < 90) continue;
                sx += x;
                sy += y;
                count++;
            }
        }
        if (count == 0) return new PointF(152f, 88f);
        return new PointF(sx / (float)count, sy / (float)count);
    }

    static void SaveSheet(Bitmap[] frames, string path)
    {
        int cell = 170;
        int cols = 8;
        int rows = (frames.Length + cols - 1) / cols;
        using (var bmp = new Bitmap(cols * cell, rows * cell, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(255, 16, 18, 22));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (int i = 0; i < frames.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                g.DrawImage(frames[i], col * cell + 8, row * cell + 8, cell - 16, cell - 16);
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }

    static PointF FindAnchor(Bitmap b)
    {
        int top = -1, spoonL = 0, spoonR = -1;
        for (int y = 0; y < b.Height && top < 0; y++)
        {
            for (int x = 0; x < b.Width; x++)
            {
                if (b.GetPixel(x, y).A <= 128) continue;
                top = y;
                spoonL = x;
                spoonR = x;
                while (spoonR + 1 < b.Width && b.GetPixel(spoonR + 1, y).A > 128) spoonR++;
                break;
            }
        }
        if (top < 0) return new PointF(b.Width / 2f, 8f);
        int headY = -1;
        for (int y = top; y < b.Height && headY < 0; y++)
        {
            int x = 0;
            while (x < b.Width)
            {
                while (x < b.Width && b.GetPixel(x, y).A <= 128) x++;
                if (x >= b.Width) break;
                int left = x;
                while (x < b.Width && b.GetPixel(x, y).A > 128) x++;
                int right = x - 1;
                if (right < spoonL - 2) headY = y;
                else if (left <= spoonR + 2)
                {
                    if (left < spoonL) spoonL = left;
                    if (right > spoonR) spoonR = right;
                }
            }
        }
        if (headY < 0) headY = top;
        long sx = 0, sy = 0, count = 0;
        int bottom = Math.Min(b.Height - 1, headY + 5);
        for (int y = headY; y <= bottom; y++)
        {
            int x = 0;
            while (x < b.Width && b.GetPixel(x, y).A <= 128) x++;
            while (x < b.Width && b.GetPixel(x, y).A > 128)
            {
                sx += x;
                sy += y;
                count++;
                x++;
            }
        }
        if (count == 0) return new PointF(b.Width / 2f, headY);
        return new PointF(sx / (float)count, sy / (float)count);
    }

    static void PackExtracted(string dir, string gifName)
    {
        string[] files = Directory.GetFiles(dir, "f*.png");
        Array.Sort(files, StringComparer.Ordinal);
        var frames = new Bitmap[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            using (var src = new Bitmap(files[i]))
                frames[i] = new Bitmap(src);
        }
        string root = Directory.GetCurrentDirectory();
        string gifPath = Path.Combine(root, "assets", gifName);
        Gif.Save(gifPath, frames, 10);
        frames[0].Save(Path.Combine(Path.GetTempPath(), "coldy-form3-a.png"), ImageFormat.Png);
        frames[frames.Length / 2].Save(Path.Combine(Path.GetTempPath(), "coldy-form3-b.png"), ImageFormat.Png);
        frames[frames.Length - 1].Save(Path.Combine(Path.GetTempPath(), "coldy-form3-c.png"), ImageFormat.Png);
        PointF hung = FindAnchor(frames[0]);
        Console.WriteLine(gifPath);
        Console.WriteLine("frames " + frames.Length + " anchor " + hung.X.ToString("0.0") + "," + hung.Y.ToString("0.0"));
        for (int i = 0; i < frames.Length; i++) frames[i].Dispose();
    }
}

class Gif
{
    public static void Save(string path, Bitmap[] frames, int delayCs)
    {
        ImageCodecInfo encoder = null;
        ImageCodecInfo[] all = ImageCodecInfo.GetImageEncoders();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].FormatID == ImageFormat.Gif.Guid) encoder = all[i];
        }
        var first = FrameBitmap(frames[0]);
        var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        first.Save(path, encoder, ep);
        ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
        for (int i = 1; i < frames.Length; i++)
        {
            using (var frame = FrameBitmap(frames[i]))
                first.SaveAdd(frame, ep);
        }
        ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
        first.SaveAdd(ep);
        first.Dispose();
        Patch(path, delayCs);
    }

    static Bitmap FrameBitmap(Bitmap src)
    {
        var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        var from = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var to = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        int h = src.Height;
        int w = src.Width;
        byte[] rowIn = new byte[from.Stride];
        byte[] rowOut = new byte[to.Stride];
        for (int y = 0; y < h; y++)
        {
            Marshal.Copy(new IntPtr(from.Scan0.ToInt64() + y * from.Stride), rowIn, 0, from.Stride);
            for (int x = 0; x < w; x++)
            {
                int i = x * 4;
                if (rowIn[i + 3] < 128)
                {
                    rowOut[i] = 255;
                    rowOut[i + 1] = 0;
                    rowOut[i + 2] = 255;
                    rowOut[i + 3] = 255;
                }
                else
                {
                    rowOut[i] = rowIn[i];
                    rowOut[i + 1] = rowIn[i + 1];
                    rowOut[i + 2] = rowIn[i + 2];
                    rowOut[i + 3] = 255;
                }
            }
            Marshal.Copy(rowOut, 0, new IntPtr(to.Scan0.ToInt64() + y * to.Stride), to.Stride);
        }
        src.UnlockBits(from);
        bmp.UnlockBits(to);
        return bmp;
    }

    static bool HasNetscapeLoop(byte[] file, int pos)
    {
        int i = pos;
        while (i + 16 < file.Length)
        {
            if (file[i] == 0x3B) return false;
            if (file[i] == 0x21 && file[i + 1] == 0xFF && file[i + 2] == 0x0B)
            {
                bool net = true;
                string tag = "NETSCAPE2.0";
                for (int k = 0; k < 11; k++)
                {
                    if (file[i + 3 + k] != (byte)tag[k]) net = false;
                }
                if (net) return true;
            }
            if (file[i] == 0x21)
            {
                i += 2;
                while (i < file.Length)
                {
                    int n = file[i];
                    i++;
                    if (n == 0) break;
                    i += n;
                }
                continue;
            }
            if (file[i] == 0x2C) return false;
            return false;
        }
        return false;
    }

    static void Patch(string path, int delayCs)
    {
        byte[] file = File.ReadAllBytes(path);
        int packed = file[10];
        int colors = 1 << ((packed & 7) + 1);
        int table = 13;
        int magenta = 0;
        bool found = false;
        if ((packed & 0x80) != 0)
        {
            for (int i = 0; i < colors; i++)
            {
                int o = table + i * 3;
                if (file[o] == 255 && file[o + 1] == 0 && file[o + 2] == 255)
                {
                    magenta = i;
                    found = true;
                    break;
                }
            }
        }
        if (!found) throw new InvalidOperationException("magenta key missing");
        int pos = table + colors * 3;
        if (!HasNetscapeLoop(file, pos))
        {
            byte[] loop = {
                0x21, 0xFF, 0x0B,
                (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E',
                (byte)'2', (byte)'.', (byte)'0',
                0x03, 0x01, 0x00, 0x00, 0x00
            };
            byte[] grown = new byte[file.Length + loop.Length];
            Array.Copy(file, 0, grown, 0, pos);
            Array.Copy(loop, 0, grown, pos, loop.Length);
            Array.Copy(file, pos, grown, pos + loop.Length, file.Length - pos);
            file = grown;
        }
        // Walk the blocks. A raw search also matches this sequence inside compressed
        // frames and corrupts those frames (the lower body flashes on the way up).
        int p = pos;
        while (p < file.Length)
        {
            if (file[p] == 0x3B) break;
            if (file[p] == 0x21)
            {
                bool gce = p + 7 < file.Length && file[p + 1] == 0xF9 && file[p + 2] == 0x04;
                if (gce)
                {
                    file[p + 3] = 0x09;
                    file[p + 4] = (byte)(delayCs & 255);
                    file[p + 5] = (byte)((delayCs >> 8) & 255);
                    file[p + 6] = (byte)magenta;
                }
                p += 2;
                while (p < file.Length)
                {
                    int n = file[p];
                    p++;
                    if (n == 0) break;
                    p += n;
                }
                continue;
            }
            if (file[p] == 0x2C)
            {
                p += 9;
                int ip = file[p];
                p++;
                if ((ip & 0x80) != 0)
                {
                    int ncolors = 1 << ((ip & 7) + 1);
                    p += ncolors * 3;
                }
                p++;
                while (p < file.Length)
                {
                    int n = file[p];
                    p++;
                    if (n == 0) break;
                    p += n;
                }
                continue;
            }
            break;
        }
        File.WriteAllBytes(path, file);
    }
}
