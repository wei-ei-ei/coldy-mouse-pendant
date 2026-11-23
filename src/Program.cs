using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Coldy hangs from the cursor on a rope that can bend.
// The control window sets the name, rope length, size, form, cursor, and hide key.
// The pendant itself is a transparent, topmost, click-through layered window.
class Program
{
    public const string AppVersion = "1.6.1";
    // One-time changelog revision, decoupled from AppVersion so showing the
    // update log never bumps the version nor triggers the auto-update offer.
    public const string NotesRev = "kge-6";
    static EventWaitHandle showEvent;

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool createdNew;
        using (new Mutex(true, @"Local\Coldy.MousePendant", out createdNew))
        {
            if (!createdNew)
            {
                try { EventWaitHandle.OpenExisting(@"Local\Coldy.MousePendant.Show").Set(); }
                catch { }
                return;
            }
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Coldy.MousePendant.Show");
            RunApp();
        }
    }

    static void RunApp()
    {
        try
        {
            Application.Run(new SettingsForm());
        }
        catch (Exception ex)
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                File.WriteAllText(Path.Combine(dir, "coldy-error.txt"), ex.ToString());
            }
            catch { }
            MessageBox.Show(ex.Message, "Coldy");
        }
    }

    public static void WaitForShow()
    {
        if (showEvent != null) showEvent.WaitOne();
    }
}

class PendantForm : Form
{
    const int RopeSegments = 7;
    const float Gravity = 11000f;

    readonly Bitmap sprite;
    readonly float anchorSpriteX;
    readonly float anchorSpriteY;
    Bitmap[] form2;
    float anchor2X;
    float anchor2Y;
    Bitmap[] form4;
    float anchor4X;
    float anchor4Y;
    // Every form is scaled by its own opaque height, so the "人物大小"
    // slider produces exactly the same on-screen snowman height everywhere.
    float bodySpriteH = 1f;
    float body2H = 1f;
    float body4H = 1f;
    int formIndex;
    int animFrame;
    double animTime;
    const int AnimFrameMs = 100;
    // One bounce in about three quarters of a second, close to the recorded dance.
    const int SingFrameMs = 8;
    // Scaled singing frames, shifted inside the bitmap so the bob never resamples the edge.
    Bitmap[] singScaled;
    int singScaledW;
    int singScaledH;
    Bitmap singShifted;
    int singReadyFrame = -1;
    float singReadyDip = -1f;
    readonly Rope rope = new Rope(RopeSegments, 120f, Gravity);
    float scale;
    float anchorX;
    float anchorY;
    float ropeLength = 120f;
    float displayHeight = 190f;
    float cursorSize = 32f;
    // Hang offset uses the pointer size that is actually in effect. In system
    // cursor mode that is the system cursor, not the (possibly huge) scheme size.
    float cursorScale = 32f;
    Color ropeColor = Color.FromArgb(230, 55, 55, 62);
    bool hidden;
    bool hiddenApplied;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly NotifyIcon tray = new NotifyIcon();
    bool placed;
    bool ticking;
    bool hasLast;
    int lastX;
    int lastY;
    int lastW;
    int lastH;
    readonly Stopwatch clock = Stopwatch.StartNew();
    long lastClock;
    Icon appIcon;
    MemoryStream iconStream;

    public PendantForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(8, 8);
        BackColor = Color.Black;

        string dir = Path.GetDirectoryName(Application.ExecutablePath);
        string path = Path.Combine(dir, "assets", "coldy.png");
        sprite = new Bitmap(path);
        PointF anchor = FindAnchor(sprite);
        anchorSpriteX = anchor.X;
        anchorSpriteY = anchor.Y;
        string gifPath = Path.Combine(dir, "assets", "coldy-form2.gif");
        if (File.Exists(gifPath))
        {
            try
            {
                form2 = LoadGif(gifPath);
                if (form2.Length > 0)
                {
                    PointF anchor2 = FindAnchor(form2[0]);
                    anchor2X = anchor2.X;
                    anchor2Y = anchor2.Y;
                }
            }
            catch
            {
                form2 = null;
            }
        }
        // The singing form (cocococo / coldy-form4.gif) is archived in the
        // repository but is no longer loaded or offered by the software.
        bodySpriteH = BodyHeight(sprite);
        if (form2 != null && form2.Length > 0) body2H = BodyHeight(form2[0]);
        ApplyMetrics();

        MemoryStream stream;
        appIcon = MakeColdyIcon(sprite, out stream);
        iconStream = stream;
        tray.Icon = appIcon;
        tray.Text = "Coldy";
        tray.Visible = true;
        tray.ContextMenu = new ContextMenu();

        timer.Interval = 8;
        timer.Tick += delegate { Tick(); };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x00080000;
            const int WS_EX_TRANSPARENT = 0x00000020;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_NOACTIVATE = 0x08000000;
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Point cursor = CursorPosition();
        float hang = cursorScale * 0.45f;
        rope.SetAnchor(cursor.X + hang * 0.25f, cursor.Y + hang, true);
        rope.Reset();
        timeBeginPeriod(1);
        lastClock = clock.ElapsedMilliseconds;
        timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        timeEndPeriod(1);
        tray.Visible = false;
        tray.Dispose();
        if (appIcon != null) appIcon.Dispose();
        if (iconStream != null) iconStream.Dispose();
        sprite.Dispose();
        if (form2 != null)
        {
            for (int i = 0; i < form2.Length; i++) form2[i].Dispose();
        }
        if (form4 != null)
        {
            for (int i = 0; i < form4.Length; i++) form4[i].Dispose();
        }
        ClearSingCache();
        base.OnFormClosed(e);
    }

    public NotifyIcon TrayIcon
    {
        get { return tray; }
    }

    public Icon AppIcon
    {
        get { return appIcon; }
    }

    public void SetName(string name)
    {
        if (name == null || name.Trim().Length == 0) name = "Coldy";
        tray.Text = name.Length > 63 ? name.Substring(0, 63) : name;
    }

    public void SetRopeLength(float length)
    {
        ropeLength = length;
        rope.SetLength(length);
    }

    public void SetDisplayHeight(float height)
    {
        displayHeight = height;
        ApplyMetrics();
    }

    public void SetCursorSize(float size, bool scheme)
    {
        cursorSize = size;
        float system = SystemInformation.CursorSize.Width;
        if (system < 8f) system = 32f;
        cursorScale = scheme ? size : system;
        if (cursorScale < 8f) cursorScale = system;
    }

    public void SetRopeColor(Color color)
    {
        ropeColor = Color.FromArgb(230, color.R, color.G, color.B);
    }

    public void SetHidden(bool value)
    {
        hidden = value;
    }

    public void SetForm(int index)
    {
        if (index == 2 && (form4 == null || form4.Length == 0)) index = 0;
        if (index == 1 && (form2 == null || form2.Length == 0)) index = 0;
        if (index < 0 || index > 2) index = 0;
        if (formIndex == index) return;
        formIndex = index;
        animFrame = 0;
        animTime = 0;
        ApplyMetrics();
    }

    void ApplyMetrics()
    {
        if (displayHeight < 30f) displayHeight = 30f;
        float body = bodySpriteH;
        if (formIndex == 1) body = body2H;
        else if (formIndex == 2) body = body4H;
        if (body < 1f) body = 1f;
        scale = displayHeight / body;
        float ax = anchorSpriteX;
        float ay = anchorSpriteY;
        if (formIndex == 1)
        {
            ax = anchor2X;
            ay = anchor2Y;
        }
        else if (formIndex == 2)
        {
            ax = anchor4X;
            ay = anchor4Y;
        }
        anchorX = ax * scale;
        anchorY = ay * scale;
        rope.SetLength(ropeLength);
    }

    Bitmap CurrentPicture()
    {
        if (formIndex == 1 && form2 != null && form2.Length > 0)
            return form2[animFrame];
        if (formIndex == 2 && form4 != null && form4.Length > 0)
            return form4[animFrame];
        return sprite;
    }

    // Height of the opaque figure rather than the transparent canvas, so a
    // form with a taller canvas does not make the snowman look smaller.
    static float BodyHeight(Bitmap b)
    {
        int top = -1, bottom = -1;
        BitmapData data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] row = new byte[Math.Abs(data.Stride)];
        for (int y = 0; y < b.Height; y++)
        {
            Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + y * data.Stride), row, 0, data.Stride);
            for (int x = 0; x < b.Width; x++)
            {
                if (row[x * 4 + 3] <= 128) continue;
                if (top < 0) top = y;
                bottom = y;
                break;
            }
        }
        b.UnlockBits(data);
        if (top < 0) return (float)b.Height;
        float h = (float)(bottom - top + 1);
        return h < 1f ? 1f : h;
    }

    // Pulls the dark cutout fringe off the white fur, and gives nearby
    // transparent pixels the fur color so scaling does not mix in black.
    static unsafe void SoftenOutline(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            byte* scan = (byte*)data.Scan0;
            int stride = data.Stride;
            long sx = 0, sy = 0, count = 0;
            int left = w, top = h, right = -1, bottom = -1;
            for (int y = 0; y < h; y++)
            {
                byte* row = scan + y * stride;
                for (int x = 0; x < w; x++)
                {
                    if (row[x * 4 + 3] <= 200) continue;
                    sx += x;
                    sy += y;
                    count++;
                    if (x < left) left = x;
                    if (y < top) top = y;
                    if (x > right) right = x;
                    if (y > bottom) bottom = y;
                }
            }
            if (count < 10) return;
            float cx = sx / (float)count;
            float cy = sy / (float)count;
            if (left > 5) left -= 5;
            else left = 0;
            if (top > 5) top -= 5;
            else top = 0;
            if (right < w - 6) right += 5;
            else right = w - 1;
            if (bottom < h - 6) bottom += 5;
            else bottom = h - 1;

            for (int pass = 0; pass < 4; pass++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    byte* row = scan + y * stride;
                    for (int x = left; x <= right; x++)
                    {
                        int p = x * 4;
                        if (row[p + 3] != 0) continue;
                        int rb = 0, gb = 0, bb = 0, found = 0;
                        if (x > 0 && row[p - 1] > 0) { bb = row[p - 4]; gb = row[p - 3]; rb = row[p - 2]; found = 1; }
                        else if (x + 1 < w && row[p + 7] > 0) { bb = row[p + 4]; gb = row[p + 5]; rb = row[p + 6]; found = 1; }
                        else if (y > 0)
                        {
                            byte* up = row - stride;
                            if (up[p + 3] > 0) { bb = up[p]; gb = up[p + 1]; rb = up[p + 2]; found = 1; }
                        }
                        if (found == 0 && y + 1 < h)
                        {
                            byte* down = row + stride;
                            if (down[p + 3] > 0) { bb = down[p]; gb = down[p + 1]; rb = down[p + 2]; found = 1; }
                        }
                        if (found == 0) continue;
                        row[p] = (byte)bb;
                        row[p + 1] = (byte)gb;
                        row[p + 2] = (byte)rb;
                        row[p + 3] = 1;
                    }
                }
            }

            for (int y = top; y <= bottom; y++)
            {
                byte* row = scan + y * stride;
                for (int x = left; x <= right; x++)
                {
                    int p = x * 4;
                    if (row[p + 3] <= 200) continue;
                    bool rim = false;
                    if (x > 0 && row[p - 1] < 128) rim = true;
                    else if (x + 1 < w && row[p + 7] < 128) rim = true;
                    else if (y > 0 && (row - stride)[p + 3] < 128) rim = true;
                    else if (y + 1 < h && (row + stride)[p + 3] < 128) rim = true;
                    if (!rim) continue;
                    int lum = (row[p] + row[p + 1] + row[p + 2]) / 3;
                    if (lum >= 115) continue;
                    float dx = cx - x;
                    float dy = cy - y;
                    float len = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1f) continue;
                    dx /= len;
                    dy /= len;
                    int ib = 0, ig = 0, ir = 0, inward = 0;
                    for (int step = 3; step <= 7; step += 2)
                    {
                        int xx = (int)Math.Round(x + dx * step);
                        int yy = (int)Math.Round(y + dy * step);
                        if ((uint)xx >= (uint)w || (uint)yy >= (uint)h) continue;
                        byte* ip = scan + yy * stride + xx * 4;
                        if (ip[3] <= 200) continue;
                        int c0 = ip[0], c1 = ip[1], c2 = ip[2];
                        int hi = c0, lo = c0;
                        if (c1 > hi) hi = c1;
                        if (c2 > hi) hi = c2;
                        if (c1 < lo) lo = c1;
                        if (c2 < lo) lo = c2;
                        int ilum = (c0 + c1 + c2) / 3;
                        if (ilum > 175 && hi - lo < 36)
                        {
                            ib = c0;
                            ig = c1;
                            ir = c2;
                            inward = 1;
                            break;
                        }
                    }
                    if (inward == 0) continue;
                    row[p] = (byte)ib;
                    row[p + 1] = (byte)ig;
                    row[p + 2] = (byte)ir;
                }
            }

            for (int y = top; y <= bottom; y++)
            {
                byte* row = scan + y * stride;
                for (int x = left; x <= right; x++)
                {
                    if (row[x * 4 + 3] == 1) row[x * 4 + 3] = 0;
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    static Bitmap[] LoadGif(string path)
    {
        using (Image img = Image.FromFile(path))
        {
            var dim = new FrameDimension(img.FrameDimensionsList[0]);
            int count = img.GetFrameCount(dim);
            var frames = new Bitmap[count];
            using (var attr = new ImageAttributes())
            {
                attr.SetColorKey(Color.Magenta, Color.Magenta);
                for (int i = 0; i < count; i++)
                {
                    img.SelectActiveFrame(dim, i);
                    var frame = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(frame))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImage(img,
                            new Rectangle(0, 0, img.Width, img.Height),
                            0, 0, img.Width, img.Height,
                            GraphicsUnit.Pixel, attr);
                    }
                    frames[i] = frame;
                }
            }
            return frames;
        }
    }

    void Tick()
    {
        if (ticking) return;
        ticking = true;
        try
        {
            TickCore();
        }
        finally
        {
            ticking = false;
        }
    }

    void ClearSingCache()
    {
        if (singScaled != null)
        {
            for (int i = 0; i < singScaled.Length; i++)
            {
                if (singScaled[i] != null) singScaled[i].Dispose();
            }
        }
        singScaled = null;
        singScaledW = 0;
        singScaledH = 0;
        singReadyFrame = -1;
        singReadyDip = -1f;
        if (singShifted != null)
        {
            singShifted.Dispose();
            singShifted = null;
        }
    }

    // Scale each singing frame once, then slide that bitmap by a fraction of a pixel.
    // The slide stays inside the transparent padding, so the belly edge is never resampled.
    Bitmap SingMoved(int frame, int iw, int ih, float dip)
    {
        if (iw < 1) iw = 1;
        if (ih < 1) ih = 1;
        if (singScaled == null || singScaledW != iw || singScaledH != ih)
        {
            ClearSingCache();
            singScaled = new Bitmap[form4.Length];
            singScaledW = iw;
            singScaledH = ih;
        }
        if (singScaled[frame] == null)
        {
            var scaled = new Bitmap(iw, ih, PixelFormat.Format32bppArgb);
            using (var sg = Graphics.FromImage(scaled))
            {
                sg.Clear(Color.Transparent);
                sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                sg.PixelOffsetMode = PixelOffsetMode.Half;
                sg.CompositingMode = CompositingMode.SourceOver;
                Bitmap raw = form4[frame];
                sg.DrawImage(raw,
                    new Rectangle(0, 0, iw, ih),
                    0, 0, raw.Width, raw.Height,
                    GraphicsUnit.Pixel);
            }
            singScaled[frame] = scaled;
            singReadyFrame = -1;
        }
        if (singShifted == null || singShifted.Width != iw || singShifted.Height != ih)
        {
            if (singShifted != null) singShifted.Dispose();
            singShifted = new Bitmap(iw, ih, PixelFormat.Format32bppArgb);
            singReadyFrame = -1;
        }
        if (singReadyFrame != frame || singReadyDip != dip)
        {
            ShiftDown(singScaled[frame], singShifted, dip);
            singReadyFrame = frame;
            singReadyDip = dip;
        }
        return singShifted;
    }

    static unsafe void ShiftDown(Bitmap src, Bitmap dst, float dip)
    {
        int w = src.Width;
        int h = src.Height;
        if (dip < 0f) dip = 0f;
        var rect = new Rectangle(0, 0, w, h);
        BitmapData sd = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData dd = dst.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte* s = (byte*)sd.Scan0;
            byte* d = (byte*)dd.Scan0;
            int ss = sd.Stride;
            int ds = dd.Stride;
            for (int y = 0; y < h; y++)
            {
                float sy = y - dip;
                int y0 = (int)Math.Floor(sy);
                float f = sy - y0;
                float w0 = 1f - f;
                int y1 = y0 + 1;
                byte* dp = d + y * ds;
                byte* r0 = ((uint)y0 < (uint)h) ? s + y0 * ss : null;
                byte* r1 = ((uint)y1 < (uint)h) ? s + y1 * ss : null;
                for (int x = 0; x < w; x++)
                {
                    int p = x * 4;
                    int b0 = 0, g0 = 0, r = 0, a0 = 0;
                    int b1 = 0, g1 = 0, rr = 0, a1 = 0;
                    if (r0 != null)
                    {
                        b0 = r0[p];
                        g0 = r0[p + 1];
                        r = r0[p + 2];
                        a0 = r0[p + 3];
                    }
                    if (r1 != null)
                    {
                        b1 = r1[p];
                        g1 = r1[p + 1];
                        rr = r1[p + 2];
                        a1 = r1[p + 3];
                    }
                    float af = a0 * w0 + a1 * f;
                    if (af < 0.5f)
                    {
                        dp[p] = 0;
                        dp[p + 1] = 0;
                        dp[p + 2] = 0;
                        dp[p + 3] = 0;
                        continue;
                    }
                    float inv = 1f / af;
                    dp[p] = ClampByte((b0 * a0 * w0 + b1 * a1 * f) * inv);
                    dp[p + 1] = ClampByte((g0 * a0 * w0 + g1 * a1 * f) * inv);
                    dp[p + 2] = ClampByte((r * a0 * w0 + rr * a1 * f) * inv);
                    dp[p + 3] = ClampByte(af);
                }
            }
        }
        finally
        {
            src.UnlockBits(sd);
            dst.UnlockBits(dd);
        }
    }

    static byte ClampByte(float v)
    {
        if (v <= 0f) return 0;
        if (v >= 255f) return 255;
        return (byte)(v + 0.5f);
    }

    void TickCore()
    {
        long now = clock.ElapsedMilliseconds;
        int elapsed = (int)(now - lastClock);
        lastClock = now;
        if (elapsed < 1) elapsed = 1;
        if (elapsed > 33) elapsed = 33;

        if (hidden)
        {
            if (!hiddenApplied)
            {
                ShowWindow(Handle, 0);
                hiddenApplied = true;
            }
            return;
        }
        if (hiddenApplied)
        {
            ShowWindow(Handle, 8);
            hiddenApplied = false;
        }

        Bitmap[] anim = null;
        if (formIndex == 1) anim = form2;
        else if (formIndex == 2) anim = form4;
        if (anim != null && anim.Length > 1)
        {
            animTime += elapsed;
            if (formIndex == 2)
            {
                // One frame per tick, so the faster dance still shows every frame.
                if (animTime >= SingFrameMs)
                {
                    animTime -= SingFrameMs;
                    if (animTime > SingFrameMs) animTime = SingFrameMs;
                    animFrame++;
                    if (animFrame >= anim.Length) animFrame = 0;
                }
            }
            else
            {
                while (animTime >= AnimFrameMs)
                {
                    animTime -= AnimFrameMs;
                    animFrame++;
                    if (animFrame >= anim.Length) animFrame = 0;
                }
            }
        }

        Point cursor = CursorPosition();

        float hang = cursorScale * 0.45f;
        rope.SetAnchor(cursor.X + hang * 0.25f, cursor.Y + hang, !placed);
        placed = true;
        rope.Advance(elapsed / 1000.0);

        float angle = rope.Angle();
        float px = (float)rope.X[rope.Points - 1];
        float py = (float)rope.Y[rope.Points - 1];
        Bitmap picture = CurrentPicture();
        float dw = picture.Width * scale;
        float dh = picture.Height * scale;

        float minX = px, minY = py, maxX = px, maxY = py;
        float ax = anchorX, ay = anchorY;
        float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
        float[] cx = { -ax, dw - ax, dw - ax, -ax };
        float[] cy = { -ay, -ay, dh - ay, dh - ay };
        for (int i = 0; i < 4; i++)
        {
            float rx = px + cx[i] * cos - cy[i] * sin;
            float ry = py + cx[i] * sin + cy[i] * cos;
            if (rx < minX) minX = rx;
            if (ry < minY) minY = ry;
            if (rx > maxX) maxX = rx;
            if (ry > maxY) maxY = ry;
        }
        for (int i = 0; i < rope.Points; i++)
        {
            float x = (float)rope.X[i], y = (float)rope.Y[i];
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        const int pad = 12;
        int tightX = (int)Math.Floor(minX) - pad;
        int tightY = (int)Math.Floor(minY) - pad;
        int tightW = (int)Math.Ceiling(maxX) - tightX + pad;
        int tightH = (int)Math.Ceiling(maxY) - tightY + pad;
        if (tightW < 1) tightW = 1;
        if (tightH < 1) tightH = 1;

        int winX = tightX;
        int winY = tightY;
        int winR = tightX + tightW;
        int winB = tightY + tightH;
        if (hasLast)
        {
            int area = Math.Max(tightW, lastW) * Math.Max(tightH, lastH);
            if (area < 1500 * 1500)
            {
                if (lastX < winX) winX = lastX;
                if (lastY < winY) winY = lastY;
                if (lastX + lastW > winR) winR = lastX + lastW;
                if (lastY + lastH > winB) winB = lastY + lastH;
            }
        }
        hasLast = true;
        lastX = tightX;
        lastY = tightY;
        lastW = tightW;
        lastH = tightH;
        int winW = winR - winX;
        int winH = winB - winY;

        using (var bmp = new Bitmap(winW, winH, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.CompositingMode = CompositingMode.SourceOver;

            var pts = new PointF[rope.Points];
            for (int i = 0; i < rope.Points; i++)
                pts[i] = new PointF((float)rope.X[i] - winX, (float)rope.Y[i] - winY);
            using (var pen = new Pen(ropeColor, 2.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (pts.Length >= 3) g.DrawCurve(pen, pts, 0.45f);
                else g.DrawLines(pen, pts);
            }

            var state = g.Save();
            g.TranslateTransform(px - winX, py - winY);
            g.RotateTransform(angle * (180f / (float)Math.PI));
            g.TranslateTransform(-anchorX, -anchorY);
            if (formIndex == 2)
            {
                int iw = (int)Math.Round(dw);
                int ih = (int)Math.Round(dh);
                if (iw < 1) iw = 1;
                if (ih < 1) ih = 1;
                float dip = 0f;
                if (form4 != null && form4.Length > 1)
                {
                    double turn = animFrame / (double)form4.Length;
                    double wave = 0.5 - 0.5 * Math.Cos(turn * 2.0 * Math.PI);
                    dip = (float)(wave * 18.0 * scale);
                }
                // Bob is inside the bitmap. A 1:1 draw keeps the belly edge from being filtered away.
                Bitmap moved = SingMoved(animFrame, iw, ih, dip);
                g.DrawImage(moved, new Rectangle(0, 0, iw, ih), 0, 0, iw, ih, GraphicsUnit.Pixel);
            }
            else
            {
                g.DrawImage(picture, 0, 0, dw, dh);
            }
            g.Restore(state);

            Premultiply(bmp);
            Present(bmp, winX, winY);
        }
    }

    // The spoon sits above the crown, off to one side. Hang from the head fur
    // so the rope meets the top of the head and leaves the spoon clear.
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

        long sx = 0, sy = 0, n = 0;
        int bottom = Math.Min(b.Height - 1, headY + 5);
        for (int y = headY; y <= bottom; y++)
        {
            int x = 0;
            while (x < b.Width && b.GetPixel(x, y).A <= 128) x++;
            while (x < b.Width && b.GetPixel(x, y).A > 128)
            {
                sx += x;
                sy += y;
                n++;
                x++;
            }
        }
        if (n == 0) return new PointF(b.Width / 2f, headY);
        return new PointF(sx / (float)n, sy / (float)n);
    }

    static void Premultiply(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int bytes = Math.Abs(data.Stride) * bmp.Height;
        byte[] px = new byte[bytes];
        Marshal.Copy(data.Scan0, px, 0, bytes);
        for (int i = 0; i < bytes; i += 4)
        {
            byte a = px[i + 3];
            if (a == 0 || a == 255) continue;
            px[i] = (byte)(px[i] * a / 255);
            px[i + 1] = (byte)(px[i + 1] * a / 255);
            px[i + 2] = (byte)(px[i + 2] * a / 255);
        }
        Marshal.Copy(px, 0, data.Scan0, bytes);
        bmp.UnlockBits(data);
    }

    void Present(Bitmap bmp, int x, int y)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr mem = CreateCompatibleDC(screen);
        var header = new BitmapInfoHeader();
        header.biSize = 40;
        header.biWidth = bmp.Width;
        header.biHeight = -bmp.Height;
        header.biPlanes = 1;
        header.biBitCount = 32;
        IntPtr bits;
        IntPtr hBmp = CreateDIBSection(screen, ref header, 0, out bits, IntPtr.Zero, 0);
        if (hBmp == IntPtr.Zero)
        {
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
            return;
        }
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int rowBytes = bmp.Width * 4;
        for (int row = 0; row < bmp.Height; row++)
        {
            IntPtr src = new IntPtr(data.Scan0.ToInt64() + (long)row * data.Stride);
            IntPtr dst = new IntPtr(bits.ToInt64() + (long)row * rowBytes);
            CopyMemory(dst, src, (uint)rowBytes);
        }
        bmp.UnlockBits(data);
        IntPtr old = SelectObject(mem, hBmp);
        try
        {
            var size = new Size(bmp.Width, bmp.Height);
            var dst = new Point(x, y);
            var srcPt = new Point(0, 0);
            var blend = new BlendFunction
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = 1
            };
            UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref srcPt, 0, ref blend, 2);
        }
        finally
        {
            SelectObject(mem, old);
            DeleteObject(hBmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    static Icon MakeColdyIcon(Bitmap source, out MemoryStream kept)
    {
        Rectangle content = ContentBounds(source);
        int[] sizes = { 16, 32, 48, 256 };
        byte[][] blobs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
            blobs[i] = IconPng(source, content, sizes[i]);
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int side = sizes[i] >= 256 ? 0 : sizes[i];
            bw.Write((byte)side);
            bw.Write((byte)side);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write(blobs[i].Length);
            bw.Write(offset);
            offset += blobs[i].Length;
        }
        for (int i = 0; i < blobs.Length; i++) bw.Write(blobs[i]);
        bw.Flush();
        ms.Position = 0;
        kept = ms;
        return new Icon(ms);
    }

    static byte[] IconPng(Bitmap source, Rectangle content, int size)
    {
        using (var square = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(square))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int margin = Math.Max(1, size / 16);
            int inner = size - margin * 2;
            int dw = inner;
            int dh = inner;
            if (content.Width > 0 && content.Height > 0)
            {
                if (content.Width * inner >= content.Height * inner)
                {
                    dh = inner * content.Height / content.Width;
                    if (dh < 1) dh = 1;
                }
                else
                {
                    dw = inner * content.Width / content.Height;
                    if (dw < 1) dw = 1;
                }
            }
            var dest = new Rectangle((size - dw) / 2, (size - dh) / 2, dw, dh);
            g.DrawImage(source, dest, content, GraphicsUnit.Pixel);
            using (var png = new MemoryStream())
            {
                square.Save(png, ImageFormat.Png);
                return png.ToArray();
            }
        }
    }

    static Rectangle ContentBounds(Bitmap bitmap)
    {
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 16) continue;
                if (x < left) left = x;
                if (y < top) top = y;
                if (x > right) right = x;
                if (y > bottom) bottom = y;
            }
        }
        if (right < 0) return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    static Point CursorPosition()
    {
        Point p;
        GetCursorPos(out p);
        return p;
    }

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst,
        ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);

    [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
    static extern void CopyMemory(IntPtr dest, IntPtr src, uint length);

    [DllImport("winmm.dll")]
    static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    static extern uint timeEndPeriod(uint period);

    [StructLayout(LayoutKind.Sequential)]
    struct BitmapInfoHeader
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }
}

// Verlet rope. Distances stay fixed, so the rope bends instead of stretching,
// and the last point is where Coldy hangs.
class Rope
{
    const double FixedStep = 1.0 / 240.0;
    const double MaxFrameDelta = 0.033;
    const int MaxSteps = 8;
    const double VelocityDamping = 0.993;
    const double AirDamping = 0.960;
    const double GravityScale = 0.864;
    const double AnchorVelocityTransfer = 0.225;
    const double ConstraintPreserve = 0.88;
    const double StretchGuardRatio = 1.18;
    const double AbsoluteStretchRatio = 1.32;
    const double LongSegmentReference = 18;
    const double LongSegmentTight = 1.10;
    const double MaxPointDelta = 28;
    const double MaxPendantDelta = 38;
    const int MinimumIterations = 5;
    const int ConstraintIterations = 10;
    const int StretchGuardIterations = 2;
    const int LongSegmentExtra = 8;
    const double RestMoveThreshold = 0.05;
    const double RestAngleDegrees = 1;
    const double RestDelay = 0.3;

    public int Segments;
    public int Points;
    public double[] X, Y, OldX, OldY;
    public float RestLen;
    public float Gravity;
    public float SegLen;
    public float AnchorX, AnchorY, TargetX, TargetY, PrevAnchorX, PrevAnchorY;
    double accumulator;
    double stillSeconds;
    bool frozen;

    public Rope(int segments, float restLen, float gravity)
    {
        Segments = Math.Max(1, segments);
        Points = Segments + 1;
        RestLen = restLen;
        Gravity = gravity;
        SegLen = RestLen / Segments;
        X = new double[Points];
        Y = new double[Points];
        OldX = new double[Points];
        OldY = new double[Points];
    }

    public void SetAnchor(float x, float y, bool instant)
    {
        if (instant)
        {
            float dx = x - AnchorX, dy = y - AnchorY;
            AnchorX = TargetX = PrevAnchorX = x;
            AnchorY = TargetY = PrevAnchorY = y;
            for (int i = 0; i < Points; i++)
            {
                X[i] += dx; Y[i] += dy;
                OldX[i] += dx; OldY[i] += dy;
            }
        }
        else
        {
            TargetX = x;
            TargetY = y;
        }
    }

    public void SetLength(float restLen)
    {
        if (restLen < 1f) restLen = 1f;
        RestLen = restLen;
        // Roughly one joint per 40 px: a 1000 px rope bends smoothly instead
        // of becoming a handful of long straight rods.
        int segments = (int)Math.Round(restLen / 40f) + 2;
        if (segments < 2) segments = 2;
        if (segments > 28) segments = 28;
        Resize(segments);
        SegLen = RestLen / Segments;
        frozen = false;
        stillSeconds = 0;
    }

    // Keep the current shape while changing the number of joints, so dragging
    // the length slider never makes the rope snap back to a straight line.
    public void Resize(int segments)
    {
        if (segments < 2) segments = 2;
        if (segments > 28) segments = 28;
        if (segments == Segments) return;
        int points = segments + 1;
        double[] nx = new double[points], ny = new double[points];
        double[] nox = new double[points], noy = new double[points];
        for (int i = 0; i < points; i++)
        {
            double pos = (double)i * Segments / segments;
            int j = (int)pos;
            if (j >= Segments) j = Segments - 1;
            double f = pos - j;
            nx[i] = X[j] + (X[j + 1] - X[j]) * f;
            ny[i] = Y[j] + (Y[j + 1] - Y[j]) * f;
            nox[i] = OldX[j] + (OldX[j + 1] - OldX[j]) * f;
            noy[i] = OldY[j] + (OldY[j + 1] - OldY[j]) * f;
        }
        Segments = segments;
        Points = points;
        X = nx; Y = ny; OldX = nox; OldY = noy;
        SegLen = RestLen / Segments;
        frozen = false;
        stillSeconds = 0;
    }

    public void Reset()
    {
        for (int i = 0; i < Points; i++)
        {
            X[i] = AnchorX;
            Y[i] = AnchorY + SegLen * i;
            OldX[i] = X[i];
            OldY[i] = Y[i];
        }
        PrevAnchorX = AnchorX;
        PrevAnchorY = AnchorY;
        accumulator = 0;
        stillSeconds = 0;
        frozen = false;
    }

    public float Angle()
    {
        int last = Points - 1;
        int prev = last > 0 ? last - 1 : last;
        double dx = X[last] - X[prev];
        double dy = Y[last] - Y[prev];
        double angle = Math.Atan2(dy, dx) - Math.PI / 2;
        if (double.IsNaN(angle) || double.IsInfinity(angle)) return 0;
        return (float)angle;
    }

    public void Advance(double frameDelta)
    {
        if (frameDelta <= 0) return;
        accumulator += Math.Min(frameDelta, MaxFrameDelta);
        int steps = Math.Min(MaxSteps, (int)Math.Floor(accumulator / FixedStep));
        if (steps <= 0) return;

        float startX = AnchorX, startY = AnchorY;
        float endX = TargetX, endY = TargetY;
        for (int step = 1; step <= steps; step++)
        {
            float t = step / (float)steps;
            AnchorX = startX + (endX - startX) * t;
            AnchorY = startY + (endY - startY) * t;
            Step(FixedStep);
        }
        accumulator -= steps * FixedStep;
        if (accumulator >= FixedStep) accumulator = 0;
    }

    void Step(double dt)
    {
        int last = Points - 1;
        float moveX = AnchorX - PrevAnchorX;
        float moveY = AnchorY - PrevAnchorY;
        if (TryFreeze(moveX, moveY, dt)) return;

        X[0] = AnchorX;
        Y[0] = AnchorY;
        OldX[0] = AnchorX - moveX * AnchorVelocityTransfer;
        OldY[0] = AnchorY - moveY * AnchorVelocityTransfer;
        PrevAnchorX = AnchorX;
        PrevAnchorY = AnchorY;

        double damping = Math.Min(0.997, Math.Max(0.94, VelocityDamping * AirDamping));
        double gStep = Gravity * GravityScale * dt * dt;
        for (int i = 1; i < Points; i++)
        {
            double vx = (X[i] - OldX[i]) * damping;
            double vy = (Y[i] - OldY[i]) * damping;
            OldX[i] = X[i];
            OldY[i] = Y[i];
            X[i] += vx;
            Y[i] += vy + gStep;
            ClampVelocity(i, last);
        }

        int iterations = Math.Max(MinimumIterations, ConstraintIterations);
        double move = Math.Sqrt(moveX * moveX + moveY * moveY);
        if (move > 24) iterations += Math.Min(8, (int)Math.Ceiling((move - 24) / 26));
        if (SegLen > LongSegmentReference)
        {
            double s = (SegLen - LongSegmentReference) / LongSegmentReference;
            iterations += Math.Min(LongSegmentExtra, (int)Math.Ceiling(s * 4));
        }

        for (int iter = 0; iter < iterations; iter++)
        {
            X[0] = AnchorX;
            Y[0] = AnchorY;
            for (int i = 0; i < Points - 1; i++)
                DistanceConstraint(i, i + 1, SegLen, ConstraintPreserve);
        }

        double guard = SegLen <= LongSegmentReference
            ? StretchGuardRatio
            : StretchGuardRatio - Math.Min(StretchGuardRatio - LongSegmentTight,
                ((SegLen - LongSegmentReference) / LongSegmentReference) * 0.05);
        double maxSeg = SegLen * guard;
        for (int iter = 0; iter < StretchGuardIterations; iter++)
        {
            bool adjusted = false;
            X[0] = AnchorX;
            Y[0] = AnchorY;
            for (int i = 0; i < Points - 1; i++)
            {
                double dx = X[i + 1] - X[i];
                double dy = Y[i + 1] - Y[i];
                if (dx * dx + dy * dy > maxSeg * maxSeg)
                {
                    DistanceConstraint(i, i + 1, maxSeg, 1);
                    adjusted = true;
                }
            }
            if (!adjusted) break;
        }

        X[0] = OldX[0] = AnchorX;
        Y[0] = OldY[0] = AnchorY;

        double ex = X[last] - AnchorX;
        double ey = Y[last] - AnchorY;
        double dist = Math.Sqrt(ex * ex + ey * ey);
        double maxDist = Math.Max(SegLen, RestLen * AbsoluteStretchRatio);
        if (dist > maxDist && dist > 0.0001)
        {
            double sx = AnchorX + ex / dist * maxDist;
            double sy = AnchorY + ey / dist * maxDist;
            OldX[last] += sx - X[last];
            OldY[last] += sy - Y[last];
            X[last] = sx;
            Y[last] = sy;
        }
    }

    void ClampVelocity(int i, int pendantIndex)
    {
        double vx = X[i] - OldX[i];
        double vy = Y[i] - OldY[i];
        double maxDelta = i >= pendantIndex ? MaxPendantDelta : MaxPointDelta;
        double len = Math.Sqrt(vx * vx + vy * vy);
        if (len <= maxDelta || len <= 0.0001) return;
        double k = maxDelta / len;
        OldX[i] = X[i] - vx * k;
        OldY[i] = Y[i] - vy * k;
    }

    void DistanceConstraint(int a, int b, double target, double preserve)
    {
        double dx = X[b] - X[a];
        double dy = Y[b] - Y[a];
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist <= 0.0001) return;
        double correction = (dist - target) / dist;
        double aWeight = a == 0 ? 0 : 0.5;
        double bWeight = a == 0 ? 1 : 0.5;
        double offX = dx * correction, offY = dy * correction;
        if (a != 0)
        {
            X[a] += offX * aWeight;
            Y[a] += offY * aWeight;
            OldX[a] += offX * aWeight * preserve;
            OldY[a] += offY * aWeight * preserve;
        }
        X[b] -= offX * bWeight;
        Y[b] -= offY * bWeight;
        OldX[b] -= offX * bWeight * preserve;
        OldY[b] -= offY * bWeight * preserve;
    }

    bool TryFreeze(float moveX, float moveY, double dt)
    {
        if (Math.Abs(moveX) > RestMoveThreshold || Math.Abs(moveY) > RestMoveThreshold)
        {
            stillSeconds = 0;
            frozen = false;
            return false;
        }
        int last = Points - 1;
        double dx = X[last] - AnchorX;
        double dy = Y[last] - AnchorY;
        double len = Math.Sqrt(dx * dx + dy * dy);
        double tol = Math.Cos(RestAngleDegrees * Math.PI / 180);
        if (len <= 0.0001 || dy <= 0 || dy / len < tol)
        {
            stillSeconds = 0;
            frozen = false;
            return false;
        }
        if (frozen) return true;
        stillSeconds += Math.Max(0, dt);
        if (stillSeconds < RestDelay) return false;
        frozen = true;
        for (int i = 0; i < Points; i++) { OldX[i] = X[i]; OldY[i] = Y[i]; }
        PrevAnchorX = AnchorX;
        PrevAnchorY = AnchorY;
        return true;
    }
}

class SettingsForm : Form
{
    readonly PendantForm pendant = new PendantForm();
    readonly System.Windows.Forms.Timer hotkeyTimer = new System.Windows.Forms.Timer();
    readonly IceSlider ropeBar = new IceSlider();
    readonly IceSlider sizeBar = new IceSlider();
    readonly IceSlider cursorSizeBar = new IceSlider();
    readonly ComboBox formBox = new ComboBox();
    readonly ComboBox cursorBox = new ComboBox();
    readonly Button colorButton = new Button();
    readonly Panel colorSwatch = new Panel();
    readonly Button hotkeyButton = new Button();
    readonly Label ropeValue = new Label();
    readonly Label sizeValue = new Label();
    readonly Label cursorSizeValue = new Label();
    readonly Button hideButton = new Button();
    readonly CheckBox startupBox = new CheckBox();
    readonly CheckBox backgroundBox = new CheckBox();
    readonly MarkButton minButton = new MarkButton();
    readonly MarkButton closeButton = new MarkButton();
    readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
    Bitmap logo;
    int hairlineY;
    int markDrawH;
    int editorX;
    int labelWidth;
    bool draggingWindow;
    Point dragOffset;
    int hotkeyVk = 0x77;
    int hotkeyVk2;
    int capturedVk;
    int capturedCount;
    bool capturingHotkey;
    bool chordDown;
    bool loading = true;
    bool releaseNotesSeen;
    bool cursorApplied;
    bool exitRequested;
    bool startHidden;
    bool allowShow;
    bool updateBusy;
    bool updating;
    string settingsPath;
    Color ropeColor = Color.FromArgb(55, 55, 62);
    readonly int[] customColors = DefaultCustomColors();
    int boxHeight = 34;
    static readonly Color Ice = Color.FromArgb(231, 243, 248);
    static readonly Color Ink = Color.FromArgb(45, 58, 68);
    static readonly Color Field = Color.White;
    static readonly Color Line = Color.FromArgb(214, 226, 234);
    static readonly Color Muted = Color.FromArgb(90, 104, 116);

    public SettingsForm()
    {
        Text = "Coldy";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        Font = new Font("Microsoft YaHei UI", 9f);
        BackColor = Ice;
        ForeColor = Ink;
        KeyPreview = true;
        string dir = Path.GetDirectoryName(Application.ExecutablePath);
        settingsPath = Path.Combine(dir, "coldy.ini");
        string logoPath = Path.Combine(dir, "assets", "coldy-logo.png");
        if (File.Exists(logoPath))
        {
            try { logo = new Bitmap(logoPath); }
            catch { logo = null; }
        }

        boxHeight = TextRenderer.MeasureText("选择颜色", Font).Height + 16;
        if (boxHeight < 28) boxHeight = 28;
        labelWidth = TextRenderer.MeasureText("隐藏快捷键", Font).Width + 16;
        editorX = 28 + labelWidth;
        int valueW = TextRenderer.MeasureText("420", Font).Width + 16;
        int sliderW = 220;
        int clientW = editorX + sliderW + 10 + valueW + 28;
        int hotkeyW = ButtonWidth("请按两个键");
        int hideW = ButtonWidth("隐藏人物");
        int rowW = editorX + hotkeyW + 12 + hideW + 28;
        if (clientW < rowW) clientW = rowW;
        int colorW = ButtonWidth("选择颜色");
        int colorRow = editorX + colorW + 8 + boxHeight + 28;
        if (clientW < colorRow) clientW = colorRow;
        if (clientW < 460) clientW = 460;
        ClientSize = new Size(clientW, 720);

        int chrome = boxHeight;
        if (chrome > 32) chrome = 32;
        if (chrome < 26) chrome = 26;
        closeButton.CloseMark = true;
        closeButton.SetBounds(clientW - 16 - chrome, 12, chrome, chrome);
        minButton.SetBounds(closeButton.Left - 6 - chrome, 12, chrome, chrome);
        Controls.Add(minButton);
        Controls.Add(closeButton);

        Icon = pendant.AppIcon;
        startHidden = StartedInTray();

        int bodyH = boxHeight * 2 + 4;
        if (bodyH < 64) bodyH = 64;
        markDrawH = bodyH * 380 / 242;
        hairlineY = 12 + markDrawH + 12;
        int y = hairlineY + 14;
        int gap = 8;
        int right = clientW - 28;
        int fieldW = right - editorX;
        if (fieldW > 280) fieldW = 280;

        SetupBar(ropeBar, 0, 1000, 120);
        SetupBar(sizeBar, 30, 420, 190);
        SetupBar(cursorSizeBar, 16, 128, 32);
        PrepareButton(colorButton);
        colorButton.Text = "选择颜色";
        PrepareCombo(formBox);
        // The singing form (cocococo) is archived in assets/ but no longer
        // offered; only the two base forms remain in the dropdown.
        formBox.Items.AddRange(new object[] { "形态 1", "形态 2" });
        formBox.SelectedIndex = 0;
        PrepareCombo(cursorBox);
        cursorBox.Items.AddRange(new object[] { "系统默认", "Ice", "Pixel", "Frost's Descent" });
        cursorBox.SelectedIndex = 0;
        PrepareButton(hotkeyButton);
        hotkeyButton.Text = "F8";
        PrepareButton(hideButton);
        hideButton.Text = "隐藏人物";
        Controls.Add(hideButton);

        y = AddSection(y, "角色");
        AddRow(y, "人物大小", sizeBar, sliderW, false);
        PlaceValue(sizeValue, y, sliderW, valueW);
        y += boxHeight + gap;
        AddRow(y, "形态", formBox, fieldW < 180 ? fieldW : 180, true);
        y += boxHeight + gap;
        AddRow(y, "隐藏快捷键", hotkeyButton, hotkeyW, true);
        FitButton(hotkeyButton, "请按两个键");
        FitButton(hideButton, "隐藏人物");
        hideButton.SetBounds(right - hideButton.Width, y, hideButton.Width, boxHeight);
        y += boxHeight + 14;

        y = AddSection(y, "绳子");
        AddRow(y, "绳子长度", ropeBar, sliderW, false);
        PlaceValue(ropeValue, y, sliderW, valueW);
        y += boxHeight + gap;
        AddRow(y, "绳子颜色", colorButton, colorW, true);
        FitButton(colorButton, "选择颜色");
        colorSwatch.BorderStyle = BorderStyle.FixedSingle;
        colorSwatch.SetBounds(colorButton.Right + 8, colorButton.Top, boxHeight, boxHeight);
        Controls.Add(colorSwatch);
        ShowRopeColor();
        y += boxHeight + 14;

        y = AddSection(y, "光标");
        AddRow(y, "光标", cursorBox, fieldW < 180 ? fieldW : 180, true);
        y += boxHeight + gap;
        AddRow(y, "光标大小", cursorSizeBar, sliderW, false);
        PlaceValue(cursorSizeValue, y, sliderW, valueW);
        y += boxHeight + 14;

        startupBox.Text = "开机自启动";
        startupBox.AutoSize = true;
        startupBox.BackColor = Ice;
        startupBox.ForeColor = Ink;
        startupBox.SetBounds(28, y + 4, 120, boxHeight);
        Controls.Add(startupBox);
        backgroundBox.Text = "后台运行";
        backgroundBox.AutoSize = true;
        backgroundBox.BackColor = Ice;
        backgroundBox.ForeColor = Ink;
        backgroundBox.SetBounds(168, y + 4, 120, boxHeight);
        Controls.Add(backgroundBox);
        y += boxHeight + 20;
        ClientSize = new Size(clientW, y);

        LoadSettings();
        startupBox.Checked = StartupEnabled();
        ShowHotkey();
        ShowRopeColor();
        RefreshNumbers();
        ApplyBackground();
        loading = false;
        ApplyAll(true);

        ropeBar.ValueChanged += delegate { if (!loading) ApplyAll(false); };
        colorButton.Click += delegate { PickRopeColor(); };
        sizeBar.ValueChanged += delegate { if (!loading) ApplyAll(false); };
        cursorSizeBar.ValueChanged += delegate { if (!loading) ApplyAll(true); };
        formBox.SelectedIndexChanged += delegate
        {
            if (loading) return;
            ApplyAll(false);
            Invalidate(new Rectangle(0, 0, ClientSize.Width, hairlineY + 1));
        };
        cursorBox.SelectedIndexChanged += delegate { if (!loading) ApplyAll(true); };
        hotkeyButton.Click += delegate { BeginHotkeyCapture(); };
        hideButton.Click += delegate { ToggleHidden(); };
        minButton.Click += delegate
        {
            if (backgroundBox.Checked) HideToTray();
            else WindowState = FormWindowState.Minimized;
        };
        closeButton.Click += delegate
        {
            if (backgroundBox.Checked) HideToTray();
            else RequestExit();
        };
        startupBox.CheckedChanged += delegate
        {
            if (loading) return;
            SetStartup(startupBox.Checked);
            SaveSettings();
        };
        backgroundBox.CheckedChanged += delegate
        {
            if (loading) return;
            ApplyBackground();
            SaveSettings();
        };

        var menu = pendant.TrayIcon.ContextMenu;
        menu.MenuItems.Add(new MenuItem("显示控制台", delegate { ShowSettings(); }));
        menu.MenuItems.Add(new MenuItem("隐藏人物", delegate { ToggleHidden(); }));
        menu.MenuItems.Add(new MenuItem("退出", delegate { RequestExit(); }));
        pendant.TrayIcon.DoubleClick += delegate { ShowSettings(); };

        hotkeyTimer.Interval = 30;
        hotkeyTimer.Tick += delegate { PollHotkey(); };
        hotkeyTimer.Start();
        updateTimer.Interval = 10 * 60 * 1000;
        updateTimer.Tick += delegate { CheckForUpdate(); };
        updateTimer.Start();
        var firstCheck = new System.Windows.Forms.Timer();
        firstCheck.Interval = 4000;
        firstCheck.Tick += delegate
        {
            firstCheck.Stop();
            firstCheck.Dispose();
            CheckForUpdate();
        };
        firstCheck.Start();
        var listener = new Thread((ThreadStart)delegate
        {
            while (!IsDisposed)
            {
                Program.WaitForShow();
                if (IsDisposed) return;
                try { BeginInvoke((MethodInvoker)delegate { ShowSettings(); }); }
                catch { return; }
            }
        });
        listener.IsBackground = true;
        listener.Start();
        MatchBoxHeights();
        ApplyRound();
        if (NotesRequested() || !releaseNotesSeen)
            Load += delegate { BeginInvoke((MethodInvoker)ShowReleaseNotes); };

        pendant.Show();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000;
            return cp;
        }
    }

    void MatchBoxHeights()
    {
        int item = boxHeight - 8;
        if (item < 8) item = 8;
        formBox.ItemHeight = item;
        cursorBox.ItemHeight = item;
        colorButton.Height = boxHeight;
        hotkeyButton.Height = boxHeight;
        hideButton.Height = boxHeight;
        formBox.Height = boxHeight;
        cursorBox.Height = boxHeight;
        colorSwatch.SetBounds(colorButton.Right + 8, colorButton.Top, boxHeight, boxHeight);
        hideButton.Top = hotkeyButton.Top;
    }

    int AddSection(int y, string title)
    {
        var bar = new Panel();
        bar.BackColor = Color.FromArgb(150, 200, 226);
        bar.SetBounds(28, y + 5, 3, 14);
        Controls.Add(bar);
        var label = new Label();
        label.Text = title;
        label.AutoSize = false;
        label.BackColor = Ice;
        label.ForeColor = Ink;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.SetBounds(38, y, 160, 22);
        Controls.Add(label);
        return y + 22;
    }

    void PollHotkey()
    {
        if (capturingHotkey || hotkeyVk == 0) return;
        bool down = (GetAsyncKeyState(hotkeyVk) & 0x8000) != 0;
        if (hotkeyVk2 != 0)
            down = down && (GetAsyncKeyState(hotkeyVk2) & 0x8000) != 0;
        if (down && !chordDown) ToggleHidden();
        chordDown = down;
    }

    void AddRow(int y, string caption, Control editor, int editorWidth, bool box)
    {
        var label = new Label();
        label.Text = caption;
        label.AutoSize = false;
        label.BackColor = Ice;
        label.ForeColor = Ink;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.SetBounds(28, y, labelWidth, boxHeight);
        int editorH = box ? boxHeight : editor.Height;
        int editorY = y + (boxHeight - editorH) / 2;
        editor.SetBounds(editorX, editorY, editorWidth, editorH);
        Controls.Add(label);
        Controls.Add(editor);
    }

    void PlaceValue(Label label, int y, int sliderW, int valueW)
    {
        label.AutoSize = false;
        label.BackColor = Ice;
        label.ForeColor = Ink;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.SetBounds(editorX + sliderW + 10, y, valueW, boxHeight);
        Controls.Add(label);
    }

    int ButtonWidth(string text)
    {
        int width = TextRenderer.MeasureText(text, Font).Width + 28;
        if (width < 96) width = 96;
        return width;
    }

    static void PrepareButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = Field;
        button.ForeColor = Ink;
        button.FlatAppearance.BorderColor = Color.FromArgb(196, 214, 224);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 236, 244);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(206, 228, 238);
    }

    static void PrepareCombo(ComboBox box)
    {
        box.IntegralHeight = true;
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.FlatStyle = FlatStyle.Flat;
        box.BackColor = Field;
        box.ForeColor = Ink;
        box.DropDownClosed += delegate
        {
            Control parent = box.Parent;
            if (parent != null) parent.SelectNextControl(box, true, true, true, true);
        };
    }

    void SetupBar(IceSlider bar, int min, int max, int value)
    {
        bar.Minimum = min;
        bar.Maximum = max;
        bar.Value = value;
        bar.Height = 28;
    }

    void PickRopeColor()
    {
        using (var dialog = new ColorDialog())
        {
            dialog.AllowFullOpen = true;
            dialog.FullOpen = false;
            dialog.AnyColor = true;
            dialog.SolidColorOnly = false;
            dialog.Color = ropeColor;
            dialog.CustomColors = (int[])customColors.Clone();
            DialogResult result = dialog.ShowDialog(this);
            if (dialog.CustomColors != null && dialog.CustomColors.Length > 0)
            {
                int count = Math.Min(customColors.Length, dialog.CustomColors.Length);
                for (int i = 0; i < count; i++) customColors[i] = dialog.CustomColors[i];
            }
            if (result != DialogResult.OK)
            {
                SaveSettings();
                return;
            }
            ropeColor = dialog.Color;
            ShowRopeColor();
            ApplyAll(false);
        }
    }

    static int[] DefaultCustomColors()
    {
        int[] colors = new int[16];
        for (int i = 0; i < colors.Length; i++) colors[i] = 0xFFFFFF;
        return colors;
    }

    void ParseCustomColors(string val)
    {
        if (val == null) return;
        string[] parts = val.Split(',');
        for (int i = 0; i < parts.Length && i < customColors.Length; i++)
        {
            int color;
            if (int.TryParse(parts[i].Trim(), out color) && color >= 0) customColors[i] = color;
        }
    }

    void ShowRopeColor()
    {
        colorSwatch.BackColor = ropeColor;
    }

    void FitButton(Button button, string text)
    {
        Size measured = TextRenderer.MeasureText(text, Font);
        int width = measured.Width + 28;
        if (width < 96) width = 96;
        button.SetBounds(button.Left, button.Top, width, boxHeight);
    }

    static Color ParseColor(string val)
    {
        Color fallback = Color.FromArgb(55, 55, 62);
        if (val == null) return fallback;
        string[] parts = val.Split(',');
        if (parts.Length != 3) return fallback;
        int r, g, b;
        if (!int.TryParse(parts[0].Trim(), out r)) return fallback;
        if (!int.TryParse(parts[1].Trim(), out g)) return fallback;
        if (!int.TryParse(parts[2].Trim(), out b)) return fallback;
        if (r < 0) r = 0;
        if (g < 0) g = 0;
        if (b < 0) b = 0;
        if (r > 255) r = 255;
        if (g > 255) g = 255;
        if (b > 255) b = 255;
        return Color.FromArgb(r, g, b);
    }

    void RefreshNumbers()
    {
        ropeValue.Text = ropeBar.Value.ToString();
        sizeValue.Text = sizeBar.Value.ToString();
        cursorSizeValue.Text = cursorSizeBar.Value.ToString();
        cursorSizeBar.Enabled = cursorBox.SelectedIndex != 0;
    }

    void ApplyAll(bool cursor)
    {
        RefreshNumbers();
        Text = "Coldy";
        pendant.SetName("Coldy");
        pendant.SetRopeLength(ropeBar.Value);
        pendant.SetRopeColor(ropeColor);
        pendant.SetDisplayHeight(sizeBar.Value);
        pendant.SetForm(formBox.SelectedIndex);
        pendant.SetCursorSize(cursorSizeBar.Value, cursorBox.SelectedIndex != 0);
        if (cursor) ApplyCursor();
        SaveSettings();
    }

    void ToggleHidden()
    {
        bool next = hideButton.Text == "隐藏人物";
        pendant.SetHidden(next);
        hideButton.Text = next ? "显示人物" : "隐藏人物";
        MenuItem item = pendant.TrayIcon.ContextMenu.MenuItems[1];
        item.Text = next ? "显示人物" : "隐藏人物";
    }

    void ApplyCursor()
    {
        CursorFactory.Apply(cursorBox.SelectedIndex, cursorSizeBar.Value);
        cursorApplied = cursorBox.SelectedIndex != 0;
    }

    void BeginHotkeyCapture()
    {
        capturingHotkey = true;
        capturedCount = 0;
        capturedVk = 0;
        hotkeyButton.Text = "请按两个键";
    }

    void RememberKey(int vk)
    {
        vk = NormalizeVk(vk);
        if (!capturingHotkey || vk == 0) return;
        if (capturedCount == 0)
        {
            capturedVk = vk;
            capturedCount = 1;
            hotkeyButton.Text = KeyName(vk) + " + …";
            return;
        }
        if (vk == capturedVk) return;
        hotkeyVk = capturedVk;
        hotkeyVk2 = vk;
        capturingHotkey = false;
        chordDown = true;
        ShowHotkey();
        SaveSettings();
    }

    static int NormalizeVk(int vk)
    {
        if (vk == 0xA2 || vk == 0xA3) return 0x11;
        if (vk == 0xA0 || vk == 0xA1) return 0x10;
        if (vk == 0xA4 || vk == 0xA5) return 0x12;
        if (vk == 0x5C) return 0x5B;
        return vk;
    }

    void ShowHotkey()
    {
        string shown = KeyName(hotkeyVk);
        if (hotkeyVk2 != 0) shown = shown + " + " + KeyName(hotkeyVk2);
        hotkeyButton.Text = shown;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!capturingHotkey) return base.ProcessCmdKey(ref msg, keyData);
        Keys code = keyData & Keys.KeyCode;
        if (code == Keys.Escape)
        {
            capturingHotkey = false;
            ShowHotkey();
            return true;
        }
        if ((keyData & Keys.Control) == Keys.Control) RememberKey(0x11);
        if ((keyData & Keys.Alt) == Keys.Alt) RememberKey(0x12);
        if ((keyData & Keys.Shift) == Keys.Shift) RememberKey(0x10);
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0)
            RememberKey(0x5B);
        if (code != Keys.ControlKey && code != Keys.LControlKey && code != Keys.RControlKey
            && code != Keys.ShiftKey && code != Keys.LShiftKey && code != Keys.RShiftKey
            && code != Keys.Menu && code != Keys.LMenu && code != Keys.RMenu
            && code != Keys.LWin && code != Keys.RWin)
            RememberKey((int)code);
        return true;
    }

    static string KeyName(int vk)
    {
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        if (vk >= 0x60 && vk <= 0x69) return "Num" + (vk - 0x60);
        if (vk == 0x11) return "Ctrl";
        if (vk == 0x12) return "Alt";
        if (vk == 0x10) return "Shift";
        if (vk == 0x5B) return "Win";
        if (vk == 0x20) return "Space";
        if (vk == 0x2D) return "Insert";
        if (vk == 0x2E) return "Delete";
        if (vk == 0x24) return "Home";
        if (vk == 0x23) return "End";
        if (vk == 0x21) return "PageUp";
        if (vk == 0x22) return "PageDown";
        if (vk == 0x25) return "Left";
        if (vk == 0x26) return "Up";
        if (vk == 0x27) return "Right";
        if (vk == 0x28) return "Down";
        if (vk == 0x0D) return "Enter";
        if (vk == 0x09) return "Tab";
        return ((Keys)vk).ToString();
    }

    static bool ParseHotkey(string val, out int vk, out int mod)
    {
        vk = 0;
        mod = 0;
        if (val == null || val.Length == 0) return false;
        string[] parts = val.Split('+');
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (part.Length == 0) continue;
            string lower = part.ToLowerInvariant();
            if (lower == "ctrl" || lower == "control") { mod |= 2; continue; }
            if (lower == "alt") { mod |= 1; continue; }
            if (lower == "shift") { mod |= 4; continue; }
            if (lower == "win") { mod |= 8; continue; }
            if (part.Length >= 2 && (part[0] == 'F' || part[0] == 'f'))
            {
                int n;
                if (int.TryParse(part.Substring(1), out n) && n >= 1 && n <= 24)
                {
                    vk = 0x6F + n;
                    continue;
                }
            }
            if (part.Length == 1)
            {
                char c = char.ToUpperInvariant(part[0]);
                if (c >= 'A' && c <= 'Z') { vk = c; continue; }
                if (c >= '0' && c <= '9') { vk = c; continue; }
            }
            if (lower.StartsWith("num") && part.Length == 4)
            {
                int n;
                if (int.TryParse(part.Substring(3), out n) && n >= 0 && n <= 9)
                {
                    vk = 0x60 + n;
                    continue;
                }
            }
        }
        return vk != 0;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MatchBoxHeights();
        ShowHotkey();
        ApplyRound();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!IsHandleCreated || WindowState == FormWindowState.Minimized) return;
        ApplyRound();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int top = 12;
        // Top-left corner: just the wordmark, no live character preview.
        if (logo != null && logo.Width > 0 && logo.Height > 0)
        {
            int maxW = minButton.Left - 12 - 28;
            if (maxW < 40) maxW = 40;
            float ls = Math.Min((float)maxW / logo.Width, (float)markDrawH / logo.Height);
            int lw = (int)Math.Round(logo.Width * ls);
            int lh = (int)Math.Round(logo.Height * ls);
            if (lw > 0 && lh > 0)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.DrawImage(logo,
                    new Rectangle(28, top + (markDrawH - lh) / 2, lw, lh));
            }
        }
        using (var pen = new Pen(Line))
            e.Graphics.DrawLine(pen, 28, hairlineY, ClientSize.Width - 28, hairlineY);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || e.Y >= hairlineY) return;
        draggingWindow = true;
        dragOffset = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!draggingWindow) return;
        Point screen = PointToScreen(e.Location);
        Location = new Point(screen.X - dragOffset.X, screen.Y - dragOffset.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        draggingWindow = false;
    }

    void ApplyRound()
    {
        if (Width < 8 || Height < 8) return;
        using (GraphicsPath path = RoundPath(Width, Height, 20))
        {
            Region old = Region;
            Region = new Region(path);
            if (old != null) old.Dispose();
        }
    }

    static GraphicsPath RoundPath(int width, int height, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        if (d > width) d = width;
        if (d > height) d = height;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(width - d - 1, 0, d, d, 270, 90);
        path.AddArc(width - d - 1, height - d - 1, d, d, 0, 90);
        path.AddArc(0, height - d - 1, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void SetVisibleCore(bool value)
    {
        if (startHidden && !allowShow)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    void ShowSettings()
    {
        allowShow = true;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        SetTaskbarButton(!backgroundBox.Checked);
        Activate();
    }

    void HideToTray()
    {
        SetTaskbarButton(false);
        Hide();
    }

    void RequestExit()
    {
        exitRequested = true;
        Close();
    }

    void ApplyBackground()
    {
        bool showButton = !(backgroundBox.Checked || (!allowShow && startHidden));
        SetTaskbarButton(showButton);
    }

    bool taskbarShown = true;

    void SetTaskbarButton(bool show)
    {
        if (!IsHandleCreated)
        {
            ShowInTaskbar = show;
            taskbarShown = show;
            return;
        }
        if (show == taskbarShown) return;
        int ex = GetWindowLong(Handle, -20);
        if (show) ex = (ex | 0x00040000) & ~0x00000080;
        else ex = (ex | 0x00000080) & ~0x00040000;
        SetWindowLong(Handle, -20, ex);
        try
        {
            ITaskbarList list = (ITaskbarList)new TaskbarListCom();
            list.HrInit();
            if (show) list.AddTab(Handle);
            else list.DeleteTab(Handle);
        }
        catch { }
        taskbarShown = show;
    }

    static bool StartedInTray()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--tray") return true;
        }
        return false;
    }

    static bool NotesRequested()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--notes") return true;
        }
        return false;
    }

    void ShowReleaseNotes()
    {
        using (Form dialog = new Form())
        {
            dialog.FormBorderStyle = FormBorderStyle.None;
            dialog.ShowInTaskbar = false;
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.AutoScaleMode = AutoScaleMode.None;
            dialog.Font = new Font("Microsoft YaHei UI", 9f);
            dialog.BackColor = Color.White;
            dialog.TopMost = true;
            string heading = "再见了k歌之王";
            string body = "更新改动：\r\n"
                + "· 移除 cocococo「k歌」唱歌形态：下拉仅形态1/2，不再加载 form4。\r\n"
                + "· 新增四档光标方案。\r\n"
                + "· 统一光标尺寸接口；修复像素光标大小无法调整的问题。";
            Font headFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            Size measured;
            int lineH;
            int headH;
            int headW;
            string[] lines;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                lineH = (int)Math.Ceiling(dialog.Font.GetHeight(g));
                headH = (int)Math.Ceiling(headFont.GetHeight(g));
                headW = (int)Math.Ceiling(g.MeasureString(heading, headFont).Width);
                float maxBodyW = 340f;
                var wrapped = new List<string>();
                foreach (string raw in body.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    if (line.Length == 0) { wrapped.Add(" "); continue; }
                    string cur = "";
                    foreach (char ch in line)
                    {
                        string trial = cur + ch;
                        if (g.MeasureString(trial, dialog.Font).Width > maxBodyW && cur.Length > 0)
                        { wrapped.Add(cur); cur = ch.ToString(); }
                        else cur = trial;
                    }
                    wrapped.Add(cur);
                }
                lines = wrapped.ToArray();
                float maxW = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    SizeF size = g.MeasureString(lines[i], dialog.Font);
                    if (size.Width > maxW) maxW = size.Width;
                }
                measured = new Size((int)Math.Ceiling(maxW), lineH * lines.Length);
            }

            Color bar = Color.FromArgb(240, 240, 240);
            int titleH = lineH + 8;
            int buttonH = lineH + 12;
            int footerH = buttonH + 6;
            int imgSize = 190;
            int markSize = 20;
            int rightW = Math.Max(markSize + 8 + headW, measured.Width);
            int clientW = 12 + imgSize + 18 + rightW + 14;
            int contentTop = titleH + 12;
            int rightHeadH = Math.Max(24, headH + 4);
            int contentH = Math.Max(imgSize, rightHeadH + 8 + measured.Height);
            int rightX = 12 + imgSize + 18;
            int footerY = contentTop + contentH + 12;

            var title = new Panel();
            title.BackColor = bar;
            title.SetBounds(0, 0, clientW, titleH);
            var caption = new Label();
            caption.Text = "Coldy";
            caption.BackColor = bar;
            caption.ForeColor = Color.Black;
            caption.AutoSize = false;
            caption.TextAlign = ContentAlignment.MiddleLeft;
            caption.SetBounds(8, 0, 80, titleH);
            title.Controls.Add(caption);
            var close = new Button();
            close.Text = "×";
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.BackColor = bar;
            close.ForeColor = Color.Black;
            close.TabStop = false;
            close.SetBounds(clientW - titleH, 0, titleH, titleH);
            close.DialogResult = DialogResult.OK;
            title.Controls.Add(close);

            var icon = new PictureBox();
            Image sad = null;
            try
            {
                string sadPath = Path.Combine(Application.StartupPath, "assets", "coldy-sad.png");
                if (File.Exists(sadPath))
                    using (var fs = File.OpenRead(sadPath))
                    using (var tmp = new Bitmap(fs))
                        sad = new Bitmap(tmp);
            }
            catch { }
            icon.Image = sad ?? SystemIcons.Information.ToBitmap();
            icon.SizeMode = PictureBoxSizeMode.Zoom;
            icon.BackColor = Color.White;
            icon.SetBounds(12, contentTop + (contentH - imgSize) / 2, imgSize, imgSize);

            // Single owner-drawn heading row: icon and title share one vertical
            // centre line, so they can never sit off-axis from each other.
            var headRow = new Panel();
            headRow.BackColor = Color.White;
            headRow.SetBounds(rightX, contentTop, rightW, rightHeadH);
            headRow.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.DrawIcon(SystemIcons.Information,
                    new Rectangle(0, (rightHeadH - markSize) / 2, markSize, markSize));
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(heading, headFont, Brushes.Black,
                        new RectangleF(markSize + 8, 0, rightW - markSize - 8, rightHeadH), sf);
                }
            };

            var text = new Label();
            text.BackColor = Color.White;
            text.ForeColor = Color.Black;
            text.AutoSize = false;
            text.UseCompatibleTextRendering = true;
            text.Text = string.Join("\r\n", lines);
            text.SetBounds(rightX, contentTop + rightHeadH + 8, measured.Width + 2, measured.Height);

            var footer = new Panel();
            footer.BackColor = bar;
            footer.SetBounds(0, footerY, clientW, footerH);
            var button = new Button();
            button.Text = "确定";
            button.SetBounds(clientW - 8 - 72, (footerH - buttonH) / 2, 72, buttonH);
            button.DialogResult = DialogResult.OK;
            footer.Controls.Add(button);

            dialog.ClientSize = new Size(clientW, footerY + footerH);
            dialog.Controls.Add(icon);
            dialog.Controls.Add(headRow);
            dialog.Controls.Add(text);
            dialog.Controls.Add(title);
            dialog.Controls.Add(footer);
            dialog.AcceptButton = button;
            dialog.CancelButton = close;
            dialog.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(196, 196, 196)))
                    e.Graphics.DrawRectangle(pen, 0, 0, dialog.ClientSize.Width - 1, dialog.ClientSize.Height - 1);
            };
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            dialog.Location = new Point(
                area.Left + (area.Width - dialog.Width) / 2,
                area.Top + (area.Height - dialog.Height) / 2);
            dialog.ShowDialog();
            releaseNotesSeen = true;
            SaveSettings();
        }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "Coldy";

    static bool StartupEnabled()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
        {
            if (key == null) return false;
            object value = key.GetValue(RunName);
            return value is string && ((string)value).Length > 0;
        }
    }

    static void SetStartup(bool enabled)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (key == null) return;
            if (!enabled)
            {
                key.DeleteValue(RunName, false);
                return;
            }
            string exe = Application.ExecutablePath;
            key.SetValue(RunName, "\"" + exe + "\" --tray");
        }
    }

    void CheckForUpdate()
    {
        if (updateBusy || updating || exitRequested) return;
        updateBusy = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string tag = null;
            string url = null;
            bool newer = false;
            try { newer = UpdateClient.TryGetNewer(Program.AppVersion, out tag, out url); }
            catch { newer = false; }
            if (!newer || IsDisposed)
            {
                updateBusy = false;
                return;
            }
            string foundTag = tag;
            string foundUrl = url;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    try { OfferUpdate(foundTag, foundUrl); }
                    finally { updateBusy = false; }
                });
            }
            catch
            {
                updateBusy = false;
            }
        });
    }

    void OfferUpdate(string tag, string url)
    {
        if (updating || exitRequested || IsDisposed) return;
        DialogResult answer = MessageBox.Show(
            "发现新版本 " + tag + "，是否现在更新？",
            "Coldy",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.DefaultDesktopOnly);
        if (answer != DialogResult.Yes) return;
        updating = true;
        updateTimer.Stop();
        BeginDownload(url);
    }

    void BeginDownload(string url)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string root = Path.Combine(Path.GetTempPath(), "ColdyUpdate");
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                string zipPath = Path.Combine(root, "update.zip");
                UpdateClient.Download(url, zipPath);
                string unpacked = Path.Combine(root, "files");
                ZipFile.ExtractToDirectory(zipPath, unpacked);
                string payload = UpdateClient.PayloadRoot(unpacked);
                BeginInvoke((MethodInvoker)delegate { ApplyUpdate(payload); });
            }
            catch (Exception ex)
            {
                updating = false;
                try
                {
                    string message = ex.Message;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        updateTimer.Start();
                        MessageBox.Show("更新没有完成。\r\n" + message, "Coldy");
                    });
                }
                catch { }
            }
        });
    }

    void ApplyUpdate(string unpacked)
    {
        string exe = Application.ExecutablePath;
        string dest = Path.GetDirectoryName(exe);
        if (dest.EndsWith("\\")) dest = dest.Substring(0, dest.Length - 1);
        int pid = Process.GetCurrentProcess().Id;
        string bat = Path.Combine(Path.GetTempPath(), "coldy-update.cmd");
        string script = "@echo off\r\n"
            + ":wait\r\n"
            + "tasklist /FI \"PID eq " + pid + "\" | find \"" + pid + "\" >nul\r\n"
            + "if not errorlevel 1 (\r\n"
            + "ping -n 2 127.0.0.1 >nul\r\n"
            + "goto wait\r\n"
            + ")\r\n"
            + "xcopy /e /y /i \"" + unpacked + "\\*\" \"" + dest + "\" >nul\r\n"
            + "start \"\" \"" + exe + "\" --notes\r\n"
            + "del \"%~f0\"\r\n";
        File.WriteAllText(bat, script, Encoding.Default);
        var start = new ProcessStartInfo();
        start.FileName = "cmd.exe";
        start.Arguments = "/c \"" + bat + "\"";
        start.CreateNoWindow = true;
        start.UseShellExecute = false;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        Process.Start(start);
        RequestExit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exitRequested && backgroundBox.Checked)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        exitRequested = true;
        hotkeyTimer.Stop();
        updateTimer.Stop();
        if (cursorApplied) CursorFactory.Restore();
        SaveSettings();
        if (logo != null) logo.Dispose();
        logo = null;
        Icon = null;
        if (pendant != null && !pendant.IsDisposed) pendant.Close();
        base.OnFormClosing(e);
    }

    void LoadSettings()
    {
        if (!File.Exists(settingsPath)) return;
        string[] lines = File.ReadAllLines(settingsPath);
        int fileVk = 0;
        int fileVk2 = 0;
        int fileMod = 0;
        bool sawVk = false;
        bool sawVk2 = false;
        string hotkeyText = null;
        for (int i = 0; i < lines.Length; i++)
        {
            int eq = lines[i].IndexOf('=');
            if (eq <= 0) continue;
            string key = lines[i].Substring(0, eq).Trim();
            string val = lines[i].Substring(eq + 1).Trim();
            int n;
            if (key == "rope") SetBar(ropeBar, val);
            else if (key == "ropeColor") ropeColor = ParseColor(val);
            else if (key == "customColors") ParseCustomColors(val);
            else if (key == "size") SetBar(sizeBar, val);
            else if (key == "cursorSize") SetBar(cursorSizeBar, val);
            else if (key == "cursor") SelectCursor(val);
            else if (key == "form") SelectForm(val);
            else if (key == "background") backgroundBox.Checked = val == "1";
            else if (key == "notes") releaseNotesSeen = val == Program.NotesRev;
            else if (key == "hotkeyVk")
            {
                if (int.TryParse(val, out n) && n > 0) { fileVk = n; sawVk = true; }
            }
            else if (key == "hotkeyVk2")
            {
                if (int.TryParse(val, out n) && n > 0) { fileVk2 = n; sawVk2 = true; }
            }
            else if (key == "hotkeyMod")
            {
                if (int.TryParse(val, out n)) fileMod = n;
            }
            else if (key == "hotkey") hotkeyText = val;
        }
        if (sawVk) hotkeyVk = fileVk;
        if (sawVk2) hotkeyVk2 = fileVk2;
        if (!sawVk2 && fileMod != 0 && hotkeyVk != 0)
        {
            int modKey = ModifierKey(fileMod);
            if (modKey != 0)
            {
                hotkeyVk2 = hotkeyVk;
                hotkeyVk = modKey;
            }
        }
        if (!sawVk && hotkeyText != null)
        {
            int vk, mod;
            if (ParseHotkey(hotkeyText, out vk, out mod))
            {
                int modKey = ModifierKey(mod);
                if (modKey != 0)
                {
                    hotkeyVk = modKey;
                    hotkeyVk2 = vk;
                }
                else hotkeyVk = vk;
            }
        }
    }

    static int ModifierKey(int mod)
    {
        if ((mod & 2) != 0) return 0x11;
        if ((mod & 1) != 0) return 0x12;
        if ((mod & 4) != 0) return 0x10;
        if ((mod & 8) != 0) return 0x5B;
        return 0;
    }

    static void SetBar(IceSlider bar, string val)
    {
        int n;
        if (!int.TryParse(val, out n)) return;
        if (n < bar.Minimum) n = bar.Minimum;
        if (n > bar.Maximum) n = bar.Maximum;
        bar.Value = n;
    }

    void SelectCursor(string val)
    {
        if (val == "pixel")
            cursorBox.SelectedIndex = 2;
        else if (val == "felt")
            cursorBox.SelectedIndex = 3;
        else if (val == "scheme" || val == "arrow" || val == "hand" || val == "cross" || val == "ibeam" || val == "dot")
            cursorBox.SelectedIndex = 1;
        else
            cursorBox.SelectedIndex = 0;
    }

    void SelectForm(string val)
    {
        // "3"/"4" are the archived cocococo keys; fall back to the base form.
        if (val == "2" || val == "gif") formBox.SelectedIndex = 1;
        else formBox.SelectedIndex = 0;
    }

    static string FormKey(int index)
    {
        if (index == 1) return "2";
        return "1";
    }

    void SaveSettings()
    {
        if (loading) return;
        string[] styles = { "system", "scheme", "pixel", "felt" };
        int index = cursorBox.SelectedIndex;
        if (index < 0 || index >= styles.Length) index = 0;
        StringBuilder colors = new StringBuilder();
        for (int i = 0; i < customColors.Length; i++)
        {
            if (i > 0) colors.Append(',');
            colors.Append(customColors[i]);
        }
        string text = "rope=" + ropeBar.Value + "\r\n"
            + "ropeColor=" + ropeColor.R + "," + ropeColor.G + "," + ropeColor.B + "\r\n"
            + "customColors=" + colors + "\r\n"
            + "size=" + sizeBar.Value + "\r\n"
            + "form=" + FormKey(formBox.SelectedIndex) + "\r\n"
            + "cursor=" + styles[index] + "\r\n"
            + "cursorSize=" + cursorSizeBar.Value + "\r\n"
            + "hotkey=" + hotkeyButton.Text + "\r\n"
            + "hotkeyVk=" + hotkeyVk + "\r\n"
            + "hotkeyVk2=" + hotkeyVk2 + "\r\n"
            + "startup=" + (startupBox.Checked ? "1" : "0") + "\r\n"
            + "background=" + (backgroundBox.Checked ? "1" : "0") + "\r\n"
            + "notes=" + (releaseNotesSeen ? Program.NotesRev : "") + "\r\n";
        try { File.WriteAllText(settingsPath, text); }
        catch { }
    }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

[ComImport, Guid("56FDF342-FD6D-11d0-958A-006097C9A090"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ITaskbarList
{
    void HrInit();
    void AddTab(IntPtr hwnd);
    void DeleteTab(IntPtr hwnd);
    void ActivateTab(IntPtr hwnd);
    void SetActiveAlt(IntPtr hwnd);
}

[ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
class TaskbarListCom
{
}

class IceSlider : Control
{
    int minimum;
    int maximum = 100;
    int current;
    bool dragging;
    public event EventHandler ValueChanged;

    public int Minimum
    {
        get { return minimum; }
        set { minimum = value; Invalidate(); }
    }

    public int Maximum
    {
        get { return maximum; }
        set { maximum = value; Invalidate(); }
    }

    public int Value
    {
        get { return current; }
        set
        {
            if (value < minimum) value = minimum;
            if (value > maximum) value = maximum;
            if (current == value) return;
            current = value;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    public IceSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 28;
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(231, 243, 248);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left) return;
        dragging = true;
        Capture = true;
        SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging) SetFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    void SetFromX(int x)
    {
        int pad = 8;
        int span = Width - pad * 2;
        if (span < 1) span = 1;
        float t = (x - pad) / (float)span;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        int range = maximum - minimum;
        if (range < 1) range = 1;
        Value = minimum + (int)Math.Round(t * range);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int pad = 8;
        int cy = Height / 2;
        int trackW = Width - pad * 2;
        if (trackW < 1) trackW = 1;
        int range = maximum - minimum;
        float t = range <= 0 ? 0f : (current - minimum) / (float)range;
        int fillW = (int)(trackW * t);
        Color fill = Enabled ? Color.FromArgb(75, 168, 212) : Color.FromArgb(170, 186, 196);
        using (var brush = new SolidBrush(Color.FromArgb(206, 222, 232)))
            g.FillRectangle(brush, pad, cy - 2, trackW, 4);
        if (fillW > 0)
        {
            using (var brush = new SolidBrush(fill))
                g.FillRectangle(brush, pad, cy - 2, fillW, 4);
        }
        int thumb = 14;
        int tx = pad + fillW - thumb / 2;
        if (tx < pad - 2) tx = pad - 2;
        if (tx > pad + trackW - thumb + 2) tx = pad + trackW - thumb + 2;
        using (var brush = new SolidBrush(Color.White))
            g.FillEllipse(brush, tx, cy - thumb / 2, thumb, thumb);
        using (var pen = new Pen(fill, 2f))
            g.DrawEllipse(pen, tx, cy - thumb / 2, thumb, thumb);
    }
}

class MarkButton : Control
{
    bool close;
    bool hot;

    public bool CloseMark
    {
        get { return close; }
        set { close = value; Invalidate(); }
    }

    public MarkButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(231, 243, 248);
        Size = new Size(34, 34);
        TabStop = false;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hot = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hot = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (hot)
        {
            using (var brush = new SolidBrush(Color.FromArgb(214, 228, 236)))
                g.FillEllipse(brush, 1, 1, Width - 3, Height - 3);
        }
        using (var pen = new Pen(Color.FromArgb(45, 58, 68), 2f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            float m = Width * 0.32f;
            if (close)
            {
                g.DrawLine(pen, m, m, Width - m, Height - m);
                g.DrawLine(pen, Width - m, m, m, Height - m);
            }
            else
            {
                float y = Height * 0.62f;
                g.DrawLine(pen, m, y, Width - m, y);
            }
        }
    }
}

public class CursorFactory
{
    public static void ApplyScheme(int size)
    {
        if (size < 16) size = 16;
        if (size > 128) size = 128;
        Set(32512, "arrow", size);
        Set(32513, "ibeam", size);
        Set(32514, "wait", size);
        Set(32515, "cross", size);
        Set(32516, "up", size);
        Set(32642, "nwse", size);
        Set(32643, "nesw", size);
        Set(32644, "we", size);
        Set(32645, "ns", size);
        Set(32646, "move", size);
        Set(32648, "no", size);
        Set(32649, "hand", size);
        Set(32650, "busy", size);
        Set(32651, "help", size);
    }

    // Single size-controlled interface for all four cursor schemes
    // (0 = system default, 1 = Glacier, 2 = pixel, 3 = Felt & Frost). The
    // settings size slider is the one size source and every scheme change
    // routes through here.
    public static void Apply(int scheme, int size)
    {
        if (scheme == 1) ApplyScheme(size);
        else if (scheme == 2) ApplyPixel(size);
        else if (scheme == 3) ApplyFelt(size);
        else Restore();
    }

    static void Set(int id, string style, int size)
    {
        IntPtr cur = Make(style, size);
        if (cur == IntPtr.Zero) return;
        if (!SetSystemCursor(cur, id)) DestroyCursor(cur);
    }

    public static void Restore()
    {
        SystemParametersInfo(0x0057, 0, IntPtr.Zero, 0);
        try
        {
            string t = Path.Combine(Path.GetTempPath(), "ColdyPixel");
            if (Directory.Exists(t)) Directory.Delete(t, true);
            string tf = Path.Combine(Path.GetTempPath(), "ColdyFelt");
            if (Directory.Exists(tf)) Directory.Delete(tf, true);
        }
        catch { }
    }

    // ---- Pixel scheme -----------------------------------------------------
    // Animated 16x16 pixel cursors by 线框xiankuang, shipped as .ani files in
    // assets/cursors/pixel. Frames are decoded once, then rebuilt into scaled
    // .ani files on demand (nearest neighbour keeps the pixel grid crisp), so
    // the animation survives at every cursor size.

    class PixelFrame { public int hx, hy, w, h; public Color[] px; }
    class PixelAni
    {
        public int steps, jif, flags;
        public byte[] rate, seq;
        public List<PixelFrame> frames = new List<PixelFrame>();
    }

    static readonly int[] PixelIds = {
        32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644,
        32645, 32646, 32648, 32649, 32650, 32651, 32652, 32653
    };
    static readonly string[] PixelFiles = {
        "Normal", "Text", "Busy", "Precision", "Alternate", "Diagonal1",
        "Diagonal2", "Horizontal", "Vertical", "Move", "Unavailable",
        "Link", "Working", "Help", "Person", "Pin"
    };
    static readonly string[] PixelStyles = {
        "arrow", "ibeam", "wait", "cross", "up", "nwse", "nesw", "we",
        "ns", "move", "no", "hand", "busy", "help"
    };

    static Dictionary<string, PixelAni> pixelCache;
    static string pixelDir;

    public static void ApplyPixel(int size)
    {
        if (size < 16) size = 16;
        if (size > 128) size = 128;
        if (!EnsurePixel()) { ApplyScheme(size); return; }
        for (int i = 0; i < PixelIds.Length; i++)
        {
            PixelAni ani;
            if (!pixelCache.TryGetValue(PixelFiles[i], out ani)) continue;
            string path = size == ani.frames[0].w
                ? Path.Combine(pixelDir, PixelFiles[i] + ".ani")
                : BuildScaledAni(PixelFiles[i], ani, size);
            if (path == null) continue;
            // LoadCursorFromFile normalizes .ani to the system cursor metric
            // (always 32px), which froze the pixel size. LoadImage with
            // LR_LOADFROMFILE honors the rebuilt frame size and keeps animation.
            IntPtr cur = LoadImage(IntPtr.Zero, path, 2, size, size, 0x10);
            if (cur != IntPtr.Zero && !SetSystemCursor(cur, PixelIds[i])) DestroyCursor(cur);
        }
    }

    // First animation frame, scaled — used by the preview tooling.
    public static Bitmap RenderPixelFirst(string style, int size, out int hx, out int hy)
    {
        hx = 0; hy = 0;
        int idx = Array.IndexOf(PixelStyles, style);
        if (idx < 0 || !EnsurePixel()) return null;
        PixelAni ani;
        if (!pixelCache.TryGetValue(PixelFiles[idx], out ani)) return null;
        PixelFrame f = ani.frames[0];
        float s = (float)size / f.w;
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = f.px[Math.Min(f.h - 1, (int)(y / s)) * f.w + Math.Min(f.w - 1, (int)(x / s))];
                if (c.A > 0) bmp.SetPixel(x, y, c);
            }
        hx = (int)Math.Round(f.hx * s);
        hy = (int)Math.Round(f.hy * s);
        return bmp;
    }

    static bool EnsurePixel()
    {
        if (pixelCache != null) return pixelCache.Count > 0;
        pixelCache = new Dictionary<string, PixelAni>();
        pixelDir = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "assets", "cursors", "pixel");
        if (!Directory.Exists(pixelDir))
            pixelDir = Path.Combine(Environment.CurrentDirectory, "assets", "cursors", "pixel");
        if (!Directory.Exists(pixelDir)) return false;
        foreach (string name in PixelFiles)
        {
            string p = Path.Combine(pixelDir, name + ".ani");
            if (!File.Exists(p)) continue;
            PixelAni ani = ParseAni(File.ReadAllBytes(p));
            if (ani != null && ani.frames.Count > 0) pixelCache[name] = ani;
        }
        return pixelCache.Count > 0;
    }

    static byte[] Slice(byte[] d, int off, int len)
    {
        var r = new byte[len];
        Array.Copy(d, off, r, 0, len);
        return r;
    }

    static PixelAni ParseAni(byte[] d)
    {
        if (d.Length < 16 || Encoding.ASCII.GetString(d, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(d, 8, 4) != "ACON") return null;
        var ani = new PixelAni();
        int i = 12;
        while (i + 8 <= d.Length)
        {
            string cid = Encoding.ASCII.GetString(d, i, 4);
            int sz = BitConverter.ToInt32(d, i + 4);
            if (sz < 0 || i + 8 + sz > d.Length) break;
            if (cid == "anih" && sz >= 36)
            {
                ani.steps = BitConverter.ToInt32(d, i + 16);
                ani.jif = BitConverter.ToInt32(d, i + 36);
                ani.flags = BitConverter.ToInt32(d, i + 40);
            }
            else if (cid == "rate") ani.rate = Slice(d, i + 8, sz);
            else if (cid == "seq ") ani.seq = Slice(d, i + 8, sz);
            else if (cid == "LIST" && sz >= 4 && Encoding.ASCII.GetString(d, i + 8, 4) == "fram")
            {
                int j = i + 12, end = i + 8 + sz;
                while (j + 8 <= end)
                {
                    string c2 = Encoding.ASCII.GetString(d, j, 4);
                    int s2 = BitConverter.ToInt32(d, j + 4);
                    if (s2 < 0 || j + 8 + s2 > end) break;
                    if (c2 == "icon")
                    {
                        PixelFrame f = ParseFrame(d, j + 8, s2);
                        if (f != null) ani.frames.Add(f);
                    }
                    j += 8 + s2 + (s2 & 1);
                }
            }
            i += 8 + sz + (sz & 1);
        }
        if (ani.jif <= 0) ani.jif = 5;
        return ani;
    }

    // Each fram/icon chunk is a complete mini .cur; the entry's planes and
    // bit-count fields carry the hotspot, and the bitmap height is doubled
    // (XOR colour rows + AND mask rows).
    static PixelFrame ParseFrame(byte[] d, int off, int len)
    {
        if (len < 22) return null;
        int w = d[off + 6], h = d[off + 7];
        int hx = BitConverter.ToInt16(d, off + 10);
        int hy = BitConverter.ToInt16(d, off + 12);
        int imgOff = BitConverter.ToInt32(d, off + 18);
        if (imgOff < 22 || off + imgOff + 40 > d.Length) return null;
        int bo = off + imgOff;
        int bw = BitConverter.ToInt32(d, bo + 4);
        int bh = BitConverter.ToInt32(d, bo + 8) / 2;
        int bits = BitConverter.ToInt16(d, bo + 14);
        if (bw <= 0 || bh <= 0 || bw > 256 || bh > 256 || w != bw || h != bh) return null;
        int clrUsed = BitConverter.ToInt32(d, bo + 32);
        int palCount = bits <= 8 ? (clrUsed > 0 ? clrUsed : (1 << bits)) : 0;
        int stride = ((bw * bits + 31) / 32) * 4;
        int mstride = ((bw + 31) / 32) * 4;
        int palBase = bo + 40;
        int xorBase = palBase + palCount * 4;
        int andBase = xorBase + stride * bh;
        if (andBase + mstride * bh > off + len) return null;
        var f = new PixelFrame { hx = hx, hy = hy, w = bw, h = bh, px = new Color[bw * bh] };
        for (int y = 0; y < bh; y++)
        {
            int sy = bh - 1 - y;
            for (int x = 0; x < bw; x++)
            {
                bool trans = (d[andBase + sy * mstride + (x >> 3)] & (0x80 >> (x & 7))) != 0;
                Color c = Color.Black;
                if (bits == 8)
                {
                    int idx = d[xorBase + sy * stride + x];
                    if (idx < palCount) c = Color.FromArgb(d[palBase + idx * 4 + 2], d[palBase + idx * 4 + 1], d[palBase + idx * 4]);
                }
                else if (bits == 32)
                {
                    int o = xorBase + sy * stride + x * 4;
                    c = Color.FromArgb(d[o + 3], d[o + 2], d[o + 1], d[o]);
                }
                else if (bits == 4 || bits == 1)
                {
                    int perByte = 8 / bits;
                    int idx = (d[xorBase + sy * stride + (x / perByte)] >> (bits * (perByte - 1 - (x % perByte)))) & ((1 << bits) - 1);
                    if (idx < palCount) c = Color.FromArgb(d[palBase + idx * 4 + 2], d[palBase + idx * 4 + 1], d[palBase + idx * 4]);
                }
                else return null;
                f.px[y * bw + x] = trans ? Color.Transparent : c;
            }
        }
        return f;
    }

    static string BuildScaledAni(string name, PixelAni ani, int size)
    {
        string dir = Path.Combine(Path.GetTempPath(), "ColdyPixel", size.ToString());
        string path = Path.Combine(dir, name + ".ani");
        if (File.Exists(path)) return path;
        try
        {
            Directory.CreateDirectory(dir);
            var chunks = new List<byte[]>();
            chunks.Add(AniChunk("anih", AniHeader(ani.frames.Count, ani.steps, size, ani.jif, ani.flags)));
            if (ani.rate != null) chunks.Add(AniChunk("rate", ani.rate));
            if (ani.seq != null) chunks.Add(AniChunk("seq ", ani.seq));
            var fram = new List<byte>();
            fram.AddRange(Encoding.ASCII.GetBytes("fram"));
            foreach (PixelFrame f in ani.frames) fram.AddRange(AniChunk("icon", ScaleFrame(f, size)));
            chunks.Add(AniChunk("LIST", fram.ToArray()));
            int total = 4;
            foreach (byte[] c in chunks) total += c.Length;
            using (var ms = new MemoryStream())
            {
                ms.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                ms.Write(BitConverter.GetBytes(total), 0, 4);
                ms.Write(Encoding.ASCII.GetBytes("ACON"), 0, 4);
                foreach (byte[] c in chunks) ms.Write(c, 0, c.Length);
                File.WriteAllBytes(path, ms.ToArray());
            }
            return path;
        }
        catch { return null; }
    }

    static byte[] AniChunk(string cid, byte[] data)
    {
        var r = new byte[8 + data.Length + (data.Length & 1)];
        Encoding.ASCII.GetBytes(cid, 0, 4, r, 0);
        BitConverter.GetBytes(data.Length).CopyTo(r, 4);
        data.CopyTo(r, 8);
        return r;
    }

    static byte[] AniHeader(int frames, int steps, int size, int jif, int flags)
    {
        var r = new byte[36];
        int[] v = { 36, frames, steps, size, size, 32, 1, jif, flags };
        for (int i = 0; i < 9; i++) BitConverter.GetBytes(v[i]).CopyTo(r, i * 4);
        return r;
    }

    // Re-encode one frame as a 32bpp mini .cur at the target size.
    static byte[] ScaleFrame(PixelFrame f, int size)
    {
        float s = (float)size / f.w;
        int hx = (int)Math.Round(f.hx * s), hy = (int)Math.Round(f.hy * s);
        int stride = size * 4;
        int mstride = ((size + 31) / 32) * 4;
        var xor = new byte[stride * size];
        var and = new byte[mstride * size];
        for (int y = 0; y < size; y++)
        {
            int sy = size - 1 - y;
            for (int x = 0; x < size; x++)
            {
                Color c = f.px[Math.Min(f.h - 1, (int)(y / s)) * f.w + Math.Min(f.w - 1, (int)(x / s))];
                int o = sy * stride + x * 4;
                xor[o] = c.B; xor[o + 1] = c.G; xor[o + 2] = c.R; xor[o + 3] = c.A;
                if (c.A < 128) and[sy * mstride + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }
        int imgLen = 40 + xor.Length + and.Length;
        var img = new byte[imgLen];
        BitConverter.GetBytes(40).CopyTo(img, 0);
        BitConverter.GetBytes(size).CopyTo(img, 4);
        BitConverter.GetBytes(size * 2).CopyTo(img, 8);
        BitConverter.GetBytes((short)1).CopyTo(img, 12);
        BitConverter.GetBytes((short)32).CopyTo(img, 14);
        BitConverter.GetBytes(xor.Length + and.Length).CopyTo(img, 20);
        xor.CopyTo(img, 40);
        and.CopyTo(img, 40 + xor.Length);
        var r = new byte[22 + imgLen];
        BitConverter.GetBytes((short)2).CopyTo(r, 2);
        BitConverter.GetBytes((short)1).CopyTo(r, 4);
        r[6] = (byte)size; r[7] = (byte)size;
        BitConverter.GetBytes((short)hx).CopyTo(r, 10);
        BitConverter.GetBytes((short)hy).CopyTo(r, 12);
        BitConverter.GetBytes(imgLen).CopyTo(r, 14);
        BitConverter.GetBytes(22).CopyTo(r, 18);
        img.CopyTo(r, 22);
        return r;
    }

    // ---- Felt & Frost design system (scheme 3) ---------------------------
    // Shape-only cursors cut from Coldy's material world: felt white, knitted
    // blue-white stripe, sage/red/cream scarf, chrome spoon, one cyan glow and
    // a rainbow cable trail. No character and no face - every state is an
    // object or pattern whose motif carries the function (scarf stretch =
    // resize, spoon = help, snowball = move, scarf knot = no, snowflake = wait).

    static readonly Color FeltWhite = Color.FromArgb(255, 245, 247, 248);
    static readonly Color FeltShade = Color.FromArgb(255, 226, 232, 236);
    static readonly Color KnitBlue = Color.FromArgb(255, 91, 135, 166);
    static readonly Color KnitWhite = Color.FromArgb(255, 244, 248, 250);
    static readonly Color Sage = Color.FromArgb(255, 157, 184, 154);
    static readonly Color ScarfCream = Color.FromArgb(255, 239, 231, 216);
    static readonly Color ChromeHi = Color.FromArgb(255, 250, 252, 253);
    static readonly Color ChromeLo = Color.FromArgb(255, 140, 152, 162);
    static readonly Color Cyan = Color.FromArgb(255, 53, 198, 244);
    static readonly Color[] Rainbow = new Color[] {
        Color.FromArgb(255,226,87,75), Color.FromArgb(255,240,150,60),
        Color.FromArgb(255,240,200,80), Color.FromArgb(255,120,180,110),
        Color.FromArgb(255,90,140,200)
    };

    // Parallel to AllStyles.
    static readonly int[] FeltIds = {
        32512, 32649, 32513, 32514, 32650, 32651, 32515,
        32648, 32644, 32645, 32642, 32643, 32646, 32516
    };

    public static void ApplyFelt(int size)
    {
        if (size < 16) size = 16;
        if (size > 128) size = 128;
        for (int i = 0; i < AllStyles.Length; i++)
        {
            string style = AllStyles[i];
            IntPtr cur;
            if (FeltAnimated(style))
            {
                string path = BuildFeltAni(style, size);
                cur = path == null ? IntPtr.Zero
                    : LoadImage(IntPtr.Zero, path, 2, size, size, 0x10);
            }
            else
            {
                int hx, hy;
                using (var bmp = RenderFelt(style, size, 0, out hx, out hy))
                    cur = FromBitmap(bmp, hx, hy);
            }
            if (cur != IntPtr.Zero && !SetSystemCursor(cur, FeltIds[i])) DestroyCursor(cur);
        }
    }

    static bool FeltAnimated(string s)
    {
        return s == "wait" || s == "busy" || s == "move" || s == "help" || s == "no"
            || s == "we" || s == "ns" || s == "nwse" || s == "nesw";
    }

    static int FeltFrames(string s)
    {
        if (!FeltAnimated(s)) return 1;
        return s == "wait" ? 12 : 8;
    }

    // Renders one felt pointer; frame drives the micro-motion. Used by
    // ApplyFelt and by the preview tooling so previews match the live set.
    public static Bitmap RenderFelt(string style, int size, int frame, out int hx, out int hy)
    {
        if (size < 8) size = 8;
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float u = size / 32f;
            int n = FeltFrames(style);
            float t = n <= 1 ? 0f : (frame % n) / (float)n;
            float ang = t * 360f;
            float wave = (float)Math.Sin(t * Math.PI * 2f);
            hx = size / 2; hy = size / 2;
            if (style == "hand") FeltHand(g, u, out hx, out hy);
            else if (style == "ibeam") FeltIBeam(g, u, out hx, out hy);
            else if (style == "wait") FeltWait(g, u, ang, out hx, out hy);
            else if (style == "busy") FeltBusy(g, u, ang, out hx, out hy);
            else if (style == "help") FeltHelp(g, u, wave, out hx, out hy);
            else if (style == "cross") FeltCross(g, u, out hx, out hy);
            else if (style == "no") FeltNo(g, u, wave, out hx, out hy);
            else if (style == "move") FeltMove(g, u, ang, out hx, out hy);
            else if (style == "up") FeltUp(g, u, out hx, out hy);
            else if (style == "we" || style == "ns" || style == "nwse" || style == "nesw")
                FeltResize(g, u, style, wave, out hx, out hy);
            else FeltArrow(g, u, out hx, out hy);
        }
        if (hx < 0) hx = 0;
        if (hx >= size) hx = size - 1;
        if (hy < 0) hy = 0;
        if (hy >= size) hy = size - 1;
        return bmp;
    }

    // Felt body: white halo, felt-white gradient, clipped motif, ink edge.
    static void FeltBody(Graphics g, GraphicsPath path, float u, Action<Graphics, float> detail)
    {
        float ink = Math.Max(1.0f, 1.6f * u);
        float halo = Math.Max(1.0f, 1.4f * u);
        // Soft contact shadow so the object sits on the surface.
        using (var shifted = (GraphicsPath)path.Clone())
        using (var m = new Matrix())
        using (var sh = new SolidBrush(Color.FromArgb(38, 24, 32, 48)))
        {
            m.Translate(0.5f * u + 0.3f, 0.8f * u + 0.5f);
            shifted.Transform(m);
            using (var sp = new Pen(sh, ink + halo * 2f))
            {
                sp.LineJoin = LineJoin.Round;
                g.DrawPath(sp, shifted);
            }
            g.FillPath(sh, shifted);
        }
        // Plush fuzz fringe just outside the ink line.
        using (var fp = new Pen(Color.FromArgb(150, 245, 247, 248), ink + halo * 2f + 1.2f * u))
        {
            fp.LineJoin = LineJoin.Round;
            g.DrawPath(fp, path);
        }
        using (var hp = new Pen(Color.White, ink + halo * 2f))
        {
            hp.LineJoin = LineJoin.Round;
            g.DrawPath(hp, path);
        }
        var b = path.GetBounds();
        if (b.Width < 1f) b.Width = 1f;
        if (b.Height < 1f) b.Height = 1f;
        using (var fb = new LinearGradientBrush(b, FeltWhite, FeltShade, 90f))
        {
            var blend = new ColorBlend(3);
            blend.Colors = new Color[] { FeltWhite, Color.FromArgb(255, 238, 242, 244), FeltShade };
            blend.Positions = new float[] { 0f, 0.55f, 1f };
            fb.InterpolationColors = blend;
            g.FillPath(fb, path);
        }
        var st = g.Save();
        g.SetClip(path);
        FeltFuzz(g, b, u);
        if (detail != null) detail(g, u);
        g.Restore(st);
        using (var ip = new Pen(Ink, ink))
        {
            ip.LineJoin = LineJoin.Round;
            g.DrawPath(ip, path);
        }
    }

    // Deterministic plush speckle so the felt reads as wool, not flat plastic.
    // Fixed seed keeps the texture identical across animation frames (no flicker).
    static void FeltFuzz(Graphics g, RectangleF b, float u)
    {
        if (u < 0.7f) return;
        var rnd = new Random(20261123);
        int n = (int)(b.Width * b.Height / (u * u) / 6f);
        if (n < 40) n = 40;
        if (n > 260) n = 260;
        for (int i = 0; i < n; i++)
        {
            float x = b.Left + (float)rnd.NextDouble() * b.Width;
            float y = b.Top + (float)rnd.NextDouble() * b.Height;
            float s = (0.4f + (float)rnd.NextDouble() * 0.5f) * u;
            bool light = rnd.Next(2) == 0;
            using (var sb = new SolidBrush(light
                ? Color.FromArgb(70, 255, 255, 255)
                : Color.FromArgb(55, 205, 214, 221)))
                g.FillEllipse(sb, x, y, s, s);
        }
    }

    // Knitted blue-white inlay rendered as rows of V stitches (not flat bands).
    static void KnitStripes(Graphics g, RectangleF r, float u, bool horizontal)
    {
        using (var wb = new SolidBrush(KnitWhite)) g.FillRectangle(wb, r);
        float band = Math.Max(1f, 1.6f * u);
        if (u < 0.9f)
        {
            // Small sizes: plain bands stay legible.
            using (var bb = new SolidBrush(KnitBlue))
            {
                if (horizontal)
                    for (float y = r.Top; y < r.Bottom; y += band * 2f)
                        g.FillRectangle(bb, r.Left, y, r.Width, band);
                else
                    for (float x = r.Left; x < r.Right; x += band * 2f)
                        g.FillRectangle(bb, x, r.Top, band, r.Height);
            }
            return;
        }
        float sx = 1.5f * u, sy = 1.5f * u;
        using (var bp = new Pen(KnitBlue, Math.Max(0.6f, 0.55f * u)))
        using (var wp = new Pen(Color.FromArgb(150, 120, 150, 175), Math.Max(0.5f, 0.45f * u)))
        {
            bp.StartCap = LineCap.Round; bp.EndCap = LineCap.Round;
            wp.StartCap = LineCap.Round; wp.EndCap = LineCap.Round;
            int row = 0;
            for (float y = r.Top; y < r.Bottom; y += sy, row++)
            {
                bool blueRow = horizontal ? ((row / 2) % 2 == 0) : true;
                int col = 0;
                for (float x = r.Left; x < r.Right; x += sx, col++)
                {
                    bool blue = horizontal ? blueRow : ((col / 2) % 2 == 0);
                    var p = blue ? bp : wp;
                    g.DrawLine(p, x, y, x + sx * 0.5f, y + sy * 0.75f);
                    g.DrawLine(p, x + sx * 0.5f, y + sy * 0.75f, x + sx, y);
                }
            }
        }
    }

    // Scarf wrap as ribbed knit: each rib gets a centre highlight + dark seam.
    static void ScarfStripes(Graphics g, RectangleF r, float u)
    {
        using (var cb = new SolidBrush(ScarfCream)) g.FillRectangle(cb, r);
        float band = Math.Max(1f, 1.8f * u);
        using (var sb = new SolidBrush(Sage))
        using (var rb = new SolidBrush(ScarfRed))
        using (var cb2 = new SolidBrush(ScarfCream))
        {
            int k = 0;
            for (float x = r.Left; x < r.Right; x += band, k++)
            {
                var fill = (k % 3 == 0) ? sb : (k % 3 == 1) ? rb : cb2;
                g.FillRectangle(fill, x, r.Top, band, r.Height);
            }
        }
        if (u < 0.9f) return;
        using (var hi = new Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(0.4f, 0.4f * u)))
        using (var lo = new Pen(Color.FromArgb(70, 31, 40, 56), Math.Max(0.3f, 0.3f * u)))
        {
            for (float x = r.Left; x < r.Right; x += band)
            {
                g.DrawLine(hi, x + band * 0.35f, r.Top, x + band * 0.35f, r.Bottom);
                g.DrawLine(lo, x + band * 0.9f, r.Top, x + band * 0.9f, r.Bottom);
            }
        }
    }

    // Chrome spoon antenna: cylindrical multi-stop metal, specular streak, concave bowl.
    static void ChromeSpoon(Graphics g, float cx, float cy, float ang, float len, float u)
    {
        var st = g.Save();
        g.TranslateTransform(cx * u, cy * u);
        g.RotateTransform(ang);
        float hw = Math.Max(1f, 1.1f * u);
        float rb = Math.Max(2f, 2.6f * u);
        float L = len * u;
        using (var path = new GraphicsPath())
        {
            path.AddRectangle(new RectangleF(-hw, -L, hw * 2f, L));
            path.AddEllipse(new RectangleF(-rb, -L - rb * 1.6f, rb * 2f, rb * 2.2f));
            using (var hp = new Pen(Color.White, Math.Max(1f, 1.2f * u) * 2f + 1f))
            {
                hp.LineJoin = LineJoin.Round;
                g.DrawPath(hp, path);
            }
            var b = path.GetBounds();
            if (b.Width < 1f) b.Width = 1f;
            using (var cb = new LinearGradientBrush(b, ChromeHi, ChromeLo, 0f))
            {
                var blend = new ColorBlend(5);
                blend.Colors = new Color[] { ChromeLo, ChromeHi, Color.FromArgb(255, 205, 214, 221), ChromeHi, ChromeLo };
                blend.Positions = new float[] { 0f, 0.28f, 0.5f, 0.72f, 1f };
                cb.InterpolationColors = blend;
                g.FillPath(cb, path);
            }
            // concave bowl shade + rim light
            using (var bs = new SolidBrush(Color.FromArgb(90, 140, 152, 162)))
                g.FillEllipse(bs, -rb * 0.6f, -L - rb * 1.3f, rb * 1.2f, rb * 1.5f);
            using (var ip = new Pen(Ink, Math.Max(0.8f, 0.9f * u)))
            {
                ip.LineJoin = LineJoin.Round;
                g.DrawPath(ip, path);
            }
            // specular streak on handle + hot dot on bowl
            using (var sp = new Pen(Color.FromArgb(210, 255, 255, 255), Math.Max(0.5f, 0.5f * u)))
            {
                sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
                g.DrawLine(sp, -hw * 0.35f, -L * 0.9f, -hw * 0.35f, -L * 0.15f);
            }
            using (var sb = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                g.FillEllipse(sb, -rb * 0.45f, -L - rb * 1.25f, rb * 0.6f, rb * 0.75f);
        }
        g.Restore(st);
    }

    // The single cyan glow: radial halo + radial core, Coldy's luminous eye.
    static void CyanGlow(Graphics g, float cx, float cy, float r, float u)
    {
        float x = cx * u, y = cy * u, rc = r * u;
        using (var halo = new GraphicsPath())
        {
            halo.AddEllipse(x - rc * 2.2f, y - rc * 2.2f, rc * 4.4f, rc * 4.4f);
            using (var pg = new PathGradientBrush(halo))
            {
                pg.CenterColor = Color.FromArgb(110, 53, 198, 244);
                pg.SurroundColors = new Color[] { Color.FromArgb(0, 53, 198, 244) };
                g.FillPath(pg, halo);
            }
        }
        using (var ib = new SolidBrush(Ink))
            g.FillEllipse(ib, x - rc - 0.8f * u, y - rc - 0.8f * u, (rc + 0.8f * u) * 2f, (rc + 0.8f * u) * 2f);
        using (var core = new GraphicsPath())
        {
            core.AddEllipse(x - rc, y - rc, rc * 2f, rc * 2f);
            using (var pg = new PathGradientBrush(core))
            {
                pg.CenterColor = Color.FromArgb(255, 150, 230, 252);
                pg.SurroundColors = new Color[] { Cyan };
                pg.CenterPoint = new PointF(x - rc * 0.3f, y - rc * 0.3f);
                g.FillPath(pg, core);
            }
        }
        using (var wb = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            g.FillEllipse(wb, x - rc * 0.45f, y - rc * 0.5f, rc * 0.6f, rc * 0.6f);
    }

    // Felt snowball: plush body + top-light / bottom-shade arcs + knit rotation marker.
    static void Snowball(Graphics g, float cx, float cy, float r, float u, float rot)
    {
        float x = cx * u, y = cy * u, rc = r * u;
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(x - rc, y - rc, rc * 2f, rc * 2f);
            FeltBody(g, path, u, null);
        }
        using (var hi = new Pen(Color.FromArgb(140, 255, 255, 255), Math.Max(0.8f, 1.1f * u)))
        {
            hi.StartCap = LineCap.Round; hi.EndCap = LineCap.Round;
            g.DrawArc(hi, x - rc * 0.75f, y - rc * 0.75f, rc * 1.5f, rc * 1.5f, 190, 90);
        }
        using (var lo = new Pen(Color.FromArgb(90, 205, 214, 221), Math.Max(0.8f, 1.1f * u)))
        {
            lo.StartCap = LineCap.Round; lo.EndCap = LineCap.Round;
            g.DrawArc(lo, x - rc * 0.75f, y - rc * 0.75f, rc * 1.5f, rc * 1.5f, 20, 90);
        }
        var st = g.Save();
        g.TranslateTransform(x, y);
        g.RotateTransform(rot);
        using (var kp = new Pen(KnitBlue, Math.Max(1f, 1.2f * u)))
        {
            kp.StartCap = LineCap.Round;
            kp.EndCap = LineCap.Round;
            g.DrawArc(kp, -rc * 0.55f, -rc * 0.55f, rc * 1.1f, rc * 1.1f, 200, 120);
        }
        g.Restore(st);
    }

    // Rainbow cable trail: each strand gets a dark seam + colour + top highlight.
    static void RainbowTrail(Graphics g, float x0, float y0, float x1, float y1, float u)
    {
        for (int i = 0; i < Rainbow.Length; i++)
        {
            float off = (i - 2f) * 0.9f * u;
            float w = Math.Max(0.8f, 0.8f * u);
            float mx = (x0 + x1) / 2f * u, my = (y0 + y1) / 2f * u;
            using (var seam = new Pen(Color.FromArgb(70, 31, 40, 56), w + 0.6f * u))
            {
                seam.StartCap = LineCap.Round; seam.EndCap = LineCap.Round;
                g.DrawBezier(seam, x0 * u, y0 * u + off, mx, my + off * 2f, mx, my - off, x1 * u, y1 * u + off);
            }
            using (var p = new Pen(Rainbow[i], w))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawBezier(p, x0 * u, y0 * u + off, mx, my + off * 2f, mx, my - off, x1 * u, y1 * u + off);
            }
            using (var hi = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(0.4f, w * 0.35f)))
            {
                hi.StartCap = LineCap.Round; hi.EndCap = LineCap.Round;
                g.DrawBezier(hi, x0 * u, y0 * u + off - w * 0.25f, mx, my + off * 2f - w * 0.25f, mx, my - off - w * 0.25f, x1 * u, y1 * u + off - w * 0.25f);
            }
        }
    }

    static void FeltArrow(Graphics g, float u, out int hx, out int hy)
    {
        using (var path = ArrowPath(0, 0, 1, u))
            GlacierFill(g, path, u, true, delegate(Graphics gg, float uu)
            {
                KnitStripes(gg, new RectangleF(4 * uu, 15 * uu, 12 * uu, 4 * uu), uu, true);
                CyanGlow(gg, 7.5f, 21f, 1.4f, uu);
            });
        hx = Px(4, u); hy = Px(2.6f, u);
    }

    static void FeltHand(Graphics g, float u, out int hx, out int hy)
    {
        PointF[] pts = new PointF[] {
            new PointF(9*u,25*u), new PointF(8*u,12*u), new PointF(11*u,5*u),
            new PointF(15*u,3.5f*u), new PointF(18*u,6*u), new PointF(19*u,11*u),
            new PointF(23*u,12*u), new PointF(24*u,15*u), new PointF(20*u,17*u),
            new PointF(19*u,25*u)
        };
        using (var path = new GraphicsPath())
        {
            path.AddClosedCurve(pts, 0.4f);
            FeltBody(g, path, u, delegate(Graphics gg, float uu)
            {
                KnitStripes(gg, new RectangleF(7 * uu, 22.5f * uu, 14 * uu, 4 * uu), uu, true);
            });
        }
        hx = Px(14, u); hy = Px(4, u);
    }

    static void FeltIBeam(Graphics g, float u, out int hx, out int hy)
    {
        using (var path = new GraphicsPath())
        {
            path.AddRectangle(new RectangleF(12 * u, 4 * u, 8 * u, 2.2f * u));
            path.AddRectangle(new RectangleF(12 * u, 25.8f * u, 8 * u, 2.2f * u));
            path.AddRectangle(new RectangleF(15 * u, 4 * u, 2 * u, 24 * u));
            FeltBody(g, path, u, delegate(Graphics gg, float uu)
            {
                KnitStripes(gg, new RectangleF(11 * uu, 3 * uu, 10 * uu, 26 * uu), uu, true);
            });
        }
        hx = Px(16, u); hy = Px(16, u);
    }

    static void FeltWait(Graphics g, float u, float ang, out int hx, out int hy)
    {
        using (var path = SnowflakePath(16, 16, 12, u))
        using (var m = new Matrix())
        {
            m.RotateAt(ang, new PointF(16 * u, 16 * u));
            path.Transform(m);
            GlacierIceStroke(g, path, Math.Max(1f, 1.6f * u), u);
        }
        ChromeSpoon(g, 16, 16, ang, 3.5f, u);
        hx = Px(16, u); hy = Px(16, u);
    }

    static void FeltBusy(Graphics g, float u, float ang, out int hx, out int hy)
    {
        RainbowTrail(g, 8, 25, 24, 25, u);
        Snowball(g, 16, 15, 9, u, ang);
        hx = Px(16, u); hy = Px(15, u);
    }

    static void FeltHelp(Graphics g, float u, float wave, out int hx, out int hy)
    {
        float bob = wave * 1.2f;
        ChromeSpoon(g, 13, 20 + bob, -25, 12, u);
        using (var sf = SnowflakePath(24, 9 + bob, 4, u))
            GlacierIceStroke(g, sf, Math.Max(0.8f, 1f * u), u);
        CyanGlow(g, 24, 9 + bob, 1.2f, u);
        hx = Px(16, u); hy = Px(16, u);
    }

    static void FeltCross(Graphics g, float u, out int hx, out int hy)
    {
        using (var ring = new GraphicsPath())
        {
            ring.AddEllipse(8 * u, 8 * u, 16 * u, 16 * u);
            GlacierIceStroke(g, ring, Math.Max(1f, 1.2f * u), u);
        }
        using (var ticks = new GraphicsPath())
        {
            ticks.AddLine(16 * u, 2 * u, 16 * u, 7 * u);
            ticks.AddLine(16 * u, 25 * u, 16 * u, 30 * u);
            ticks.AddLine(2 * u, 16 * u, 7 * u, 16 * u);
            ticks.AddLine(25 * u, 16 * u, 30 * u, 16 * u);
            GlacierIceStroke(g, ticks, Math.Max(1f, 1.4f * u), u);
        }
        CyanGlow(g, 16, 16, 1.6f, u);
        hx = Px(16, u); hy = Px(16, u);
    }

    static void FeltNo(Graphics g, float u, float wave, out int hx, out int hy)
    {
        float shake = wave * 5f;
        for (int k = 0; k < 2; k++)
        {
            var st = g.Save();
            g.TranslateTransform(16 * u, 16 * u);
            g.RotateTransform(45f + (k == 0 ? 0f : 90f) + shake);
            var band = new RectangleF(-12 * u, -2.6f * u, 24 * u, 5.2f * u);
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(band);
                using (var hp = new Pen(Color.White, Math.Max(1f, 1.2f * u) * 2f + 1f))
                {
                    hp.LineJoin = LineJoin.Round;
                    g.DrawPath(hp, path);
                }
                ScarfStripes(g, band, u);
                using (var ip = new Pen(Ink, Math.Max(0.8f, 1f * u)))
                {
                    ip.LineJoin = LineJoin.Round;
                    g.DrawPath(ip, path);
                }
            }
            g.Restore(st);
        }
        hx = Px(16, u); hy = Px(16, u);
    }

    static void FeltResize(Graphics g, float u, string style, float wave, out int hx, out int hy)
    {
        float ang = style == "we" ? 0f : style == "ns" ? 90f : style == "nwse" ? 45f : 135f;
        float L = 11f + wave * 2f;
        var st = g.Save();
        g.TranslateTransform(16 * u, 16 * u);
        g.RotateTransform(ang);
        var band = new RectangleF(-L * u, -2.4f * u, L * 2f * u, 4.8f * u);
        using (var path = new GraphicsPath())
        {
            path.AddRectangle(band);
            using (var hp = new Pen(Color.White, Math.Max(1f, 1.2f * u) * 2f + 1f))
            {
                hp.LineJoin = LineJoin.Round;
                g.DrawPath(hp, path);
            }
            KnitStripes(g, band, u, false);
            using (var ip = new Pen(Ink, Math.Max(0.8f, 1f * u)))
            {
                ip.LineJoin = LineJoin.Round;
                g.DrawPath(ip, path);
            }
        }
        using (var ap = new GraphicsPath())
        {
            ap.AddPolygon(new PointF[] {
                new PointF(-L*u-3*u,0), new PointF(-L*u+1*u,-2.6f*u), new PointF(-L*u+1*u,2.6f*u) });
            ap.AddPolygon(new PointF[] {
                new PointF(L*u+3*u,0), new PointF(L*u-1*u,-2.6f*u), new PointF(L*u-1*u,2.6f*u) });
            using (var hp = new Pen(Color.White, Math.Max(1f, 1f * u) * 2f + 1f))
            {
                hp.LineJoin = LineJoin.Round;
                g.DrawPath(hp, ap);
            }
            using (var ib = new SolidBrush(Ink)) g.FillPath(ib, ap);
        }
        g.Restore(st);
        hx = Px(16, u); hy = Px(16, u);
    }

    static void FeltMove(Graphics g, float u, float ang, out int hx, out int hy)
    {
        RainbowTrail(g, 4, 26, 14, 20, u);
        Snowball(g, 17, 14, 9, u, ang);
        hx = Px(17, u); hy = Px(14, u);
    }

    static void FeltUp(Graphics g, float u, out int hx, out int hy)
    {
        using (var basep = new GraphicsPath())
        {
            basep.AddEllipse(11 * u, 24 * u, 10 * u, 5 * u);
            FeltBody(g, basep, u, null);
        }
        ChromeSpoon(g, 16, 26, 0, 16, u);
        hx = Px(16, u); hy = Px(8, u);
    }

    // Encode the felt micro-motion as a real .ani at the requested size so
    // the animation survives scaling (same LoadImage path as the pixel pack).
    static string BuildFeltAni(string style, int size)
    {
        string dir = Path.Combine(Path.GetTempPath(), "ColdyFelt", size.ToString());
        string path = Path.Combine(dir, style + ".ani");
        if (File.Exists(path)) return path;
        try
        {
            Directory.CreateDirectory(dir);
            int n = FeltFrames(style);
            var fram = new List<byte>();
            fram.AddRange(Encoding.ASCII.GetBytes("fram"));
            for (int f = 0; f < n; f++)
            {
                int hx, hy;
                using (var bmp = RenderFelt(style, size, f, out hx, out hy))
                    fram.AddRange(AniChunk("icon", CurFromBitmap(bmp, hx, hy)));
            }
            var chunks = new List<byte[]>();
            chunks.Add(AniChunk("anih", AniHeader(n, n, size, 5, 1)));
            chunks.Add(AniChunk("LIST", fram.ToArray()));
            int total = 4;
            foreach (byte[] c in chunks) total += c.Length;
            using (var ms = new MemoryStream())
            {
                ms.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                ms.Write(BitConverter.GetBytes(total), 0, 4);
                ms.Write(Encoding.ASCII.GetBytes("ACON"), 0, 4);
                foreach (byte[] c in chunks) ms.Write(c, 0, c.Length);
                File.WriteAllBytes(path, ms.ToArray());
            }
            return path;
        }
        catch { return null; }
    }

    // Pack a rendered bitmap as a 32bpp mini .cur (XOR + AND mask).
    static byte[] CurFromBitmap(Bitmap bmp, int hx, int hy)
    {
        int size = bmp.Width;
        int stride = size * 4;
        int mstride = ((size + 31) / 32) * 4;
        var xor = new byte[stride * size];
        var and = new byte[mstride * size];
        var data = bmp.LockBits(new Rectangle(0, 0, size, size),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var raw = new byte[data.Stride * size];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raw, 0, raw.Length);
        bmp.UnlockBits(data);
        for (int y = 0; y < size; y++)
        {
            int sy = size - 1 - y;
            for (int x = 0; x < size; x++)
            {
                int o = y * data.Stride + x * 4;
                byte a = raw[o + 3];
                int q = sy * stride + x * 4;
                xor[q] = raw[o]; xor[q + 1] = raw[o + 1]; xor[q + 2] = raw[o + 2]; xor[q + 3] = a;
                if (a < 128) and[sy * mstride + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }
        int imgLen = 40 + xor.Length + and.Length;
        var img = new byte[imgLen];
        BitConverter.GetBytes(40).CopyTo(img, 0);
        BitConverter.GetBytes(size).CopyTo(img, 4);
        BitConverter.GetBytes(size * 2).CopyTo(img, 8);
        BitConverter.GetBytes((short)1).CopyTo(img, 12);
        BitConverter.GetBytes((short)32).CopyTo(img, 14);
        BitConverter.GetBytes(xor.Length + and.Length).CopyTo(img, 20);
        xor.CopyTo(img, 40);
        and.CopyTo(img, 40 + xor.Length);
        var r = new byte[22 + imgLen];
        BitConverter.GetBytes((short)2).CopyTo(r, 2);
        BitConverter.GetBytes((short)1).CopyTo(r, 4);
        r[6] = (byte)size; r[7] = (byte)size;
        BitConverter.GetBytes((short)hx).CopyTo(r, 10);
        BitConverter.GetBytes((short)hy).CopyTo(r, 12);
        BitConverter.GetBytes(imgLen).CopyTo(r, 14);
        BitConverter.GetBytes(22).CopyTo(r, 18);
        img.CopyTo(r, 22);
        return r;
    }

    // ---- Glacier design system ------------------------------------------
    // Every pointer lives on the same 32-unit grid and is built from three
    // layers: a white halo (visible on dark backgrounds), a polar-night ink
    // outline, and an ice-blue gradient body borrowed from Coldy's glowing
    // eye. Glyph cursors use an ink core under the same halo; the snowflake
    // wait cursor and the scarf-red accent tie the set to the
    // character. All strokes have round caps and joins so the set stays
    // smooth from 16 px to 128 px.

    public static readonly string[] AllStyles = new string[] {
        "arrow", "hand", "ibeam", "wait", "busy", "help", "cross",
        "no", "we", "ns", "nwse", "nesw", "move", "up"
    };

    static readonly Color Ink = Color.FromArgb(255, 31, 40, 56);
    static readonly Color IceHi = Color.FromArgb(255, 248, 254, 255);
    static readonly Color IceMid = Color.FromArgb(255, 216, 243, 255);
    static readonly Color IceLo = Color.FromArgb(255, 158, 220, 245);
    static readonly Color ScarfRed = Color.FromArgb(255, 226, 87, 75);

    static IntPtr Make(string style, int size)
    {
        int hx, hy;
        using (var bmp = Render(style, size, out hx, out hy))
            return FromBitmap(bmp, hx, hy);
    }

    // Renders one pointer with the production drawing code. Make uses it to
    // build the real system cursors; tools/MakeCursorSheet.cs uses it to draw
    // previews, so the preview images are pixel-identical to the live set.
    public static Bitmap Render(string style, int size, out int hx, out int hy)
    {
        if (size < 8) size = 8;
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float u = size / 32f;
            hx = size / 2;
            hy = size / 2;
            if (style == "hand") DrawHand(g, u, out hx, out hy);
            else if (style == "cross") DrawCross(g, u, out hx, out hy);
            else if (style == "ibeam") DrawIBeam(g, u, out hx, out hy);
            else if (style == "wait") DrawWait(g, u, out hx, out hy);
            else if (style == "no") DrawNo(g, u, out hx, out hy);
            else if (style == "up") DrawUp(g, u, out hx, out hy);
            else if (style == "busy") DrawBusy(g, u, out hx, out hy);
            else if (style == "help") DrawHelp(g, u, out hx, out hy);
            else if (style == "we" || style == "ns" || style == "nwse" || style == "nesw" || style == "move")
                DrawSize(g, u, style, out hx, out hy);
            else DrawArrow(g, u, out hx, out hy);
        }
        if (hx < 0) hx = 0;
        if (hx >= size) hx = size - 1;
        if (hy < 0) hy = 0;
        if (hy >= size) hy = size - 1;
        return bmp;
    }

    static int Px(float units, float u)
    {
        return (int)Math.Round(units * u);
    }

    static LinearGradientBrush IceBrush(RectangleF b)
    {
        if (b.Width < 1f) b.Width = 1f;
        if (b.Height < 1f) b.Height = 1f;
        var brush = new LinearGradientBrush(b, IceHi, IceLo, 65f);
        var blend = new ColorBlend(3);
        blend.Colors = new Color[] { IceHi, IceMid, IceLo };
        blend.Positions = new float[] { 0f, 0.45f, 1f };
        brush.InterpolationColors = blend;
        return brush;
    }

    static void StrokePath(Graphics g, GraphicsPath path, Color color, float width)
    {
        using (var pen = new Pen(color, width))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawPath(pen, path);
        }
    }

    // Filled pointer body: soft shadow, white halo, ice gradient, ink edge.
    // The optional detail pass is clipped to the body (finger creases, cuff).
    static void GlacierFill(Graphics g, GraphicsPath path, float u, bool shadow, Action<Graphics, float> detail)
    {
        float ink = Math.Max(1.0f, 1.7f * u);
        float halo = Math.Max(1.0f, 1.5f * u);
        if (shadow)
        {
            using (var shifted = (GraphicsPath)path.Clone())
            using (var m = new Matrix())
            using (var sh = new SolidBrush(Color.FromArgb(42, 24, 32, 48)))
            {
                m.Translate(0.6f * u + 0.4f, 0.9f * u + 0.6f);
                shifted.Transform(m);
                using (var sp = new Pen(sh, ink + halo * 2f))
                {
                    sp.LineJoin = LineJoin.Round;
                    g.DrawPath(sp, shifted);
                }
                g.FillPath(sh, shifted);
            }
        }
        using (var hp = new Pen(Color.White, ink + halo * 2f))
        {
            hp.LineJoin = LineJoin.Round;
            g.DrawPath(hp, path);
        }
        using (var fill = IceBrush(path.GetBounds()))
            g.FillPath(fill, path);
        if (detail != null)
        {
            var state = g.Save();
            g.SetClip(path);
            detail(g, u);
            g.Restore(state);
        }
        using (var ip = new Pen(Ink, ink))
        {
            ip.LineJoin = LineJoin.Round;
            g.DrawPath(ip, path);
        }
    }

    // Open glyph stroke: white halo under an ink or accent core.
    static void GlacierStroke(Graphics g, GraphicsPath path, float core, float u, Color color)
    {
        float halo = Math.Max(1.0f, 1.6f * u);
        StrokePath(g, path, Color.White, core + halo * 2f);
        StrokePath(g, path, color, core);
    }

    // Snowflake stroke: white halo, ink edge, ice-gradient core.
    static void GlacierIceStroke(Graphics g, GraphicsPath path, float core, float u)
    {
        float ink = Math.Max(0.8f, 1.0f * u);
        float halo = Math.Max(1.0f, 1.4f * u);
        StrokePath(g, path, Color.White, core + (ink + halo) * 2f);
        StrokePath(g, path, Ink, core + ink * 2f);
        using (var brush = IceBrush(path.GetBounds()))
        using (var pen = new Pen(brush, core))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawPath(pen, path);
        }
    }

    // Crystal marker: halo disc, ink ring, ice core. Marks the exact point
    // of the text insertion beam, the precision cross and the move cursor.
    static void CrystalDot(Graphics g, float cx, float cy, float r, float u)
    {
        CrystalDot(g, cx, cy, r, u, true);
    }

    // withHalo=false when the dot sits on an already-filled body (move):
    // a white halo there would eat a moat out of the ice fill.
    static void CrystalDot(Graphics g, float cx, float cy, float r, float u, bool withHalo)
    {
        float halo = withHalo ? Math.Max(0.8f, 1.2f * u) : 0f;
        float ink = Math.Max(0.7f, 0.9f * u);
        float rc = r * u;
        float ri = rc + ink;
        float ro = ri + halo;
        float x = cx * u;
        float y = cy * u;
        if (withHalo)
            g.FillEllipse(Brushes.White, x - ro, y - ro, ro * 2f, ro * 2f);
        using (var ib = new SolidBrush(Ink))
            g.FillEllipse(ib, x - ri, y - ri, ri * 2f, ri * 2f);
        using (var cb = IceBrush(new RectangleF(x - rc, y - rc, rc * 2f, rc * 2f)))
            g.FillEllipse(cb, x - rc, y - rc, rc * 2f, rc * 2f);
    }

    // Default arrow: classic pointer proportions (vertical left edge, ~45°
    // right edge, ~50° tip angle) so it reads as a pointer, not a flag. The
    // tip lands exactly on the hotspot.
    static GraphicsPath ArrowPath(float ox, float oy, float k, float u)
    {
        float[,] p = new float[,] {
            {4f,2.6f},{4f,23f},{9.4f,18.4f},{12.6f,26.2f},
            {16.8f,24.4f},{13.6f,16.9f},{20.6f,16.9f}
        };
        var pts = new PointF[7];
        for (int i = 0; i < 7; i++)
            pts[i] = new PointF((ox + p[i, 0] * k) * u, (oy + p[i, 1] * k) * u);
        var path = new GraphicsPath();
        path.AddPolygon(pts);
        return path;
    }

    // Six-armed snowflake with paired branches; replaces the hourglass.
    static GraphicsPath SnowflakePath(float cx, float cy, float r, float u)
    {
        var path = new GraphicsPath();
        for (int k = 0; k < 6; k++)
        {
            double a = (k * 60 - 90) * Math.PI / 180.0;
            float dx = (float)Math.Cos(a);
            float dy = (float)Math.Sin(a);
            path.StartFigure();
            path.AddLine(cx * u, cy * u, (cx + dx * r) * u, (cy + dy * r) * u);
            float bx = cx + dx * r * 0.60f;
            float by = cy + dy * r * 0.60f;
            for (int s = -1; s <= 1; s += 2)
            {
                double ba = a + s * 48.0 * Math.PI / 180.0;
                path.StartFigure();
                path.AddLine(bx * u, by * u,
                    (bx + (float)Math.Cos(ba) * r * 0.38f) * u,
                    (by + (float)Math.Sin(ba) * r * 0.38f) * u);
            }
        }
        return path;
    }

    static void DrawArrow(Graphics g, float u, out int hx, out int hy)
    {
        using (var path = ArrowPath(0f, 0f, 1f, u))
            GlacierFill(g, path, u, true, null);
        hx = Px(4f, u);
        hy = Px(2.6f, u);
    }

    // Alternate-select arrow (OCR_UP): vertical, same glacier body.
    static void DrawUp(Graphics g, float u, out int hx, out int hy)
    {
        PointF[] pts = {
            new PointF(16f*u, 2.8f*u), new PointF(23.2f*u, 11.6f*u),
            new PointF(18.7f*u, 11.6f*u), new PointF(18.7f*u, 27.6f*u),
            new PointF(13.3f*u, 27.6f*u), new PointF(13.3f*u, 11.6f*u),
            new PointF(8.8f*u, 11.6f*u)
        };
        using (var path = new GraphicsPath())
        {
            path.AddPolygon(pts);
            GlacierFill(g, path, u, true, null);
        }
        hx = Px(16f, u);
        hy = Px(2.8f, u);
    }

    // Pointing hand built from arcs and beziers instead of a jagged polygon;
    // the cuff carries ice / white / glacier stripes echoing the scarf tail.
    static void DrawHand(Graphics g, float u, out int hx, out int hy)
    {
        using (var path = new GraphicsPath())
        {
            path.AddLine(10.7f*u, 13.8f*u, 10.7f*u, 5.1f*u);
            path.AddArc(10.7f*u, 2.8f*u, 4.6f*u, 4.6f*u, 180f, 180f);
            path.AddLine(15.3f*u, 5.1f*u, 15.3f*u, 11.9f*u);
            path.AddArc(15.3f*u, 9.7f*u, 4.4f*u, 4.4f*u, 180f, 180f);
            path.AddLine(19.7f*u, 11.9f*u, 19.7f*u, 12.6f*u);
            path.AddArc(19.7f*u, 10.4f*u, 4.2f*u, 4.2f*u, 180f, 180f);
            path.AddLine(23.9f*u, 12.5f*u, 23.9f*u, 13.4f*u);
            path.AddArc(23.9f*u, 11.4f*u, 4.0f*u, 4.0f*u, 180f, 180f);
            path.AddLine(27.9f*u, 13.4f*u, 27.9f*u, 19.6f*u);
            path.AddBezier(27.9f*u, 19.6f*u, 27.9f*u, 24.3f*u, 26.2f*u, 28.4f*u, 22.3f*u, 28.4f*u);
            path.AddLine(22.3f*u, 28.4f*u, 14.2f*u, 28.4f*u);
            path.AddBezier(14.2f*u, 28.4f*u, 11.0f*u, 28.4f*u, 9.2f*u, 26.6f*u, 7.9f*u, 23.9f*u);
            path.AddBezier(7.9f*u, 23.9f*u, 6.9f*u, 21.8f*u, 5.2f*u, 19.3f*u, 5.2f*u, 17.4f*u);
            path.AddArc(5.2f*u, 15.1f*u, 4.6f*u, 4.6f*u, 180f, 140f);
            path.CloseFigure();
            GlacierFill(g, path, u, true, delegate(Graphics gg, float uu)
            {
                using (var crease = new Pen(Ink, Math.Max(0.8f, 1.0f * uu)))
                {
                    crease.StartCap = LineCap.Round;
                    crease.EndCap = LineCap.Round;
                    gg.DrawLine(crease, 15.3f*uu, 12.4f*uu, 15.3f*uu, 16.4f*uu);
                    gg.DrawLine(crease, 19.7f*uu, 13.0f*uu, 19.7f*uu, 16.4f*uu);
                    gg.DrawLine(crease, 23.9f*uu, 13.8f*uu, 23.9f*uu, 16.6f*uu);
                }
                using (var gb = new SolidBrush(Color.FromArgb(255, 197, 236, 255)))
                    gg.FillRectangle(gb, 4f*uu, 23.9f*uu, 26f*uu, 1.8f*uu);
                using (var cb = new SolidBrush(Color.White))
                    gg.FillRectangle(cb, 4f*uu, 25.7f*uu, 26f*uu, 0.9f*uu);
                using (var rb = new SolidBrush(IceLo))
                    gg.FillRectangle(rb, 4f*uu, 26.6f*uu, 26f*uu, 3f*uu);
            });
        }
        hx = Px(13f, u);
        hy = Px(2.8f, u);
    }

    // Text beam with flared serif caps and a crystal insertion marker.
    static void DrawIBeam(Graphics g, float u, out int hx, out int hy)
    {
        using (var path = new GraphicsPath())
        {
            path.AddLine(16f*u, 7.4f*u, 16f*u, 24.6f*u);
            path.StartFigure();
            path.AddBezier(10.6f*u, 5.8f*u, 13.0f*u, 5.8f*u, 14.6f*u, 7.4f*u, 16f*u, 7.4f*u);
            path.AddBezier(16f*u, 7.4f*u, 17.4f*u, 7.4f*u, 19.0f*u, 5.8f*u, 21.4f*u, 5.8f*u);
            path.StartFigure();
            path.AddBezier(10.6f*u, 26.2f*u, 13.0f*u, 26.2f*u, 14.6f*u, 24.6f*u, 16f*u, 24.6f*u);
            path.AddBezier(16f*u, 24.6f*u, 17.4f*u, 24.6f*u, 19.0f*u, 26.2f*u, 21.4f*u, 26.2f*u);
            GlacierStroke(g, path, Math.Max(1.6f, 2.3f*u), u, Ink);
        }
        CrystalDot(g, 16f, 16f, 1.5f, u);
        hx = Px(16f, u);
        hy = Px(16f, u);
    }

    // Precision cross with a center gap and crystal dot, so the exact pixel
    // stays visible instead of being buried under two thick bars.
    static void DrawCross(Graphics g, float u, out int hx, out int hy)
    {
        using (var path = new GraphicsPath())
        {
            path.AddLine(16f*u, 3.4f*u, 16f*u, 11.4f*u);
            path.StartFigure();
            path.AddLine(16f*u, 20.6f*u, 16f*u, 28.6f*u);
            path.StartFigure();
            path.AddLine(3.4f*u, 16f*u, 11.4f*u, 16f*u);
            path.StartFigure();
            path.AddLine(20.6f*u, 16f*u, 28.6f*u, 16f*u);
            GlacierStroke(g, path, Math.Max(1.6f, 2.2f*u), u, Ink);
        }
        CrystalDot(g, 16f, 16f, 1.4f, u);
        hx = Px(16f, u);
        hy = Px(16f, u);
    }

    // Wait: a six-armed snowflake instead of the two-triangle hourglass.
    static void DrawWait(Graphics g, float u, out int hx, out int hy)
    {
        using (var path = SnowflakePath(16f, 16f, 12.2f, u))
            GlacierIceStroke(g, path, Math.Max(1.5f, 2.1f*u), u);
        hx = Px(16f, u);
        hy = Px(16f, u);
    }

    // Working-in-background: glacier arrow with a snowflake badge.
    static void DrawBusy(Graphics g, float u, out int hx, out int hy)
    {
        using (var arrow = ArrowPath(0.4f, 0.4f, 0.66f, u))
            GlacierFill(g, arrow, u, true, null);
        using (var flake = SnowflakePath(21.8f, 21.8f, 6.5f, u))
            GlacierIceStroke(g, flake, Math.Max(1.2f, 1.7f*u), u);
        hx = Px(0.4f + 4f * 0.66f, u);
        hy = Px(0.4f + 2.6f * 0.66f, u);
    }

    // Help: glacier arrow with an ice "?" badge.
    static void DrawHelp(Graphics g, float u, out int hx, out int hy)
    {
        using (var arrow = ArrowPath(0.4f, 0.4f, 0.66f, u))
            GlacierFill(g, arrow, u, true, null);
        using (var disc = new GraphicsPath())
        {
            disc.AddEllipse(16.4f*u, 16.4f*u, 13.2f*u, 13.2f*u);
            GlacierFill(g, disc, u, false, null);
        }
        using (var q = new GraphicsPath())
        using (var fmt = new StringFormat())
        {
            fmt.Alignment = StringAlignment.Center;
            fmt.LineAlignment = StringAlignment.Center;
            q.AddString("?", FontFamily.GenericSansSerif, (int)FontStyle.Bold,
                9.2f * u, new RectangleF(16.4f*u, 16.0f*u, 13.2f*u, 13.6f*u), fmt);
            using (var wb = new SolidBrush(Color.White))
                g.FillPath(wb, q);
        }
        hx = Px(0.4f + 4f * 0.66f, u);
        hy = Px(0.4f + 2.6f * 0.66f, u);
    }

    // Unavailable: ink ring with a scarf-red slash.
    static void DrawNo(Graphics g, float u, out int hx, out int hy)
    {
        using (var ring = new GraphicsPath())
        {
            ring.AddEllipse(5.4f*u, 5.4f*u, 21.2f*u, 21.2f*u);
            GlacierStroke(g, ring, Math.Max(1.7f, 2.7f*u), u, Ink);
        }
        using (var slash = new GraphicsPath())
        {
            slash.AddLine(8.8f*u, 8.8f*u, 23.2f*u, 23.2f*u);
            GlacierStroke(g, slash, Math.Max(1.7f, 2.7f*u), u, ScarfRed);
        }
        hx = Px(16f, u);
        hy = Px(16f, u);
    }

    static void DrawSize(Graphics g, float u, string kind, out int hx, out int hy)
    {
        if (kind == "move") DrawMove(g, u);
        else if (kind == "we") ResizeAxis(g, u, 0f);
        else if (kind == "ns") ResizeAxis(g, u, 90f);
        else if (kind == "nwse") ResizeAxis(g, u, 45f);
        else if (kind == "nesw") ResizeAxis(g, u, 135f);
        hx = Px(16f, u);
        hy = Px(16f, u);
    }

    // Move: one unified 4-way polygon. Two crossed axes would cut each other
    // apart with their white halos, leaving four detached heads.
    static void DrawMove(Graphics g, float u)
    {
        float[,] p = new float[,] {
            {0f,-12.6f},{-4.8f,-6.2f},{-1.5f,-6.2f},{-1.5f,-1.5f},
            {-6.2f,-1.5f},{-6.2f,-4.8f},{-12.6f,0f},{-6.2f,4.8f},
            {-6.2f,1.5f},{-1.5f,1.5f},{-1.5f,6.2f},{-4.8f,6.2f},
            {0f,12.6f},{4.8f,6.2f},{1.5f,6.2f},{1.5f,1.5f},
            {6.2f,1.5f},{6.2f,4.8f},{12.6f,0f},{6.2f,-4.8f},
            {6.2f,-1.5f},{1.5f,-1.5f},{1.5f,-6.2f},{4.8f,-6.2f}
        };
        var pts = new PointF[24];
        for (int i = 0; i < 24; i++)
            pts[i] = new PointF((16f + p[i, 0]) * u, (16f + p[i, 1]) * u);
        using (var body = new GraphicsPath())
        {
            body.AddPolygon(pts);
            GlacierFill(g, body, u, false, null);
        }
        CrystalDot(g, 16f, 16f, 1.1f, u, false);
    }

    // One resize axis as a single filled polygon (heads and shaft share the
    // ice gradient and one outline, so nothing looks detached). Built
    // horizontally around the origin, then rotated into place, so all four
    // directions and the move cursor share identical geometry.
    static void ResizeAxis(Graphics g, float u, float angle)
    {
        using (var m = new Matrix())
        {
            m.Translate(16f * u, 16f * u);
            m.Rotate(angle);
            m.Scale(u, u);
            using (var body = new GraphicsPath())
            {
                PointF[] pts = {
                    new PointF(-12.6f, 0f), new PointF(-6.2f, -4.8f),
                    new PointF(-6.2f, -1.5f), new PointF(6.2f, -1.5f),
                    new PointF(6.2f, -4.8f), new PointF(12.6f, 0f),
                    new PointF(6.2f, 4.8f), new PointF(6.2f, 1.5f),
                    new PointF(-6.2f, 1.5f), new PointF(-6.2f, 4.8f)
                };
                body.AddPolygon(pts);
                body.Transform(m);
                GlacierFill(g, body, u, false, null);
            }
        }
    }

    static void DrawDot(Graphics g, int size, out int hx, out int hy)
    {
        float d = size * 0.45f;
        float o = (size - d) / 2f;
        g.FillEllipse(Brushes.White, o, o, d, d);
        using (var pen = new Pen(Color.Black, Math.Max(1.2f, size / 16f)))
            g.DrawEllipse(pen, o, o, d, d);
        hx = size / 2;
        hy = size / 2;
    }

    static IntPtr FromBitmap(Bitmap bmp, int hx, int hy)
    {
        IntPtr hIcon = bmp.GetHicon();
        IconInfo info;
        GetIconInfo(hIcon, out info);
        info.fIcon = false;
        info.xHotspot = hx;
        info.yHotspot = hy;
        IntPtr cur = CreateIconIndirect(ref info);
        if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
        if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
        DestroyIcon(hIcon);
        return cur;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll")]
    static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

    [DllImport("user32.dll")]
    static extern IntPtr CreateIconIndirect(ref IconInfo piconinfo);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    static extern bool DestroyCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    static extern bool SetSystemCursor(IntPtr hcur, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);
}

static class UpdateClient
{
    const string Latest = "https://api.github.com/repos/wei-ei-ei/coldy-mouse-pendant/releases/latest";

    public static bool TryGetNewer(string current, out string tag, out string url)
    {
        tag = null;
        url = null;
        EnsureTls();
        string json;
        using (var client = new WebClient())
        {
            client.Headers[HttpRequestHeader.UserAgent] = "Coldy";
            json = client.DownloadString(Latest);
        }
        // Update mode: only a published (non-draft, non-prerelease) GitHub release
        // newer than the running version may trigger an update; commits/tags never do.
        if (JsonBool(json, "draft") || JsonBool(json, "prerelease")) return false;
        tag = JsonString(json, "tag_name", 0);
        if (tag == null || tag.Length == 0) return false;
        if (CompareVersions(tag, current) <= 0) return false;
        url = FindZip(json);
        return url != null;
    }

    public static void Download(string url, string path)
    {
        EnsureTls();
        using (var client = new WebClient())
        {
            client.Headers[HttpRequestHeader.UserAgent] = "Coldy";
            client.DownloadFile(url, path);
        }
    }

    public static string PayloadRoot(string unpacked)
    {
        if (File.Exists(Path.Combine(unpacked, "Coldy.exe"))) return unpacked;
        string[] dirs = Directory.GetDirectories(unpacked);
        if (dirs.Length == 1 && File.Exists(Path.Combine(dirs[0], "Coldy.exe"))) return dirs[0];
        return unpacked;
    }

    static void EnsureTls()
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
        catch { }
    }

    static string FindZip(string json)
    {
        string found = null;
        int from = 0;
        while (from < json.Length)
        {
            string value = JsonString(json, "browser_download_url", from);
            if (value == null) break;
            int at = json.IndexOf(value, from);
            from = at < 0 ? json.Length : at + value.Length;
            value = value.Replace("\\/", "/");
            if (!value.EndsWith(".zip")) continue;
            if (value.IndexOf("/wei-ei-ei/coldy-mouse-pendant/") < 0) continue;
            found = value;
            if (value.IndexOf("win") >= 0) return value;
        }
        return found;
    }

    static string JsonString(string json, string key, int from)
    {
        string token = "\"" + key + "\"";
        int at = json.IndexOf(token, from);
        if (at < 0) return null;
        int quote = json.IndexOf('"', at + token.Length);
        if (quote < 0) return null;
        int end = json.IndexOf('"', quote + 1);
        if (end < 0) return null;
        return json.Substring(quote + 1, end - quote - 1);
    }

    static bool JsonBool(string json, string key)
    {
        string token = "\"" + key + "\"";
        int at = json.IndexOf(token, 0);
        if (at < 0) return false;
        int colon = json.IndexOf(':', at + token.Length);
        if (colon < 0) return false;
        int i = colon + 1;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        return i + 4 <= json.Length && json.Substring(i, 4) == "true";
    }

    static int CompareVersions(string remote, string local)
    {
        int[] left = ParseVersion(remote);
        int[] right = ParseVersion(local);
        for (int i = 0; i < 3; i++)
        {
            if (left[i] == right[i]) continue;
            return left[i] > right[i] ? 1 : -1;
        }
        return 0;
    }

    static int[] ParseVersion(string text)
    {
        int[] version = new int[3];
        if (text == null) return version;
        if (text.StartsWith("v") || text.StartsWith("V")) text = text.Substring(1);
        string[] parts = text.Split('.');
        for (int i = 0; i < parts.Length && i < 3; i++)
        {
            string digits = "";
            for (int c = 0; c < parts[i].Length; c++)
            {
                char ch = parts[i][c];
                if (ch < '0' || ch > '9') break;
                digits += ch;
            }
            int n;
            if (digits.Length > 0 && int.TryParse(digits, out n)) version[i] = n;
        }
        return version;
    }
}
