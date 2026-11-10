using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Coldy hangs from the cursor on a rope that can bend.
// The control window sets the name, rope length, size, form, cursor, and hide key.
// The pendant itself is a transparent, topmost, click-through layered window.
class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
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
    readonly Timer timer = new Timer();
    readonly NotifyIcon tray = new NotifyIcon();
    bool placed;

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

        tray.Icon = Icon.FromHandle(sprite.GetHicon());
        tray.Text = "Coldy";
        tray.Visible = true;
        tray.ContextMenu = new ContextMenu();

        timer.Interval = 16;
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
        timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
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
            animTime += timer.Interval;
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
        rope.Advance(timer.Interval / 1000.0);

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

        const int pad = 8;
        int winX = (int)Math.Floor(minX) - pad;
        int winY = (int)Math.Floor(minY) - pad;
        int winW = (int)Math.Ceiling(maxX) - winX + pad;
        int winH = (int)Math.Ceiling(maxY) - winY + pad;
        if (winW < 1) winW = 1;
        if (winH < 1) winH = 1;

        using (var bmp = new Bitmap(winW, winH, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
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
        IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0, 0, 0, 0));
        IntPtr old = SelectObject(mem, hBmp);
        try
        {
            var size = new Size(bmp.Width, bmp.Height);
            var dst = new Point(x, y);
            var src = new Point(0, 0);
            var blend = new BlendFunction
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = 1
            };
            UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref blend, 2);
        }
        finally
        {
            SelectObject(mem, old);
            DeleteObject(hBmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
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
    readonly Timer hotkeyTimer = new Timer();
    readonly TextBox nameBox = new TextBox();
    readonly Panel nameFrame = new Panel();
    readonly TrackBar ropeBar = new TrackBar();
    readonly TrackBar sizeBar = new TrackBar();
    readonly TrackBar cursorSizeBar = new TrackBar();
    readonly ComboBox formBox = new ComboBox();
    readonly ComboBox cursorBox = new ComboBox();
    readonly Button colorButton = new Button();
    readonly Panel colorSwatch = new Panel();
    readonly Button hotkeyButton = new Button();
    readonly Label ropeValue = new Label();
    readonly Label sizeValue = new Label();
    readonly Label cursorSizeValue = new Label();
    readonly Button hideButton = new Button();
    readonly Button exitButton = new Button();
    readonly Label status = new Label();
    int hotkeyVk = 0x77;
    int hotkeyVk2;
    int capturedVk;
    int capturedCount;
    bool capturingHotkey;
    bool chordDown;
    bool loading = true;
    bool cursorApplied;
    string settingsPath;
    Color ropeColor = Color.FromArgb(55, 55, 62);
    int boxHeight = 34;

    public SettingsForm()
    {
        Text = "Coldy";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 520);
        Font = new Font("Microsoft YaHei UI", 10.5f);
        KeyPreview = true;
        string dir = Path.GetDirectoryName(Application.ExecutablePath);
        settingsPath = Path.Combine(dir, "coldy.ini");
        boxHeight = TextRenderer.MeasureText("选择颜色", Font).Height + 16;
        if (boxHeight < 34) boxHeight = 34;
        nameBox.BorderStyle = BorderStyle.None;
        nameBox.BackColor = SystemColors.Window;
        nameFrame.BorderStyle = BorderStyle.FixedSingle;
        nameFrame.BackColor = SystemColors.Window;
        nameFrame.Controls.Add(nameBox);
        AddRow(28, "名字", nameFrame, 280);
        nameBox.Text = "Coldy";

        SetupBar(ropeBar, 40, 320, 120);
        SetupBar(sizeBar, 80, 420, 190);
        SetupBar(cursorSizeBar, 16, 128, 32);
        AddRow(78, "绳子长度", ropeBar, 250);
        ropeValue.SetBounds(390, 82, 60, 24);
        Controls.Add(ropeValue);

        colorButton.Text = "选择颜色";
        AddRow(132, "绳子颜色", colorButton, 160);
        FitButton(colorButton, "选择颜色");
        colorSwatch.BorderStyle = BorderStyle.FixedSingle;
        colorSwatch.SetBounds(colorButton.Right + 8, colorButton.Top, boxHeight, boxHeight);
        Controls.Add(colorSwatch);
        ShowRopeColor();

        AddRow(182, "人物大小", sizeBar, 250);
        sizeValue.SetBounds(390, 186, 60, 24);
        Controls.Add(sizeValue);

        formBox.IntegralHeight = false;
        formBox.DropDownStyle = ComboBoxStyle.DropDownList;
        formBox.Items.AddRange(new object[] { "形态 1", "形态 2" });
        formBox.SelectedIndex = 0;
        AddRow(236, "形态", formBox, 180);

        cursorBox.IntegralHeight = false;
        cursorBox.DropDownStyle = ComboBoxStyle.DropDownList;
        cursorBox.Items.AddRange(new object[] { "跟随场景", "系统默认" });
        cursorBox.SelectedIndex = 0;
        AddRow(286, "光标", cursorBox, 180);

        AddRow(336, "光标大小", cursorSizeBar, 250);
        cursorSizeValue.SetBounds(390, 340, 60, 24);
        Controls.Add(cursorSizeValue);

        hotkeyButton.Text = "F8";
        AddRow(390, "隐藏快捷键", hotkeyButton, 200);
        hideButton.Text = "隐藏人物";
        Controls.Add(hideButton);
        FitButton(hideButton, "隐藏人物");
        int hideLeft = hotkeyButton.Right + 12;
        int needed = hideLeft + hideButton.Width + 24;
        if (needed > ClientSize.Width) ClientSize = new Size(needed, ClientSize.Height);
        hideButton.SetBounds(ClientSize.Width - 24 - hideButton.Width, hotkeyButton.Top, hideButton.Width, boxHeight);

        exitButton.Text = "退出";
        exitButton.SetBounds(24, 460, 100, boxHeight);
        Controls.Add(exitButton);
        int bottom = exitButton.Bottom + 16;
        if (bottom > ClientSize.Height) ClientSize = new Size(ClientSize.Width, bottom);

        status.SetBounds(140, exitButton.Top + (boxHeight - 28) / 2, 360, 28);
        status.ForeColor = Color.DimGray;
        Controls.Add(status);

        LoadSettings();
        ShowHotkey();
        ShowRopeColor();
        RefreshNumbers();
        loading = false;
        ApplyAll(true);

        nameBox.TextChanged += delegate { if (!loading) ApplyAll(false); };
        ropeBar.ValueChanged += delegate { if (!loading) ApplyAll(false); };
        colorButton.Click += delegate { PickRopeColor(); };
        sizeBar.ValueChanged += delegate { if (!loading) ApplyAll(false); };
        cursorSizeBar.ValueChanged += delegate { if (!loading) ApplyAll(true); };
        formBox.SelectedIndexChanged += delegate
        {
            if (loading) return;
            ApplyAll(false);
            status.Text = formBox.SelectedIndex == 1
                ? "形态 2：蹲跳、眨眼，勺子和彩虹线会晃"
                : "形态 1：原来的静图";
        };
        cursorBox.SelectedIndexChanged += delegate { if (!loading) ApplyAll(true); };
        hotkeyButton.Click += delegate { BeginHotkeyCapture(); };
        hideButton.Click += delegate { ToggleHidden(); };
        exitButton.Click += delegate { Close(); };

        var menu = pendant.TrayIcon.ContextMenu;
        menu.MenuItems.Add(new MenuItem("显示控制台", delegate { Show(); Activate(); }));
        menu.MenuItems.Add(new MenuItem("隐藏人物", delegate { ToggleHidden(); }));
        menu.MenuItems.Add(new MenuItem("退出", delegate { Close(); }));
        pendant.TrayIcon.DoubleClick += delegate { Show(); Activate(); };

        hotkeyTimer.Interval = 30;
        hotkeyTimer.Tick += delegate { PollHotkey(); };
        hotkeyTimer.Start();
        MatchBoxHeights();

        pendant.Show();
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
        exitButton.Height = boxHeight;
        formBox.Height = boxHeight;
        cursorBox.Height = boxHeight;
        colorSwatch.SetBounds(colorButton.Right + 8, colorButton.Top, boxHeight, boxHeight);
        hideButton.Top = hotkeyButton.Top;
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

    void PollHotkey()
    {
        if (capturingHotkey || hotkeyVk == 0) return;
        bool down = (GetAsyncKeyState(hotkeyVk) & 0x8000) != 0;
        if (hotkeyVk2 != 0)
            down = down && (GetAsyncKeyState(hotkeyVk2) & 0x8000) != 0;
        if (down && !chordDown) ToggleHidden();
        chordDown = down;
    }

    void AddRow(int y, string caption, Control editor, int editorWidth)
    {
        var label = new Label();
        label.Text = caption;
        label.SetBounds(24, y + (boxHeight - 24) / 2, 110, 24);
        label.TextAlign = ContentAlignment.MiddleLeft;
        editor.SetBounds(140, y, editorWidth, boxHeight);
        Controls.Add(label);
        Controls.Add(editor);
    }

    static void SetupBar(TrackBar bar, int min, int max, int value)
    {
        bar.Minimum = min;
        bar.Maximum = max;
        bar.TickFrequency = Math.Max(1, (max - min) / 8);
        bar.Value = value;
        bar.AutoSize = false;
        bar.Height = 32;
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
        cursorSizeBar.Enabled = cursorBox.SelectedIndex != 1;
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
        if (cursorBox.SelectedIndex == 1)
        {
            CursorFactory.Restore();
            cursorApplied = false;
            status.Text = "已恢复系统光标";
            return;
        }
        CursorFactory.ApplyScheme(cursorSizeBar.Value);
        cursorApplied = true;
        status.Text = "光标会随文字、链接和窗口边缘切换";
    }

    void BeginHotkeyCapture()
    {
        capturingHotkey = true;
        capturedCount = 0;
        capturedVk = 0;
        hotkeyButton.Text = "请按两个键";
        status.Text = "同时或依次按下两个键，Esc 取消";
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
        if (!loading)
            status.Text = "同时按下 " + shown + " 隐藏或显示人物";
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
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        hotkeyTimer.Stop();
        if (cursorApplied) CursorFactory.Restore();
        SaveSettings();
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

    static void SetBar(TrackBar bar, string val)
    {
        int n;
        if (!int.TryParse(val, out n)) return;
        if (n < bar.Minimum) n = bar.Minimum;
        if (n > bar.Maximum) n = bar.Maximum;
        bar.Value = n;
    }

    void SelectCursor(string val)
    {
        cursorBox.SelectedIndex = val == "system" ? 1 : 0;
    }

    void SelectForm(string val)
    {
        formBox.SelectedIndex = (val == "2" || val == "gif") ? 1 : 0;
    }

    void SaveSettings()
    {
        if (loading) return;
        string[] styles = { "scheme", "system" };
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
            + "hotkeyVk2=" + hotkeyVk2 + "\r\n";
        try { File.WriteAllText(settingsPath, text); }
        catch { }
    }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);
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
            if (style == "hand") DrawHand(g, s, out hx, out hy);
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

    static void DrawHand(Graphics g, float s, out int hx, out int hy)
    {
        using (var fill = new SolidBrush(Color.White))
        using (var pen = new Pen(Color.Black, Math.Max(1.2f, s)))
        {
            g.FillEllipse(fill, 10 * s, 14 * s, 16 * s, 14 * s);
            g.DrawEllipse(pen, 10 * s, 14 * s, 16 * s, 14 * s);
            g.FillRectangle(fill, 16 * s, 4 * s, 6 * s, 16 * s);
            g.DrawRectangle(pen, 16 * s, 4 * s, 6 * s, 16 * s);
        }
        hx = (int)(19 * s);
        hy = (int)(4 * s);
    }

    static void DrawCross(Graphics g, int size, out int hx, out int hy)
    {
        float t = Math.Max(2f, size / 10f);
        using (var pen = new Pen(Color.White, t))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawLine(pen, size / 2f, 2, size / 2f, size - 2);
            g.DrawLine(pen, 2, size / 2f, size - 2, size / 2f);
        }
        using (var pen = new Pen(Color.Black, Math.Max(1f, t * 0.35f)))
        {
            g.DrawLine(pen, size / 2f, 2, size / 2f, size - 2);
            g.DrawLine(pen, 2, size / 2f, size - 2, size / 2f);
        }
        hx = size / 2;
        hy = size / 2;
    }

    static void DrawIBeam(Graphics g, int size, out int hx, out int hy)
    {
        float t = Math.Max(2f, size / 14f);
        using (var pen = new Pen(Color.Black, t))
        {
            float x = size / 2f;
            g.DrawLine(pen, x, 3, x, size - 3);
            g.DrawLine(pen, x - size * 0.28f, 3, x + size * 0.28f, 3);
            g.DrawLine(pen, x - size * 0.28f, size - 3, x + size * 0.28f, size - 3);
        }
        hx = size / 2;
        hy = size / 2;
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
        using (var pen = new Pen(Color.Black, Math.Max(2f, size / 12f)))
        {
            g.DrawEllipse(pen, m, m, d, d);
            g.DrawLine(pen, m + d * 0.2f, m + d * 0.8f, m + d * 0.8f, m + d * 0.2f);
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
