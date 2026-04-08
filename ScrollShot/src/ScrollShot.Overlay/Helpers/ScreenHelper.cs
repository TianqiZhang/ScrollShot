using System.Windows;
using System.Windows.Media;
using ScrollShot.Capture.Models;

namespace ScrollShot.Overlay.Helpers;

public static class ScreenHelper
{
    public static Rect GetVirtualScreenBounds()
    {
        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    public static ScreenRect ToPhysicalScreenRect(Rect wpfRect, Visual visual)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        return new ScreenRect(
            (int)Math.Round(wpfRect.X * dpi.DpiScaleX),
            (int)Math.Round(wpfRect.Y * dpi.DpiScaleY),
            Math.Max(1, (int)Math.Round(wpfRect.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Round(wpfRect.Height * dpi.DpiScaleY)));
    }
}
