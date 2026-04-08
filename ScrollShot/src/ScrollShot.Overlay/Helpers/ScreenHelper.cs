using System.Windows;
using System.Windows.Media;
using ScrollShot.Capture.Models;
using Screen = System.Windows.Forms.Screen;

namespace ScrollShot.Overlay.Helpers;

public static class ScreenHelper
{
    public static Rect GetVirtualScreenBounds()
    {
        var screens = Screen.AllScreens;
        var left = screens.Min(screen => screen.Bounds.Left);
        var top = screens.Min(screen => screen.Bounds.Top);
        var right = screens.Max(screen => screen.Bounds.Right);
        var bottom = screens.Max(screen => screen.Bounds.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    public static ScreenRect ToPhysicalScreenRect(Rect rect, Visual visual)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        return new ScreenRect(
            (int)Math.Round(rect.X * dpi.DpiScaleX),
            (int)Math.Round(rect.Y * dpi.DpiScaleY),
            Math.Max(1, (int)Math.Round(rect.Width * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Round(rect.Height * dpi.DpiScaleY)));
    }
}
