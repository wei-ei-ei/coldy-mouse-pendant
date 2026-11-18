using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Form 2 is the same coldy.png, with the motions from the clips.
// The crown stays put so the rope still meets the head.
class MakeForm2Gif
{
    const int W = 340;
    const int H = 320;
    const float DestX = 160f;
    const float DestY = 78f;

    static int Main()
    {
        string root = Directory.GetCurrentDirectory();
        string pngPath = Path.Combine(root, "assets", "coldy.png");
        using (var src = new Bitmap(pngPath))
        {
            PointF crown = FindAnchor(src);
            float[] scaleY = {
                1f, 0.94f, 0.88f, 0.94f, 1.04f, 1.08f, 1.03f, 1f,
                1f, 1f, 1f, 1f,
                1f, 1f, 1f, 1f, 1f, 1f
            };
            float[] blink = {
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0.45f, 1f, 1f, 0.4f,
                0f, 0f, 0f, 0f, 0f, 0f
            };
            float[] tilt = {
                0f, 1.2f, 2.2f, 1f, -1f, -2f, -0.8f, 0f,
                0f, 0f, 0f, 0f,
                -2f, -5f, -2f, 2.5f, 5f, 2f
            };
            var frames = new Bitmap[scaleY.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                float sy = scaleY[i];
                float sx = 1f + (1f - sy) * 0.55f;
                frames[i] = Render(src, crown, sx, sy, tilt[i], blink[i]);
            }
            string gifPath = Path.Combine(root, "assets", "coldy-form2.gif");
            Gif.Save(gifPath, frames, 10);
            string sheet = Path.Combine(Path.GetTempPath(), "coldy-form2-sheet.png");
            SaveSheet(src, frames, sheet);
            PointF hung = FindAnchor(frames[0]);
            Console.WriteLine(gifPath);
            Console.WriteLine(sheet);
            Console.WriteLine("crown " + crown.X.ToString("0.0") + "," + crown.Y.ToString("0.0")
                + " anchor " + hung.X.ToString("0.0") + "," + hung.Y.ToString("0.0"));
            for (int i = 0; i < frames.Length; i++) frames[i].Dispose();
        }
        return 0;
    }

    static Bitmap Render(Bitmap src, PointF crown, float sx, float sy, float degrees, float blink)
    {
        var frame = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var painted = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
        {
            using (var pg = Graphics.FromImage(painted))
            {
                pg.Clear(Color.Transparent);
                pg.DrawImage(src, 0, 0, src.Width, src.Height);
                PaintLid(pg, src, blink);
            }
            using (var g = Graphics.FromImage(frame))
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
            }
        }
        return frame;
    }

    // Close only the glowing eye, using the fur just above it.
    // The wink and the smile stay as they are.
    static void PaintLid(Graphics g, Bitmap src, float blink)
    {
        if (blink <= 0.02f) return;
        var eye = new Rectangle(139, 73, 28, 32);
        int drop = (int)Math.Round(eye.Height * blink);
        if (drop < 2) return;
        var fur = new Rectangle(eye.X, eye.Y - 18, eye.Width, 16);
        using (var cover = new GraphicsPath())
        {
            cover.AddEllipse(eye);
            g.SetClip(cover);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, new Rectangle(eye.X, eye.Y - 1, eye.Width, drop + 1), fur, GraphicsUnit.Pixel);
            using (var line = new Pen(Color.FromArgb(170, 80, 116, 136), 1.6f))
                g.DrawLine(line, eye.X + 4, eye.Y + drop - 1, eye.Right - 4, eye.Y + drop - 1);
            g.ResetClip();
        }
    }

    static void SaveSheet(Bitmap src, Bitmap[] frames, string path)
    {
        int cell = 170;
        int n = frames.Length + 1;
        int cols = 6;
        int rows = (n + cols - 1) / cols;
        using (var bmp = new Bitmap(cols * cell, rows * cell, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(255, 16, 18, 22));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 8, 8, cell - 16, cell - 16);
            for (int i = 0; i < frames.Length; i++)
            {
                int col = (i + 1) % cols;
                int row = (i + 1) / cols;
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
        int insertAt = table + colors * 3;
        bool hasLoop = false;
        for (int i = 0; i + 11 < file.Length; i++)
        {
            if (file[i] == (byte)'N' && file[i + 1] == (byte)'E' && file[i + 2] == (byte)'T') hasLoop = true;
        }
        if (!hasLoop)
        {
            byte[] loop = {
                0x21, 0xFF, 0x0B,
                (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E',
                (byte)'2', (byte)'.', (byte)'0',
                0x03, 0x01, 0x00, 0x00, 0x00
            };
            byte[] grown = new byte[file.Length + loop.Length];
            Array.Copy(file, 0, grown, 0, insertAt);
            Array.Copy(loop, 0, grown, insertAt, loop.Length);
            Array.Copy(file, insertAt, grown, insertAt + loop.Length, file.Length - insertAt);
            file = grown;
        }
        for (int i = 0; i + 7 < file.Length; i++)
        {
            if (file[i] != 0x21 || file[i + 1] != 0xF9 || file[i + 2] != 0x04) continue;
            file[i + 3] = 0x09;
            file[i + 4] = (byte)(delayCs & 255);
            file[i + 5] = (byte)((delayCs >> 8) & 255);
            file[i + 6] = (byte)magenta;
        }
        File.WriteAllBytes(path, file);
    }
}
