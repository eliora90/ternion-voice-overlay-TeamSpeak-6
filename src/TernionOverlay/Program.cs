using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using TernionCore;

namespace TernionOverlay;

internal static class Program
{
    private const int HotkeyId = 1;

    private static AppConfig _cfg = null!;
    private static Ts6Client _core = null!;
    private static OverlayForm _overlay = null!;
    private static NotifyIcon? _tray;
    private static bool _hotkeyOk;


    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "Global\\TernionOverlay.Single");
        if (!mutex.WaitOne(0, false))
        {
            // уже запущена одна копия — просто выходим
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        _cfg = ConfigStore.Load();
        Lang.Apply(_cfg.Language);
        _cfg.AppDir = Path.GetDirectoryName(Environment.ProcessPath ?? AssemblyLocation()) ?? ".";
        if (args.Contains("--settings")) { /* открыть настройки сразу — ниже */ }

        _core = new Ts6Client(_cfg.Port, ConfigStore.CreateKeyStore());

        _overlay = new OverlayForm(_cfg, _core, new AvatarCache(_overlayRedraw));
        _overlay.FormClosed += (_, _) => Application.Exit();
        // The window handle does not exist yet at startup; register only after creation.
        _overlay.HandleCreated += (_, _) => ApplyHotkey();

        BuildTray();
        ApplyHotkey();
        ApplyAutostart();

        // The overlay timer owns all Show/Hide calls on the UI thread. Remote Apps
        // callbacks are on a worker and must never manipulate WinForms windows.

        _core.Start();

        if (args.Contains("--settings")) OpenSettings();

        Application.Run(_overlay);

        Cleanup();
    }

    private static string AssemblyLocation() => AppContext.BaseDirectory;

    private static void _overlayRedraw() { if (_overlay.IsHandleCreated) _overlay.Redraw(); }


    private static SettingsForm? _settingsForm;

    private static void OpenSettings()
    {
        // Не открываем второе окно настроек, если уже открыто.
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            if (!_settingsForm.Visible) _settingsForm.Show();
            _settingsForm.Activate();
            _settingsForm.BringToFront();
            return;
        }
        _settingsForm = new SettingsForm(_cfg, ApplySettings);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.ShowDialog();
        _settingsForm = null;
    }

    // Пересборка после сохранения настроек (порт, горячая клавиша, автостарт и т.д.)
    private static void ApplySettings()
    {
        Lang.Apply(_cfg.Language);
        if (_tray?.ContextMenuStrip is { } menu)
        {
            menu.Items[0].Text = Lang.T("tray.settings");
            menu.Items[1].Text = Lang.T(_overlay.IsMoveMode ? "tray.lock" : "tray.move");
            menu.Items[2].Text = Lang.T("tray.show");
            menu.Items[4].Text = Lang.T("tray.exit");
            _tray.Text = Lang.T("tray.tooltip");
        }
        _overlay.ReapplyFlags();
        _overlay.Redraw();
        ApplyHotkey();
        ApplyAutostart();
        // если сменили порт — переподключаемся
        if (_core.Port != _cfg.Port) RestartClient();
    }

    private static void RestartClient()
    {
        _core.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _core = new Ts6Client(_cfg.Port, ConfigStore.CreateKeyStore());
        _overlay.Reattach(_core);
        _core.Start();
    }

    private static void BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Lang.T("tray.settings"), null, (_, _) => OpenSettings());
        menu.Items.Add(Lang.T(_overlay.IsMoveMode ? "tray.lock" : "tray.move"), null, (_, _) => ToggleMove());
        menu.Items.Add(Lang.T("tray.show"), null, (_, _) => _overlay.ToggleVisible());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Lang.T("tray.exit"), null, (_, _) => Application.Exit());

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            ContextMenuStrip = menu,
            Text = Lang.T("tray.tooltip"),
            Visible = true
        };
        _tray.DoubleClick += (_, _) => _overlay.ToggleVisible();
    }

    private static void ToggleMove()
    {
        _overlay.SetMoveMode(!_overlay.IsMoveMode);
        if (_tray?.ContextMenuStrip is { } menu)
            menu.Items[1].Text = Lang.T(_overlay.IsMoveMode ? "tray.lock" : "tray.move");
    }

    private static Icon MakeIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var ring = new Pen(Color.FromArgb(255, 200, 90), 2f);
            g.DrawEllipse(ring, 2, 2, 12, 12);
            g.FillEllipse(Brushes.LimeGreen, 6, 6, 4, 4);
        }
        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private static void ApplyHotkey()
    {
        if (_overlay.IsHandleCreated && _hotkeyOk)
            NativeMethods.UnregisterHotKey(_overlay.Handle, HotkeyId);
        _hotkeyOk = false;

        var parts = _cfg.Hotkey.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return;
        uint mods = 0;
        uint vk = 0;
        foreach (var p in parts)
        {
            var pc = p.ToLowerInvariant();
            if (pc == "ctrl") mods |= 0x0002;
            else if (pc == "alt") mods |= 0x0001;
            else if (pc == "shift") mods |= 0x0004;
            else if (pc == "win") mods |= 0x0008;
            else if (pc.Length == 1) vk = (uint)char.ToUpperInvariant(pc[0]);
        }
        if (vk == 0 || !_overlay.IsHandleCreated) return;
        _hotkeyOk = NativeMethods.RegisterHotKey(_overlay.Handle, HotkeyId, mods, vk);
    }

    private static void ApplyAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            string exe = Environment.ProcessPath ?? Path.Combine(_cfg.AppDir, "TernionOverlay.exe");
            if (_cfg.AutoStartWindows && key is not null) key.SetValue("TernionOverlay", $"\"{exe}\"");
            else if (key is not null) key.DeleteValue("TernionOverlay", false);
        }
        catch { }
    }

    private static void Cleanup()
    {
        if (_overlay.IsHandleCreated && _hotkeyOk)
            NativeMethods.UnregisterHotKey(_overlay.Handle, HotkeyId);
        _tray?.Dispose();
        _core.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}