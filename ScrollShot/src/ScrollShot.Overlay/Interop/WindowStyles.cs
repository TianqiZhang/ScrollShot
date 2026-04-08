using System.Runtime.InteropServices;

namespace ScrollShot.Overlay.Interop;

internal static class WindowStyles
{
    public const int GwlExStyle = -20;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExTransparent = 0x00000020;
    public const uint WmMouseWheel = 0x020A;
    public const uint WmMouseHWheel = 0x020E;
    public const uint SwpNoSize = 0x0001;
    public const uint SwpNoMove = 0x0002;
    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoActivate = 0x0010;
    public const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct NativePoint
    {
        public int X;
        public int Y;
    }
}
