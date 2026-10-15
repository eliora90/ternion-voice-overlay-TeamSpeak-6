using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;
using TernionCore;

namespace TernionOverlay;

/// <summary>Окно-оверлей поверх игры: прозрачное, всегда поверх, рисуется через UpdateLayeredWindow.</summary>
public sealed class OverlayForm : Form
{
    private readonly AppConfig _cfg;
    private Ts6Client _client;
    private readonly AvatarCache _avatars;
    private readonly System.Windows.Forms.Timer _tick;
    private int _dirty = 1;
    private bool _userHidden;

    public OverlayForm(AppConfig cfg, Ts6Client client, AvatarCache avatars)
    {
        _cfg = cfg;
        _client = client;
        _avatars = avatars;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        ApplyIntrinsicSize();

        client.StateUpdated += OnState;
        client.ConnectedChanged += OnState;
        client.Error += _ => OnState();

        _tick = new System.Windows.Forms.Timer { Interval = 40 };
        _tick.Tick += (_, _) => Tick();
        _tick.Start();
    }

    private void OnState()
    {
        Interlocked.Exchange(ref _dirty, 1);
        if (!IsHandleCreated || IsDisposed) return;
        if (InvokeRequired) BeginInvoke((Action)(() => { if (!IsDisposed) Invalidate(); }));
        else Invalidate();
    }

    private void Tick()
    {
        if (_userHidden) return;
        // Display status when TS is unavailable or waiting for authorization.
        if (!Visible) { Show(); ApplyClickPass(); }
        RenderAndResize();
    }

    private void RenderAndResize()
    {
        bool need = Volatile.Read(ref _dirty) == 1;
        Interlocked.Exchange(ref _dirty, 0);
        // пересчитываем размер под содержимое и перерисовываем
        if (need)
        {
            Invalidate();
            ApplyIntrinsicSize();
        }
    }

    private void ApplyIntrinsicSize()
    {
        var visible = _client.State.Visible;
        if (!_cfg.ShowAll) visible = visible.Where(p => p.Talking).ToList();
        var (_, rowH) = TextHeights(DeviceDpi);
        int pad = 8;
        int w = Math.Max(180, _cfg.WindowWidth);
        var avSide = _cfg.AvatarEnum;
        foreach (var p in visible)
        {
            using var font = new Font("Segoe UI", Math.Max(10, _cfg.FontSize),
                p.Talking ? FontStyle.Bold : FontStyle.Regular);
            int textW = TextRenderer.MeasureText(DisplayText(p), font,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            bool avatarVisible = avSide != AvatarSide.None && p.AvatarUrl.Length > 0 && _avatars.Get(p.AvatarUrl) is not null;
            int avatarW = avatarVisible ? Math.Min(rowH - 8, 28) + 6 : 0;
            int iconW = ((p.InputMuted ? 1 : 0) + (p.OutputMuted ? 1 : 0)) * 18;
            // Match DrawRow's text margins, capsule padding, and avatar footprint.
            w = Math.Max(w, textW + 12 + avatarW + iconW + 12 + 8);
        }
        var bounds = Screen.FromPoint(IsHandleCreated ? Location : new Point(_cfg.WindowX, _cfg.WindowY)).WorkingArea;
        w = Math.Min(w, Math.Max(180, bounds.Width - 16));
        int h = Math.Max(1, visible.Count) * rowH + pad;
        if (Width != w || Height != h)
        {
            int oldRight = Right;
            bool keepRight = IsHandleCreated && _cfg.Alignment == "right";
            Size = new Size(w, h);
            if (keepRight) Left = Math.Max(bounds.Left, oldRight - w);
        }
    }

    private string DisplayText(Participant p) =>
        _cfg.NameModeEnum == NameMode.Both && !string.IsNullOrWhiteSpace(p.Description)
            ? p.DisplayName : p.Nickname;

    private (int Header, int Row) TextHeights(float dpi)
    {
        int pointSize = Math.Max(10, _cfg.FontSize);
        using var heading = new Font("Segoe UI", pointSize + 1, FontStyle.Bold);
        using var item = new Font("Segoe UI", pointSize);
        // FontSize is in POINTS, while a WinForms window is in PIXELS. For 26 pt,
        // fontSize + 18 was too short and clipped both header and participant rows.
        return (Math.Max(32, (int)Math.Ceiling(heading.GetHeight(dpi)) + 6),
                Math.Max(38, (int)Math.Ceiling(item.GetHeight(dpi)) + 16));
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            ToggleVisible();
            return;
        }
        base.WndProc(ref m);
    }

    public void ToggleVisible()
    {
        _userHidden = !_userHidden;
        if (_userHidden) Hide();
        else { Show(); ApplyClickPass(); Redraw(); }
    }

    public void ReapplyFlags()
    {
        ApplyClickPass();
    }

    public void Redraw() { Interlocked.Exchange(ref _dirty, 1); }

    // Перемещение окна: в режиме move окно кликабельно и поверх всех окон (можно схватить).
    private Point _dragOffset;

    public bool IsMoveMode => !_cfg.Locked;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!IsMoveMode) { base.OnMouseDown(e); return; }
        if (e.Button == MouseButtons.Left) _dragOffset = new Point(MousePosition.X - Location.X, MousePosition.Y - Location.Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragOffset != Point.Empty && IsMoveMode)
        {
            var p = MousePosition;
            Location = new Point(p.X - _dragOffset.X, p.Y - _dragOffset.Y);
            _cfg.WindowX = Location.X;
            _cfg.WindowY = Location.Y;
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragOffset = Point.Empty;
        ConfigStore.Save(_cfg);
        base.OnMouseUp(e);
    }

    /// <summary>Переключение режима перемещения (из трея). unlocked=true — можно двигать.</summary>
    public void SetMoveMode(bool unlocked)
    {
        _cfg.Locked = !unlocked;
        ConfigStore.Save(_cfg);
        ApplyClickPass();
        Redraw();
        Invalidate();
    }

    /// <summary>Применить «клики насквозь» с учётом режима перемещения.
    /// В обычном режиме — обычное непрозрачное окно (надёжная видимость).
    /// «Насквозь» включаем только когда включена настройка и НЕ двигаем: тогда окно
    /// становится layered+transparent. В режиме перемещения — обычное фронтовое окно.</summary>
    public void ApplyClickPass()
    {
        bool interactive = IsMoveMode || !_cfg.ClickThrough;
        if (interactive)
        {
            // обычное окно: снять layered/transparent/toolwindow, текст всегда виден
            NativeMethods.SetPlainWindow(Handle);
            NativeMethods.SetTopmost(Handle);
        }
        else
        {
            // клики насквозь в игре
            NativeMethods.SetClickThrough(Handle);
        }
    }

        /// <summary>Замена клиента после смены порта.</summary>
        public void Reattach(Ts6Client next)
        {
            _client.StateUpdated -= OnState;
            _client.ConnectedChanged -= OnState;
            _client.Error -= _ => OnState();
            _client = next;
            next.StateUpdated += OnState;
            next.ConnectedChanged += OnState;
            next.Error += _ => OnState();
            OnState();
        }

    protected override void OnPaint(PaintEventArgs e)
    {
        var visible = _client.State.Visible;
        if (!_cfg.ShowAll) visible = visible.Where(p => p.Talking).ToList();

        int fontSize = Math.Max(10, _cfg.FontSize);
        var (_, rowH) = TextHeights(e.Graphics.DpiY);
        int w = Width;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.Magenta);
        int yRow = 4;
        if (visible.Count == 0)
        {
            string msg = !_client.State.Connected ? Lang.T("overlay.wait.client") + $" {_cfg.Port}" :
                !_client.State.Authorized ? Lang.T("overlay.wait.auth") :
                !_client.State.HasServerConnection ? Lang.T("overlay.no.server") :
                _client.State.OurClientId == 0 ? Lang.T("overlay.no.channel") : Lang.T("overlay.silent");
            using var f = new Font("Segoe UI", 10f);
            int pillW = Math.Min(w - 8, TextRenderer.MeasureText(msg, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 20);
            int offset = _cfg.Alignment == "right" ? w - 8 - pillW : 0;
            using var bg = new SolidBrush(Color.FromArgb(30, 32, 37));
            // Color-key transparency cannot blend anti-aliased edges: intermediate
            // magenta pixels survive the key and appear as pink halos on Windows.
            g.SmoothingMode = SmoothingMode.None;
            g.FillRoundRect(bg, 4 + offset, 4, pillW, rowH - 4, Math.Min(14, rowH - 4));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(g, msg, f, new Rectangle(13 + offset, 4, pillW - 18, rowH - 4), Color.White,
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            return;
        }
        foreach (var p in visible)
        {
            DrawRow(g, p, fontSize, rowH, w, yRow);
            yRow += rowH;
        }
    }

    private void DrawRow(Graphics g, Participant p, int fontSize, int rowH, int w, int y)
    {
        bool talking = p.Talking;
        float py = y;

        float left = 12;
        float rightText = w - 12;
        var avSide = _cfg.AvatarEnum;

        RectangleF avatarRect = RectangleF.Empty;
        Image? avatarImage = null;
        if (avSide != AvatarSide.None && p.AvatarUrl.Length > 0)
        {
            avatarImage = _avatars.Get(p.AvatarUrl);
            if (avatarImage is not null)
            {
                int size = Math.Min(rowH - 8, 28);
                if (avSide == AvatarSide.Left) { avatarRect = new RectangleF(left, py + (rowH - size) / 2f, size, size); left += size + 6; }
                // Right avatar is placed relative to the measured capsule, below.
            }
        }

        string text = DisplayText(p);
        using var font = new Font("Segoe UI", fontSize, talking ? FontStyle.Bold : FontStyle.Regular);
        int iconCount = (p.InputMuted ? 1 : 0) + (p.OutputMuted ? 1 : 0);
        int iconsW = iconCount * 18;
        int measured = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        int avatarRightW = avatarImage is not null && avSide == AvatarSide.Right ? Math.Min(rowH - 8, 28) + 6 : 0;
        int pillW = Math.Min(w - 8, measured + (int)left + iconsW + avatarRightW + 12);
        int offset = _cfg.Alignment == "right" ? w - 8 - pillW : 0;
        int pillH = rowH - 6;
        using var fill = new SolidBrush(Color.FromArgb(30, 32, 37));
        g.SmoothingMode = SmoothingMode.None;
        g.FillRoundRect(fill, 4 + offset, y + 2, pillW, pillH, Math.Min(pillH, 18));
        if (talking)
        {
            using var border = new Pen(Color.FromArgb(68, 222, 126), 1.5f);
            g.DrawRoundRect(border, 4 + offset, y + 2, pillW, pillH, Math.Min(pillH, 18));
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        rightText = pillW - iconsW - avatarRightW - 8;
        float textW = rightText - left;
        // Center the glyphs inside the capsule, with a small optical lift for
        // Segoe UI's baseline so the visible top/bottom padding looks even.
        TextRenderer.DrawText(g, text, font,
            new Rectangle((int)left + offset, y, Math.Max(0, (int)textW), pillH),
            talking ? Color.FromArgb(71, 226, 137) : Color.FromArgb(225, 225, 228),
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        int iconX = pillW - iconsW - avatarRightW + offset;
        if (p.InputMuted) { DrawMutedIcon(g, iconX, y + (rowH - 16) / 2, false); iconX += 18; }
        if (p.OutputMuted) DrawMutedIcon(g, iconX, y + (rowH - 16) / 2, true);

        if (avatarRightW > 0)
        {
            int size = avatarRightW - 6;
            avatarRect = new RectangleF(pillW - size - 4 + offset, y + (rowH - size) / 2f, size, size);
        }
        else if (avatarRect != RectangleF.Empty)
            avatarRect.Offset(offset, 0);

        if (avatarRect != RectangleF.Empty) DrawAvatar(g, avatarRect, p.AvatarUrl);
    }

    private static void DrawMutedIcon(Graphics g, int x, int y, bool headphones)
    {
        using var pen = new Pen(Color.FromArgb(236, 95, 95), 1.8f);
        if (headphones)
        {
            g.DrawArc(pen, x + 2, y + 2, 12, 12, 185, 170);
            g.DrawLine(pen, x + 2, y + 9, x + 2, y + 14);
            g.DrawLine(pen, x + 14, y + 9, x + 14, y + 14);
        }
        else
        {
            g.DrawRoundRect(pen, x + 6, y + 2, 5, 10, 3);
            g.DrawArc(pen, x + 3, y + 5, 10, 9, 0, 180);
            g.DrawLine(pen, x + 8, y + 12, x + 8, y + 15);
        }
        g.DrawLine(pen, x + 2, y + 15, x + 15, y + 2);
    }


    private void DrawAvatar(Graphics g, RectangleF r, string url)
    {
        var img = _avatars.Get(url);
        if (img is null) return;
        using var path = new GraphicsPath();
        path.AddEllipse(r);
        using var region = new Region(path);
        var prev = g.Save();
        g.SetClip(region, CombineMode.Replace);
        g.DrawImage(img, r);
        g.Restore(prev);
        using var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1.4f);
        g.DrawEllipse(pen, r);
    }

    // Выделение цвета отряда по имени канала (у вас Земля/Небо/Штурм/ДРГ).
    private static Color TeamColor(string channel)
    {
        string c = channel.ToLowerInvariant();
        if (c.Contains("земля")) return Color.FromArgb(64, 196, 140);
        if (c.Contains("небо")) return Color.FromArgb(80, 150, 255);
        if (c.Contains("штурм")) return Color.FromArgb(240, 90, 90);
        if (c.Contains("дрг")) return Color.FromArgb(185, 120, 255);
        return Color.FromArgb(71, 226, 137);
    }

    private void Present(Bitmap bmp) => bmp.Dispose(); // устаревшее (окно рисуется через OnPaint)

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyClickPass();
        ApplySavedPosition();
    }

    private void ApplySavedPosition()
    {
        try
        {
            var b = Screen.FromPoint(new Point(_cfg.WindowX, _cfg.WindowY)).WorkingArea;
            int x = Math.Max(b.Left, Math.Min(_cfg.WindowX, b.Right - Math.Min(Width, b.Width)));
            int y = Math.Max(b.Top, Math.Min(_cfg.WindowY, b.Bottom - 80));
            Location = new Point(x, y);
        }
        catch { }
    }
}

internal static class GdiRoundRect
{
    public static void FillRoundRect(this Graphics g, Brush b, float x, float y, float w, float h, float r)
    {
        if (w <= 2 || h <= 2) { g.FillRectangle(b, x, y, w, h); return; }
        using var path = RoundRectPath(x, y, w, h, r);
        g.FillPath(b, path);
    }

    public static void DrawRoundRect(this Graphics g, Pen p, float x, float y, float w, float h, float r)
    {
        using var path = RoundRectPath(x, y, w, h, r);
        g.DrawPath(p, path);
    }

    private static GraphicsPath RoundRectPath(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = Math.Min(r, w / 2f);
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}