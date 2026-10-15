using System.Runtime.InteropServices;

namespace TernionOverlay;

internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;

    public const int WM_HOTKEY = 0x0312;

    public static readonly IntPtr HWND_TOPMOST = new(-1);

    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOACTIVATE = 0x0010;
    private const int SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointL { public int X, Y; }

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    public static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref PointL pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref PointL pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    public static void SetTopmost(IntPtr hwnd) =>
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

    private const uint CapsuleKey = 0x00FF00FF; // magenta, COLORREF for WinForms TransparencyKey
    private const uint LWA_COLORKEY = 0x01;

    /// <summary>Unlocked: capsules remain visible and draggable, empty pixels transparent.</summary>
    public static void SetPlainWindow(IntPtr hwnd)
    {
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        style = (style | WS_EX_LAYERED | WS_EX_TOOLWINDOW) & ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
        SetLayeredWindowAttributes(hwnd, CapsuleKey, 255, LWA_COLORKEY);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    /// <summary>Клики насквозь (для игры): чернила окна не мешают нажатиям.</summary>
    public static void SetClickThrough(IntPtr hwnd)
    {
        int style = GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
        SetLayeredWindowAttributes(hwnd, CapsuleKey, 255, LWA_COLORKEY);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}