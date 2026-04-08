using System.Runtime.InteropServices;

namespace ScrollShot.Capture.Interop;

internal static partial class NativeMethods
{
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(
        IntPtr hdcDest,
        int nXDest,
        int nYDest,
        int nWidth,
        int nHeight,
        IntPtr hdcSrc,
        int nXSrc,
        int nYSrc,
        int dwRop);
}
