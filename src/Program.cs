using System;
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
    public const string AppVersion = "1.5.0";
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
    int formIndex;
    int animFrame;
    double animTime;
    const int AnimFrameMs = 100;
    readonly Rope rope = new Rope(RopeSegments, 120f, Gravity);
    float scale;
    float anchorX;
    float anchorY;
    float ropeLength = 120f;
    float displayHeight = 190f;
    float cursorSize = 32f;
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
        float hang = cursorSize * 0.45f;
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

    public void SetCursorSize(float size)
    {
        cursorSize = size;
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
        if (form2 == null || form2.Length == 0) index = 0;
        if (index != 0) index = 1;
        formIndex = index;
        animFrame = 0;
        animTime = 0;
        ApplyMetrics();
    }

    void ApplyMetrics()
    {
        if (displayHeight < 40f) displayHeight = 40f;
        Bitmap picture = CurrentPicture();
        scale = displayHeight / picture.Height;
        float ax = formIndex == 1 ? anchor2X : anchorSpriteX;
        float ay = formIndex == 1 ? anchor2Y : anchorSpriteY;
        anchorX = ax * scale;
        anchorY = ay * scale;
        rope.SetLength(ropeLength);
    }

    Bitmap CurrentPicture()
    {
        if (formIndex == 1 && form2 != null && form2.Length > 0)
            return form2[animFrame];
        return sprite;
    }

    public Bitmap PreviewPicture()
    {
        return CurrentPicture();
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

        if (formIndex == 1 && form2 != null && form2.Length > 1)
        {
            animTime += elapsed;
            while (animTime >= AnimFrameMs)
            {
                animTime -= AnimFrameMs;
                animFrame++;
                if (animFrame >= form2.Length) animFrame = 0;
            }
        }

        Point cursor = CursorPosition();

        float hang = cursorSize * 0.45f;
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
            g.DrawImage(picture, 0, 0, dw, dh);
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

    public readonly int Segments;
    public readonly int Points;
    public readonly double[] X, Y, OldX, OldY;
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
        if (restLen < 24f) restLen = 24f;
        RestLen = restLen;
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
    readonly TextBox nameBox = new TextBox();
    readonly Panel nameFrame = new Panel();
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
    readonly System.Windows.Forms.Timer previewTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
    readonly Font headerFont = new Font("Microsoft YaHei UI", 13f, FontStyle.Regular);
    Bitmap mark;
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
    bool cursorApplied;
    bool exitRequested;
    bool startHidden;
    bool allowShow;
    bool updateBusy;
    bool updating;
    string settingsPath;
    Color ropeColor = Color.FromArgb(55, 55, 62);
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
        string markPath = Path.Combine(dir, "assets", "coldy.png");
        if (File.Exists(markPath))
        {
            try { mark = new Bitmap(markPath); }
            catch { mark = null; }
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
        markDrawH = bodyH * 320 / 242;
        hairlineY = 12 + markDrawH + 12;
        int y = hairlineY + 14;
        int gap = 8;
        int right = clientW - 28;
        int fieldW = right - editorX;
        if (fieldW > 280) fieldW = 280;

        nameBox.BorderStyle = BorderStyle.None;
        nameBox.BackColor = Field;
        nameFrame.BorderStyle = BorderStyle.FixedSingle;
        nameFrame.BackColor = Field;
        nameFrame.Controls.Add(nameBox);
        nameBox.Text = "Coldy";

        SetupBar(ropeBar, 40, 320, 120);
        SetupBar(sizeBar, 80, 420, 190);
        SetupBar(cursorSizeBar, 16, 128, 32);
        PrepareButton(colorButton);
        colorButton.Text = "选择颜色";
        PrepareCombo(formBox);
        formBox.Items.AddRange(new object[] { "形态 1", "形态 2" });
        formBox.SelectedIndex = 0;
        PrepareCombo(cursorBox);
        cursorBox.Items.AddRange(new object[] { "系统默认", "跟随场景" });
        cursorBox.SelectedIndex = 0;
        PrepareButton(hotkeyButton);
        hotkeyButton.Text = "F8";
        PrepareButton(hideButton);
        hideButton.Text = "隐藏人物";
        Controls.Add(hideButton);

        y = AddSection(y, "角色");
        AddRow(y, "名字", nameFrame, fieldW, true);
        y += boxHeight + gap;
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

        nameBox.TextChanged += delegate
        {
            Invalidate();
            if (!loading) ApplyAll(false);
        };
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
        previewTimer.Interval = 100;
        previewTimer.Tick += delegate
        {
            if (IsDisposed || formBox.SelectedIndex != 1) return;
            Invalidate(new Rectangle(0, 0, ClientSize.Width, hairlineY + 1));
        };
        previewTimer.Start();
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
        CenterNameText();
        colorButton.Height = boxHeight;
        hotkeyButton.Height = boxHeight;
        hideButton.Height = boxHeight;
        formBox.Height = boxHeight;
        cursorBox.Height = boxHeight;
        colorSwatch.SetBounds(colorButton.Right + 8, colorButton.Top, boxHeight, boxHeight);
        hideButton.Top = hotkeyButton.Top;
        nameFrame.Height = boxHeight;
        CenterNameText();
    }

    void CenterNameText()
    {
        int innerW = nameFrame.ClientSize.Width;
        int innerH = nameFrame.ClientSize.Height;
        if (innerW < 8) innerW = nameFrame.Width - 2;
        if (innerH < 8) innerH = nameFrame.Height - 2;
        int textH = nameBox.PreferredHeight;
        if (textH > innerH) textH = innerH;
        int textTop = (innerH - textH) / 2;
        if (textTop < 0) textTop = 0;
        nameBox.SetBounds(4, textTop, innerW - 8, textH);
    }

    int AddSection(int y, string title)
    {
        var bar = new Panel();
        bar.BackColor = Color.FromArgb(75, 168, 212);
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
        box.IntegralHeight = false;
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.FlatStyle = FlatStyle.Flat;
        box.BackColor = Field;
        box.ForeColor = Ink;
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
            dialog.FullOpen = true;
            dialog.AnyColor = true;
            dialog.SolidColorOnly = false;
            dialog.Color = ropeColor;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            ropeColor = dialog.Color;
            ShowRopeColor();
            ApplyAll(false);
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
        cursorSizeBar.Enabled = cursorBox.SelectedIndex == 1;
    }

    void ApplyAll(bool cursor)
    {
        RefreshNumbers();
        string name = nameBox.Text.Trim();
        if (name.Length == 0) name = "Coldy";
        Text = name;
        pendant.SetName(name);
        pendant.SetRopeLength(ropeBar.Value);
        pendant.SetRopeColor(ropeColor);
        pendant.SetDisplayHeight(sizeBar.Value);
        pendant.SetForm(formBox.SelectedIndex);
        pendant.SetCursorSize(cursorSizeBar.Value);
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
        if (cursorBox.SelectedIndex != 1)
        {
            CursorFactory.Restore();
            cursorApplied = false;
            return;
        }
        CursorFactory.ApplyScheme(cursorSizeBar.Value);
        cursorApplied = true;
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        nameBox.SelectionStart = nameBox.Text.Length;
        nameBox.SelectionLength = 0;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!IsHandleCreated || WindowState == FormWindowState.Minimized) return;
        ApplyRound();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        string title = nameBox.Text.Trim();
        if (title.Length == 0) title = "Coldy";
        Bitmap frame = mark;
        if (formBox.SelectedIndex == 1 && pendant != null && !pendant.IsDisposed)
        {
            Bitmap live = pendant.PreviewPicture();
            if (live != null) frame = live;
        }
        int top = 12;
        float unit = markDrawH / 320f;
        int textX = 28 + (int)Math.Round(340f * unit) + 16;
        if (frame != null && frame.Height > 0 && markDrawH > 0 && unit > 0f)
        {
            Rectangle dest;
            if (frame == mark)
            {
                int pngW = (int)Math.Round(256f * unit);
                int pngH = (int)Math.Round(242f * unit);
                int pngX = 28 + (int)Math.Round((160f - 134.1f) * unit);
                int pngY = top + (int)Math.Round((78f - 50.1f) * unit);
                dest = new Rectangle(pngX, pngY, pngW, pngH);
            }
            else
            {
                dest = new Rectangle(28, top, (int)Math.Round(340f * unit), markDrawH);
            }
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(frame, dest);
        }
        int textRight = minButton.Left - 12;
        if (textRight < textX + 40) textRight = ClientSize.Width - 24;
        var rect = new Rectangle(textX, top, textRight - textX, markDrawH);
        TextRenderer.DrawText(e.Graphics, title, headerFont, rect, Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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
        int pid = Process.GetCurrentProcess().Id;
        string bat = Path.Combine(Path.GetTempPath(), "coldy-update.cmd");
        string script = "@echo off\r\n"
            + ":wait\r\n"
            + "tasklist /FI \"PID eq " + pid + "\" | find \"" + pid + "\" >nul\r\n"
            + "if not errorlevel 1 (\r\n"
            + "ping -n 2 127.0.0.1 >nul\r\n"
            + "goto wait\r\n"
            + ")\r\n"
            + "xcopy /e /y /i \"" + unpacked + "\\*\" \"" + dest + "\\\" >nul\r\n"
            + "start \"\" \"" + exe + "\"\r\n"
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
        previewTimer.Stop();
        updateTimer.Stop();
        if (cursorApplied) CursorFactory.Restore();
        SaveSettings();
        if (mark != null) mark.Dispose();
        mark = null;
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
            if (key == "name") nameBox.Text = val;
            else if (key == "rope") SetBar(ropeBar, val);
            else if (key == "ropeColor") ropeColor = ParseColor(val);
            else if (key == "size") SetBar(sizeBar, val);
            else if (key == "cursorSize") SetBar(cursorSizeBar, val);
            else if (key == "cursor") SelectCursor(val);
            else if (key == "form") SelectForm(val);
            else if (key == "background") backgroundBox.Checked = val == "1";
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
        if (val == "scheme" || val == "arrow" || val == "hand" || val == "cross" || val == "ibeam" || val == "dot")
            cursorBox.SelectedIndex = 1;
        else
            cursorBox.SelectedIndex = 0;
    }

    void SelectForm(string val)
    {
        formBox.SelectedIndex = (val == "2" || val == "gif") ? 1 : 0;
    }

    void SaveSettings()
    {
        if (loading) return;
        string[] styles = { "system", "scheme" };
        int index = cursorBox.SelectedIndex;
        if (index < 0 || index >= styles.Length) index = 0;
        string name = nameBox.Text.Trim();
        if (name.Length == 0) name = "Coldy";
        string text = "name=" + name + "\r\n"
            + "rope=" + ropeBar.Value + "\r\n"
            + "ropeColor=" + ropeColor.R + "," + ropeColor.G + "," + ropeColor.B + "\r\n"
            + "size=" + sizeBar.Value + "\r\n"
            + "form=" + (formBox.SelectedIndex == 1 ? "2" : "1") + "\r\n"
            + "cursor=" + styles[index] + "\r\n"
            + "cursorSize=" + cursorSizeBar.Value + "\r\n"
            + "hotkey=" + hotkeyButton.Text + "\r\n"
            + "hotkeyVk=" + hotkeyVk + "\r\n"
            + "hotkeyVk2=" + hotkeyVk2 + "\r\n"
            + "startup=" + (startupBox.Checked ? "1" : "0") + "\r\n"
            + "background=" + (backgroundBox.Checked ? "1" : "0") + "\r\n";
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

class CursorFactory
{
    public static void ApplyScheme(int size)
    {
        if (size < 16) size = 16;
        if (size > 128) size = 128;
        Set(32512, "arrow", size);
        Set(32513, "ibeam", size);
        Set(32514, "wait", size);
        Set(32515, "cross", size);
        Set(32516, "arrow", size);
        Set(32642, "nwse", size);
        Set(32643, "nesw", size);
        Set(32644, "we", size);
        Set(32645, "ns", size);
        Set(32646, "move", size);
        Set(32648, "no", size);
        Set(32649, "hand", size);
        Set(32650, "arrow", size);
        Set(32651, "arrow", size);
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
    }

    static IntPtr Make(string style, int size)
    {
        int hx = 1, hy = 1;
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float s = size / 32f;
            if (style == "hand") DrawHand(g, size, out hx, out hy);
            else if (style == "cross") DrawCross(g, size, out hx, out hy);
            else if (style == "ibeam") DrawIBeam(g, size, out hx, out hy);
            else if (style == "wait") DrawWait(g, size, out hx, out hy);
            else if (style == "no") DrawNo(g, size, out hx, out hy);
            else if (style == "we" || style == "ns" || style == "nwse" || style == "nesw" || style == "move")
                DrawSize(g, size, style, out hx, out hy);
            else DrawArrow(g, s, out hx, out hy);
            return FromBitmap(bmp, hx, hy);
        }
    }

    static void DrawArrow(Graphics g, float s, out int hx, out int hy)
    {
        PointF[] pts = {
            new PointF(1 * s, 1 * s),
            new PointF(1 * s, 26 * s),
            new PointF(7 * s, 20 * s),
            new PointF(11 * s, 28 * s),
            new PointF(15 * s, 26 * s),
            new PointF(11 * s, 18 * s),
            new PointF(19 * s, 18 * s)
        };
        g.FillPolygon(Brushes.White, pts);
        using (var pen = new Pen(Color.Black, Math.Max(1.2f, s * 1.1f)))
        {
            pen.LineJoin = LineJoin.Round;
            g.DrawPolygon(pen, pts);
        }
        hx = Math.Max(1, (int)s);
        hy = Math.Max(1, (int)s);
    }

    static void DrawHand(Graphics g, int size, out int hx, out int hy)
    {
        float u = size / 32f;
        PointF[] pts = {
            new PointF(11 * u, 1 * u),
            new PointF(16 * u, 1 * u),
            new PointF(16 * u, 12 * u),
            new PointF(20 * u, 10 * u),
            new PointF(22 * u, 14 * u),
            new PointF(22 * u, 18 * u),
            new PointF(26 * u, 16 * u),
            new PointF(28 * u, 21 * u),
            new PointF(23 * u, 30 * u),
            new PointF(8 * u, 30 * u),
            new PointF(5 * u, 24 * u),
            new PointF(8 * u, 18 * u),
            new PointF(11 * u, 18 * u)
        };
        using (var fill = new SolidBrush(Color.White))
        using (var pen = new Pen(Color.Black, Math.Max(1.2f, u * 1.15f)))
        {
            pen.LineJoin = LineJoin.Round;
            g.FillPolygon(fill, pts);
            g.DrawPolygon(pen, pts);
        }
        hx = Math.Max(1, (int)(13.5f * u));
        hy = Math.Max(1, (int)u);
        if (hx >= size) hx = size - 1;
        if (hy >= size) hy = size - 1;
    }

    static void DrawCross(Graphics g, int size, out int hx, out int hy)
    {
        float t = Math.Max(2.2f, size / 10f);
        StrokeWhite(g, t, size / 2f, 2, size / 2f, size - 2);
        StrokeWhite(g, t, 2, size / 2f, size - 2, size / 2f);
        hx = size / 2;
        hy = size / 2;
    }

    static void DrawIBeam(Graphics g, int size, out int hx, out int hy)
    {
        float t = Math.Max(2.2f, size / 9f);
        float x = size / 2f;
        float arm = size * 0.28f;
        float top = 3f;
        float bottom = size - 3f;
        StrokeWhite(g, t, x, top, x, bottom);
        StrokeWhite(g, t, x - arm, top, x + arm, top);
        StrokeWhite(g, t, x - arm, bottom, x + arm, bottom);
        hx = size / 2;
        hy = size / 2;
    }

    static void StrokeWhite(Graphics g, float width, float x1, float y1, float x2, float y2)
    {
        float edge = Math.Max(1.2f, width * 0.5f);
        using (var outline = new Pen(Color.Black, width + edge * 2f))
        using (var ink = new Pen(Color.White, width))
        {
            outline.StartCap = LineCap.Round;
            outline.EndCap = LineCap.Round;
            ink.StartCap = LineCap.Round;
            ink.EndCap = LineCap.Round;
            g.DrawLine(outline, x1, y1, x2, y2);
            g.DrawLine(ink, x1, y1, x2, y2);
        }
    }

    static void DrawWait(Graphics g, int size, out int hx, out int hy)
    {
        float t = Math.Max(2f, size / 14f);
        using (var fill = new SolidBrush(Color.White))
        using (var pen = new Pen(Color.Black, t))
        {
            PointF[] top = {
                new PointF(size * 0.28f, size * 0.12f),
                new PointF(size * 0.72f, size * 0.12f),
                new PointF(size * 0.5f, size * 0.48f)
            };
            PointF[] bottom = {
                new PointF(size * 0.5f, size * 0.52f),
                new PointF(size * 0.72f, size * 0.88f),
                new PointF(size * 0.28f, size * 0.88f)
            };
            g.FillPolygon(fill, top);
            g.FillPolygon(fill, bottom);
            g.DrawPolygon(pen, top);
            g.DrawPolygon(pen, bottom);
        }
        hx = size / 2;
        hy = size / 2;
    }

    static void DrawNo(Graphics g, int size, out int hx, out int hy)
    {
        float m = size * 0.18f;
        float d = size - m * 2f;
        float t = Math.Max(2.2f, size / 12f);
        float edge = Math.Max(1.2f, t * 0.5f);
        using (var outline = new Pen(Color.Black, t + edge * 2f))
        using (var ink = new Pen(Color.White, t))
        {
            outline.StartCap = LineCap.Round;
            outline.EndCap = LineCap.Round;
            ink.StartCap = LineCap.Round;
            ink.EndCap = LineCap.Round;
            g.DrawEllipse(outline, m, m, d, d);
            g.DrawLine(outline, m + d * 0.2f, m + d * 0.8f, m + d * 0.8f, m + d * 0.2f);
            g.DrawEllipse(ink, m, m, d, d);
            g.DrawLine(ink, m + d * 0.2f, m + d * 0.8f, m + d * 0.8f, m + d * 0.2f);
        }
        hx = size / 2;
        hy = size / 2;
    }

    static void DrawSize(Graphics g, int size, string kind, out int hx, out int hy)
    {
        float m = size * 0.08f;
        float c = size / 2f;
        float head = size * 0.24f;
        float shaft = Math.Max(1.5f, size * 0.07f);
        if (kind == "we" || kind == "move")
            DrawDoubleArrow(g, m, c, size - m, c, head, shaft);
        if (kind == "ns" || kind == "move")
            DrawDoubleArrow(g, c, m, c, size - m, head, shaft);
        if (kind == "nwse")
            DrawDoubleArrow(g, m, m, size - m, size - m, head, shaft);
        if (kind == "nesw")
            DrawDoubleArrow(g, size - m, m, m, size - m, head, shaft);
        hx = size / 2;
        hy = size / 2;
    }

    static void DrawDoubleArrow(Graphics g, float x1, float y1, float x2, float y2, float head, float shaft)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;
        dx /= len;
        dy /= len;
        float px = -dy;
        float py = dx;
        if (head > len * 0.42f) head = len * 0.42f;
        float hw = head * 0.78f;
        PointF[] pts = {
            new PointF(x1, y1),
            new PointF(x1 + dx * head + px * hw, y1 + dy * head + py * hw),
            new PointF(x1 + dx * head + px * shaft, y1 + dy * head + py * shaft),
            new PointF(x2 - dx * head + px * shaft, y2 - dy * head + py * shaft),
            new PointF(x2 - dx * head + px * hw, y2 - dy * head + py * hw),
            new PointF(x2, y2),
            new PointF(x2 - dx * head - px * hw, y2 - dy * head - py * hw),
            new PointF(x2 - dx * head - px * shaft, y2 - dy * head - py * shaft),
            new PointF(x1 + dx * head - px * shaft, y1 + dy * head - py * shaft),
            new PointF(x1 + dx * head - px * hw, y1 + dy * head - py * hw)
        };
        g.FillPolygon(Brushes.White, pts);
        using (var pen = new Pen(Color.Black, Math.Max(1f, shaft * 0.45f)))
        {
            pen.LineJoin = LineJoin.Round;
            g.DrawPolygon(pen, pts);
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
