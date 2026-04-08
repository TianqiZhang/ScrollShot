using System.Runtime.InteropServices;

namespace ScrollShot.Overlay.Interop;

internal static partial class WindowStyles
{
    public const int GwlExStyle = -20;
    public const int WsExToolWindow = 0x00000080;

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
