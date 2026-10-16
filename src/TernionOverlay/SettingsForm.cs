using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TernionCore;

namespace TernionOverlay;

/// <summary>Borderless, draggable five-card settings dialog.</summary>
public sealed class SettingsForm : Form
{
    private readonly AppConfig cfg;
    private readonly Action apply;
    private readonly string originalLanguage;
    private readonly FlowLayoutPanel body = new();
    private readonly Panel header = new(), footer = new();
    private readonly List<Panel> cards = new();
    private readonly Dictionary<Label, string> labels = new();
    private readonly Dictionary<SwitchCheckBox, string> toggles = new();
    private readonly Label title = new(), subtitle = new();
    private readonly Button save = new(), cancel = new();
    private readonly ComboBox language = new(), show = new(), names = new(), avatar = new(), alignment = new();
    private readonly NumericUpDown port = new(), fontSize = new(), width = new();

    private readonly SwitchCheckBox autoWindows = new(), autoTs = new(), clickThrough = new();
    private readonly TextBox hotkey = new();
    private static readonly Color Blue = Color.FromArgb(19, 108, 231);
    private static readonly Color Ink = Color.FromArgb(20, 30, 52);
    private static readonly Color Canvas = Color.FromArgb(239, 244, 251);

    public SettingsForm(AppConfig cfg, Action apply)
    {
        this.cfg = cfg;
        this.apply = apply;
        originalLanguage = cfg.Language == "ru" ? "ru" : "en";
        Lang.Apply(originalLanguage);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(540, 480);
        Size = new Size(760, 874);
        BackColor = Canvas;
        Build();
        LoadValues();
        Relabel();
        Shown += (_, _) => FitScreen();
        FormClosed += (_, _) => { if (DialogResult != DialogResult.OK) Lang.Apply(originalLanguage); };
    }

    private void Build()
    {
        var frame = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, Padding = Padding.Empty };
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(frame);
        header.Dock = DockStyle.Top; header.Height = 70; header.BackColor = Canvas;
        header.Cursor = Cursors.SizeAll;
        header.MouseDown += DragWindow;
        header.Paint += (_, e) => {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(220, 234, 252));
            e.Graphics.FillEllipse(brush, 20, 16, 44, 44);
            TextRenderer.DrawText(e.Graphics, "⚙", new Font("Segoe UI Symbol", 19), new Rectangle(24, 20, 38, 36), Blue);
        };
        title.Font = new Font("Segoe UI", 15, FontStyle.Bold); title.ForeColor = Ink;
        title.Location = new Point(78, 7); title.AutoSize = true;
        subtitle.Font = new Font("Segoe UI", 9); subtitle.ForeColor = Color.FromArgb(100, 112, 135);
        subtitle.Location = new Point(80, 39); subtitle.AutoSize = true;
        header.Controls.Add(title); header.Controls.Add(subtitle);
        title.Cursor = Cursors.SizeAll;
        subtitle.Cursor = Cursors.SizeAll;
        title.MouseDown += DragWindow;
        subtitle.MouseDown += DragWindow;
        var close = new Button { Text = "×", FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 19),
            Width = 40, Height = 40, Top = 9, BackColor = Canvas, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Close();
        header.Controls.Add(close);
        header.Resize += (_, _) => close.Left = header.Width - 56;
        header.Dock = DockStyle.Fill;
        frame.Controls.Add(header, 0, 0);

        footer.Dock = DockStyle.Bottom; footer.Height = 55; footer.BackColor = Canvas;
        SetupButton(save, true); SetupButton(cancel, false);
        footer.Controls.Add(save); footer.Controls.Add(cancel);
        footer.Resize += (_, _) => { cancel.Left = footer.Width - 138; save.Left = footer.Width - 258; };
        save.Click += (_, _) => Save(); cancel.Click += (_, _) => Close();
        footer.Dock = DockStyle.Fill;
        frame.Controls.Add(footer, 0, 2);

        body.Dock = DockStyle.Fill; body.FlowDirection = FlowDirection.TopDown;
        body.WrapContents = false; body.AutoScroll = false;
        body.Padding = new Padding(18, 2, 18, 0); body.BackColor = Canvas;
        frame.Controls.Add(body, 0, 1);
        body.Resize += (_, _) => ResizeCards();

        var general = Card("section.general", "◎");
        SetupCombo(language, "English", "Русский");
        Add(general, Row("settings.language", language));
        language.SelectedIndexChanged += (_, _) => { Lang.Apply(language.SelectedIndex == 1 ? "ru" : "en"); Relabel(); };

        var connection = Card("section.connection", "↗");
        port.Minimum = 1; port.Maximum = 65535;
        Add(connection, Row("settings.port", port, "settings.port.hint"));

        var display = Card("section.display", "♟");
        SetupCombo(show); SetupCombo(names); SetupCombo(avatar); SetupCombo(alignment);
        Add(display, Row("settings.show", show));
        Add(display, Row("settings.names", names));
        Add(display, Row("settings.avatar", avatar));
        // The old list-opacity slider did not affect the new capsule renderer.
        // Reuse its row to offer alignment without making this dialog taller.
        Add(display, Row("settings.alignment", alignment));

        var behavior = Card("section.behavior", "⚙");
        var switches = new TableLayoutPanel { Dock = DockStyle.Top, Height = 144,
            ColumnCount = 1, RowCount = 3, BackColor = Color.White,
            Padding = new Padding(58, 8, 18, 18) };
        for (int i = 0; i < 3; i++) switches.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        int switchRow = 0;
        foreach (var (box, key) in new[] { (autoWindows, "settings.autostart.win"),
            (autoTs, "settings.autostart.ts"), (clickThrough, "settings.clickthrough") })
        {
            box.Appearance = Appearance.Normal;
            box.AutoSize = false; box.Dock = DockStyle.Fill;
            box.Margin = new Padding(0, 0, 0, 0);
            box.BackColor = Color.White; box.ForeColor = Ink;
            toggles[box] = key;
            switches.Controls.Add(box, 0, switchRow++);
        }
        Add(behavior, switches);

        var appearance = Card("section.hotkeys", "⌨");
        Add(appearance, Row("settings.hotkey", hotkey));
        fontSize.Minimum = 10; fontSize.Maximum = 26;
        width.Minimum = 180; width.Maximum = 700; width.Increment = 10;
        var dimensions = new TableLayoutPanel { Dock = DockStyle.Top, Height = 58, ColumnCount = 2,
            Padding = new Padding(58, 6, 18, 16), BackColor = Color.White };
        dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var fontPair = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        fontPair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        fontPair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        fontPair.Controls.Add(Label("settings.fontsize"), 0, 0);
        fontPair.Controls.Add(fontSize, 1, 0);
        var widthPair = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        widthPair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        widthPair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        widthPair.Controls.Add(Label("settings.width"), 0, 0);
        widthPair.Controls.Add(width, 1, 0);
        fontSize.Margin = new Padding(0, 0, 18, 0);
        width.Margin = new Padding(0, 0, 0, 0);
        fontSize.Dock = DockStyle.Fill; width.Dock = DockStyle.Fill;
        dimensions.Controls.Add(fontPair, 0, 0);
        dimensions.Controls.Add(widthPair, 1, 0);
        Add(appearance, dimensions);

        // Lay out children explicitly so WinForms Dock/Z-order cannot reverse the sections.
        foreach (var card in cards)
        {
            var children = (List<Control>)card.Tag!;
            var heading = children[0];
            int y = heading.Height;
            foreach (var child in children.Skip(1))
            {
                child.Dock = DockStyle.None;
                child.Location = new Point(0, y);
                child.Width = card.Width;
                child.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                y += child.Height;
            }
            heading.Dock = DockStyle.None;
            heading.Location = Point.Empty;
            heading.Width = card.Width;
            heading.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Height = y + 14;
            body.Controls.Add(card);
        }
        ResizeCards();
    }

    private Panel Card(string key, string icon)
    {
        var card = new RoundedCard { BackColor = Color.White, Width = 680,
            Margin = new Padding(0, 0, 0, 8) };
        var head = new Panel { Height = 42, BackColor = Color.White };
        head.Paint += (_, e) => DrawSectionIcon(e.Graphics, key);
        var caption = new Label { AutoSize = true, UseMnemonic = false, Location = new Point(58, 10),
            Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Ink };
        labels[caption] = key; head.Controls.Add(caption); card.Controls.Add(head);
        card.Tag = new List<Control> { head };
        cards.Add(card); return card;
    }

    private static void Add(Panel card, Control child)
    {
        card.Controls.Add(child);
        ((List<Control>)card.Tag!).Add(child);
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0x00A1 /* WM_NCLBUTTONDOWN */,
            (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
    }

    private static void DrawSectionIcon(Graphics g, string key)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(16, 6, 32, 32);
        var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        using var tile = new GraphicsPath();
        tile.AddArc(r.X, r.Y, 10, 10, 180, 90);
        tile.AddArc(r.Right - 10, r.Y, 10, 10, 270, 90);
        tile.AddArc(r.Right - 10, r.Bottom - 10, 10, 10, 0, 90);
        tile.AddArc(r.X, r.Bottom - 10, 10, 10, 90, 90);
        tile.CloseFigure();
        using var fill = new SolidBrush(Color.FromArgb(227, 239, 255));
        using var pen = new Pen(Blue, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.FillPath(fill, tile);
        switch (key)
        {
            case "section.general":
                g.DrawEllipse(pen, 22, 12, 20, 20);
                g.DrawEllipse(pen, 28, 12, 8, 20);
                g.DrawLine(pen, 22, 22, 42, 22);
                break;
            case "section.connection":
                g.DrawArc(pen, 21, 15, 14, 12, 120, 270);
                g.DrawArc(pen, 29, 19, 14, 12, 300, 270);
                g.DrawLine(pen, 28, 27, 36, 19);
                break;
            case "section.display":
                g.DrawEllipse(pen, 27, 12, 10, 10);
                g.DrawArc(pen, 22, 23, 20, 10, 180, 180);
                g.DrawEllipse(pen, 20, 18, 5, 5);
                g.DrawEllipse(pen, 39, 18, 5, 5);
                break;
            case "section.behavior":
                g.DrawEllipse(pen, center.X - 9, center.Y - 9, 18, 18);
                g.DrawEllipse(pen, center.X - 3, center.Y - 3, 6, 6);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    int x1 = center.X + (int)(10 * Math.Cos(a)), y1 = center.Y + (int)(10 * Math.Sin(a));
                    int x2 = center.X + (int)(12 * Math.Cos(a)), y2 = center.Y + (int)(12 * Math.Sin(a));
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;
            default:
                g.DrawRectangle(pen, center.X - 11, center.Y - 8, 22, 15);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                        g.FillEllipse(Brushes.CornflowerBlue, center.X - 7 + i * 6, center.Y - 4 + j * 5, 2, 2);
                break;
        }
    }

    private TableLayoutPanel Row(string key, Control control, string? hint = null)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = hint is null ? 33 : 47,
            ColumnCount = 3, RowCount = hint is null ? 1 : 2,
            Padding = new Padding(58, 1, 18, 1), BackColor = Color.White };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        if (hint is not null) row.RowStyles.Add(new RowStyle(SizeType.Absolute, 17));
        row.Controls.Add(Label(key), 0, 0);
        control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 0, 0, 2);
        row.Controls.Add(control, 1, 0);
        if (hint is not null)
        {
            var note = Label(hint);
            note.Font = new Font("Segoe UI", 8.5f); note.ForeColor = Color.Gray;
            row.Controls.Add(note, 0, 1); row.SetColumnSpan(note, 2);
        }
        return row;
    }

    private Label Label(string key)
    {
        var label = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false,
            AutoEllipsis = false, ForeColor = Ink, BackColor = Color.White };
        labels[label] = key; return label;
    }

    private static void SetupCombo(ComboBox combo, params string[] options)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.AddRange(options);
    }
    private static void SetupButton(Button button, bool primary)
    {
        button.Width = 108; button.Height = 34; button.Top = 7;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = primary ? Blue : Color.White;
        button.ForeColor = primary ? Color.White : Ink;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(214, 224, 237);
        button.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        void Round()
        {
            using var path = new GraphicsPath();
            var r = new Rectangle(0, 0, button.Width, button.Height);
            const int d = 12;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            var previous = button.Region;
            button.Region = new Region(path);
            previous?.Dispose();
        }
        button.Resize += (_, _) => Round();
        Round();
        button.MouseEnter += (_, _) => button.BackColor = primary ? Color.FromArgb(8, 87, 202) : Color.FromArgb(230, 238, 250);
        button.MouseLeave += (_, _) => button.BackColor = primary ? Blue : Color.White;
    }
    private void ResizeCards()
    {
        int w = Math.Max(470, body.ClientSize.Width - body.Padding.Horizontal - 2);
        foreach (var card in cards) card.Width = w;
    }
    private void FitScreen()
    {
        var r = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, r.Width), Math.Min(Height, r.Height));
        Location = new Point(r.Left + (r.Width - Width) / 2, r.Top + (r.Height - Height) / 2);
        ResizeCards();
    }
    private static void Options(ComboBox c, int index, params string[] keys)
    {
        c.BeginUpdate(); c.Items.Clear();
        foreach (var key in keys) c.Items.Add(Lang.T(key));
        c.SelectedIndex = Math.Clamp(index, 0, keys.Length - 1); c.EndUpdate();
    }
    private void Relabel()
    {
        Text = title.Text = Lang.T("settings.title");
        subtitle.Text = Lang.T("settings.subtitle");
        foreach (var pair in labels) pair.Key.Text = Lang.T(pair.Value);
        Options(show, show.SelectedIndex, "settings.show.all", "settings.show.talking");
        Options(names, names.SelectedIndex, "settings.names.both", "settings.names.nick");
        Options(alignment, alignment.SelectedIndex, "settings.alignment.left", "settings.alignment.right");
        Options(avatar, avatar.SelectedIndex, "settings.avatar.right", "settings.avatar.left", "settings.avatar.none");
        foreach (var pair in toggles) pair.Key.Text = Lang.T(pair.Value);
        save.Text = Lang.T("settings.save"); cancel.Text = Lang.T("settings.cancel");
    }
    private void LoadValues()
    {
        language.SelectedIndex = originalLanguage == "ru" ? 1 : 0;
        port.Value = Math.Clamp(cfg.Port, 1, 65535);
        show.SelectedIndex = cfg.ShowAll ? 0 : 1;
        names.SelectedIndex = cfg.NameMode == "nick" ? 1 : 0;
        alignment.SelectedIndex = cfg.Alignment == "right" ? 1 : 0;
        avatar.SelectedIndex = cfg.AvatarSide switch { "left" => 1, "none" => 2, _ => 0 };

        autoWindows.Checked = cfg.AutoStartWindows; autoTs.Checked = cfg.AutoStartWithTs;
        clickThrough.Checked = cfg.ClickThrough;
        hotkey.Text = cfg.Hotkey;
        fontSize.Value = Math.Clamp(cfg.FontSize, 10, 26);
        width.Value = Math.Clamp(cfg.WindowWidth, 180, 700);
    }
    private void Save()
    {
        cfg.Language = language.SelectedIndex == 1 ? "ru" : "en";
        cfg.Port = (int)port.Value; cfg.ShowAll = show.SelectedIndex != 1;
        cfg.NameMode = names.SelectedIndex == 1 ? "nick" : "both";
        cfg.Alignment = alignment.SelectedIndex == 1 ? "right" : "left";
        cfg.AvatarSide = avatar.SelectedIndex switch { 1 => "left", 2 => "none", _ => "right" };

        cfg.AutoStartWindows = autoWindows.Checked; cfg.AutoStartWithTs = autoTs.Checked;
        cfg.ClickThrough = clickThrough.Checked;
        if (!string.IsNullOrWhiteSpace(hotkey.Text)) cfg.Hotkey = hotkey.Text.Trim();
        cfg.FontSize = (int)fontSize.Value; cfg.WindowWidth = (int)width.Value;
        ConfigStore.Save(cfg); DialogResult = DialogResult.OK; apply(); Close();
    }
}

internal sealed class RoundedCard : Panel
{
    public RoundedCard() => DoubleBuffered = true;
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width < 20 || Height < 20) return;
        using var path = Path();
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }
    private GraphicsPath Path()
    {
        var path = new GraphicsPath();
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        const int d = 18;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(223, 231, 241));
        using var path = Path();
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Keyboard-accessible checkbox styled like a blue/gray switch.</summary>
internal sealed class SwitchCheckBox : CheckBox
{
    public SwitchCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(1, (Height - 23) / 2, 46, 23);
        using var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, 23, 23, 90, 180);
        path.AddArc(r.Right - 23, r.Y, 23, 23, 270, 180);
        path.CloseFigure();
        using var background = new SolidBrush(Checked ? Color.FromArgb(19, 108, 231) : Color.FromArgb(179, 187, 202));
        e.Graphics.FillPath(background, path);
        using var knob = new SolidBrush(Color.White);
        e.Graphics.FillEllipse(knob, Checked ? r.Right - 21 : r.Left + 2, r.Top + 2, 19, 19);
        TextRenderer.DrawText(e.Graphics, Text, Font,
            new Rectangle(57, 0, Math.Max(0, Width - 59), Height), ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1));
    }
}
